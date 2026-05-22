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
            string projectName,
            int floor,
            string bimJsonData,
            string wallName = "",
            string schemaVersion = "V0.0.0")
        {
            ProjectId = projectId;
            WallId = wallId ?? throw new ArgumentNullException(nameof(wallId));
            ProjectName = projectName ?? throw new ArgumentNullException(nameof(projectName));
            Floor = floor;
            BimJsonData = bimJsonData ?? throw new ArgumentNullException(nameof(bimJsonData));
            WallName = wallName;
            SchemaVersion = schemaVersion;
            PipelineStage = PipelineStage.Imported;
            Priority = 0;
            Status = 0;
            AuditStatus = (int)Enums.AuditStatus.未审核;
            IsDeleted = false;
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

        /// <summary>项目名称（冗余加速查询）</summary>
        [Required]
        [MaxLength(256)]
        public string ProjectName { get; private set; } = string.Empty;

        /// <summary>楼层</summary>
        public int Floor { get; private set; }

        /// <summary>墙体名称</summary>
        [MaxLength(256)]
        public string WallName { get; private set; } = string.Empty;

        /// <summary>原始 BimJSON 数据（最大 16MB）</summary>
        [Required]
        [Column(TypeName = "MEDIUMTEXT")]
        public string BimJsonData { get; private set; } = string.Empty;

        /// <summary>管线阶段</summary>
        public PipelineStage PipelineStage { get; private set; } = PipelineStage.Imported;

        /// <summary>转换后的 MomJSON 数据（最大 16MB）</summary>
        [Column(TypeName = "MEDIUMTEXT")]
        public string? MomJsonData { get; private set; }

        /// <summary>加工优先级（int，数值越大优先级越高）</summary>
        public int Priority { get; private set; } = 0;

        /// <summary>生产状态（ProcessStatus 映射）</summary>
        public int Status { get; private set; } = 0;

        /// <summary>审核状态：0=未审核，1=已审核</summary>
        public int AuditStatus { get; private set; } = 0;

        /// <summary>Schema版本号（来自 BimJson schema 字段）</summary>
        [MaxLength(64)]
        public string SchemaVersion { get; private set; } = "V0.0.0";

		/// <summary>软删除标记</summary>
		public bool IsDeleted { get; private set; } = false;

		/// <summary>开始生产时间</summary>
		public DateTime? StartProductionTime { get; private set; }

		/// <summary>结束生产时间</summary>
		public DateTime? EndProductionTime { get; private set; }

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

        /// <summary>更新生产状态</summary>
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

        /// <summary>更新墙体名称</summary>
        public void UpdateWallName(string wallName, string updatedBy)
        {
            WallName = wallName ?? string.Empty;
            UpdatedBy = updatedBy;
            UpdatedAt = DateTime.Now;
        }

        /// <summary>设置 Schema 版本号</summary>
        public void SetSchemaVersion(string schemaVersion)
        {
            SchemaVersion = schemaVersion ?? "V0.0.0";
            UpdatedAt = DateTime.Now;
        }

        /// <summary>设置审核状态</summary>
        public void SetAuditStatus(int auditStatus, string updatedBy)
        {
            AuditStatus = auditStatus;
            UpdatedBy = updatedBy;
            UpdatedAt = DateTime.Now;
        }

        /// <summary>
        /// 同步更新 BimData（仅未审核状态可调用）。
        /// 替换 BimJsonData，清空 MomJsonData，重置 PipelineStage=Imported，Status=待校验。
        /// </summary>
        public void SyncBimData(string bimJsonData, string schemaVersion, string wallName, string updatedBy)
        {
            if (AuditStatus == (int)Enums.AuditStatus.已审核)
                throw new InvalidOperationException($"墙体 {WallId} 已审核，不允许同步更新 BimData。请先执行反审核操作。");

            BimJsonData = bimJsonData ?? throw new ArgumentNullException(nameof(bimJsonData));
            SchemaVersion = schemaVersion ?? "V0.0.0";
            WallName = wallName ?? string.Empty;
            MomJsonData = null;
            PipelineStage = PipelineStage.Imported;
            Status = (int)ProcessStatus.待校验;
            UpdatedBy = updatedBy;
            UpdatedAt = DateTime.Now;
            ImportTime = DateTime.Now;
        }

        /// <summary>软删除</summary>
        public void SoftDelete(string updatedBy)
        {
            IsDeleted = true;
            UpdatedBy = updatedBy;
            UpdatedAt = DateTime.Now;
        }

		/// <summary>恢复已删除数据</summary>
		public void Restore(string updatedBy)
		{
			IsDeleted = false;
			UpdatedBy = updatedBy;
			UpdatedAt = DateTime.Now;
		}

		/// <summary>设置生产时间</summary>
		public void SetProductionTime(DateTime? startTime, DateTime? endTime, string updatedBy)
		{
			StartProductionTime = startTime;
			EndProductionTime = endTime;
			UpdatedBy = updatedBy;
			UpdatedAt = DateTime.Now;
		}
	}
}
