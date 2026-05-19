using CncWallStation.Models.Enums;
using Volo.Abp.Domain.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CncWallStation.Models.Entities
{
    /// <summary>
    /// 墙体数据表实体（ABP 风格）
    /// </summary>
    [Table("Wall")]
    public class WallEntity : Entity<long>
    {
        // ==================== 构造函数 ====================

        /// <summary>EF Core 专用无参构造函数</summary>
        protected WallEntity() { }

        /// <summary>创建新墙体实体</summary>
        public WallEntity(
            int projectId,
            string wallId,
            string projectNumber,
            int floor,
            string bimJsonData)
        {
            ProjectId = projectId;
            WallId = wallId ?? throw new ArgumentNullException(nameof(wallId));
            ProjectNumber = projectNumber ?? throw new ArgumentNullException(nameof(projectNumber));
            Floor = floor;
            BimJsonData = bimJsonData ?? throw new ArgumentNullException(nameof(bimJsonData));
            PipelineStage = PipelineStage.Imported;
            Priority = 1;
            Status = 0;
            ImportTime = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }

        // ==================== 属性 ====================

        /// <summary>所属导入批次（外键 → Project.Id）</summary>
        [Required]
        public int ProjectId { get; private set; }

        /// <summary>墙体唯一标识</summary>
        [Required]
        [MaxLength(256)]
        public string WallId { get; private set; } = string.Empty;

        /// <summary>项目号（冗余加速查询）</summary>
        [Required]
        [MaxLength(256)]
        public string ProjectNumber { get; private set; } = string.Empty;

        /// <summary>楼层</summary>
        public int Floor { get; private set; }

        /// <summary>原始 BimJSON 数据（最大 16MB）</summary>
        [Required]
        [Column(TypeName = "MEDIUMTEXT")]
        public string BimJsonData { get; private set; } = string.Empty;

        /// <summary>管线阶段</summary>
        public PipelineStage PipelineStage { get; private set; } = PipelineStage.Imported;

        /// <summary>转换后的 MomJSON 数据（最大 16MB）</summary>
        [Column(TypeName = "MEDIUMTEXT")]
        public string? MomJsonData { get; private set; }

        /// <summary>加工优先级枚举值</summary>
        public int Priority { get; private set; } = 1;

        /// <summary>加工状态（仅 Ready 后为待加工）</summary>
        public int Status { get; private set; } = 0;

        /// <summary>导入时间</summary>
        [Required]
        public DateTime ImportTime { get; private set; } = DateTime.Now;

        /// <summary>最后更新时间</summary>
        [Required]
        public DateTime UpdatedAt { get; private set; } = DateTime.Now;

        /// <summary>最后修改人（当前 Windows 用户）</summary>
        [MaxLength(256)]
        public string? UpdatedBy { get; private set; }

        // ==================== 导航属性 ====================

        /// <summary>所属项目批次</summary>
        [ForeignKey(nameof(ProjectId))]
        public ProjectEntity Project { get; private set; } = null!;

        /// <summary>校验/转换失败详情列表</summary>
        public ICollection<ValidationErrorEntity> ValidationErrors { get; private set; } = new List<ValidationErrorEntity>();

        /// <summary>数据预检记录列表</summary>
        public ICollection<DataCheckRecordEntity> DataCheckRecords { get; private set; } = new List<DataCheckRecordEntity>();

        // ==================== 领域方法 ====================

        /// <summary>更新管线阶段</summary>
        public void UpdatePipelineStage(PipelineStage stage)
        {
            PipelineStage = stage;
            UpdatedAt = DateTime.Now;
        }

        /// <summary>更新优先级</summary>
        public void UpdatePriority(int priority, string updatedBy)
        {
            Priority = priority;
            UpdatedBy = updatedBy;
            UpdatedAt = DateTime.Now;
        }

        /// <summary>更新加工状态</summary>
        public void UpdateStatus(int status, string updatedBy)
        {
            Status = status;
            UpdatedBy = updatedBy;
            UpdatedAt = DateTime.Now;
        }

        /// <summary>更新 MomJsonData（管线转换后调用）</summary>
        public void UpdateMomJsonData(string momJsonData)
        {
            MomJsonData = momJsonData ?? throw new ArgumentNullException(nameof(momJsonData));
            UpdatedAt = DateTime.Now;
        }

        /// <summary>手动编辑 JSON 数据（异常状态下）</summary>
        public void UpdateJsonData(string? bimJsonData, string? momJsonData, string updatedBy)
        {
            if (bimJsonData != null)
                BimJsonData = bimJsonData;
            if (momJsonData != null)
                MomJsonData = momJsonData;
            UpdatedBy = updatedBy;
            UpdatedAt = DateTime.Now;
        }

        /// <summary>设置所属项目（导航属性赋值）</summary>
        public void SetProject(ProjectEntity project)
        {
            Project = project ?? throw new ArgumentNullException(nameof(project));
            ProjectId = project.Id;
        }
    }
}
