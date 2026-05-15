using CncWallStation.Models.Entities;
using CncWallStation.Models.Enums;
using Microsoft.Extensions.Logging;

namespace CncWallStation.Services
{
    /// <summary>
    /// 管线服务接口
    /// </summary>
    public interface IPipelineService
    {
        /// <summary>
        /// 手动执行完整管线：ValidateBim → ConvertToMom → ValidateMom
        /// 三步共享同一 GroupId，任一失败即终止
        /// </summary>
        Task<PipelineResult> ExecutePipelineAsync(long wallId);

        /// <summary>
        /// 对导入批次中指定状态的墙体批量执行管线
        /// </summary>
        Task<int> BatchExecuteAsync(int projectId);
    }

    /// <summary>
    /// 管线执行结果
    /// </summary>
    public class PipelineResult
    {
        public PipelineStage FinalStage { get; set; }
        public string GroupId { get; set; } = string.Empty;
        public List<ValidationErrorEntry> Errors { get; set; } = new();
        public string? MomJsonData { get; set; }
    }

    /// <summary>
    /// 校验错误条目（DTO）
    /// </summary>
    public class ValidationErrorEntry
    {
        public PipelineStage Stage { get; set; }
        public string? ErrorCode { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// 管线服务实现
    /// </summary>
    public class PipelineService : IPipelineService
    {
        private readonly Repositories.IWallRepository _wallRepo;
        private readonly ILogger<PipelineService> _logger;

        public PipelineService(Repositories.IWallRepository wallRepo, ILogger<PipelineService> logger)
        {
            _wallRepo = wallRepo;
            _logger = logger;
        }

        public async Task<PipelineResult> ExecutePipelineAsync(long wallId)
        {
            var groupId = Guid.NewGuid().ToString("N");
            var result = new PipelineResult { GroupId = groupId };

            var wall = await _wallRepo.GetWallByIdAsync(wallId);
            if (wall == null)
            {
                result.FinalStage = PipelineStage.Imported;
                result.Errors.Add(new ValidationErrorEntry
                {
                    Stage = PipelineStage.Imported,
                    ErrorMessage = $"墙体 ID={wallId} 不存在"
                });
                return result;
            }

            _logger.LogInformation("开始执行管线: WallId={WallId}, GroupId={GroupId}", wallId, groupId);

            // ========== 步骤 1：校验 BimJSON ==========
            await _wallRepo.UpdatePipelineStageAsync(wallId, PipelineStage.ValidatingBim);

            var bimErrors = ValidateBimJson(wall.BimJsonData);
            if (bimErrors.Count > 0)
            {
                await _wallRepo.UpdatePipelineStageAsync(wallId, PipelineStage.BimInvalid);

                var errorEntities = bimErrors.Select(e => new ValidationErrorEntity
                {
                    WallId = wallId,
                    GroupId = groupId,
                    PipelineStage = PipelineStage.ValidatingBim,
                    ErrorCode = e.ErrorCode,
                    ErrorMessage = e.ErrorMessage,
                    CreatedAt = DateTime.Now
                }).ToList();

                await _wallRepo.AddValidationErrorsAsync(errorEntities);

                result.FinalStage = PipelineStage.BimInvalid;
                result.Errors = bimErrors;
                _logger.LogWarning("BimJSON 校验失败: WallId={WallId}, 错误数={Count}", wallId, bimErrors.Count);
                return result;
            }

            await _wallRepo.UpdatePipelineStageAsync(wallId, PipelineStage.BimValid);
            _logger.LogInformation("BimJSON 校验通过: WallId={WallId}", wallId);

            // ========== 步骤 2：转换 BimJSON → MomJSON ==========
            await _wallRepo.UpdatePipelineStageAsync(wallId, PipelineStage.Converting);

            var convertResult = ConvertToMom(wall.BimJsonData);
            if (!convertResult.Success)
            {
                await _wallRepo.UpdatePipelineStageAsync(wallId, PipelineStage.ConversionFailed);

                await _wallRepo.AddValidationErrorsAsync(new List<ValidationErrorEntity>
                {
                    new ValidationErrorEntity
                    {
                        WallId = wallId,
                        GroupId = groupId,
                        PipelineStage = PipelineStage.Converting,
                        ErrorCode = "CONVERT_FAILED",
                        ErrorMessage = convertResult.ErrorMessage ?? "转换失败",
                        CreatedAt = DateTime.Now
                    }
                });

                result.FinalStage = PipelineStage.ConversionFailed;
                result.Errors.Add(new ValidationErrorEntry
                {
                    Stage = PipelineStage.Converting,
                    ErrorCode = "CONVERT_FAILED",
                    ErrorMessage = convertResult.ErrorMessage ?? "转换失败"
                });
                _logger.LogWarning("BimJSON→MomJSON 转换失败: WallId={WallId}", wallId);
                return result;
            }

            await _wallRepo.UpdateMomJsonDataAsync(wallId, convertResult.MomJsonData!);
            await _wallRepo.UpdatePipelineStageAsync(wallId, PipelineStage.Converted);
            _logger.LogInformation("BimJSON→MomJSON 转换完成: WallId={WallId}", wallId);

            // ========== 步骤 3：校验 MomJSON ==========
            await _wallRepo.UpdatePipelineStageAsync(wallId, PipelineStage.ValidatingMom);

            var momErrors = ValidateMomJson(convertResult.MomJsonData!);
            if (momErrors.Count > 0)
            {
                await _wallRepo.UpdatePipelineStageAsync(wallId, PipelineStage.MomInvalid);

                var errorEntities = momErrors.Select(e => new ValidationErrorEntity
                {
                    WallId = wallId,
                    GroupId = groupId,
                    PipelineStage = PipelineStage.ValidatingMom,
                    ErrorCode = e.ErrorCode,
                    ErrorMessage = e.ErrorMessage,
                    CreatedAt = DateTime.Now
                }).ToList();

                await _wallRepo.AddValidationErrorsAsync(errorEntities);

                result.FinalStage = PipelineStage.MomInvalid;
                result.Errors = momErrors;
                result.MomJsonData = convertResult.MomJsonData;
                _logger.LogWarning("MomJSON 校验失败: WallId={WallId}, 错误数={Count}", wallId, momErrors.Count);
                return result;
            }

            await _wallRepo.UpdatePipelineStageAsync(wallId, PipelineStage.MomValid);

            // ========== 全部通过 → Ready ==========
            await _wallRepo.UpdatePipelineStageAsync(wallId, PipelineStage.Ready);
            await _wallRepo.UpdateStatusAsync(wallId, 0, wall.UpdatedBy ?? Environment.UserName); // 0 = 待加工

            result.FinalStage = PipelineStage.Ready;
            result.MomJsonData = convertResult.MomJsonData;
            _logger.LogInformation("管线全部通过 → Ready: WallId={WallId}", wallId);

            return result;
        }

        public async Task<int> BatchExecuteAsync(int projectId)
        {
            var (walls, _) = await _wallRepo.QueryWallsAsync(
                page: 1, pageSize: int.MaxValue);

            var toProcess = walls
                .Where(w => w.ProjectId == projectId &&
                            (w.PipelineStage == PipelineStage.Imported ||
                             w.PipelineStage == PipelineStage.BimInvalid ||
                             w.PipelineStage == PipelineStage.ConversionFailed ||
                             w.PipelineStage == PipelineStage.MomInvalid))
                .ToList();

            int successCount = 0;
            foreach (var wall in toProcess)
            {
                try
                {
                    var result = await ExecutePipelineAsync(wall.Id);
                    if (result.FinalStage == PipelineStage.Ready)
                        successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "批量管线执行异常: WallId={WallId}", wall.Id);
                }
            }

            _logger.LogInformation("批量管线完成: 成功 {Success}/{Total}", successCount, toProcess.Count);
            return successCount;
        }

        // ==================== BimJSON 校验 ====================

        private List<ValidationErrorEntry> ValidateBimJson(string bimJsonData)
        {
            var errors = new List<ValidationErrorEntry>();

            if (string.IsNullOrWhiteSpace(bimJsonData))
            {
                errors.Add(new ValidationErrorEntry
                {
                    Stage = PipelineStage.ValidatingBim,
                    ErrorCode = "BIM_EMPTY",
                    ErrorMessage = "BimJSON 数据为空"
                });
                return errors;
            }

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(bimJsonData);
                var root = doc.RootElement;

                if (root.ValueKind != System.Text.Json.JsonValueKind.Object)
                {
                    errors.Add(new ValidationErrorEntry
                    {
                        Stage = PipelineStage.ValidatingBim,
                        ErrorCode = "BIM_NOT_OBJECT",
                        ErrorMessage = "BimJSON 根节点必须是 JSON 对象"
                    });
                    return errors;
                }

                // 校验必要字段
                bool hasId = root.TryGetProperty("id", out _) ||
                             root.TryGetProperty("wallId", out _) ||
                             root.TryGetProperty("wall_id", out _);

                if (!hasId)
                {
                    errors.Add(new ValidationErrorEntry
                    {
                        Stage = PipelineStage.ValidatingBim,
                        ErrorCode = "BIM_NO_ID",
                        ErrorMessage = "BimJSON 缺少墙体标识字段 (id/wallId/wall_id)"
                    });
                }

                // 校验 features 数组（如果存在）
                if (root.TryGetProperty("features", out var features))
                {
                    if (features.ValueKind != System.Text.Json.JsonValueKind.Array)
                    {
                        errors.Add(new ValidationErrorEntry
                        {
                            Stage = PipelineStage.ValidatingBim,
                            ErrorCode = "BIM_FEATURES_NOT_ARRAY",
                            ErrorMessage = "BimJSON 中 'features' 字段必须是数组"
                        });
                    }
                    else
                    {
                        int idx = 0;
                        foreach (var feature in features.EnumerateArray())
                        {
                            if (feature.ValueKind != System.Text.Json.JsonValueKind.Object)
                            {
                                errors.Add(new ValidationErrorEntry
                                {
                                    Stage = PipelineStage.ValidatingBim,
                                    ErrorCode = "BIM_FEATURE_NOT_OBJECT",
                                    ErrorMessage = $"features[{idx}] 不是有效对象"
                                });
                            }
                            else if (!feature.TryGetProperty("type", out _))
                            {
                                errors.Add(new ValidationErrorEntry
                                {
                                    Stage = PipelineStage.ValidatingBim,
                                    ErrorCode = "BIM_FEATURE_NO_TYPE",
                                    ErrorMessage = $"features[{idx}] 缺少 'type' 字段"
                                });
                            }
                            idx++;
                        }
                    }
                }
            }
            catch (System.Text.Json.JsonException ex)
            {
                errors.Add(new ValidationErrorEntry
                {
                    Stage = PipelineStage.ValidatingBim,
                    ErrorCode = "BIM_PARSE_ERROR",
                    ErrorMessage = $"BimJSON 解析失败: {ex.Message}"
                });
            }

            return errors;
        }

        // ==================== BimJSON → MomJSON 转换 ====================

        private (bool Success, string? MomJsonData, string? ErrorMessage) ConvertToMom(string bimJsonData)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(bimJsonData);
                var root = doc.RootElement;

                // 构建 MomJSON 结构
                var momObj = new Dictionary<string, object?>();

                // 复制基础字段
                if (root.TryGetProperty("id", out var idProp))
                    momObj["id"] = idProp.GetString() ?? string.Empty;
                else if (root.TryGetProperty("wallId", out var wallIdProp))
                    momObj["id"] = wallIdProp.GetString() ?? string.Empty;
                else
                    momObj["id"] = "unknown";

                if (root.TryGetProperty("houseNumber", out var hn))
                    momObj["houseNumber"] = hn.GetString() ?? string.Empty;
                if (root.TryGetProperty("floor", out var fl))
                    momObj["floor"] = fl.GetInt32();

                // 转换 features 为 momFeatures
                if (root.TryGetProperty("features", out var features) &&
                    features.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    var momFeatures = new List<Dictionary<string, object?>>();
                    foreach (var feature in features.EnumerateArray())
                    {
                        var momFeature = new Dictionary<string, object?>();
                        foreach (var prop in feature.EnumerateObject())
                        {
                            momFeature[prop.Name] = ConvertJsonElement(prop.Value);
                        }
                        momFeatures.Add(momFeature);
                    }
                    momObj["momFeatures"] = momFeatures;
                }
                else
                {
                    momObj["momFeatures"] = new List<object>();
                }

                // 添加元数据
                momObj["convertedAt"] = DateTime.Now.ToString("o");
                momObj["sourceVersion"] = "1.0";

                var momJson = System.Text.Json.JsonSerializer.Serialize(momObj);
                return (true, momJson, null);
            }
            catch (Exception ex)
            {
                return (false, null, $"转换异常: {ex.Message}");
            }
        }

        private static object? ConvertJsonElement(System.Text.Json.JsonElement element)
        {
            return element.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => element.GetString(),
                System.Text.Json.JsonValueKind.Number => element.TryGetInt32(out var i) ? i
                    : element.TryGetInt64(out var l) ? l
                    : element.GetDouble(),
                System.Text.Json.JsonValueKind.True => true,
                System.Text.Json.JsonValueKind.False => false,
                System.Text.Json.JsonValueKind.Null => null,
                System.Text.Json.JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
                System.Text.Json.JsonValueKind.Object => element.EnumerateObject()
                    .ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)),
                _ => element.GetRawText()
            };
        }

        // ==================== MomJSON 校验 ====================

        private List<ValidationErrorEntry> ValidateMomJson(string momJsonData)
        {
            var errors = new List<ValidationErrorEntry>();

            if (string.IsNullOrWhiteSpace(momJsonData))
            {
                errors.Add(new ValidationErrorEntry
                {
                    Stage = PipelineStage.ValidatingMom,
                    ErrorCode = "MOM_EMPTY",
                    ErrorMessage = "MomJSON 数据为空"
                });
                return errors;
            }

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(momJsonData);
                var root = doc.RootElement;

                if (root.ValueKind != System.Text.Json.JsonValueKind.Object)
                {
                    errors.Add(new ValidationErrorEntry
                    {
                        Stage = PipelineStage.ValidatingMom,
                        ErrorCode = "MOM_NOT_OBJECT",
                        ErrorMessage = "MomJSON 根节点必须是 JSON 对象"
                    });
                    return errors;
                }

                // 校验 id
                if (!root.TryGetProperty("id", out var idProp) || string.IsNullOrWhiteSpace(idProp.GetString()))
                {
                    errors.Add(new ValidationErrorEntry
                    {
                        Stage = PipelineStage.ValidatingMom,
                        ErrorCode = "MOM_NO_ID",
                        ErrorMessage = "MomJSON 缺少有效 'id' 字段"
                    });
                }

                // 校验 momFeatures
                if (root.TryGetProperty("momFeatures", out var momFeatures))
                {
                    if (momFeatures.ValueKind != System.Text.Json.JsonValueKind.Array)
                    {
                        errors.Add(new ValidationErrorEntry
                        {
                            Stage = PipelineStage.ValidatingMom,
                            ErrorCode = "MOM_FEATURES_NOT_ARRAY",
                            ErrorMessage = "MomJSON 中 'momFeatures' 字段必须是数组"
                        });
                    }
                    else
                    {
                        int idx = 0;
                        foreach (var feature in momFeatures.EnumerateArray())
                        {
                            if (feature.ValueKind != System.Text.Json.JsonValueKind.Object)
                            {
                                errors.Add(new ValidationErrorEntry
                                {
                                    Stage = PipelineStage.ValidatingMom,
                                    ErrorCode = "MOM_FEATURE_NOT_OBJECT",
                                    ErrorMessage = $"momFeatures[{idx}] 不是有效对象"
                                });
                            }
                            idx++;
                        }
                    }
                }
                else
                {
                    errors.Add(new ValidationErrorEntry
                    {
                        Stage = PipelineStage.ValidatingMom,
                        ErrorCode = "MOM_NO_FEATURES",
                        ErrorMessage = "MomJSON 缺少 'momFeatures' 字段"
                    });
                }

                // 校验转换元数据
                if (!root.TryGetProperty("convertedAt", out _))
                {
                    errors.Add(new ValidationErrorEntry
                    {
                        Stage = PipelineStage.ValidatingMom,
                        ErrorCode = "MOM_NO_CONVERTEDAT",
                        ErrorMessage = "MomJSON 缺少 'convertedAt' 元数据"
                    });
                }
            }
            catch (System.Text.Json.JsonException ex)
            {
                errors.Add(new ValidationErrorEntry
                {
                    Stage = PipelineStage.ValidatingMom,
                    ErrorCode = "MOM_PARSE_ERROR",
                    ErrorMessage = $"MomJSON 解析失败: {ex.Message}"
                });
            }

            return errors;
        }
    }
}
