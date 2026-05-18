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
                .ForMember(d => d.Version,
                    opt => opt.MapFrom(s => s.Project != null ? s.Project.Version : 0))
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
                .ForMember(d => d.HouseNumber,
                    opt => opt.MapFrom(s => s.ProjectNumber))
                .ForMember(d => d.MjsonData,
                    opt => opt.MapFrom(s => s.BimJsonData))
                .ForMember(d => d.Priority,
                    opt => opt.MapFrom(s => (Models.ProcessPriority)s.Priority))
                .ForMember(d => d.Status,
                    opt => opt.MapFrom(s => (Models.ProcessStatus)s.Status))
                .ForMember(d => d.Version,
                    opt => opt.MapFrom(s => s.Project != null ? s.Project.Version : 0))
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
