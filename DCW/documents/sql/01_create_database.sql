-- ============================================================================
-- 01_create_database.sql
-- 用途：创建 CncWallStation（DCW）系统业务库，仅建库，不建表
-- 目标服务器：10.33.120.30:3306
-- 连接串来源：CncWallStation/appsettings.json → ConnectionStrings:Default
-- 幂等性：可重复执行；库已存在时不做任何改动，不会影响已有数据
-- 排序规则：与服务器同级库（如 ShutteringStation）保持一致，均为 utf8mb4_0900_ai_ci
-- ----------------------------------------------------------------------------
-- 执行前自检（避免连错服务器）：
--   SELECT @@hostname, @@version, @@character_set_server, @@collation_server;
-- ============================================================================

CREATE DATABASE IF NOT EXISTS `DcwCncStation`
    DEFAULT CHARACTER SET utf8mb4
    DEFAULT COLLATE utf8mb4_0900_ai_ci;

-- 结果确认：应返回一行 utf8mb4 / utf8mb4_0900_ai_ci
SELECT SCHEMA_NAME              AS `数据库名`,
       DEFAULT_CHARACTER_SET_NAME AS `字符集`,
       DEFAULT_COLLATION_NAME   AS `排序规则`
FROM INFORMATION_SCHEMA.SCHEMATA
WHERE SCHEMA_NAME = 'DcwCncStation';
