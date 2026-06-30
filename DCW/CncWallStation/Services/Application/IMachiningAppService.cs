using CncWallStation.Models.Dtos;
using CncWallStation.Models.Entities;

namespace CncWallStation.Services.Application
{
    /// <summary>
    /// 加工控制应用服务接口
    /// </summary>
    public interface IMachiningAppService
    {
        /// <summary>获取待加工墙体队列（按优先级降序，仅 Status=待加工，前 N 条）</summary>
        Task<List<WallQueueItemDto>> GetWallQueueAsync(int topCount = 5);

        /// <summary>获取墙体详情（用于加工控制页）</summary>
        Task<WallInfoDto?> GetWallInfoAsync(string wallId);

        /// <summary>开始加工</summary>
        Task StartMachiningAsync(long wallId, string operatorName);

        /// <summary>暂停加工</summary>
        Task PauseMachiningAsync(long wallId);

        /// <summary>从暂停恢复</summary>
        Task ResumeMachiningAsync(long wallId);

        /// <summary>急停</summary>
        Task EmergencyStopAsync(long wallId);

        /// <summary>复位（恢复为待加工）</summary>
        Task ResetMachiningAsync(long wallId);

        /// <summary>标记异常</summary>
        Task MarkExceptionAsync(long wallId, string operatorName);

        /// <summary>完成加工（进入待质检状态，生成加工记录）</summary>
        Task CompleteMachiningAsync(long wallId, string operatorName);

        /// <summary>从异常恢复为待加工</summary>
        Task RecoverFromExceptionAsync(long wallId, string operatorName);

        /// <summary>获取墙体的加工记录</summary>
        Task<MachiningRecordEntity?> GetMachiningRecordAsync(long wallId);

        /// <summary>获取墙体的 PLC 指令数量</summary>
        Task<int> GetInstructionCountAsync(long wallId);

        /// <summary>按 WallId 模糊搜索墙体（限前20条）</summary>
        Task<List<WallQueueItemDto>> SearchWallsByWallIdAsync(string keyword);
    }
}
