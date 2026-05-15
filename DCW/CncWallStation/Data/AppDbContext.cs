using CncWallStation.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace CncWallStation.Data
{
    /// <summary>
    /// EF Core 数据库上下文
    /// </summary>
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<ProjectEntity> Projects => Set<ProjectEntity>();
        public DbSet<WallEntity> Walls => Set<WallEntity>();
        public DbSet<ValidationErrorEntity> ValidationErrors => Set<ValidationErrorEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ==================== Project 表配置 ====================
            modelBuilder.Entity<ProjectEntity>(entity =>
            {
                entity.ToTable("Project");

                entity.HasIndex(p => new { p.ProjectNumber, p.Version }).IsUnique();
                entity.HasIndex(p => p.ProjectNumber);
                entity.HasIndex(p => p.IsLatest);

                entity.Property(p => p.ImportTime)
                    .HasColumnType("timestamp")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // ==================== Wall 表配置 ====================
            modelBuilder.Entity<WallEntity>(entity =>
            {
                entity.ToTable("Wall");

                // 唯一约束：同一版本内 WallId 不重复
                entity.HasIndex(w => new { w.ProjectId, w.WallId }).IsUnique();

                // 查询索引
                entity.HasIndex(w => w.ProjectNumber).HasDatabaseName("IX_Wall_ProjectNumber");
                entity.HasIndex(w => w.Floor).HasDatabaseName("IX_Wall_Floor");
                entity.HasIndex(w => w.Status).HasDatabaseName("IX_Wall_Status");
                entity.HasIndex(w => w.Priority).HasDatabaseName("IX_Wall_Priority");
                entity.HasIndex(w => w.ImportTime).HasDatabaseName("IX_Wall_ImportTime");
                entity.HasIndex(w => w.PipelineStage).HasDatabaseName("IX_Wall_PipelineStage");
                entity.HasIndex(w => new { w.ProjectNumber, w.Status, w.Floor })
                    .HasDatabaseName("IX_Wall_ProjectNumber_Status_Floor");

                // MEDIUMTEXT 列
                entity.Property(w => w.BimJsonData).HasColumnType("MEDIUMTEXT");
                entity.Property(w => w.MomJsonData).HasColumnType("MEDIUMTEXT");

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
            });

            // ==================== ValidationError 表配置 ====================
            modelBuilder.Entity<ValidationErrorEntity>(entity =>
            {
                entity.ToTable("ValidationError");

                entity.HasIndex(e => e.WallId).HasDatabaseName("IX_ValidationError_WallId");
                entity.HasIndex(e => e.GroupId).HasDatabaseName("IX_ValidationError_GroupId");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasOne(e => e.Wall)
                    .WithMany(w => w.ValidationErrors)
                    .HasForeignKey(e => e.WallId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
