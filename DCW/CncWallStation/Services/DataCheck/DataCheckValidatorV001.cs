using BimWallData.V001;
using CncWallStation.Models.Dtos;
using CncWallStation.Models.Entities;
using CncWallStation.Models.Enums;
using CncWallStation.MomWallData;
using Newtonsoft.Json;
using System.Text.Json;

namespace CncWallStation.Services.DataCheck
{
    /// <summary>
    /// V001 版本数据校验器
    /// 校验 BimWallDtoV001 的所有特征类别以及 MomWall 的结构完整性
    /// </summary>
    public class DataCheckValidatorV001 : IDataCheckValidator
    {
        public string SupportedVersion => "0.0.1";

        // ==================== 特征类别中文名映射 ====================

        private static readonly Dictionary<string, string> FeatureNameCnMap = new()
        {
            { "aacWallElevation", "墙体轮廓" },
            { "columnAssemblies", "钢柱组件" },
            { "rebars", "钢筋" },
            { "topPlate", "顶板" },
            { "shearKeys", "剪力键" },
            { "mepCables", "MEP电缆" },
            { "mepDevices", "MEP设备" },
            { "openingHoles", "窗户" },
            { "tensionTie", "拉紧钢筋" },
            { "xps", "XPS保温" },
            { "faceFinishes", "面板装饰" },
            { "proppingConnectors", "斜撑连接件" },
            { "waffleSlabLinks", "华夫板链接" },
            { "aacSlices", "AAC砖" },
            { "coreThickness", "砖块厚度" },
            { "baseFields", "基础字段" }
        };

        // ==================== BimData 校验 ====================

        public Task<List<FeatureCategoryResult>> ValidateBimDataAsync(string bimJsonData, string wallId)
        {
            var results = new List<FeatureCategoryResult>();

            BimWallDtoV001? dto = null;
            try
            {
                dto = JsonConvert.DeserializeObject<BimWallDtoV001>(bimJsonData);
            }
            catch (Exception ex)
            {
                results.Add(new FeatureCategoryResult
                {
                    CategoryName = "baseFields",
                    CategoryNameCn = "基础字段",
                    CheckItemCount = 1,
                    CriticalCount = 1,
                    Score = 0,
                    Errors = new List<ValidationErrorEntity>
                    {
                        new(
                            wallId: null,
                            groupId: null!,
                            pipelineStage: PipelineStage.ValidatingBim,
                            errorMessage: $"BimJson 反序列化失败: {ex.Message}",
                            errorCode: "BIM_DESERIALIZE_FAILED",
                            severity: ErrorSeverity.Critical,
                            errorCategory: ErrorCategory.Bim,
                            featureCategory: "baseFields",
                            errorMessageEn: $"BimJson deserialization failed: {ex.Message}",
                            dataCheckGroupId: null)
                    }
                });
                return Task.FromResult(results);
            }

            if (dto == null)
            {
                results.Add(new FeatureCategoryResult
                {
                    CategoryName = "baseFields",
                    CategoryNameCn = "基础字段",
                    CheckItemCount = 1,
                    CriticalCount = 1,
                    Score = 0,
                    Errors = new List<ValidationErrorEntity>
                    {
                        new(
                            wallId: null, groupId: null!,
                            pipelineStage: PipelineStage.ValidatingBim,
                            errorMessage: "BimJson 反序列化结果为 null",
                            errorCode: "BIM_DESERIALIZE_NULL",
                            severity: ErrorSeverity.Critical,
                            errorCategory: ErrorCategory.Bim,
                            featureCategory: "baseFields",
                            errorMessageEn: "BimJson deserialization result is null",
                            dataCheckGroupId: null)
                    }
                });
                return Task.FromResult(results);
            }

            // ── 基础字段校验 ──
            results.Add(ValidateBaseFields(dto, wallId));

            // ── 墙体轮廓校验 ──
            results.Add(ValidateAacWallElevation(dto, wallId));

            // ── 芯层厚度校验 ──
            results.Add(ValidateCoreThickness(dto, wallId));

            // ── 钢柱组件校验（V001 新增） ──
            results.Add(ValidateColumnAssemblies(dto, wallId));

            // ── 钢筋校验 ──
            results.Add(ValidateRebars(dto, wallId));

            // ── 顶板校验 ──
            results.Add(ValidateTopPlate(dto, wallId));

            // ── 剪力键校验（V001 新增） ──
            results.Add(ValidateShearKeys(dto, wallId));

            // ── MEP电缆校验 ──
            results.Add(ValidateMepCables(dto, wallId));

            // ── MEP设备校验 ──
            results.Add(ValidateMepDevices(dto, wallId));

            // ── 开洞校验 ──
            results.Add(ValidateOpeningHoles(dto, wallId));

            // ── AAC切片校验 ──
            results.Add(ValidateAacSlices(dto, wallId));

            // ── 面板装饰校验（V001 新增） ──
            results.Add(ValidateFaceFinishes(dto, wallId));

            // ── 斜撑连接件校验（V001 新增） ──
            results.Add(ValidateProppingConnectors(dto, wallId));

            // ── 华夫板链接校验（V001 新增） ──
            results.Add(ValidateWaffleSlabLinks(dto, wallId));

            // ── XPS保温校验 ──
            results.Add(ValidateXps(dto, wallId));

            return Task.FromResult(results);
        }

        // ==================== MomData 校验 ====================

        public Task<List<FeatureCategoryResult>> ValidateMomDataAsync(string momJsonData, string wallId)
        {
            var results = new List<FeatureCategoryResult>();

            MomWall? momWall = null;
            try
            {
                momWall = System.Text.Json.JsonSerializer.Deserialize<MomWall>(momJsonData, SharedJsonOptions.Instance);

                if (momWall != null)
                {
                    foreach (var f in momWall.Features)
                        f.RestoreFaceFromInitialSide();
                }
            }
            catch (Exception ex)
            {
                results.Add(new FeatureCategoryResult
                {
                    CategoryName = "MomJson",
                    CategoryNameCn = "MomJSON解析",
                    CheckItemCount = 1,
                    CriticalCount = 1,
                    Score = 0,
                    Errors = new List<ValidationErrorEntity>
                    {
                        new(
                            wallId: null, groupId: null!,
                            pipelineStage: PipelineStage.ValidatingMom,
                            errorMessage: $"MomJson 反序列化失败: {ex.Message}",
                            errorCode: "MOM_DESERIALIZE_FAILED",
                            severity: ErrorSeverity.Critical,
                            errorCategory: ErrorCategory.Mom,
                            featureCategory: "MomJson",
                            errorMessageEn: $"MomJson deserialization failed: {ex.Message}",
                            dataCheckGroupId: null)
                    }
                });
                return Task.FromResult(results);
            }

            if (momWall == null)
            {
                results.Add(new FeatureCategoryResult
                {
                    CategoryName = "MomJson",
                    CategoryNameCn = "MomJSON解析",
                    CheckItemCount = 1,
                    CriticalCount = 1,
                    Score = 0,
                    Errors = new List<ValidationErrorEntity>
                    {
                        new(
                            wallId: null, groupId: null!,
                            pipelineStage: PipelineStage.ValidatingMom,
                            errorMessage: "MomJson 反序列化结果为 null",
                            errorCode: "MOM_DESERIALIZE_NULL",
                            severity: ErrorSeverity.Critical,
                            errorCategory: ErrorCategory.Mom,
                            featureCategory: "MomJson",
                            errorMessageEn: "MomJson deserialization result is null",
                            dataCheckGroupId: null)
                    }
                });
                return Task.FromResult(results);
            }

            // ── 墙体几何校验 ──
            results.Add(ValidateWallGeometry(momWall, wallId));

            // ── 基本属性校验 ──
            results.Add(ValidateBasicProperties(momWall, wallId));

            // ── 特征校验 ──
            results.Add(ValidateFeatures(momWall, wallId));

            // ── 空间变换校验 ──
            results.Add(ValidateTransform(momWall, wallId));

            return Task.FromResult(results);
        }

        // ==================== BimData 各特征校验方法 ====================

        private FeatureCategoryResult ValidateBaseFields(BimWallDtoV001 dto, string wallId)
        {
            var errors = new List<ValidationErrorEntity>();
            int checkCount = 3;

            if (string.IsNullOrWhiteSpace(dto.Id))
                errors.Add(CreateBimError(wallId, "baseFields", "BIM_ID_EMPTY", "墙体Id不能为空", "Wall Id is required", ErrorSeverity.Critical));

            if (string.IsNullOrWhiteSpace(dto.Schema))
                errors.Add(CreateBimError(wallId, "baseFields", "BIM_SCHEMA_EMPTY", "Schema字段不能为空", "Schema field is required", ErrorSeverity.Error));

            if (string.IsNullOrWhiteSpace(dto.Pn))
                errors.Add(CreateBimError(wallId, "baseFields", "BIM_PN_EMPTY", "项目号Pn不能为空", "Project number Pn is required", ErrorSeverity.Warning));

            return BuildFeatureResult("baseFields", checkCount, errors);
        }

        private FeatureCategoryResult ValidateAacWallElevation(BimWallDtoV001 dto, string wallId)
        {
            var errors = new List<ValidationErrorEntity>();
            int checkCount = 3;

            if (dto.AacWallElevation == null)
            {
                errors.Add(CreateBimError(wallId, "aacWallElevation", "BIM_ELEVATION_NULL",
                    "AacWallElevation 不能为空", "AacWallElevation is required", ErrorSeverity.Critical));
            }
            else
            {
                var contour = dto.AacWallElevation.Contour;
                if (contour == null || contour.Count < 3)
                {
                    errors.Add(CreateBimError(wallId, "aacWallElevation", "BIM_ELEVATION_CONTOUR_INVALID",
                        $"轮廓点数不足（需要≥3，当前{contour?.Count ?? 0}）",
                        $"Contour point count insufficient (need ≥3, current {contour?.Count ?? 0})",
                        ErrorSeverity.Critical));
                }
            }

            if (dto.CoreHeight <= 0)
                errors.Add(CreateBimError(wallId, "aacWallElevation", "BIM_CORE_HEIGHT_INVALID",
                    $"芯层高度无效（当前{dto.CoreHeight}），需大于0",
                    $"Core height invalid ({dto.CoreHeight}), must be greater than 0",
                    ErrorSeverity.Error));

            return BuildFeatureResult("aacWallElevation", checkCount, errors);
        }

        private FeatureCategoryResult ValidateCoreThickness(BimWallDtoV001 dto, string wallId)
        {
            var errors = new List<ValidationErrorEntity>();
            int checkCount = 1;

            if (dto.CoreThickness < 100)
                errors.Add(CreateBimError(wallId, "coreThickness", "BIM_THICKNESS_TOO_SMALL",
                    $"芯层厚度过小（当前{dto.CoreThickness}mm，需≥100mm）",
                    $"Core thickness too small (current {dto.CoreThickness}mm, need ≥100mm)",
                    ErrorSeverity.Error));

            return BuildFeatureResult("coreThickness", checkCount, errors);
        }

        private FeatureCategoryResult ValidateColumnAssemblies(BimWallDtoV001 dto, string wallId)
        {
            var errors = new List<ValidationErrorEntity>();
            int checkCount = 1;

            if (dto.ColumnAssemblies == null || dto.ColumnAssemblies.Count == 0)
            {
                errors.Add(CreateBimError(wallId, "columnAssemblies", "BIM_COLUMN_ASSEMBLIES_EMPTY",
                    "钢柱组件列表为空，墙体缺少结构支撑定义",
                    "Column assemblies list is empty, wall missing structural support definition",
                    ErrorSeverity.Warning));
            }
            else
            {
                checkCount = dto.ColumnAssemblies.Count;
                for (int i = 0; i < dto.ColumnAssemblies.Count; i++)
                {
                    var col = dto.ColumnAssemblies[i];
                    if (string.IsNullOrWhiteSpace(col.Pn))
                        errors.Add(CreateBimError(wallId, "columnAssemblies", "BIM_COL_ASSEMBLY_PN_EMPTY",
                            $"第{i + 1}个钢柱组件Pn为空", $"Column assembly #{i + 1} Pn is empty", ErrorSeverity.Error));

                    if (col.Origin == null)
                        errors.Add(CreateBimError(wallId, "columnAssemblies", "BIM_COL_ASSEMBLY_ORIGIN_NULL",
                            $"第{i + 1}个钢柱组件Origin为空", $"Column assembly #{i + 1} Origin is null", ErrorSeverity.Error));

                    if (col.Height <= 0)
                        errors.Add(CreateBimError(wallId, "columnAssemblies", "BIM_COL_ASSEMBLY_HEIGHT_INVALID",
                            $"第{i + 1}个钢柱组件高度无效（{col.Height}mm）",
                            $"Column assembly #{i + 1} height invalid ({col.Height}mm)",
                            ErrorSeverity.Warning));
                }
            }

            return BuildFeatureResult("columnAssemblies", checkCount, errors);
        }

        private FeatureCategoryResult ValidateRebars(BimWallDtoV001 dto, string wallId)
        {
            var errors = new List<ValidationErrorEntity>();
            int checkCount = 1;

            if (dto.Rebars == null)
            {
                errors.Add(CreateBimError(wallId, "rebars", "BIM_REBARS_NULL",
                    "钢筋数据为空", "Rebar data is null", ErrorSeverity.Warning));
            }

            return BuildFeatureResult("rebars", checkCount, errors);
        }

        private FeatureCategoryResult ValidateTopPlate(BimWallDtoV001 dto, string wallId)
        {
            var errors = new List<ValidationErrorEntity>();
            int checkCount = 1;

            if (dto.TopPlate == null || dto.TopPlate.Count == 0)
            {
                errors.Add(CreateBimError(wallId, "topPlate", "BIM_TOP_PLATE_EMPTY",
                    "顶板数据为空", "Top plate data is empty", ErrorSeverity.Warning));
            }
            else
            {
                checkCount = dto.TopPlate.Count;
            }

            return BuildFeatureResult("topPlate", checkCount, errors);
        }

        private FeatureCategoryResult ValidateShearKeys(BimWallDtoV001 dto, string wallId)
        {
            var errors = new List<ValidationErrorEntity>();
            int checkCount = 1;

            if (dto.ShearKeys == null)
            {
                errors.Add(CreateBimError(wallId, "shearKeys", "BIM_SHEAR_KEYS_NULL",
                    "剪力键数据为空", "Shear keys data is null", ErrorSeverity.Info));
            }
            else
            {
                if (string.IsNullOrWhiteSpace(dto.ShearKeys.Pn))
                    errors.Add(CreateBimError(wallId, "shearKeys", "BIM_SHEAR_KEYS_PN_EMPTY",
                        "剪力键Pn为空", "Shear keys Pn is empty", ErrorSeverity.Warning));

                if (dto.ShearKeys.Points == null || dto.ShearKeys.Points.Count == 0)
                    errors.Add(CreateBimError(wallId, "shearKeys", "BIM_SHEAR_KEYS_POINTS_EMPTY",
                        "剪力键点位为空", "Shear keys points is empty", ErrorSeverity.Warning));
                else
                    checkCount = dto.ShearKeys.Points.Count;
            }

            return BuildFeatureResult("shearKeys", checkCount, errors);
        }

        private FeatureCategoryResult ValidateMepCables(BimWallDtoV001 dto, string wallId)
        {
            var errors = new List<ValidationErrorEntity>();
            int checkCount = 1;

            if (dto.MepCables != null)
            {
                checkCount = dto.MepCables.Count;
                for (int i = 0; i < dto.MepCables.Count; i++)
                {
                    var cable = dto.MepCables[i];
                    if (string.IsNullOrWhiteSpace(cable.Pn))
                        errors.Add(CreateBimError(wallId, "mepCables", "BIM_MEP_CABLE_PN_EMPTY",
                            $"第{i + 1}个MEP电缆Pn为空", $"MEP cable #{i + 1} Pn is empty", ErrorSeverity.Error));
                }
            }

            return BuildFeatureResult("mepCables", checkCount, errors);
        }

        private FeatureCategoryResult ValidateMepDevices(BimWallDtoV001 dto, string wallId)
        {
            var errors = new List<ValidationErrorEntity>();
            int checkCount = 1;

            if (dto.MepDevices != null)
            {
                checkCount = dto.MepDevices.Count;
                for (int i = 0; i < dto.MepDevices.Count; i++)
                {
                    var device = dto.MepDevices[i];
                    if (string.IsNullOrWhiteSpace(device.Pn))
                        errors.Add(CreateBimError(wallId, "mepDevices", "BIM_MEP_DEVICE_PN_EMPTY",
                            $"第{i + 1}个MEP设备Pn为空", $"MEP device #{i + 1} Pn is empty", ErrorSeverity.Error));
                }
            }

            return BuildFeatureResult("mepDevices", checkCount, errors);
        }

        private FeatureCategoryResult ValidateOpeningHoles(BimWallDtoV001 dto, string wallId)
        {
            var errors = new List<ValidationErrorEntity>();
            int checkCount = 1;

            if (dto.OpeningHoles != null)
            {
                checkCount = dto.OpeningHoles.Count;
                for (int i = 0; i < dto.OpeningHoles.Count; i++)
                {
                    var hole = dto.OpeningHoles[i];
                    if (string.IsNullOrWhiteSpace(hole.Uuid))
                        errors.Add(CreateBimError(wallId, "openingHoles", "BIM_OPENING_HOLE_UUID_EMPTY",
                            $"第{i + 1}个窗户Uuid为空", $"Opening hole #{i + 1} Uuid is empty", ErrorSeverity.Warning));
                }
            }

            return BuildFeatureResult("openingHoles", checkCount, errors);
        }

        private FeatureCategoryResult ValidateAacSlices(BimWallDtoV001 dto, string wallId)
        {
            var errors = new List<ValidationErrorEntity>();
            int checkCount = 1;

            if (dto.AacSlices == null || dto.AacSlices.Count == 0)
            {
                errors.Add(CreateBimError(wallId, "aacSlices", "BIM_AAC_SLICES_EMPTY",
                    "AAC砖数据为空", "AAC slices data is empty", ErrorSeverity.Warning));
            }
            else
            {
                checkCount = dto.AacSlices.Count;
                for (int i = 0; i < dto.AacSlices.Count; i++)
                {
                    var slice = dto.AacSlices[i];
                    if (string.IsNullOrWhiteSpace(slice.Id))
                        errors.Add(CreateBimError(wallId, "aacSlices", "BIM_AAC_SLICE_ID_EMPTY",
                            $"第{i + 1}个AAC砖Id为空", $"AAC slice #{i + 1} Id is empty", ErrorSeverity.Error));

                    if (slice.Contour == null || slice.Contour.Count < 3)
                        errors.Add(CreateBimError(wallId, "aacSlices", "BIM_AAC_SLICE_CONTOUR_INVALID",
                            $"第{i + 1}个AAC砖轮廓无效", $"AAC slice #{i + 1} contour invalid", ErrorSeverity.Warning));
                }
            }

            return BuildFeatureResult("aacSlices", checkCount, errors);
        }

        private FeatureCategoryResult ValidateFaceFinishes(BimWallDtoV001 dto, string wallId)
        {
            var errors = new List<ValidationErrorEntity>();
            int checkCount = 1;

            if (dto.FaceFinishes == null || dto.FaceFinishes.Count == 0)
            {
                errors.Add(CreateBimError(wallId, "faceFinishes", "BIM_FACE_FINISHES_EMPTY",
                    "面板装饰数据为空", "Face finishes data is empty", ErrorSeverity.Info));
            }
            else
            {
                checkCount = dto.FaceFinishes.Count;
                for (int i = 0; i < dto.FaceFinishes.Count; i++)
                {
                    var faceFinish = dto.FaceFinishes[i];
                    if (faceFinish.Finishes == null || faceFinish.Finishes.Count == 0)
                        errors.Add(CreateBimError(wallId, "faceFinishes", "BIM_FACE_FINISH_NO_FINISHES",
                            $"第{i + 1}个面板装饰无finish数据", $"Face finish #{i + 1} has no finish data", ErrorSeverity.Warning));
                }
            }

            return BuildFeatureResult("faceFinishes", checkCount, errors);
        }

        private FeatureCategoryResult ValidateProppingConnectors(BimWallDtoV001 dto, string wallId)
        {
            var errors = new List<ValidationErrorEntity>();
            int checkCount = 1;

            if (dto.ProppingConnectors == null)
            {
                errors.Add(CreateBimError(wallId, "proppingConnectors", "BIM_PROPPING_CONNECTORS_NULL",
                    "斜撑连接件数据为空", "Propping connectors data is null", ErrorSeverity.Info));
            }
            else
            {
                int totalItems = 0;
                totalItems += dto.ProppingConnectors.ColumnBracket?.Count ?? 0;
                totalItems += dto.ProppingConnectors.Standard?.Count ?? 0;
                totalItems += dto.ProppingConnectors.TopBracket?.Count ?? 0;
                totalItems += dto.ProppingConnectors.TypeA?.Count ?? 0;
                totalItems += dto.ProppingConnectors.TypeB?.Count ?? 0;
                totalItems += dto.ProppingConnectors.TypeC?.Count ?? 0;
                totalItems += dto.ProppingConnectors.TypeD?.Count ?? 0;

                checkCount = Math.Max(totalItems, 1);

                if (totalItems == 0)
                    errors.Add(CreateBimError(wallId, "proppingConnectors", "BIM_PROPPING_CONNECTORS_EMPTY",
                        "斜撑连接件所有类型均为空", "All propping connector types are empty", ErrorSeverity.Info));
            }

            return BuildFeatureResult("proppingConnectors", checkCount, errors);
        }

        private FeatureCategoryResult ValidateWaffleSlabLinks(BimWallDtoV001 dto, string wallId)
        {
            var errors = new List<ValidationErrorEntity>();
            int checkCount = 1;

            if (dto.WaffleSlabLinks == null || dto.WaffleSlabLinks.Count == 0)
            {
                errors.Add(CreateBimError(wallId, "waffleSlabLinks", "BIM_WAFFLE_SLAB_LINKS_EMPTY",
                    "华夫板链接数据为空", "Waffle slab links data is empty", ErrorSeverity.Info));
            }
            else
            {
                checkCount = dto.WaffleSlabLinks.Count;
                for (int i = 0; i < dto.WaffleSlabLinks.Count; i++)
                {
                    var link = dto.WaffleSlabLinks[i];
                    if (string.IsNullOrWhiteSpace(link.Id))
                        errors.Add(CreateBimError(wallId, "waffleSlabLinks", "BIM_WAFFLE_LINK_ID_EMPTY",
                            $"第{i + 1}个华夫板链接Id为空", $"Waffle slab link #{i + 1} Id is empty", ErrorSeverity.Warning));
                }
            }

            return BuildFeatureResult("waffleSlabLinks", checkCount, errors);
        }

        private FeatureCategoryResult ValidateXps(BimWallDtoV001 dto, string wallId)
        {
            var errors = new List<ValidationErrorEntity>();
            int checkCount = 1;

            if (dto.Xps == null || dto.Xps.Count == 0)
            {
                errors.Add(CreateBimError(wallId, "xps", "BIM_XPS_EMPTY",
                    "XPS保温数据为空", "XPS data is empty", ErrorSeverity.Info));
            }
            else
            {
                checkCount = dto.Xps.Count;
                for (int i = 0; i < dto.Xps.Count; i++)
                {
                    var xps = dto.Xps[i];
                    if (string.IsNullOrWhiteSpace(xps.Pn))
                        errors.Add(CreateBimError(wallId, "xps", "BIM_XPS_PN_EMPTY",
                            $"第{i + 1}个XPS的Pn为空", $"XPS #{i + 1} Pn is empty", ErrorSeverity.Warning));

                    if (xps.Height <= 0)
                        errors.Add(CreateBimError(wallId, "xps", "BIM_XPS_HEIGHT_INVALID",
                            $"第{i + 1}个XPS高度无效（{xps.Height}mm）",
                            $"XPS #{i + 1} height invalid ({xps.Height}mm)",
                            ErrorSeverity.Warning));
                }
            }

            return BuildFeatureResult("xps", checkCount, errors);
        }

        // ==================== MomData 各特征校验方法 ====================

        private FeatureCategoryResult ValidateWallGeometry(MomWall momWall, string wallId)
        {
            var errors = new List<ValidationErrorEntity>();
            int checkCount = 3;

            if (momWall.Outline == null || momWall.Outline.Count < 3)
                errors.Add(CreateMomError(wallId, "WallGeometry", "MOM_OUTLINE_INVALID",
                    $"轮廓点数不足（需要≥3，当前{momWall.Outline?.Count ?? 0}）",
                    $"Outline point count insufficient (need ≥3, current {momWall.Outline?.Count ?? 0})",
                    ErrorSeverity.Critical));

            if (momWall.Thickness <= 0)
                errors.Add(CreateMomError(wallId, "WallGeometry", "MOM_THICKNESS_INVALID",
                    $"墙体厚度无效（当前{momWall.Thickness}mm）",
                    $"Wall thickness invalid ({momWall.Thickness}mm)",
                    ErrorSeverity.Error));

            if (momWall.Length <= 0 || momWall.Width <= 0)
                errors.Add(CreateMomError(wallId, "WallGeometry", "MOM_DIMENSION_INVALID",
                    $"墙体尺寸无效（长{momWall.Length}×宽{momWall.Width}mm）",
                    $"Wall dimension invalid (L{momWall.Length}×W{momWall.Width}mm)",
                    ErrorSeverity.Error));

            return BuildFeatureResult("WallGeometry", checkCount, errors);
        }

        private FeatureCategoryResult ValidateBasicProperties(MomWall momWall, string wallId)
        {
            var errors = new List<ValidationErrorEntity>();
            int checkCount = 2;

            if (string.IsNullOrWhiteSpace(momWall.Id))
                errors.Add(CreateMomError(wallId, "BasicProperties", "MOM_ID_EMPTY",
                    "MomWall.Id为空", "MomWall.Id is empty", ErrorSeverity.Critical));

            if (string.IsNullOrWhiteSpace(momWall.Material))
                errors.Add(CreateMomError(wallId, "BasicProperties", "MOM_MATERIAL_EMPTY",
                    "MomWall.Material为空", "MomWall.Material is empty", ErrorSeverity.Warning));

            return BuildFeatureResult("BasicProperties", checkCount, errors);
        }

        private FeatureCategoryResult ValidateFeatures(MomWall momWall, string wallId)
        {
            var errors = new List<ValidationErrorEntity>();
            int checkCount = 1 + (momWall.Features?.Count ?? 0);

            if (momWall.Features != null)
            {
                foreach (var feature in momWall.Features)
                {
                    if (string.IsNullOrWhiteSpace(feature.Id))
                        errors.Add(CreateMomError(wallId, "Features", "MOM_FEATURE_ID_EMPTY",
                            "加工特征Id为空", "Feature Id is empty", ErrorSeverity.Error));

                    if (feature.Depth <= 0)
                        errors.Add(CreateMomError(wallId, "Features", "MOM_FEATURE_DEPTH_INVALID",
                            $"加工特征[{feature.Id}]深度无效（{feature.Depth}mm）",
                            $"Feature [{feature.Id}] depth invalid ({feature.Depth}mm)",
                            ErrorSeverity.Warning));
                }
            }

            return BuildFeatureResult("Features", Math.Max(checkCount, 1), errors);
        }

        private FeatureCategoryResult ValidateTransform(MomWall momWall, string wallId)
        {
            var errors = new List<ValidationErrorEntity>();
            int checkCount = 1;

            var trans = momWall.Transform?.Translation;
            if (Math.Abs(trans?.X ?? 0) > 50000 ||
                Math.Abs(trans?.Y ?? 0) > 50000 ||
                Math.Abs(trans?.Z ?? 0) > 50000)
            {
                errors.Add(CreateMomError(wallId, "Transform", "MOM_TRANSFORM_SUSPICIOUS",
                    "空间变换位置分量值异常（超出50000mm），请确认数据正确性",
                    "Transform position component suspicious ( > 50000mm ), please verify",
                    ErrorSeverity.Warning));
            }

            return BuildFeatureResult("Transform", checkCount, errors);
        }

        // ==================== 辅助方法 ====================

        private static ValidationErrorEntity CreateBimError(
            string wallId, string featureCategory, string errorCode,
            string messageCn, string messageEn, ErrorSeverity severity)
        {
            return new ValidationErrorEntity(
                wallId: null,
                groupId: null!,
                pipelineStage: PipelineStage.ValidatingBim,
                errorMessage: messageCn,
                errorCode: errorCode,
                severity: severity,
                errorCategory: ErrorCategory.Bim,
                featureCategory: featureCategory,
                errorMessageEn: messageEn,
                dataCheckGroupId: null);
        }

        private static ValidationErrorEntity CreateMomError(
            string wallId, string featureCategory, string errorCode,
            string messageCn, string messageEn, ErrorSeverity severity)
        {
            return new ValidationErrorEntity(
                wallId: null,
                groupId: null!,
                pipelineStage: PipelineStage.ValidatingMom,
                errorMessage: messageCn,
                errorCode: errorCode,
                severity: severity,
                errorCategory: ErrorCategory.Mom,
                featureCategory: featureCategory,
                errorMessageEn: messageEn,
                dataCheckGroupId: null);
        }

        private FeatureCategoryResult BuildFeatureResult(
            string categoryName, int checkCount, List<ValidationErrorEntity> errors)
        {
            return new FeatureCategoryResult
            {
                CategoryName = categoryName,
                CategoryNameCn = FeatureNameCnMap.TryGetValue(categoryName, out var cn) ? cn : categoryName,
                CheckItemCount = checkCount,
                CriticalCount = errors.Count(e => e.Severity == ErrorSeverity.Critical),
                ErrorCount = errors.Count(e => e.Severity == ErrorSeverity.Error),
                WarningCount = errors.Count(e => e.Severity == ErrorSeverity.Warning),
                InfoCount = errors.Count(e => e.Severity == ErrorSeverity.Info),
                Score = DataCheckValidatorV000.CalculateScore(errors),
                Errors = errors
            };
        }
    }
}
