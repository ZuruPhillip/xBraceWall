namespace CncWallStation.Models.Enums
{
    /// <summary>
    /// 数据管线阶段枚举
    /// </summary>
    public enum PipelineStage
    {
        /// <summary>BimJSON 待校验</summary>
        Imported = 0,

        /// <summary>BimJSON 校验中</summary>
        ValidatingBim = 1,

        /// <summary>BimJSON 校验通过</summary>
        BimValid = 2,

        /// <summary>BimJSON 校验失败（终止）</summary>
        BimInvalid = 3,

        /// <summary>BimJSON → MomJSON 转换中</summary>
        Converting = 4,

        /// <summary>转换失败（终止）</summary>
        ConversionFailed = 5,

        /// <summary>转换完成，MomJSON 待校验</summary>
        Converted = 6,

        /// <summary>MomJSON 校验中</summary>
        ValidatingMom = 7,

        /// <summary>MomJSON 校验通过</summary>
        MomValid = 8,

        /// <summary>MomJSON 校验失败（终止）</summary>
        MomInvalid = 9,

        /// <summary>全部通过，Status 设为待加工</summary>
        Ready = 10
    }

    /// <summary>
    /// PipelineStage 扩展方法
    /// </summary>
    public static class PipelineStageExtensions
    {
        /// <summary>获取管线阶段的中文显示文本</summary>
        public static string ToDisplayText(this PipelineStage stage) => stage switch
        {
            PipelineStage.Imported => "待校验",
            PipelineStage.ValidatingBim => "校验Bim中",
            PipelineStage.BimValid => "Bim校验通过",
            PipelineStage.BimInvalid => "Bim校验失败",
            PipelineStage.Converting => "转换中",
            PipelineStage.ConversionFailed => "转换失败",
            PipelineStage.Converted => "已转换",
            PipelineStage.ValidatingMom => "校验Mom中",
            PipelineStage.MomValid => "Mom校验通过",
            PipelineStage.MomInvalid => "Mom校验失败",
            PipelineStage.Ready => "待加工",
            _ => "未知"
        };

        /// <summary>获取管线阶段的英文显示文本</summary>
        public static string ToDisplayTextEn(this PipelineStage stage) => stage switch
        {
            PipelineStage.Imported => "Pending Validation",
            PipelineStage.ValidatingBim => "Validating BIM",
            PipelineStage.BimValid => "BIM Valid",
            PipelineStage.BimInvalid => "BIM Invalid",
            PipelineStage.Converting => "Converting",
            PipelineStage.ConversionFailed => "Conversion Failed",
            PipelineStage.Converted => "Converted",
            PipelineStage.ValidatingMom => "Validating MOM",
            PipelineStage.MomValid => "MOM Valid",
            PipelineStage.MomInvalid => "MOM Invalid",
            PipelineStage.Ready => "Ready",
            _ => "Unknown"
        };
    }
}
