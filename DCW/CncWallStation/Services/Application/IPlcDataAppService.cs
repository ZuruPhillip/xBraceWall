using CncWallStation.Models.Dtos;
using CncWallStation.Models.Entities;
using CncWallStation.Plcs;

namespace CncWallStation.Services.Application
{
    /// <summary>
    /// PLC 数据应用服务接口
    /// </summary>
    public interface IPlcDataAppService
    {
        /// <summary>根据墙体 Id 查询墙体基本信息</summary>
        Task<WallInfoDto?> GetWallInfoAsync(string wallId);

        /// <summary>
        /// 按 Handler 分组生成 PLC 指令，并写入 PlcInstructionEntity 表
        /// 返回分组结果
        /// </summary>
        Task<List<PlcFeatureGroup>> GeneratePlcInstructionsGroupedAsync(long wallId);

        /// <summary>从 PLC 指令表加载已保存的指令</summary>
        Task<List<PlcInstructionEntity>> LoadInstructionsAsync(long wallId);

        /// <summary>
        /// 保存指令草稿：先删除墙体旧指令，再批量插入新指令，同步设置 UpdatedBy / UpdatedAt
        /// </summary>
        Task SaveDraftAsync(long wallId, List<PlcInstructionEntity> instructions, string updatedBy);
    }
}
