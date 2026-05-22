using AutoMapper;
using CncWallStation.Models.Dtos;
using CncWallStation.Models.Entities;

namespace CncWallStation.Services.Mappings
{
    /// <summary>
    /// AutoMapper 映射配置
    /// </summary>
    public class CncWallStationAutoMapperProfile : Profile
    {
        public CncWallStationAutoMapperProfile()
        {
            // ==================== WallEntity → WallDto（列表项） ====================
            CreateMap<WallEntity, WallDto>()
                .ForMember(d => d.SchemaVersion,
                    opt => opt.MapFrom(s => s.SchemaVersion ?? "V0.0.0"))
                .ForMember(d => d.StartProductionTime,
                    opt => opt.MapFrom(s => s.StartProductionTime))
                .ForMember(d => d.EndProductionTime,
                    opt => opt.MapFrom(s => s.EndProductionTime))
                .ForMember(d => d.ValidationErrorSummary,
                    opt => opt.MapFrom(s => MapValidationErrorsToSummary(s.ValidationErrors)));

            // ==================== WallEntity → WallDetailDto（详情） ====================
            CreateMap<WallEntity, WallDetailDto>()
                .IncludeBase<WallEntity, WallDto>()
                .ForMember(d => d.ValidationErrors,
                    opt => opt.MapFrom(s => s.ValidationErrors));

            // ==================== ProjectEntity → ProjectDto ====================
            CreateMap<ProjectEntity, ProjectDto>();

            // ==================== ValidationErrorEntity → ValidationErrorDto ====================
            CreateMap<ValidationErrorEntity, ValidationErrorDto>();

            // ==================== WallEntity → WallListItem（兼容现有 MVVM 展示层） ====================
            CreateMap<WallEntity, Models.WallListItem>()
                .ForMember(d => d.ProjectName,
                    opt => opt.MapFrom(s => s.ProjectName))
                .ForMember(d => d.WallName,
                    opt => opt.MapFrom(s => s.WallName))
                .ForMember(d => d.MjsonData,
                    opt => opt.MapFrom(s => s.BimJsonData))
                .ForMember(d => d.Priority,
                    opt => opt.MapFrom(s => s.Priority))
                .ForMember(d => d.Status,
                    opt => opt.MapFrom(s => (Models.ProcessStatus)s.Status))
                .ForMember(d => d.AuditStatus,
                    opt => opt.MapFrom(s => s.AuditStatus))
                .ForMember(d => d.SchemaVersion,
                    opt => opt.MapFrom(s => s.SchemaVersion ?? "V0.0.0"))
                .ForMember(d => d.ValidationErrorSummary,
                    opt => opt.MapFrom(s => MapValidationErrorsToSummary(s.ValidationErrors)))
                .ForMember(d => d.IsSelected, opt => opt.Ignore());
        }

        /// <summary>校验错误列表 → 摘要文本</summary>
        private static string? MapValidationErrorsToSummary(ICollection<ValidationErrorEntity>? errors)
        {
            if (errors == null || errors.Count == 0)
                return null;

            return string.Join("; ",
                errors.OrderByDescending(e => e.CreatedAt)
                      .Select(e => e.ErrorMessage));
        }
    }
}
