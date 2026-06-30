using CncWallStation.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace CncWallStation.EntityFrameworkCore
{
    /// <summary>
    /// EF Core 数据库上下文（ABP 风格实体）
    /// </summary>
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<ProjectEntity> Projects => Set<ProjectEntity>();
        public DbSet<WallEntity> Walls => Set<WallEntity>();
        public DbSet<ValidationErrorEntity> ValidationErrors => Set<ValidationErrorEntity>();
        public DbSet<DataCheckRecordEntity> DataCheckRecords => Set<DataCheckRecordEntity>();
        public DbSet<PlcInstructionEntity> PlcInstructions => Set<PlcInstructionEntity>();
        public DbSet<OpcWriteRecordEntity> OpcWriteRecords => Set<OpcWriteRecordEntity>();
        public DbSet<MachiningExceptionEntity> MachiningExceptions => Set<MachiningExceptionEntity>();
        public DbSet<MachiningRecordEntity> MachiningRecords => Set<MachiningRecordEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ==================== PlcInstruction 表配置 ====================
            modelBuilder.Entity<PlcInstructionEntity>(entity =>
            {
                entity.ToTable("PlcInstruction");

                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id)
                    .ValueGeneratedOnAdd();

                entity.HasIndex(e => e.WallId).HasDatabaseName("IX_PlcInstruction_WallId");
                entity.HasIndex(e => new { e.WallId, e.SortOrder }).HasDatabaseName("IX_PlcInstruction_WallId_SortOrder");

                entity.Property(e => e.HandlerName).HasMaxLength(64);
                entity.Property(e => e.FeatureName).HasMaxLength(64);
                entity.Property(e => e.UpdatedBy).HasMaxLength(64).IsRequired(false);

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("timestamp")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasOne(e => e.Wall)
                    .WithMany()
                    .HasForeignKey(e => e.WallId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ==================== Opc 表配置 ====================
            modelBuilder.Entity<OpcWriteRecordEntity>(entity =>
            {
                entity.ToTable("Opc");

                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id)
                    .ValueGeneratedOnAdd();

                entity.HasIndex(e => e.GroupId).HasDatabaseName("IX_Opc_GroupId");
                entity.HasIndex(e => e.WallId).HasDatabaseName("IX_Opc_WallId");

                entity.Property(e => e.GroupId).HasMaxLength(64).IsRequired();
                entity.Property(e => e.NodeId).HasMaxLength(256).IsRequired();
                entity.Property(e => e.Value).HasMaxLength(128).IsRequired();

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // ==================== Project 表配置 ====================
            modelBuilder.Entity<ProjectEntity>(entity =>
            {
                entity.ToTable("Project");

                // 主键自增（因 Entity<int> 基类不携带 [DatabaseGenerated] 注解）
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Id)
                    .ValueGeneratedOnAdd();

                entity.HasIndex(p => p.ProjectName);

                entity.Property(p => p.ImportTime)
                    .HasColumnType("timestamp")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // ==================== Wall 表配置 ====================
            modelBuilder.Entity<WallEntity>(entity =>
            {
                entity.ToTable("Wall");

                // 全局查询过滤器：默认不查询已软删除的数据
                entity.HasQueryFilter(w => !w.IsDeleted);

                // 主键自增
                entity.HasKey(w => w.Id);
                entity.Property(w => w.Id)
                    .ValueGeneratedOnAdd();

                // 唯一约束：同一版本内 WallId 不重复
                entity.HasIndex(w => new { w.ProjectId, w.WallId }).IsUnique();

                // 查询索引
                entity.HasIndex(w => w.ProjectName).HasDatabaseName("IX_Wall_ProjectName");
                entity.HasIndex(w => w.Floor).HasDatabaseName("IX_Wall_Floor");
                entity.HasIndex(w => w.Status).HasDatabaseName("IX_Wall_Status");
                entity.HasIndex(w => w.Priority).HasDatabaseName("IX_Wall_Priority");
                entity.HasIndex(w => w.ImportTime).HasDatabaseName("IX_Wall_ImportTime");
                entity.HasIndex(w => w.EndProductionTime).HasDatabaseName("IX_Wall_EndProductionTime");
                entity.HasIndex(w => w.PipelineStage).HasDatabaseName("IX_Wall_PipelineStage");
                entity.HasIndex(w => w.AuditStatus).HasDatabaseName("IX_Wall_AuditStatus");
                entity.HasIndex(w => w.WallName).HasDatabaseName("IX_Wall_WallName");
                entity.HasIndex(w => w.IsDeleted).HasDatabaseName("IX_Wall_IsDeleted");
                entity.HasIndex(w => new { w.ProjectName, w.Status, w.Floor })
                    .HasDatabaseName("IX_Wall_ProjectName_Status_Floor");

                // MEDIUMTEXT 列
                entity.Property(w => w.BimJsonData).HasColumnType("MEDIUMTEXT");
                entity.Property(w => w.MomJsonData).HasColumnType("MEDIUMTEXT");

                // 默认值
                entity.Property(w => w.AuditStatus).HasDefaultValue(0);
                entity.Property(w => w.SchemaVersion).HasMaxLength(64).HasDefaultValue("V0.0.0");
                entity.Property(w => w.IsDeleted).HasDefaultValue(false);

                // timestamp 列
                entity.Property(w => w.ImportTime)
                    .HasColumnType("timestamp")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(w => w.UpdatedAt)
                    .HasColumnType("timestamp")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                // 导航属性
                entity.HasOne(w => w.Project)
                    .WithMany(p => p.Walls)
                    .HasForeignKey(w => w.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(w => w.ValidationErrors)
                    .WithOne(e => e.Wall)
                    .HasForeignKey(e => e.WallId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(w => w.DataCheckRecords)
                    .WithOne(r => r.Wall)
                    .HasForeignKey(r => r.WallId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ==================== ValidationError 表配置 ====================
            modelBuilder.Entity<ValidationErrorEntity>(entity =>
            {
                entity.ToTable("ValidationError");

                // 主键自增
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id)
                    .ValueGeneratedOnAdd();

                entity.HasIndex(e => e.WallId).HasDatabaseName("IX_ValidationError_WallId");
                entity.HasIndex(e => e.GroupId).HasDatabaseName("IX_ValidationError_GroupId");
                entity.HasIndex(e => e.DataCheckGroupId).HasDatabaseName("IX_ValidationError_DataCheckGroupId");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasOne(e => e.Wall)
                    .WithMany(w => w.ValidationErrors)
                    .HasForeignKey(e => e.WallId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.DataCheckRecord)
                    .WithMany(r => r.Errors)
                    .HasForeignKey(e => e.DataCheckGroupId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ==================== DataCheckRecord 表配置 ====================
            modelBuilder.Entity<DataCheckRecordEntity>(entity =>
            {
                entity.ToTable("DataCheckRecord");

                entity.HasKey(r => r.Id);
                entity.Property(r => r.Id)
                    .HasMaxLength(64)
                    .ValueGeneratedNever(); // GroupId 由应用生成

                entity.HasIndex(r => r.WallId).HasDatabaseName("IX_DataCheckRecord_WallId");
                entity.HasIndex(r => r.CheckTime).HasDatabaseName("IX_DataCheckRecord_CheckTime");

                entity.Property(r => r.CheckTime)
                    .HasColumnType("timestamp")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasOne(r => r.Wall)
                    .WithMany(w => w.DataCheckRecords)
                    .HasForeignKey(r => r.WallId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ==================== MachiningException 表配置 ====================
            modelBuilder.Entity<MachiningExceptionEntity>(entity =>
            {
                entity.ToTable("MachiningException");

                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id)
                    .ValueGeneratedOnAdd();

                entity.HasIndex(e => e.WallId).HasDatabaseName("IX_MachiningException_WallId");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasOne<WallEntity>()
                    .WithMany()
                    .HasForeignKey(e => e.WallId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ==================== MachiningRecord 表配置 ====================
            modelBuilder.Entity<MachiningRecordEntity>(entity =>
            {
                entity.ToTable("MachiningRecord");

                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id)
                    .ValueGeneratedOnAdd();

                entity.HasIndex(e => e.WallId).HasDatabaseName("IX_MachiningRecord_WallId");

                entity.HasOne<WallEntity>()
                    .WithMany()
                    .HasForeignKey(e => e.WallId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
