-- ============================================================================
-- 06_create_opc_node.sql
-- 用途：创建 OPC 节点配置相关表（2 张）
--   OpcNodeGroup —— OPC 节点分组
--   OpcNode      —— OPC 节点配置（NodeId 唯一）
-- 背景：原“系统设置”页中的 OPC 节点管理已独立为【OPC节点配置】页面，
--       数据由 %LocalAppData%/CncWallStation/opc_nodes.json 迁移至 MySQL。
--       应用首次启动且 OpcNode 表为空时会自动导入旧 JSON（并重命名为 .bak）。
-- 目标库：DcwCncStation（执行前请先执行 01_create_database.sql）
-- 字符集：utf8mb4 / utf8mb4_0900_ai_ci
-- 幂等性：全部 CREATE TABLE IF NOT EXISTS，可重复执行，不会清空数据
-- ----------------------------------------------------------------------------
-- 执行方式：
--   mysql -h 10.33.120.30 -P 3306 -u root -p DcwCncStation < 06_create_opc_node.sql
-- ============================================================================

SET NAMES utf8mb4;

-- ----------------------------------------------------------------------------
-- 1. OpcNodeGroup —— OPC 节点分组
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `OpcNodeGroup` (
    `Id`          int          NOT NULL AUTO_INCREMENT,
    `Name`        varchar(64)  CHARACTER SET utf8mb4 NOT NULL COMMENT '分组名称（唯一）',
    `Description` varchar(256) CHARACTER SET utf8mb4 NULL COMMENT '分组描述',
    `SortOrder`   int          NOT NULL DEFAULT 0 COMMENT '排序序号（升序显示）',
    `CreatedAt`   timestamp    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT `PK_OpcNodeGroup` PRIMARY KEY (`Id`),
    UNIQUE KEY `UX_OpcNodeGroup_Name` (`Name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='OPC 节点分组';

-- ----------------------------------------------------------------------------
-- 2. OpcNode —— OPC 节点配置
--    注意：GroupId 可空，NULL 表示“未分组”；分组被删除时由数据库 SET NULL 兜底
--          （应用层会先显式把 GroupId 置空再删除分组）
--    NodeId 唯一 —— 同时是 OPC UA 订阅键，编辑时不允许修改
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `OpcNode` (
    `Id`               bigint       NOT NULL AUTO_INCREMENT,
    `GroupId`          int          NULL COMMENT '所属分组（NULL=未分组）',
    `NodeId`           varchar(256) CHARACTER SET utf8mb4 NOT NULL COMMENT 'OPC UA NodeId',
    `Description`      varchar(256) CHARACTER SET utf8mb4 NULL COMMENT '节点描述/别名',
    `IsWritable`       tinyint(1)   NOT NULL DEFAULT 0 COMMENT '是否可写',
    `IsShowInRealtime` tinyint(1)   NOT NULL DEFAULT 0 COMMENT '是否在控制页实时参数面板显示',
    `SortOrder`        int          NOT NULL DEFAULT 0 COMMENT '排序序号（升序显示）',
    `CreatedAt`        timestamp    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt`        timestamp    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT `PK_OpcNode` PRIMARY KEY (`Id`),
    UNIQUE KEY `UX_OpcNode_NodeId` (`NodeId`),
    KEY `IX_OpcNode_GroupId` (`GroupId`),
    CONSTRAINT `FK_OpcNode_OpcNodeGroup_GroupId` FOREIGN KEY (`GroupId`)
        REFERENCES `OpcNodeGroup` (`Id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='OPC 节点配置';

-- ============================================================================
-- 建表完成。可继续执行 04_verify.sql 做结构与完整性校验。
-- ============================================================================
