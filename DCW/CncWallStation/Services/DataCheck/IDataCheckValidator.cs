using CncWallStation.Models.Dtos;

namespace CncWallStation.Services.DataCheck
{
    /// <summary>
    /// 版本化数据校验器接口 — 参照 IBimWallMapper 模式
    /// 每种 BimData 版本实现独立的校验器，MomData 固定结构共用一个校验器
    /// </summary>
    public interface IDataCheckValidator
    {
        /// <summary>支持的 BimJson 数据版本号（如 "0.0.0"）</summary>
        string SupportedVersion { get; }

        /// <summary>
        /// 执行 BimData 校验
        /// </summary>
        /// <param name="bimJsonData">BimJson 原始数据</param>
        /// <param name="wallId">墙体标识</param>
        /// <returns>BimData 特征校验结果列表</returns>
        Task<List<FeatureCategoryResult>> ValidateBimDataAsync(string bimJsonData, string wallId);

        /// <summary>
        /// 执行 MomData 校验（固定结构，所有版本共用实现）
        /// </summary>
        /// <param name="momJsonData">MomJson 原始数据</param>
        /// <param name="wallId">墙体标识</param>
        /// <returns>MomData 特征校验结果列表</returns>
        Task<List<FeatureCategoryResult>> ValidateMomDataAsync(string momJsonData, string wallId);
    }
}
