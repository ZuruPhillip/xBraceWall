using CncWallStation.Models.Dtos;
using CncWallStation.Models.Entities;

namespace CncWallStation.Services.Application
{
    /// <summary>
    /// 异常报告应用服务接口
    /// </summary>
    public interface IExceptionReportAppService
    {
        /// <summary>保存新的异常报告</summary>
        Task<MachiningExceptionEntity> SaveReportAsync(MachiningExceptionEntity entity);

        /// <summary>查询墙体的历史异常报告列表（包含 Wall 表字符串 WallId）</summary>
        Task<List<ExceptionReportDto>> GetReportsByWallIdAsync(long wallId);

        /// <summary>更新现有异常报告（编辑）</summary>
        Task UpdateReportAsync(long reportId, int exceptionType, string? customType, string description, string? photoPaths);

        /// <summary>标记异常已解决</summary>
        Task ResolveReportAsync(long reportId);

        /// <summary>获取单个异常报告</summary>
        Task<MachiningExceptionEntity?> GetReportAsync(long reportId);

        /// <summary>分页查询历史异常报告</summary>
        Task<PagedResult<ExceptionReportDto>> GetPagedReportsAsync(long? wallId, int pageIndex, int pageSize);
    }
}
