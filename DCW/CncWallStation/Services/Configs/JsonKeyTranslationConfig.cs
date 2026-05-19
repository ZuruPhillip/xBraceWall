using System.Collections.Generic;

namespace CncWallStation.Services.Configs;

/// <summary>
/// JSON Key 中文翻译字典配置
/// 树视图和编辑区通过此字典将英文 Key 显示为中文
/// </summary>
public class JsonKeyTranslationConfig
{
    private readonly Dictionary<string, string> _translations = new()
    {
        // === 通用字段 ===
        { "Schema", "架构版本" },
        { "Version", "版本号" },
        { "Id", "标识" },
        { "Name", "名称" },
        { "Type", "类型" },
        { "Description", "描述" },

        // === 墙体基础信息 ===
        { "WallId", "墙体ID" },
        { "ProjectNumber", "项目编号" },
        { "Floor", "楼层" },
        { "Length", "长度" },
        { "Width", "宽度" },
        { "Height", "高度" },
        { "Thickness", "厚度" },
        { "Area", "面积" },
        { "Volume", "体积" },
        { "Position", "位置" },
        { "Rotation", "旋转角度" },
        { "Origin", "原点" },
        { "Center", "中心点" },

        // === 坐标信息 ===
        { "X", "X坐标" },
        { "Y", "Y坐标" },
        { "Z", "Z坐标" },
        { "StartPoint", "起点" },
        { "EndPoint", "终点" },
        { "Contour", "轮廓" },
        { "Points", "坐标点集" },
        { "Polygon", "多边形" },
        { "BoundingBox", "包围盒" },
        { "Min", "最小值" },
        { "Max", "最大值" },

        // === 墙体特征 ===
        { "Features", "特征列表" },
        { "Openings", "开洞" },
        { "Rebar", "钢筋" },
        { "Conduit", "线管" },
        { "EmbeddedParts", "预埋件" },
        { "XPS", "挤塑板" },
        { "SteelColumn", "钢立柱" },
        { "TieRod", "拉杆" },
        { "TopPlate", "顶板" },

        // === 开洞相关 ===
        { "OpeningType", "开洞类型" },
        { "Diameter", "直径" },
        { "Shape", "形状" },
        { "Rectangle", "矩形" },
        { "Circle", "圆形" },
        { "Offset", "偏移量" },

        // === 钢筋相关 ===
        { "Spacing", "间距" },
        { "Count", "数量" },
        { "Direction", "方向" },
        { "Horizontal", "水平" },
        { "Vertical", "垂直" },
        { "Layer", "层" },

        // === 材质信息 ===
        { "Material", "材质" },
        { "MaterialType", "材质类型" },
        { "ConcreteGrade", "混凝土等级" },
        { "SteelGrade", "钢材等级" },
        { "Density", "密度" },

        // === 工艺参数 ===
        { "ProcessParams", "工艺参数" },
        { "CuttingDepth", "切削深度" },
        { "FeedRate", "进给速度" },
        { "SpindleSpeed", "主轴转速" },
        { "ToolType", "刀具类型" },
        { "ToolDiameter", "刀具直径" },

        // === 结构信息 ===
        { "Level", "层级" },
        { "Levels", "层级列表" },
        { "Elevation", "标高" },
        { "TopElevation", "顶部标高" },
        { "BottomElevation", "底部标高" },

        // === 状态/标记 ===
        { "Status", "状态" },
        { "IsActive", "是否激活" },
        { "IsValid", "是否有效" },
        { "Enabled", "启用" },
        { "Disabled", "禁用" },
        { "Visible", "可见" },
        { "Hidden", "隐藏" },

        // === 其他 ===
        { "Properties", "属性" },
        { "Parameters", "参数" },
        { "Metadata", "元数据" },
        { "Tags", "标签" },
        { "Comments", "备注" },
        { "Children", "子节点" },
        { "Items", "项目列表" },
        { "Data", "数据" },
        { "Value", "值" },
        { "Unit", "单位" },
        { "Color", "颜色" },
        { "Index", "序号" },
        { "Weight", "重量" },
        { "Price", "价格" },
    };

    /// <summary>
    /// 根据英文 Key 获取中文翻译，未找到时返回原始 Key
    /// </summary>
    public string GetTranslation(string key)
    {
        if (string.IsNullOrEmpty(key))
            return key ?? string.Empty;

        if (_translations.TryGetValue(key, out var translation))
            return translation;

        return key;
    }

    /// <summary>
    /// 获取翻译字典的只读副本
    /// </summary>
    public IReadOnlyDictionary<string, string> GetAllTranslations()
    {
        return _translations;
    }
}
