using CncWallStation.EntityFrameworkCore;
using CncWallStation.Models.Dtos;
using CncWallStation.Models.Entities;
using CncWallStation.Models.Enums;
using CncWallStation.VersionMappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace CncWallStation.Services.DataCheck
{
    /// <summary>
    /// 数据预检服务实现
    /// </summary>
    public class DataCheckService : IDataCheckService
    {
        private readonly IDbContextFactory<AppDbContext> _dbFactory;
        private readonly DataCheckValidatorFactory _validatorFactory;
        private readonly BimWallMapperFactory _mapperFactory;
        private readonly ILogger<DataCheckService> _logger;

        public DataCheckService(
            IDbContextFactory<AppDbContext> dbFactory,
            DataCheckValidatorFactory validatorFactory,
            BimWallMapperFactory mapperFactory,
            ILogger<DataCheckService> logger)
        {
            _dbFactory = dbFactory;
            _validatorFactory = validatorFactory;
            _mapperFactory = mapperFactory;
            _logger = logger;
        }

        // ==================== 单墙预检 ====================

        public async Task<DataCheckResultDto> CheckSingleWallAsync(long wallId, string @operator)
        {
            var sw = Stopwatch.StartNew();
            var groupId = Guid.NewGuid().ToString("N");

            await using var db = await _dbFactory.CreateDbContextAsync();
            var wall = await db.Walls
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == wallId);

            if (wall == null)
                throw new InvalidOperationException($"墙体不存在：Id={wallId}");

            _logger.LogInformation("开始预检 WallId={WallKey} (DB Id={Id})，GroupId={GroupId}",
                wall.WallId, wall.Id, groupId);

            // 1. 版本解析
            string version;
            try
            {
                version = BimDataVersionResolver.ResolveVersion(wall.BimJsonData);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "版本解析失败，使用默认版本 0.0.0");
                version = "0.0.0";
            }

            // 2. 获取版本化校验器
            var validator = _validatorFactory.GetValidator(version);
            var allErrors = new List<ValidationErrorEntity>();

            // 3. BimData 校验
            List<FeatureCategoryResult> bimResults;
            try
            {
                bimResults = await validator.ValidateBimDataAsync(wall.BimJsonData, wall.WallId);
                foreach (var fr in bimResults)
                    allErrors.AddRange(fr.Errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BimData 校验异常");
                bimResults = new List<FeatureCategoryResult>
                {
                    new FeatureCategoryResult
                    {
                        CategoryName = "BimValidation",
                        CategoryNameCn = "Bim校验异常",
                        CheckItemCount = 1,
                        CriticalCount = 1,
                        Score = 0,
                        Errors = new List<ValidationErrorEntity>
                        {
                            new(
                                wallId: wallId, groupId: groupId,
                                pipelineStage: PipelineStage.ValidatingBim,
                                errorMessage: $"BimData 校验过程异常: {ex.Message}",
                                errorCode: "BIM_VALIDATION_EXCEPTION",
                                severity: ErrorSeverity.Critical,
                                errorCategory: ErrorCategory.Bim,
                                featureCategory: "BimValidation",
                                errorMessageEn: $"BimData validation exception: {ex.Message}",
                                dataCheckGroupId: groupId)
                        }
                    }
                };
                allErrors.AddRange(bimResults[0].Errors);
            }

            // 4. MomData 校验（如果存在）
            List<FeatureCategoryResult> momResults = new();
            if (!string.IsNullOrWhiteSpace(wall.MomJsonData))
            {
                try
                {
                    momResults = await validator.ValidateMomDataAsync(wall.MomJsonData, wall.WallId);
                    foreach (var fr in momResults)
                        allErrors.AddRange(fr.Errors);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "MomData 校验异常");
                    momResults = new List<FeatureCategoryResult>
                    {
                        new FeatureCategoryResult
                        {
                            CategoryName = "MomValidation",
                            CategoryNameCn = "Mom校验异常",
                            CheckItemCount = 1,
                            CriticalCount = 1,
                            Score = 0,
                            Errors = new List<ValidationErrorEntity>
                            {
                                new(
                                    wallId: wallId, groupId: groupId,
                                    pipelineStage: PipelineStage.ValidatingMom,
                                    errorMessage: $"MomData 校验过程异常: {ex.Message}",
                                    errorCode: "MOM_VALIDATION_EXCEPTION",
                                    severity: ErrorSeverity.Critical,
                                    errorCategory: ErrorCategory.Mom,
                                    featureCategory: "MomValidation",
                                    errorMessageEn: $"MomData validation exception: {ex.Message}",
                                    dataCheckGroupId: groupId)
                            }
                        }
                    };
                    allErrors.AddRange(momResults[0].Errors);
                }
            }

            // 5. 汇总统计
            double bimScore = ComputeTotalScore(bimResults);
            double momScore = ComputeTotalScore(momResults);
            int criticalCount = allErrors.Count(e => e.Severity == ErrorSeverity.Critical);
            int errorCount = allErrors.Count(e => e.Severity == ErrorSeverity.Error);
            int warningCount = allErrors.Count(e => e.Severity == ErrorSeverity.Warning);
            int infoCount = allErrors.Count(e => e.Severity == ErrorSeverity.Info);
            int totalErrors = criticalCount + errorCount + warningCount + infoCount;

            // 6. 绑定 GroupId 到预检记录
            foreach (var err in allErrors)
            {
                err.BindToCheckRecord(groupId, wallId);
            }

            // 7. 判断结果
            bool isPassed = criticalCount == 0 && bimScore >= 60 && (string.IsNullOrWhiteSpace(wall.MomJsonData) || momScore >= 60);
            var checkResult = isPassed ? CheckResult.Pass : CheckResult.Fail;

            // 8. 保存 DataCheckRecord 和 ValidationErrors
            await using var saveDb = await _dbFactory.CreateDbContextAsync();
            var record = new DataCheckRecordEntity(
                groupId, wallId, version, bimScore, momScore,
                totalErrors, criticalCount, @operator, checkResult, sw.ElapsedMilliseconds);

            saveDb.Set<DataCheckRecordEntity>().Add(record);
            foreach (var err in allErrors)
                saveDb.Set<ValidationErrorEntity>().Add(err);

            // 9. PipelineStage 联动
            var wallToUpdate = await saveDb.Walls.FirstOrDefaultAsync(w => w.Id == wallId);
            if (wallToUpdate != null)
            {
                // Converted 阶段：Bim→Mom 转换
                if (wallToUpdate.PipelineStage == PipelineStage.Converted)
                {
                    try
                    {
                        var mapper = _mapperFactory.GetMapper(version);
                        var momWall = mapper.Map(wall.BimJsonData);
                        var momJson = Newtonsoft.Json.JsonConvert.SerializeObject(momWall);
                        wallToUpdate.UpdateMomJsonData(momJson);
                        _logger.LogInformation("Converted 阶段：已生成 MomJson 写入 Wall.MomJsonData");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Converted 阶段 Bim→Mom 转换失败");
                    }
                }

                if (isPassed)
                {
                    // 预检通过 → 推进阶段
                    var nextStage = GetNextStage(wallToUpdate.PipelineStage);
                    wallToUpdate.UpdatePipelineStage(nextStage);
                    _logger.LogInformation("预检通过，PipelineStage: {From} → {To}",
                        wallToUpdate.PipelineStage, nextStage);
                }
                else
                {
                    // 预检失败 → 阻断
                    var failStage = GetFailStage(wallToUpdate.PipelineStage);
                    wallToUpdate.UpdatePipelineStage(failStage);
                    _logger.LogWarning("预检失败，PipelineStage → {Stage}", failStage);
                }
            }

            await saveDb.SaveChangesAsync();
            sw.Stop();

            // 10. 构建返回 DTO
            return new DataCheckResultDto
            {
                GroupId = groupId,
                WallId = wallId,
                WallKey = wall.WallId,
                Version = version,
                BimFeatureResults = bimResults,
                MomFeatureResults = momResults,
                BimTotalScore = bimScore,
                MomTotalScore = momScore,
                CriticalCount = criticalCount,
                ErrorCount = errorCount,
                WarningCount = warningCount,
                InfoCount = infoCount,
                AllErrors = allErrors,
                DurationMs = sw.ElapsedMilliseconds,
                Operator = @operator
            };
        }

        // ==================== 批量预检 ====================

        public async Task<BatchCheckSummaryDto> CheckBatchAsync(
            WallFilterDto filter,
            string @operator,
            IProgress<(int Done, int Total, int Errors)>? progress = null)
        {
            var summary = new BatchCheckSummaryDto
            {
                Filter = filter,
                StartTime = DateTime.Now,
                Operator = @operator
            };

            await using var db = await _dbFactory.CreateDbContextAsync();
            var query = db.Walls.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.ProjectNumber))
                query = query.Where(w => w.ProjectNumber == filter.ProjectNumber);

            if (filter.Floor.HasValue)
                query = query.Where(w => w.Floor == filter.Floor.Value);

            if (filter.StartTime.HasValue)
                query = query.Where(w => w.ImportTime >= filter.StartTime.Value);

            if (filter.EndTime.HasValue)
                query = query.Where(w => w.ImportTime <= filter.EndTime.Value);

            if (filter.PipelineStages != null && filter.PipelineStages.Count > 0)
                query = query.Where(w => filter.PipelineStages.Contains(w.PipelineStage));

            // 跳过已经 Ready 或 Invalid 的墙体
            query = query.Where(w => w.PipelineStage != PipelineStage.Ready
                                  && w.PipelineStage != PipelineStage.BimInvalid
                                  && w.PipelineStage != PipelineStage.ConversionFailed
                                  && w.PipelineStage != PipelineStage.MomInvalid);

            if (filter.MaxCount > 0)
                query = query.Take(filter.MaxCount);

            var wallIds = await query.Select(w => w.Id).ToListAsync();
            summary.TotalCount = wallIds.Count;

            int totalErrors = 0;

            for (int i = 0; i < wallIds.Count; i++)
            {
                try
                {
                    var result = await CheckSingleWallCoreAsync(wallIds[i], @operator);
                    summary.WallResults.Add(result);
                    summary.CompletedCount++;

                    if (result.IsPassed)
                        summary.PassedCount++;
                    else
                        summary.FailedCount++;

                    totalErrors += result.TotalErrorCount;
                    progress?.Report((i + 1, wallIds.Count, totalErrors));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "批量预检异常：WallId={Id}", wallIds[i]);
                    totalErrors++;
                    progress?.Report((i + 1, wallIds.Count, totalErrors));
                }
            }

            summary.TotalErrors = totalErrors;
            summary.EndTime = DateTime.Now;

            return summary;
        }

        /// <summary>内部预检方法（不更新 PipelineStage，用于批量）</summary>
        private async Task<DataCheckResultDto> CheckSingleWallCoreAsync(long wallId, string @operator)
        {
            var sw = Stopwatch.StartNew();
            var groupId = Guid.NewGuid().ToString("N");

            await using var db = await _dbFactory.CreateDbContextAsync();
            var wall = await db.Walls.AsNoTracking().FirstOrDefaultAsync(w => w.Id == wallId);

            if (wall == null)
                throw new InvalidOperationException($"墙体不存在：Id={wallId}");

            string version;
            try
            {
                version = BimDataVersionResolver.ResolveVersion(wall.BimJsonData);
            }
            catch
            {
                version = "0.0.0";
            }

            var validator = _validatorFactory.GetValidator(version);
            var allErrors = new List<ValidationErrorEntity>();

            var bimResults = await validator.ValidateBimDataAsync(wall.BimJsonData, wall.WallId);
            foreach (var fr in bimResults) allErrors.AddRange(fr.Errors);

            List<FeatureCategoryResult> momResults = new();
            if (!string.IsNullOrWhiteSpace(wall.MomJsonData))
            {
                momResults = await validator.ValidateMomDataAsync(wall.MomJsonData, wall.WallId);
                foreach (var fr in momResults) allErrors.AddRange(fr.Errors);
            }

            double bimScore = ComputeTotalScore(bimResults);
            double momScore = ComputeTotalScore(momResults);
            int criticalCount = allErrors.Count(e => e.Severity == ErrorSeverity.Critical);
            int errorCount = allErrors.Count(e => e.Severity == ErrorSeverity.Error);
            int warningCount = allErrors.Count(e => e.Severity == ErrorSeverity.Warning);
            int infoCount = allErrors.Count(e => e.Severity == ErrorSeverity.Info);
            bool isPassed = criticalCount == 0 && bimScore >= 60 && (string.IsNullOrWhiteSpace(wall.MomJsonData) || momScore >= 60);

            foreach (var err in allErrors)
            {
                err.BindToCheckRecord(groupId, wallId);
            }

            // 保存到数据库
            await using var saveDb = await _dbFactory.CreateDbContextAsync();
            var record = new DataCheckRecordEntity(
                groupId, wallId, version, bimScore, momScore,
                allErrors.Count, criticalCount, @operator,
                isPassed ? CheckResult.Pass : CheckResult.Fail, sw.ElapsedMilliseconds);

            saveDb.Set<DataCheckRecordEntity>().Add(record);
            foreach (var err in allErrors)
                saveDb.Set<ValidationErrorEntity>().Add(err);

            var wallToUpdate = await saveDb.Walls.FirstOrDefaultAsync(w => w.Id == wallId);
            if (wallToUpdate != null)
            {
                if (isPassed)
                    wallToUpdate.UpdatePipelineStage(GetNextStage(wallToUpdate.PipelineStage));
                else
                    wallToUpdate.UpdatePipelineStage(GetFailStage(wallToUpdate.PipelineStage));
            }

            await saveDb.SaveChangesAsync();
            sw.Stop();

            return new DataCheckResultDto
            {
                GroupId = groupId,
                WallId = wallId,
                WallKey = wall.WallId,
                Version = version,
                BimFeatureResults = bimResults,
                MomFeatureResults = momResults,
                BimTotalScore = bimScore,
                MomTotalScore = momScore,
                CriticalCount = criticalCount,
                ErrorCount = errorCount,
                WarningCount = warningCount,
                InfoCount = infoCount,
                AllErrors = allErrors,
                DurationMs = sw.ElapsedMilliseconds,
                Operator = @operator
            };
        }

        // ==================== 历史记录 ====================

        public async Task<List<DataCheckRecordDto>> GetHistoryAsync(long wallId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            return await db.Set<DataCheckRecordEntity>()
                .AsNoTracking()
                .Where(r => r.WallId == wallId)
                .OrderByDescending(r => r.CheckTime)
                .Select(r => new DataCheckRecordDto
                {
                    GroupId = r.Id,
                    WallId = r.WallId,
                    Version = r.Version,
                    BimScore = r.BimScore,
                    MomScore = r.MomScore,
                    ErrorCount = r.ErrorCount,
                    CriticalCount = r.CriticalCount,
                    Operator = r.Operator,
                    CheckTime = r.CheckTime,
                    DurationMs = r.DurationMs,
                    ResultText = r.Result.ToDisplayText()
                })
                .ToListAsync();
        }

        // ==================== 差异对比 ====================

        public async Task<HistoryDiffResultDto> CompareAsync(string groupId1, string groupId2)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var record1 = await db.Set<DataCheckRecordEntity>()
                .AsNoTracking()
                .Include(r => r.Errors)
                .FirstOrDefaultAsync(r => r.Id == groupId1);

            var record2 = await db.Set<DataCheckRecordEntity>()
                .AsNoTracking()
                .Include(r => r.Errors)
                .FirstOrDefaultAsync(r => r.Id == groupId2);

            if (record1 == null || record2 == null)
                throw new InvalidOperationException("预检记录不存在");

            var result = new HistoryDiffResultDto
            {
                Record1 = record1,
                Record2 = record2
            };

            // 按 ErrorCode + FeatureCategory 做 Key 匹配
            var errors1 = record1.Errors
                .Select(e => (Key: $"{e.ErrorCode}|{e.FeatureCategory}", Entry: ToDiffEntry(e)))
                .ToList();

            var errors2 = record2.Errors
                .Select(e => (Key: $"{e.ErrorCode}|{e.FeatureCategory}", Entry: ToDiffEntry(e)))
                .ToList();

            var set1 = errors1.Select(e => e.Key).ToHashSet();
            var set2 = errors2.Select(e => e.Key).ToHashSet();

            // 新增（在 Record2 中，不在 Record1 中）
            result.NewErrors = errors2
                .Where(e => !set1.Contains(e.Key))
                .Select(e => e.Entry)
                .ToList();

            // 已修复（在 Record1 中，不在 Record2 中）
            result.FixedErrors = errors1
                .Where(e => !set2.Contains(e.Key))
                .Select(e => e.Entry)
                .ToList();

            // 仍存在（两边都有）
            result.PersistentErrors = errors1
                .Where(e => set2.Contains(e.Key))
                .Select(e => e.Entry)
                .ToList();

            return result;
        }

        // ==================== 辅助方法 ====================

        private static double ComputeTotalScore(List<FeatureCategoryResult> results)
        {
            if (results.Count == 0) return 100;
            // 取各特征类别得分平均值作为总分
            return Math.Max(0, results.Average(r => r.Score));
        }

        private static PipelineStage GetNextStage(PipelineStage current) => current switch
        {
            PipelineStage.Imported or PipelineStage.ValidatingBim or PipelineStage.BimValid
                => PipelineStage.Converting,

            PipelineStage.Converting or PipelineStage.ConversionFailed => PipelineStage.Converted,

            PipelineStage.Converted or PipelineStage.ValidatingMom or PipelineStage.MomValid
                => PipelineStage.Ready,

            PipelineStage.Ready => PipelineStage.Ready,
            _ => current
        };

        private static PipelineStage GetFailStage(PipelineStage current) => current switch
        {
            PipelineStage.Imported or PipelineStage.ValidatingBim or PipelineStage.BimValid
                => PipelineStage.BimInvalid,

            PipelineStage.Converting or PipelineStage.Converted
            or PipelineStage.ValidatingMom or PipelineStage.MomValid
                => PipelineStage.MomInvalid,

            _ => current
        };

        private static DiffErrorEntry ToDiffEntry(ValidationErrorEntity err)
        {
            return new DiffErrorEntry
            {
                ErrorCode = err.ErrorCode,
                FeatureCategory = err.FeatureCategory,
                ErrorMessage = err.ErrorMessage,
                ErrorMessageEn = err.ErrorMessageEn,
                SeverityText = err.Severity.ToDisplayText()
            };
        }

    }
}
