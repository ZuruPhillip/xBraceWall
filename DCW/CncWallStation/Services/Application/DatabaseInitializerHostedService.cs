using CncWallStation.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CncWallStation.Services.Application
{
    /// <summary>
    /// 数据库初始化：EnsureCreated + 手动补建/升级表结构。
    /// 说明：当前沿用 EnsureCreated + 手动 DDL 方案。建议后续迁移至 EF Core Migrations
    /// （用 db.Database.Migrate() 替代本类的全部手动 SQL）。
    /// </summary>
    public class DatabaseInitializerHostedService : IHostedService
    {
        private readonly IDbContextFactory<AppDbContext> _dbFactory;
        private readonly ILogger<DatabaseInitializerHostedService> _logger;

        public DatabaseInitializerHostedService(
            IDbContextFactory<AppDbContext> dbFactory,
            ILogger<DatabaseInitializerHostedService> logger)
        {
            _dbFactory = dbFactory;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("开始初始化数据库...");

            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            await db.Database.EnsureCreatedAsync(cancellationToken);

            // ===== PlcInstruction =====
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS PlcInstruction (
                    Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                    WallId BIGINT NOT NULL,
                    T INT NOT NULL,
                    F INT NOT NULL,
                    D INT NOT NULL,
                    X0 FLOAT NOT NULL,
                    Y0 FLOAT NOT NULL,
                    Z0 FLOAT NOT NULL,
                    X1 FLOAT NOT NULL,
                    Y1 FLOAT NOT NULL,
                    Z1 FLOAT NOT NULL,
                    SortOrder INT NOT NULL,
                    HandlerName VARCHAR(64) NOT NULL,
                    FeatureName VARCHAR(64) NOT NULL,
                    UpdatedBy VARCHAR(64) NULL,
                    UpdatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    INDEX IX_PlcInstruction_WallId (WallId),
                    INDEX IX_PlcInstruction_WallId_SortOrder (WallId, SortOrder),
                    CONSTRAINT FK_PlcInstruction_Wall FOREIGN KEY (WallId) REFERENCES Wall(Id) ON DELETE CASCADE
                );", cancellationToken);

            // ===== Opc =====
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS Opc (
                    Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                    WallId BIGINT NOT NULL,
                    GroupId VARCHAR(64) NOT NULL,
                    NodeId VARCHAR(256) NOT NULL,
                    Value VARCHAR(128) NOT NULL,
                    CreatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    INDEX IX_Opc_GroupId (GroupId),
                    INDEX IX_Opc_WallId (WallId)
                );", cancellationToken);

            // ===== MachiningException =====
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS MachiningException (
                    Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                    WallId BIGINT NOT NULL,
                    ExceptionType INT NOT NULL,
                    CustomType VARCHAR(128) NULL,
                    Description MEDIUMTEXT NOT NULL,
                    PhotoPaths TEXT NULL,
                    Registrant VARCHAR(64) NOT NULL,
                    CreatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    OccurredAt DATETIME NULL,
                    FrequencyCount INT NOT NULL DEFAULT 1,
                    IsResolved TINYINT(1) NOT NULL DEFAULT 0,
                    RepairMethod VARCHAR(512) NULL,
                    Resolver VARCHAR(64) NULL,
                    RepairDuration DECIMAL(10,2) NULL,
                    CompletionTime DATETIME NULL,
                    ImprovementSuggestion TEXT NULL,
                    Remarks TEXT NULL,
                    INDEX IX_MachiningException_WallId (WallId),
                    CONSTRAINT FK_MachiningException_Wall FOREIGN KEY (WallId) REFERENCES Wall(Id) ON DELETE CASCADE
                );", cancellationToken);

            // ===== MachiningException 升级：先查已有列，仅补缺失列 =====
            var existingColumns = db.Database.SqlQueryRaw<string>(
                "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'MachiningException'"
            ).ToHashSet();

            if (existingColumns.Contains("Operator") && !existingColumns.Contains("Registrant"))
            {
                await db.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE MachiningException CHANGE COLUMN Operator Registrant VARCHAR(64) NOT NULL",
                    cancellationToken);
            }

            var columnsToAdd = new[]
            {
                ("OccurredAt", "ALTER TABLE MachiningException ADD COLUMN OccurredAt DATETIME NULL"),
                ("FrequencyCount", "ALTER TABLE MachiningException ADD COLUMN FrequencyCount INT NOT NULL DEFAULT 1"),
                ("RepairMethod", "ALTER TABLE MachiningException ADD COLUMN RepairMethod VARCHAR(512) NULL"),
                ("Resolver", "ALTER TABLE MachiningException ADD COLUMN Resolver VARCHAR(64) NULL"),
                ("RepairDuration", "ALTER TABLE MachiningException ADD COLUMN RepairDuration DECIMAL(10,2) NULL"),
                ("CompletionTime", "ALTER TABLE MachiningException ADD COLUMN CompletionTime DATETIME NULL"),
                ("ImprovementSuggestion", "ALTER TABLE MachiningException ADD COLUMN ImprovementSuggestion TEXT NULL"),
                ("Remarks", "ALTER TABLE MachiningException ADD COLUMN Remarks TEXT NULL"),
            };

            foreach (var (columnName, sql) in columnsToAdd)
            {
                if (!existingColumns.Contains(columnName))
                    await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
            }

            // ===== MachiningRecord =====
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS MachiningRecord (
                    Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                    WallId BIGINT NOT NULL,
                    Operator VARCHAR(64) NOT NULL,
                    StartTime TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    EndTime TIMESTAMP NULL,
                    TotalDurationSeconds BIGINT NULL,
                    Status INT NOT NULL,
                    INDEX IX_MachiningRecord_WallId (WallId),
                    CONSTRAINT FK_MachiningRecord_Wall FOREIGN KEY (WallId) REFERENCES Wall(Id) ON DELETE CASCADE
                );", cancellationToken);

            _logger.LogInformation("数据库初始化完成");
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}