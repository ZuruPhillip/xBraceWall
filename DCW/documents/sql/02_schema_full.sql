-- ============================================================================
-- 02_schema_full.sql
-- 用途：创建 CncWallStation（DCW）系统全部业务表（8 张）
-- 生成依据：CncWallStation/Migrations/AppDbContextModelSnapshot.cs
--           + CncWallStation/EntityFrameworkCore/AppDbContext.OnModelCreating
-- 生成方式：dotnet ef dbcontext script（EF Core 8.0.10 / Pomelo 8.0.2）
-- 目标库：DcwCncStation（执行前请先执行 01_create_database.sql）
-- 字符集：utf8mb4 / utf8mb4_0900_ai_ci（与服务器同级库一致）
-- 幂等性：全部 CREATE TABLE IF NOT EXISTS + 索引内联定义，可重复执行
--         已存在的表不会被修改，不会清空任何数据
-- 表创建顺序：按外键依赖排列（Project → Wall → DataCheckRecord → 其余业务表）
-- ----------------------------------------------------------------------------
-- 执行方式：
--   mysql -h 10.33.120.30 -P 3306 -u root -p DcwCncStation < 02_schema_full.sql
-- ============================================================================

SET NAMES utf8mb4;

-- ----------------------------------------------------------------------------
-- 1. Project —— 项目（一次 BIM 数据导入对应一个项目）
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `Project` (
    `Id`               int          NOT NULL AUTO_INCREMENT,
    `ProjectName`      varchar(256) CHARACTER SET utf8mb4 NOT NULL,
    `SourceFolderPath` varchar(1024) CHARACTER SET utf8mb4 NULL,
    `HostName`         varchar(256) CHARACTER SET utf8mb4 NULL,
    `ImportedBy`       varchar(256) CHARACTER SET utf8mb4 NULL,
    `TotalWalls`       int          NOT NULL,
    `ImportTime`       timestamp    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `Notes`            text         CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_Project` PRIMARY KEY (`Id`),
    KEY `IX_Project_ProjectName` (`ProjectName`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ----------------------------------------------------------------------------
-- 2. Wall —— 墙体主数据（BIM/MOM JSON 原文 + 生产状态）
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `Wall` (
    `Id`                  bigint       NOT NULL AUTO_INCREMENT,
    `ProjectId`           int          NOT NULL,
    `WallId`              varchar(256) CHARACTER SET utf8mb4 NOT NULL,
    `ProjectName`         varchar(256) CHARACTER SET utf8mb4 NOT NULL,
    `Floor`               int          NOT NULL,
    `WallName`            varchar(256) CHARACTER SET utf8mb4 NOT NULL,
    `BimJsonData`         MEDIUMTEXT   CHARACTER SET utf8mb4 NOT NULL,
    `PipelineStage`       int          NOT NULL,
    `MomJsonData`         MEDIUMTEXT   CHARACTER SET utf8mb4 NULL,
    `Priority`            int          NOT NULL,
    `Status`              int          NOT NULL,
    `AuditStatus`         int          NOT NULL DEFAULT 0,
    `SchemaVersion`       varchar(64)  CHARACTER SET utf8mb4 NOT NULL DEFAULT 'V0.0.0',
    `IsDeleted`           tinyint(1)   NOT NULL DEFAULT FALSE,
    `StartProductionTime` datetime(6)  NULL,
    `EndProductionTime`   datetime(6)  NULL,
    `ImportTime`          timestamp    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`           timestamp    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedBy`           varchar(256) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_Wall` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Wall_Project_ProjectId` FOREIGN KEY (`ProjectId`) REFERENCES `Project` (`Id`) ON DELETE CASCADE,
    UNIQUE KEY `IX_Wall_ProjectId_WallId` (`ProjectId`, `WallId`),
    KEY `IX_Wall_ProjectName` (`ProjectName`),
    KEY `IX_Wall_Floor` (`Floor`),
    KEY `IX_Wall_Status` (`Status`),
    KEY `IX_Wall_Priority` (`Priority`),
    KEY `IX_Wall_ImportTime` (`ImportTime`),
    KEY `IX_Wall_EndProductionTime` (`EndProductionTime`),
    KEY `IX_Wall_PipelineStage` (`PipelineStage`),
    KEY `IX_Wall_AuditStatus` (`AuditStatus`),
    KEY `IX_Wall_WallName` (`WallName`),
    KEY `IX_Wall_IsDeleted` (`IsDeleted`),
    KEY `IX_Wall_IsDeleted_EndProductionTime_Id` (`IsDeleted`, `EndProductionTime`, `Id`),
    KEY `IX_Wall_ProjectName_Floor` (`ProjectName`, `Floor`),
    KEY `IX_Wall_ProjectName_Status_Floor` (`ProjectName`, `Status`, `Floor`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ----------------------------------------------------------------------------
-- 3. DataCheckRecord —— 加工前数据核对记录（主键由应用生成的字符串 GroupId）
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `DataCheckRecord` (
    `Id`            varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `WallId`        bigint      NOT NULL,
    `Version`       varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    `BimScore`      double      NOT NULL,
    `MomScore`      double      NOT NULL,
    `ErrorCount`    int         NOT NULL,
    `CriticalCount` int         NOT NULL,
    `Operator`      varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `CheckTime`     timestamp   NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `DurationMs`    bigint      NOT NULL,
    `Result`        int         NOT NULL,
    CONSTRAINT `PK_DataCheckRecord` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_DataCheckRecord_Wall_WallId` FOREIGN KEY (`WallId`) REFERENCES `Wall` (`Id`) ON DELETE CASCADE,
    KEY `IX_DataCheckRecord_WallId` (`WallId`),
    KEY `IX_DataCheckRecord_CheckTime` (`CheckTime`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ----------------------------------------------------------------------------
-- 4. ValidationError —— 数据校验错误明细
--    注意：WallId 可空（数据核对场景允许脱离具体墙体），DataCheckGroupId 关联核对记录
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `ValidationError` (
    `Id`              bigint       NOT NULL AUTO_INCREMENT,
    `WallId`          bigint       NULL,
    `GroupId`         varchar(64)  CHARACTER SET utf8mb4 NOT NULL,
    `DataCheckGroupId` varchar(64) CHARACTER SET utf8mb4 NULL,
    `PipelineStage`   int          NOT NULL,
    `ErrorCode`       varchar(128) CHARACTER SET utf8mb4 NULL,
    `ErrorMessage`    text         CHARACTER SET utf8mb4 NOT NULL,
    `ErrorMessageEn`  text         CHARACTER SET utf8mb4 NULL,
    `Severity`        int          NOT NULL,
    `ErrorCategory`   int          NOT NULL,
    `FeatureCategory` varchar(128) CHARACTER SET utf8mb4 NULL,
    `CreatedAt`       timestamp    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT `PK_ValidationError` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ValidationError_Wall_WallId` FOREIGN KEY (`WallId`) REFERENCES `Wall` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_ValidationError_DataCheckRecord_DataCheckGroupId` FOREIGN KEY (`DataCheckGroupId`) REFERENCES `DataCheckRecord` (`Id`) ON DELETE CASCADE,
    KEY `IX_ValidationError_WallId` (`WallId`),
    KEY `IX_ValidationError_GroupId` (`GroupId`),
    KEY `IX_ValidationError_DataCheckGroupId` (`DataCheckGroupId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ----------------------------------------------------------------------------
-- 5. PlcInstruction —— PLC 加工指令（按墙体 + 排序序号组织）
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `PlcInstruction` (
    `Id`          bigint      NOT NULL AUTO_INCREMENT,
    `WallId`      bigint      NOT NULL,
    `T`           int         NOT NULL,
    `F`           int         NOT NULL,
    `D`           int         NOT NULL,
    `X0`          float       NOT NULL,
    `Y0`          float       NOT NULL,
    `Z0`          float       NOT NULL,
    `X1`          float       NOT NULL,
    `Y1`          float       NOT NULL,
    `Z1`          float       NOT NULL,
    `SortOrder`   int         NOT NULL,
    `Side`        int         NOT NULL DEFAULT 0,
    `HandlerName` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `FeatureName` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `UpdatedBy`   varchar(64) CHARACTER SET utf8mb4 NULL,
    `UpdatedAt`   timestamp   NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT `PK_PlcInstruction` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_PlcInstruction_Wall_WallId` FOREIGN KEY (`WallId`) REFERENCES `Wall` (`Id`) ON DELETE CASCADE,
    KEY `IX_PlcInstruction_WallId` (`WallId`),
    KEY `IX_PlcInstruction_WallId_SortOrder` (`WallId`, `SortOrder`),
    KEY `IX_PlcInstruction_WallId_Side` (`WallId`, `Side`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ----------------------------------------------------------------------------
-- 6. Opc —— OPC UA 写入记录（历史日志表，按模型定义无外键，便于保留已删除墙体的日志）
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `Opc` (
    `Id`        bigint       NOT NULL AUTO_INCREMENT,
    `WallId`    bigint       NOT NULL,
    `GroupId`   varchar(64)  CHARACTER SET utf8mb4 NOT NULL,
    `NodeId`    varchar(256) CHARACTER SET utf8mb4 NOT NULL,
    `Value`     varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` timestamp    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT `PK_Opc` PRIMARY KEY (`Id`),
    KEY `IX_Opc_GroupId` (`GroupId`),
    KEY `IX_Opc_WallId` (`WallId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ----------------------------------------------------------------------------
-- 7. MachiningException —— 加工异常报告
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `MachiningException` (
    `Id`                    bigint        NOT NULL AUTO_INCREMENT,
    `WallId`                bigint        NOT NULL,
    `ExceptionType`         int           NOT NULL,
    `CustomType`            VARCHAR(128)  CHARACTER SET utf8mb4 NULL,
    `Description`           MEDIUMTEXT    CHARACTER SET utf8mb4 NOT NULL,
    `PhotoPaths`            TEXT          CHARACTER SET utf8mb4 NULL,
    `Registrant`            VARCHAR(64)   CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt`             timestamp     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `OccurredAt`            datetime(6)   NOT NULL,
    `FrequencyCount`        int           NOT NULL,
    `IsResolved`            tinyint(1)    NOT NULL,
    `RepairMethod`          VARCHAR(512)  CHARACTER SET utf8mb4 NULL,
    `Resolver`              VARCHAR(64)   CHARACTER SET utf8mb4 NULL,
    `RepairDuration`        DECIMAL(10,2) NULL,
    `CompletionTime`        datetime(6)   NULL,
    `ImprovementSuggestion` TEXT          CHARACTER SET utf8mb4 NULL,
    `Remarks`               TEXT          CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_MachiningException` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_MachiningException_Wall_WallId` FOREIGN KEY (`WallId`) REFERENCES `Wall` (`Id`) ON DELETE CASCADE,
    KEY `IX_MachiningException_WallId` (`WallId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ----------------------------------------------------------------------------
-- 8. MachiningRecord —— 加工记录（开始/结束时间、耗时、状态）
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `MachiningRecord` (
    `Id`                   bigint     NOT NULL AUTO_INCREMENT,
    `WallId`               bigint     NOT NULL,
    `Operator`             VARCHAR(64) CHARACTER SET utf8mb4 NOT NULL,
    `StartTime`            datetime(6) NOT NULL,
    `EndTime`              datetime(6) NULL,
    `TotalDurationSeconds` bigint     NULL,
    `Status`               int        NOT NULL,
    CONSTRAINT `PK_MachiningRecord` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_MachiningRecord_Wall_WallId` FOREIGN KEY (`WallId`) REFERENCES `Wall` (`Id`) ON DELETE CASCADE,
    KEY `IX_MachiningRecord_WallId` (`WallId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ============================================================================
-- 建表完成。请继续执行 03_migration_history_baseline.sql 建立迁移基线，
-- 再执行 04_verify.sql 做结构与完整性校验。
-- ============================================================================
