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
        Task UpdateReportAsync(long reportId, int exceptionType, string? customType, string description, string? photoPaths, DateTime occurredAt, int frequencyCount);

        /// <summary>标记异常已解决，并保存维修信息</summary>
        Task ResolveReportAsync(long reportId, string repairMethod, string resolver, decimal? repairDuration, DateTime? completionTime, string? improvementSuggestion, string? remarks);

        /// <summary>获取单个异常报告</summary>
        Task<MachiningExceptionEntity?> GetReportAsync(long reportId);

        /// <summary>分页查询历史异常报告（支持异常类型、时间段、是否解决过滤）</summary>
        Task<PagedResult<ExceptionReportDto>> GetPagedReportsAsync(long? wallId, int? exceptionType, DateTime? startDate, DateTime? endDate, bool? isResolved, int pageIndex, int pageSize);

        /// <summary>导出用全量查询（不分页，支持异常类型、时间段、是否解决过滤）</summary>
        Task<List<ExceptionReportDto>> GetAllReportsForExportAsync(long? wallId, int? exceptionType, DateTime? startDate, DateTime? endDate, bool? isResolved);
    }
}
