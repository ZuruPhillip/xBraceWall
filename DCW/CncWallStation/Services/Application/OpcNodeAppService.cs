using CncWallStation.EntityFrameworkCore;
using CncWallStation.Models.Dtos;
using CncWallStation.Models.Entities;
using CncWallStation.Services.OpcUa;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;

namespace CncWallStation.Services.Application
{
    /// <summary>
    /// OPC 节点配置应用服务实现（EF Core + MySQL）
    /// </summary>
    public class OpcNodeAppService : IOpcNodeAppService
    {
        /// <summary>NodeId 最大长度（与表结构保持一致）</summary>
        private const int MaxNodeIdLength = 256;

        /// <summary>节点描述最大长度</summary>
        private const int MaxDescriptionLength = 256;

        /// <summary>分组名称最大长度</summary>
        private const int MaxGroupNameLength = 64;

        private readonly IDbContextFactory<AppDbContext> _dbFactory;
        private readonly ILogger<OpcNodeAppService> _logger;

        public OpcNodeAppService(
            IDbContextFactory<AppDbContext> dbFactory,
            ILogger<OpcNodeAppService> logger)
        {
            _dbFactory = dbFactory;
            _logger = logger;
        }

        // ══════════════════════════════════════════
        //  分组
        // ══════════════════════════════════════════

        /// <inheritdoc/>
        public async Task<List<OpcNodeGroupDto>> GetGroupsAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var groups = await db.OpcNodeGroups
                .AsNoTracking()
                .OrderBy(g => g.SortOrder)
                .ThenBy(g => g.Id)
                .ToListAsync();

            var counts = await db.OpcNodes
                .AsNoTracking()
                .Where(n => n.GroupId != null)
                .GroupBy(n => n.GroupId!.Value)
                .Select(g => new { GroupId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.GroupId, x => x.Count);

            return groups.Select(g => new OpcNodeGroupDto
            {
                Id = g.Id,
                Name = g.Name,
                Description = g.Description,
                NodeCount = counts.TryGetValue(g.Id, out var c) ? c : 0
            }).ToList();
        }

        /// <inheritdoc/>
        public async Task<OpcNodeGroupDto> CreateGroupAsync(string name, string? description)
        {
            var groupName = NormalizeGroupName(name);

            await using var db = await _dbFactory.CreateDbContextAsync();

            if (await db.OpcNodeGroups.AnyAsync(g => g.Name == groupName))
                throw new InvalidOperationException($"分组名称已存在: {groupName}");

            var maxSort = await db.OpcNodeGroups.MaxAsync(g => (int?)g.SortOrder) ?? 0;
            var entity = new OpcNodeGroupEntity(groupName, TrimTo(description, MaxDescriptionLength), maxSort + 1);

            db.OpcNodeGroups.Add(entity);
            await db.SaveChangesAsync();

            _logger.LogInformation("已创建 OPC 节点分组: {GroupName} (Id={GroupId})", entity.Name, entity.Id);

            return new OpcNodeGroupDto
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                NodeCount = 0
            };
        }

        /// <inheritdoc/>
        public async Task RenameGroupAsync(int groupId, string name, string? description)
        {
            var groupName = NormalizeGroupName(name);

            await using var db = await _dbFactory.CreateDbContextAsync();

            var entity = await db.OpcNodeGroups.FirstOrDefaultAsync(g => g.Id == groupId)
                ?? throw new InvalidOperationException($"分组不存在: Id={groupId}");

            if (await db.OpcNodeGroups.AnyAsync(g => g.Name == groupName && g.Id != groupId))
                throw new InvalidOperationException($"分组名称已存在: {groupName}");

            var oldName = entity.Name;
            entity.Update(groupName, TrimTo(description, MaxDescriptionLength));
            await db.SaveChangesAsync();

            _logger.LogInformation("已重命名 OPC 节点分组: {OldName} → {NewName}", oldName, groupName);
        }

        /// <inheritdoc/>
        public async Task<int> DeleteGroupAsync(int groupId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var entity = await db.OpcNodeGroups.FirstOrDefaultAsync(g => g.Id == groupId);
            if (entity == null)
                return 0;

            // 先把分组下的节点移到“未分组”，避免连带删除节点
            var nodes = await db.OpcNodes.Where(n => n.GroupId == groupId).ToListAsync();
            foreach (var node in nodes)
                node.MoveToGroup(null);

            db.OpcNodeGroups.Remove(entity);
            await db.SaveChangesAsync();

            _logger.LogInformation(
                "已删除 OPC 节点分组: {GroupName}，{Count} 个节点移至未分组", entity.Name, nodes.Count);

            return nodes.Count;
        }

        // ══════════════════════════════════════════
        //  节点
        // ══════════════════════════════════════════

        /// <inheritdoc/>
        public async Task<List<OpcNodeDto>> GetNodesAsync(
            int? groupId = null, string? keyword = null, bool ungroupedOnly = false)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var query = db.OpcNodes.AsNoTracking();

            if (ungroupedOnly)
                query = query.Where(n => n.GroupId == null);
            else if (groupId.HasValue)
                query = query.Where(n => n.GroupId == groupId.Value);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var k = keyword.Trim();
                query = query.Where(n =>
                    n.NodeId.Contains(k) ||
                    (n.Description != null && n.Description.Contains(k)));
            }

            var nodes = await query
                .OrderBy(n => n.SortOrder)
                .ThenBy(n => n.Id)
                .ToListAsync();

            var groupNames = await db.OpcNodeGroups
                .AsNoTracking()
                .ToDictionaryAsync(g => g.Id, g => g.Name);

            return nodes.Select(n => MapToDto(n, groupNames)).ToList();
        }

        /// <inheritdoc/>
        public async Task<List<OpcNodeDto>> GetRealtimeNodesAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var nodes = await db.OpcNodes
                .AsNoTracking()
                .Where(n => n.IsShowInRealtime)
                .OrderBy(n => n.SortOrder)
                .ThenBy(n => n.Id)
                .ToListAsync();

            return nodes.Select(n => MapToDto(n, null)).ToList();
        }

        /// <inheritdoc/>
        public async Task<OpcNodeDto> CreateNodeAsync(OpcNodeDto dto)
        {
            var nodeId = NormalizeNodeId(dto.NodeId);

            await using var db = await _dbFactory.CreateDbContextAsync();

            if (await db.OpcNodes.AnyAsync(n => n.NodeId == nodeId))
                throw new InvalidOperationException($"该 NodeId 已存在: {nodeId}");

            if (dto.GroupId.HasValue &&
                !await db.OpcNodeGroups.AnyAsync(g => g.Id == dto.GroupId.Value))
            {
                throw new InvalidOperationException($"分组不存在: Id={dto.GroupId}");
            }

            var maxSort = await db.OpcNodes.MaxAsync(n => (int?)n.SortOrder) ?? 0;

            var entity = new OpcNodeEntity(
                nodeId,
                TrimTo(dto.Description, MaxDescriptionLength),
                dto.GroupId,
                dto.IsWritable,
                dto.IsShowInRealtime,
                maxSort + 1);

            db.OpcNodes.Add(entity);
            await db.SaveChangesAsync();

            _logger.LogInformation("已创建 OPC 节点: {NodeId} (Id={Id})", entity.NodeId, entity.Id);

            return new OpcNodeDto
            {
                Id = entity.Id,
                NodeId = entity.NodeId,
                Description = entity.Description ?? string.Empty,
                GroupId = entity.GroupId,
                IsWritable = entity.IsWritable,
                IsShowInRealtime = entity.IsShowInRealtime
            };
        }

        /// <inheritdoc/>
        public async Task UpdateNodeAsync(OpcNodeDto dto)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var entity = await db.OpcNodes.FirstOrDefaultAsync(n => n.Id == dto.Id)
                ?? throw new InvalidOperationException($"节点不存在: Id={dto.Id}");

            if (dto.GroupId.HasValue &&
                !await db.OpcNodeGroups.AnyAsync(g => g.Id == dto.GroupId.Value))
            {
                throw new InvalidOperationException($"分组不存在: Id={dto.GroupId}");
            }

            var description = TrimTo(dto.Description, MaxDescriptionLength);
            if (entity.Description == description &&
                entity.GroupId == dto.GroupId &&
                entity.IsWritable == dto.IsWritable &&
                entity.IsShowInRealtime == dto.IsShowInRealtime)
            {
                return;
            }

            entity.Update(description, dto.GroupId, dto.IsWritable, dto.IsShowInRealtime);
            await db.SaveChangesAsync();

            _logger.LogInformation("已更新 OPC 节点: {NodeId} (Id={Id})", entity.NodeId, entity.Id);
        }

        /// <inheritdoc/>
        public async Task<int> DeleteNodesAsync(IEnumerable<long> ids)
        {
            var idList = ids.Distinct().ToList();
            if (idList.Count == 0)
                return 0;

            await using var db = await _dbFactory.CreateDbContextAsync();

            var nodes = await db.OpcNodes.Where(n => idList.Contains(n.Id)).ToListAsync();
            if (nodes.Count == 0)
                return 0;

            db.OpcNodes.RemoveRange(nodes);
            await db.SaveChangesAsync();

            _logger.LogInformation("已删除 {Count} 个 OPC 节点", nodes.Count);
            return nodes.Count;
        }

        /// <inheritdoc/>
        public async Task<bool> NodeIdExistsAsync(string nodeId, long excludeId = 0)
        {
            var normalized = nodeId?.Trim() ?? string.Empty;
            if (normalized.Length == 0)
                return false;

            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.OpcNodes.AnyAsync(n => n.NodeId == normalized && n.Id != excludeId);
        }

        /// <inheritdoc/>
        public async Task<OpcNodeImportResult> ImportAsync(IReadOnlyList<OpcNodeImportRow> rows)
        {
            if (rows.Count == 0)
                return new OpcNodeImportResult();

            await using var db = await _dbFactory.CreateDbContextAsync();

            // appsettings 中启用了 EnableRetryOnFailure（MySqlRetryingExecutionStrategy），
            // EF Core 不允许在未包装执行策略时开启用户事务，否则抛 InvalidOperationException。
            // 因此把「事务 + 全部读写」作为一个整体交给执行策略执行，同时具备重试能力。
            var strategy = db.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                // 重试前清空变更跟踪，避免上一轮已 Add 的实体被重复提交
                db.ChangeTracker.Clear();

                await using var transaction = await db.Database.BeginTransactionAsync();

                try
                {
                    var result = await ImportCoreAsync(db, rows);

                    await db.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation(
                        "OPC 节点导入完成：新增 {Added} 条，更新 {Updated} 条，跳过 {Skipped} 条",
                        result.AddedCount, result.UpdatedCount, result.SkippedCount);

                    return result;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    _logger.LogError("OPC 节点导入失败，已回滚");
                    throw;
                }
            });
        }

        /// <summary>
        /// 导入主体：按 NodeId 合并（已存在则更新，不存在则新增），分组名不存在时自动创建。
        /// 仅在 <see cref="ImportAsync"/> 的事务与执行策略内调用。
        /// </summary>
        private static async Task<OpcNodeImportResult> ImportCoreAsync(
            AppDbContext db, IReadOnlyList<OpcNodeImportRow> rows)
        {
            var result = new OpcNodeImportResult();

            var existingNodes = await db.OpcNodes.ToListAsync();
            var nodeMap = existingNodes.ToDictionary(n => n.NodeId, StringComparer.OrdinalIgnoreCase);

            var existingGroups = await db.OpcNodeGroups.ToListAsync();
            var groupMap = existingGroups.ToDictionary(g => g.Name, StringComparer.OrdinalIgnoreCase);

            var maxSort = existingNodes.Count == 0 ? 0 : existingNodes.Max(n => n.SortOrder);

            foreach (var row in rows)
            {
                var nodeId = row.NodeId?.Trim() ?? string.Empty;

                if (nodeId.Length == 0)
                {
                    result.SkippedCount++;
                    result.Errors.Add($"第 {row.RowNumber} 行：NodeId 为空");
                    continue;
                }

                if (nodeId.Length > MaxNodeIdLength)
                {
                    result.SkippedCount++;
                    result.Errors.Add($"第 {row.RowNumber} 行：NodeId 长度超过 {MaxNodeIdLength}");
                    continue;
                }

                var groupId = await ResolveGroupIdAsync(db, groupMap, row.GroupName);
                var description = TrimTo(row.Description, MaxDescriptionLength);

                if (nodeMap.TryGetValue(nodeId, out var entity))
                {
                    entity.Update(description, groupId, row.IsWritable, row.IsShowInRealtime);
                    result.UpdatedCount++;
                }
                else
                {
                    var created = new OpcNodeEntity(
                        nodeId, description, groupId,
                        row.IsWritable, row.IsShowInRealtime, ++maxSort);
                    db.OpcNodes.Add(created);
                    nodeMap[nodeId] = created;
                    result.AddedCount++;
                }
            }

            return result;
        }

        /// <inheritdoc/>
        public async Task<int> MigrateLegacyJsonIfEmptyAsync(string legacyJsonPath)
        {
            if (string.IsNullOrWhiteSpace(legacyJsonPath) || !File.Exists(legacyJsonPath))
                return 0;

            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync();

                if (await db.OpcNodes.AnyAsync())
                    return 0;

                var json = await File.ReadAllTextAsync(legacyJsonPath);
                var legacyNodes = JsonSerializer.Deserialize<List<OpcNodeConfig>>(json);
                if (legacyNodes == null || legacyNodes.Count == 0)
                    return 0;

                var imported = 0;
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // 旧配置不含分组信息，统一落到“未分组”，由用户后续整理
                foreach (var node in legacyNodes)
                {
                    var nodeId = node.NodeId?.Trim() ?? string.Empty;
                    if (nodeId.Length == 0 || nodeId.Length > MaxNodeIdLength || !seen.Add(nodeId))
                        continue;

                    db.OpcNodes.Add(new OpcNodeEntity(
                        nodeId,
                        TrimTo(node.Description, MaxDescriptionLength),
                        null,
                        node.IsWritable,
                        node.IsShowInRealtime,
                        ++imported));
                }

                if (imported == 0)
                    return 0;

                await db.SaveChangesAsync();

                // 迁移成功后重命名旧文件，避免重复导入，同时保留备份便于人工核对
                try
                {
                    File.Move(legacyJsonPath, legacyJsonPath + ".bak", overwrite: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "旧 OPC 节点配置文件重命名失败: {Path}", legacyJsonPath);
                }

                _logger.LogInformation("已从旧配置文件迁移 {Count} 个 OPC 节点到数据库", imported);
                return imported;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "迁移旧 OPC 节点配置失败: {Path}", legacyJsonPath);
                return 0;
            }
        }

        // ══════════════════════════════════════════
        //  内部辅助
        // ══════════════════════════════════════════

        private static OpcNodeDto MapToDto(OpcNodeEntity entity, Dictionary<int, string>? groupNames)
        {
            string? groupName = null;
            if (entity.GroupId.HasValue && groupNames != null)
                groupNames.TryGetValue(entity.GroupId.Value, out groupName);

            return new OpcNodeDto
            {
                Id = entity.Id,
                GroupId = entity.GroupId,
                GroupName = groupName,
                NodeId = entity.NodeId,
                Description = entity.Description ?? string.Empty,
                IsWritable = entity.IsWritable,
                IsShowInRealtime = entity.IsShowInRealtime
            };
        }

        /// <summary>
        /// 把 Excel 中的分组名解析为分组 Id：空名 → 未分组；不存在 → 自动创建。
        /// 新建分组会立即保存（同一事务内），以便拿到自增 Id。
        /// </summary>
        private static async Task<int?> ResolveGroupIdAsync(
            AppDbContext db,
            Dictionary<string, OpcNodeGroupEntity> groupMap,
            string? groupName)
        {
            var name = groupName?.Trim();
            if (string.IsNullOrEmpty(name))
                return null;

            if (name.Length > MaxGroupNameLength)
                name = name[..MaxGroupNameLength];

            if (groupMap.TryGetValue(name, out var existing))
                return existing.Id;

            // 名称唯一索引可能已存在同名分组（大小写差异），再查一次库
            var entity = await db.OpcNodeGroups.FirstOrDefaultAsync(g => g.Name == name);
            if (entity == null)
            {
                var maxSort = await db.OpcNodeGroups.MaxAsync(g => (int?)g.SortOrder) ?? 0;
                entity = new OpcNodeGroupEntity(name, null, maxSort + 1);
                db.OpcNodeGroups.Add(entity);
                await db.SaveChangesAsync();
            }

            groupMap[name] = entity;
            return entity.Id;
        }

        private static string NormalizeNodeId(string? nodeId)
        {
            var value = nodeId?.Trim() ?? string.Empty;
            if (value.Length == 0)
                throw new InvalidOperationException("NodeId 不能为空");

            if (value.Length > MaxNodeIdLength)
                throw new InvalidOperationException($"NodeId 长度不能超过 {MaxNodeIdLength} 个字符");

            return value;
        }

        private static string NormalizeGroupName(string? name)
        {
            var value = name?.Trim() ?? string.Empty;
            if (value.Length == 0)
                throw new InvalidOperationException("分组名称不能为空");

            if (value.Length > MaxGroupNameLength)
                throw new InvalidOperationException($"分组名称长度不能超过 {MaxGroupNameLength} 个字符");

            return value;
        }

        private static string? TrimTo(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var trimmed = value.Trim();
            return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
        }
    }
}
