namespace CncWallStation.Models.Enums
{
    /// <summary>
    /// 数据管线阶段枚举
    /// </summary>
    public enum PipelineStage
    {
        /// <summary>BimJSON 已导入，待校验</summary>
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
}
