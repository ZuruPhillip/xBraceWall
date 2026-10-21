-- ============================================================================
-- 03_migration_history_baseline.sql
-- 用途：建立 EF Core 迁移历史基线
-- 背景：本项目 Migrations/ 目录下只有 3 个「增量」迁移，没有初始建表迁移，
--       因此库结构由 02_schema_full.sql 全量创建，再把已有迁移登记为「已应用」，
--       使 __EFMigrationsHistory 与 AppDbContextModelSnapshot 状态一致。
-- 作用：此后新增迁移时可正常执行 `dotnet ef migrations add xxx`；
--       对已建好的库不要执行 `dotnet ef database update`（会重放增量迁移并报错）。
-- 幂等性：CREATE TABLE IF NOT EXISTS + INSERT IGNORE，可重复执行
-- 依赖：先执行 01_create_database.sql、02_schema_full.sql
-- ============================================================================

CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId`    varchar(150) NOT NULL,
    `ProductVersion` varchar(32)  NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES
    ('20260519083754_AddDataCheckSupport',  '8.0.10'),
    ('20260713022100_AddPlcInstructionSide', '8.0.10'),
    ('20260727022215_AddPerformanceIndexes', '8.0.10');

-- 结果确认：应返回 3 行
SELECT `MigrationId`, `ProductVersion`
FROM `__EFMigrationsHistory`
ORDER BY `MigrationId`;
