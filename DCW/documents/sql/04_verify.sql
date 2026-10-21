-- ============================================================================
-- 04_verify.sql
-- 用途：建库建表后的结构校验（只读，不改动任何数据）
-- 执行顺序：01 → 02 → 03 → 04
-- 执行方式：
--   mysql -h 10.33.120.30 -P 3306 -u root -p DcwCncStation < 04_verify.sql
-- 判读标准：每个结果集的「判定」列全部为 OK；任何 NG 需按《数据库创建操作手册》排查
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 校验 1：库存在性、字符集与排序规则（期望 1 行，utf8mb4 / utf8mb4_0900_ai_ci）
-- ----------------------------------------------------------------------------
SELECT SCHEMA_NAME                AS `数据库名`,
       DEFAULT_CHARACTER_SET_NAME AS `字符集`,
       DEFAULT_COLLATION_NAME     AS `排序规则`,
       CASE WHEN DEFAULT_CHARACTER_SET_NAME = 'utf8mb4' THEN 'OK' ELSE 'NG' END AS `判定`
FROM INFORMATION_SCHEMA.SCHEMATA
WHERE SCHEMA_NAME = 'DcwCncStation';

-- ----------------------------------------------------------------------------
-- 校验 2：表清单与数量（期望 9 行：8 张业务表 + 1 张迁移历史表）
-- ----------------------------------------------------------------------------
SELECT TABLE_NAME AS `表名`,
       ENGINE     AS `存储引擎`,
       TABLE_COLLATION AS `排序规则`
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'DcwCncStation'
ORDER BY TABLE_NAME;

SELECT COUNT(*) AS `表数量`,
       CASE WHEN COUNT(*) = 9 THEN 'OK' ELSE 'NG（期望 9）' END AS `判定`
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'DcwCncStation'
  AND TABLE_NAME IN ('Project', 'Wall', 'ValidationError', 'DataCheckRecord',
                     'PlcInstruction', 'Opc', 'MachiningException', 'MachiningRecord',
                     '__EFMigrationsHistory');

-- ----------------------------------------------------------------------------
-- 校验 3：每张表的列数与索引数（索引数含 PRIMARY 主键；期望值见表内注释）
--   Project 2 / Wall 15 / DataCheckRecord 3 / ValidationError 4
--   PlcInstruction 4 / Opc 3 / MachiningException 2 / MachiningRecord 2
-- ----------------------------------------------------------------------------
SELECT t.TABLE_NAME AS `表名`,
       (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS c
         WHERE c.TABLE_SCHEMA = t.TABLE_SCHEMA AND c.TABLE_NAME = t.TABLE_NAME) AS `列数`,
       (SELECT COUNT(DISTINCT s.INDEX_NAME) FROM INFORMATION_SCHEMA.STATISTICS s
         WHERE s.TABLE_SCHEMA = t.TABLE_SCHEMA AND s.TABLE_NAME = t.TABLE_NAME) AS `索引数`
FROM INFORMATION_SCHEMA.TABLES t
WHERE t.TABLE_SCHEMA = 'DcwCncStation'
  AND t.TABLE_NAME IN ('Project', 'Wall', 'ValidationError', 'DataCheckRecord',
                       'PlcInstruction', 'Opc', 'MachiningException', 'MachiningRecord')
ORDER BY t.TABLE_NAME;

-- ----------------------------------------------------------------------------
-- 校验 4：Wall 表索引逐项核对（期望 15 条 = PRIMARY + 13 普通索引 + 1 唯一索引）
-- ----------------------------------------------------------------------------
SELECT INDEX_NAME AS `索引名`,
       GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) AS `列`,
       IF(NON_UNIQUE = 0, 'UNIQUE', '') AS `唯一性`
FROM INFORMATION_SCHEMA.STATISTICS
WHERE TABLE_SCHEMA = 'DcwCncStation' AND TABLE_NAME = 'Wall'
GROUP BY INDEX_NAME, NON_UNIQUE
ORDER BY INDEX_NAME;

SELECT COUNT(DISTINCT INDEX_NAME) AS `Wall索引数`,
       CASE WHEN COUNT(DISTINCT INDEX_NAME) = 15 THEN 'OK' ELSE 'NG（期望 15）' END AS `判定`
FROM INFORMATION_SCHEMA.STATISTICS
WHERE TABLE_SCHEMA = 'DcwCncStation' AND TABLE_NAME = 'Wall';

-- ----------------------------------------------------------------------------
-- 校验 5：外键与级联规则（期望 7 条，全部 ON DELETE CASCADE）
-- ----------------------------------------------------------------------------
SELECT CONSTRAINT_NAME AS `外键名`,
       TABLE_NAME      AS `子表`,
       COLUMN_NAME     AS `列`,
       REFERENCED_TABLE_NAME  AS `父表`,
       REFERENCED_COLUMN_NAME AS `父列`
FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
WHERE TABLE_SCHEMA = 'DcwCncStation' AND REFERENCED_TABLE_NAME IS NOT NULL
ORDER BY TABLE_NAME, CONSTRAINT_NAME;

SELECT COUNT(*) AS `外键数量`,
       CASE WHEN COUNT(*) = 7 THEN 'OK' ELSE 'NG（期望 7）' END AS `判定`
FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
WHERE TABLE_SCHEMA = 'DcwCncStation' AND REFERENCED_TABLE_NAME IS NOT NULL;

SELECT COUNT(*) AS `级联删除外键数`,
       CASE WHEN COUNT(*) = 7 THEN 'OK' ELSE 'NG（期望 7）' END AS `判定`
FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS
WHERE CONSTRAINT_SCHEMA = 'DcwCncStation' AND DELETE_RULE = 'CASCADE';

-- ----------------------------------------------------------------------------
-- 校验 6：关键列类型与默认值
--   Wall.BimJsonData / Wall.MomJsonData / MachiningException.Description → mediumtext
--   所有 timestamp 列必须带 DEFAULT CURRENT_TIMESTAMP
-- ----------------------------------------------------------------------------
SELECT TABLE_NAME AS `表名`, COLUMN_NAME AS `列名`, COLUMN_TYPE AS `类型`,
       IFNULL(COLUMN_DEFAULT, '(NULL)') AS `默认值`,
       CASE WHEN COLUMN_TYPE = 'mediumtext' THEN 'OK' ELSE 'NG' END AS `判定`
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'DcwCncStation'
  AND ((TABLE_NAME = 'Wall' AND COLUMN_NAME IN ('BimJsonData', 'MomJsonData'))
    OR (TABLE_NAME = 'MachiningException' AND COLUMN_NAME = 'Description'));

SELECT TABLE_NAME AS `表名`, COLUMN_NAME AS `列名`,
       IFNULL(COLUMN_DEFAULT, '(无默认值)') AS `默认值`,
       CASE WHEN COLUMN_DEFAULT = 'CURRENT_TIMESTAMP' THEN 'OK' ELSE 'NG' END AS `判定`
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'DcwCncStation'
  AND DATA_TYPE = 'timestamp'
ORDER BY TABLE_NAME, COLUMN_NAME;

-- ----------------------------------------------------------------------------
-- 校验 7：迁移历史基线（期望 3 行）
-- ----------------------------------------------------------------------------
SELECT `MigrationId`, `ProductVersion`
FROM `__EFMigrationsHistory`
ORDER BY `MigrationId`;

SELECT COUNT(*) AS `迁移记录数`,
       CASE WHEN COUNT(*) = 3 THEN 'OK' ELSE 'NG（期望 3）' END AS `判定`
FROM `__EFMigrationsHistory`;

-- ----------------------------------------------------------------------------
-- 校验 8：综合结论（单项为 0 表示缺项，全部非 0 即通过）
-- ----------------------------------------------------------------------------
SELECT
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
      WHERE TABLE_SCHEMA = 'DcwCncStation'
        AND TABLE_NAME IN ('Project','Wall','ValidationError','DataCheckRecord',
                           'PlcInstruction','Opc','MachiningException','MachiningRecord')) AS `业务表数(期望8)`,
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
      WHERE TABLE_SCHEMA = 'DcwCncStation' AND REFERENCED_TABLE_NAME IS NOT NULL) AS `外键数(期望7)`,
    (SELECT COUNT(DISTINCT INDEX_NAME) FROM INFORMATION_SCHEMA.STATISTICS
      WHERE TABLE_SCHEMA = 'DcwCncStation' AND TABLE_NAME = 'Wall') AS `Wall索引数(期望15)`,
    (SELECT COUNT(*) FROM `__EFMigrationsHistory`) AS `迁移基线数(期望3)`;
