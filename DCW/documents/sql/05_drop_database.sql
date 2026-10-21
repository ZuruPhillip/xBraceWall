-- ============================================================================
-- 05_drop_database.sql  ——  危险操作 / 仅作回滚参考，默认不执行
-- 用途：彻底删除 CncWallStation（DCW）系统业务库
-- 警告：
--   1. 执行后库内全部业务数据不可恢复，只能在执行前通过备份还原；
--   2. 执行前必须完成备份（见《数据库创建操作手册》第 7 章）；
--   3. 请确认未连接生产工位，或已停止 CncWallStation 客户端程序。
-- ----------------------------------------------------------------------------
-- 备份命令（在服务器或具备权限的机器上执行）：
--   mysqldump -h 10.33.120.30 -P 3306 -u root -p \
--             --single-transaction --routines --triggers \
--             --default-character-set=utf8mb4 \
--             DcwCncStation > DcwCncStation_backup_YYYYMMDD.sql
-- ============================================================================

DROP DATABASE IF EXISTS `DcwCncStation`;
