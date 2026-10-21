# CncWallStation (DCW) Database Setup Manual

| Item | Value |
| --- | --- |
| Document | CncWallStation (DCW) Database Setup Manual |
| Applies to | CncWallStation wall-panel CNC host application (.NET 8 / WPF) |
| Database | MySQL 8.0 (target version 8.0.46) |
| Database name | `DcwCncStation` |
| Target server | `10.33.120.30:3306` |
| Document version | V1.1 (EN) |
| Date | 2026-10-21 |
| Audience | Field implementation engineers, IT operations, backend developers |

---

## Table of Contents

1. [Overview](#1-overview)
2. [Environment and Prerequisites](#2-environment-and-prerequisites)
3. [Delivered Scripts and Execution Order](#3-delivered-scripts-and-execution-order)
4. [Method 1: Command-Line Setup (Recommended)](#4-method-1-command-line-setup-recommended)
5. [Method 2: GUI Client Setup](#5-method-2-gui-client-setup)
6. [Method 3: EF Core Design-Time Commands](#6-method-3-ef-core-design-time-commands)
7. [Automatic Table Creation at Startup](#7-automatic-table-creation-at-startup)
8. [Verifying the Result](#8-verifying-the-result)
9. [Troubleshooting](#9-troubleshooting)
10. [Backup, Restore and Rollback](#10-backup-restore-and-rollback)
11. [Appendices](#11-appendices)

---

## 1 Overview

### 1.1 Purpose

This manual explains how to create the MySQL database `DcwCncStation` required by CncWallStation from scratch. It covers the database itself, all 8 business tables, every index and foreign key, the EF Core migration baseline, plus executable verification and troubleshooting procedures.

### 1.2 Where the Schema Comes From

The schema is not hand-written. It is derived from the application code so that it always matches what the application writes:

| Source | Purpose |
| --- | --- |
| `CncWallStation/appsettings.json` → `ConnectionStrings:Default` | Single source of truth for connection settings (server, port, database, account) |
| `CncWallStation/Migrations/AppDbContextModelSnapshot.cs` | Final state of the EF Core model; basis for the full DDL |
| `CncWallStation/EntityFrameworkCore/AppDbContext.cs` | Index names, default values, cascade deletes, `MEDIUMTEXT` column types |
| `CncWallStation/Migrations/2026*_*.cs` | Three existing incremental migrations, used to create the migration baseline |
| `CncWallStation/Services/Application/DatabaseInitializerHostedService.cs` | Fallback table creation at startup, controlled by the `Database:AutoInitialize` switch (see Chapter 7) |

### 1.3 Scope and Limitations

- This manual applies to creating a **brand-new empty database**, and to re-running the scripts idempotently on an existing database (no table is dropped, no data is deleted).
- If the target database already holds production data with an older schema, do not run this manual directly. Back it up first (Chapter 10) and let the development team confirm the upgrade path.
- After the migration baseline is created, **never run `dotnet ef database update` against this database**. See 6.3 for the reason.

---

## 2 Environment and Prerequisites

### 2.1 Target Database Information

| Parameter | Value | Remarks |
| --- | --- | --- |
| Host (IP) | `10.33.120.30` | Follow the `appsettings.json` shipped with the station |
| Port | `3306` | Default instance port |
| Database name | `DcwCncStation` | Case-sensitive; do not write `DcwCNCStation` |
| Character set | `utf8mb4` | Mandatory; otherwise Chinese characters will fail to persist |
| Collation | `utf8mb4_0900_ai_ci` | Same as sibling databases on this server (e.g. `ShutteringStation`) |
| Storage engine | `InnoDB` | Required for foreign keys and transactions |
| Account | `root` (current configuration) | A dedicated account is recommended for production, see 2.4 |

> **Security note**: the password is deliberately not recorded in this manual. Obtain it from `ConnectionStrings:Default` in `CncWallStation/appsettings.json`, or from the on-site DBA. Never write the password into text files on the station PC, chat history, or ticket screenshots.

### 2.2 Client Tools and Versions

| Tool | Minimum version | Purpose |
| --- | --- | --- |
| `mysql` CLI client | 5.7 or later (8.0 recommended) | Run scripts, verify results |
| MySQL Workbench / Navicat / DBeaver | Any recent version | Run scripts and inspect the schema visually |
| .NET SDK | 8.0 | Only for Method 3 and for running the application |
| `dotnet-ef` tool | 8.0.10 or later | Only for Method 3 |

Typical Windows location of `mysql.exe`: `C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe`. Confirm first:

```powershell
mysql --version
```

### 2.3 Connectivity Checks

Always confirm network reachability before running any script, so that a "connect timeout" is not misread as a script error.

```powershell
# Windows PowerShell: check whether port 3306 is reachable
Test-NetConnection -ComputerName 10.33.120.30 -Port 3306
```

```bash
# Linux / on the database server itself
nc -zv 10.33.120.30 3306
```

Then confirm the account can log in (enter the password interactively; never put it on the command line):

```powershell
mysql -h 10.33.120.30 -P 3306 -u root -p -e "SELECT @@hostname, @@version, @@character_set_server;"
```

> If `skip_name_resolve` is not enabled, MySQL reverse-resolves the client host name. The first connection may stall for 10–15 seconds or even time out; a retry usually succeeds immediately. The application's `DatabaseInitializerHostedService` implements the same retry policy (retries after 2 s and 5 s).

### 2.4 Required Privileges

The account used for setup needs at least:

| Privilege | Purpose |
| --- | --- |
| `CREATE` | Create the database and tables |
| `ALTER` | Apply future schema changes |
| `INDEX` | Create indexes |
| `REFERENCES` | Create foreign keys |
| `CREATE TEMPORARY TABLES` | Used by some EF Core scenarios |
| `SELECT` / `INSERT` / `UPDATE` / `DELETE` | Normal business reads and writes |

For production, prefer a dedicated account (run the statements below as `root` on the server, replacing the password per site policy):

```sql
-- Run on the server as root
CREATE USER IF NOT EXISTS 'dcw_app'@'%' IDENTIFIED BY '<strong-password>';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, INDEX, REFERENCES, CREATE TEMPORARY TABLES
    ON `DcwCncStation`.* TO 'dcw_app'@'%';
FLUSH PRIVILEGES;
```

After switching accounts, update `ConnectionStrings:Default` in `CncWallStation/appsettings.json` accordingly.

> Note the difference between two privilege sets: the table above is the minimum for the **setup account** that runs this manual's SQL. The **runtime account** does **not** need `CREATE` / `ALTER` / `INDEX` / `REFERENCES` when `Database:AutoInitialize=false` (the shipped value); `SELECT` / `INSERT` / `UPDATE` / `DELETE` are sufficient (see 7.3).

---

## 3 Delivered Scripts and Execution Order

All scripts live in `documents/sql/` and **must be executed in numeric order**.

| No. | Script | Purpose | Idempotent | Safe to re-run |
| --- | --- | --- | --- | --- |
| 01 | `01_create_database.sql` | Creates the `DcwCncStation` database (utf8mb4 / utf8mb4_0900_ai_ci) | Yes | Yes |
| 02 | `02_schema_full.sql` | Creates the 8 business tables (indexes, foreign keys, defaults) | Yes | Yes |
| 03 | `03_migration_history_baseline.sql` | Creates `__EFMigrationsHistory` and records the 3 existing migrations | Yes | Yes |
| 04 | `04_verify.sql` | Structural verification (database charset, table count, indexes, foreign keys, defaults, baseline) | Read-only | Yes |
| 05 | `05_drop_database.sql` | Drops the whole database (**dangerous; rollback reference only, do not execute**) | Yes | No (do not run casually) |

Execution order:

```
01_create_database.sql  →  02_schema_full.sql  →  03_migration_history_baseline.sql  →  04_verify.sql
```

**Stop on first error**: if any script fails, stop immediately and do not run the remaining scripts. A half-built database (missing tables or indexes) is much harder to diagnose than an empty one.

---

## 4 Method 1: Command-Line Setup (Recommended)

### 4.1 Windows (PowerShell)

Open PowerShell at the repository root `DCW/`:

```powershell
# 1) Pass the password through an environment variable so it never appears
#    in the command line or shell history
$env:MYSQL_PWD = Read-Host "MySQL password" -AsSecureString |
    ForEach-Object { [Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [Runtime.InteropServices.Marshal]::SecureStringToBSTR($_)) }

# 2) Create the database
mysql -h 10.33.120.30 -P 3306 -u root --default-character-set=utf8mb4 `
      -e "source d:/Works/workspace/projects/WorkStations/xBraceWall/DCW/documents/sql/01_create_database.sql"

# 3) Create the tables
mysql -h 10.33.120.30 -P 3306 -u root --default-character-set=utf8mb4 DcwCncStation `
      -e "source d:/Works/workspace/projects/WorkStations/xBraceWall/DCW/documents/sql/02_schema_full.sql"

# 4) Create the migration baseline
mysql -h 10.33.120.30 -P 3306 -u root --default-character-set=utf8mb4 DcwCncStation `
      -e "source d:/Works/workspace/projects/WorkStations/xBraceWall/DCW/documents/sql/03_migration_history_baseline.sql"

# 5) Verify
mysql -h 10.33.120.30 -P 3306 -u root --default-character-set=utf8mb4 DcwCncStation `
      -e "source d:/Works/workspace/projects/WorkStations/xBraceWall/DCW/documents/sql/04_verify.sql"

# 6) Clear the password variable
Remove-Item Env:MYSQL_PWD
```

> Note: `mysql -e "source ..."` accepts only one `source` command. Do not chain several `source` statements with semicolons in a single `-e` argument.

### 4.2 Linux / macOS (bash)

```bash
cd /path/to/DCW/documents/sql

read -s -p "MySQL password: " MYSQL_PWD; export MYSQL_PWD

mysql -h 10.33.120.30 -P 3306 -u root --default-character-set=utf8mb4 < 01_create_database.sql
mysql -h 10.33.120.30 -P 3306 -u root --default-character-set=utf8mb4 DcwCncStation < 02_schema_full.sql
mysql -h 10.33.120.30 -P 3306 -u root --default-character-set=utf8mb4 DcwCncStation < 03_migration_history_baseline.sql
mysql -h 10.33.120.30 -P 3306 -u root --default-character-set=utf8mb4 DcwCncStation < 04_verify.sql

unset MYSQL_PWD
```

If you see `ERROR 1045 (28000): Access denied`, refer to Chapter 9.

### 4.3 Combined Single-File Execution (Optional)

When the setup must be handed over to someone else, merge 01–03 into one file. The order must be preserved, otherwise foreign key creation fails:

```powershell
Get-Content 01_create_database.sql, 02_schema_full.sql, 03_migration_history_baseline.sql |
    Set-Content -Encoding utf8 all_in_one.sql
mysql -h 10.33.120.30 -P 3306 -u root --default-character-set=utf8mb4 DcwCncStation -e "source all_in_one.sql"
```

> `04_verify.sql` must still be executed separately for verification.

---

## 5 Method 2: GUI Client Setup

Using MySQL Workbench as the example:

1. **Create a connection**: `Database` → `Manage Connections`; Hostname `10.33.120.30`, Port `3306`, Username `root`; enter the password under `Store in Vault`; confirm with `Test Connection` and save.
2. **Open a SQL editor**: connect, then `File` → `Open SQL Script` and open `documents/sql/01_create_database.sql`.
3. **Create the database**: click the lightning icon (Execute), or press `Ctrl+Shift+Enter` to run everything. The result grid should show `DcwCncStation | utf8mb4 | utf8mb4_0900_ai_ci`.
4. **Select the schema**: refresh the `SCHEMAS` panel, right-click `DcwCncStation` → `Set as Default Schema`.
5. **Create the tables**: open `02_schema_full.sql`, make sure the schema selector shows `DcwCncStation`, then run everything (`Ctrl+Shift+Enter`). All 8 tables should appear under `SCHEMAS`.
6. **Create the baseline**: open `03_migration_history_baseline.sql` and run it. The result set should return 3 migration rows.
7. **Verify**: open `04_verify.sql` and run it; compare each row with the expected values in Chapter 8.

Navicat works the same way: `Connection` → `New Query` → open the script → `Run`. Make sure the connection's default database is `DcwCncStation`, otherwise the script runs against the default schema.

> **Important**: GUI clients may execute the entire editor buffer. Always confirm the active schema is `DcwCncStation` so that tables are not created inside `ShutteringStation` or `mysql`.

---

## 6 Method 3: EF Core Design-Time Commands

Use this method when DDL must be re-derived from the code (development or hand-over scenarios).

### 6.1 Where the Design-Time Connection String Comes From

`CncWallStation/EntityFrameworkCore/AppDbContextFactory.cs` now reads `ConnectionStrings:Default` from the project's `appsettings.json`, exactly like the runtime:

- Lookup order: current working directory → assembly output directory, walking up the directory tree;
- Temporary override without touching code:

```powershell
$env:DCW_DB_CONNECTION = "Server=10.33.120.30;Port=3306;Database=DcwCncStation;User ID=root;Password=***;Charset=utf8mb4;"
```

> Historical pitfall: `AppDbContextFactory` used to hard-code `Server=10.34.120.31`, which differs from `10.33.120.30` in `appsettings.json`, so design-time commands could target the wrong server. This is fixed in the current revision.

### 6.2 Generating the Full DDL

```powershell
cd d:/Works/workspace/projects/WorkStations/xBraceWall/DCW/CncWallStation

# Install or upgrade the design-time tool (first time, or when versions mismatch)
dotnet tool install --global dotnet-ef

# Generate the full table creation script from the model snapshot
dotnet ef dbcontext script -o ../documents/sql/02_schema_full.sql
```

Review the generated output against `AppDbContext.OnModelCreating`:

| Check | Expectation |
| --- | --- |
| Table count | 8 (`Project`, `Wall`, `ValidationError`, `DataCheckRecord`, `PlcInstruction`, `Opc`, `MachiningException`, `MachiningRecord`) |
| `Wall` indexes | 15 including the primary key; names identical to `HasDatabaseName` in code |
| Large text columns | `Wall.BimJsonData`, `Wall.MomJsonData`, `MachiningException.Description` are `MEDIUMTEXT` |
| `timestamp` columns | Must carry `DEFAULT CURRENT_TIMESTAMP` |
| Foreign keys | 7, all `ON DELETE CASCADE` (`Opc` has no foreign key by model design) |

> Note: `dotnet ef dbcontext script` emits plain `CREATE TABLE` (no `IF NOT EXISTS`), standalone `CREATE INDEX` statements, and a leading `ALTER DATABASE CHARACTER SET utf8mb4;`. The `02_schema_full.sql` in this repository is that output rewritten idempotently (`CREATE TABLE IF NOT EXISTS` + inline indexes + explicit collation); the structure is equivalent.

### 6.3 Why `dotnet ef database update` Must Not Be Used

The `Migrations/` folder contains only three **incremental** migrations and **no initial create-table migration**:

- `20260519083754_AddDataCheckSupport` (adds columns to `ValidationError`, creates `DataCheckRecord`)
- `20260713022100_AddPlcInstructionSide` (adds the `Side` column and index to `PlcInstruction`)
- `20260727022215_AddPerformanceIndexes` (adds two composite indexes on `Wall`)

On an empty database, `dotnet ef database update` replays the first migration, which tries to `ALTER` the non-existent `ValidationError` table and fails. Therefore: build the full structure with `02_schema_full.sql`, then register the existing migrations with `03_migration_history_baseline.sql`.

### 6.4 Standard Workflow for Future Migrations

After the baseline exists, developers changing the model (entities or `OnModelCreating`) should:

```powershell
dotnet ef migrations add <MigrationName>            # create the incremental migration
dotnet ef migrations script <Previous> <Current>    # generate the reviewed upgrade SQL for the site
```

The site executes the reviewed upgrade SQL and updates `__EFMigrationsHistory` accordingly (or runs `database update` only after DBA approval). **Never run `database update` directly against production.**

---

## 7 Automatic Table Creation at Startup

Understanding this mechanism avoids duplicate setup work and misdiagnosis of "the application created the database by itself".

### 7.1 Switch: `Database:AutoInitialize`

`Database:AutoInitialize` in `appsettings.json` decides whether the application creates tables at startup:

```json
{
  "ConnectionStrings": {
    "Default": "Server=10.33.120.30;Port=3306;Database=DcwCncStation;User ID=root;Password=<password>;Charset=utf8mb4;Connection Timeout=30;"
  },
  "Database": {
    "AutoInitialize": false
  }
}
```

| Value | Startup behaviour | Intended use |
| --- | --- | --- |
| `false` (shipped default) | **Skips everything**: no database creation, no table creation, no upgrade DDL | Production stations; schema is owned by the SQL scripts in this manual |
| `true` | Runs the `EnsureCreated` + hand-written DDL flow described in 7.2 | Development and first-time environment bring-up |
| Missing or unparsable | Treated as `true`, with a warning log entry | Backward compatibility with older configuration files |

> The switch is read from `appsettings.json` and can be overridden by an environment variable (e.g. `Database__AutoInitialize=true`). Restart the client after changing it.

### 7.2 Behaviour When the Switch Is `true`

`CncWallStation/Services/Application/DatabaseInitializerHostedService.cs` runs during startup (`IHostedService.StartAsync`):

1. `db.Database.EnsureCreatedAsync()` — if the database does not exist, it **creates the database and tables**; if it already exists, it **does nothing** (no schema comparison, no schema change).
2. It then runs a set of hand-written `CREATE TABLE IF NOT EXISTS` statements (`PlcInstruction`, `Opc`, `MachiningException`, `MachiningRecord`) and several `ALTER TABLE` statements (adding the `Side` column, renaming `Operator` to `Registrant`, adding new columns to the exception table, etc.).
3. Connection-class errors (timeouts, network hiccups) are retried after 2 s and 5 s (3 attempts total); privilege errors (`AccessDenied` / `UnknownDatabase`) are not retried and surface immediately.

### 7.3 Consequences and Log Interpretation

- With the shipped configuration (`false`) the application **executes no DDL at startup**; the schema is fully owned by this manual's scripts, and the application can be started repeatedly.
- In that mode the runtime account **does not need `CREATE DATABASE` / `ALTER` privileges**; `SELECT` / `INSERT` / `UPDATE` / `DELETE` are sufficient (index-related privileges are only needed by the setup account).
- With auto-initialization disabled, a missing database or table does not block startup; the main window opens and business queries fail instead (`Table 'dcwc...' doesn't exist`). Run the setup scripts first.
- Log interpretation (`logs/log-YYYYMMDD.txt`):
  - `数据库自动初始化已禁用（Database:AutoInitialize = false）...` — switch is effective, as expected in production;
  - `开始初始化数据库...` / `数据库初始化完成` — the auto-creation flow ran (`true`);
  - `数据库初始化第 x/3 次尝试失败` — network latency or slow reverse DNS resolution.

---

## 8 Verifying the Result

### 8.1 Automated Verification

Run `04_verify.sql` and compare each result with the table below (values measured after a successful run on 2026-10-21):

| Check | Expected | Measured |
| --- | --- | --- |
| Database charset | `utf8mb4` / `utf8mb4_0900_ai_ci` | OK |
| Table count | 9 (8 business tables + `__EFMigrationsHistory`) | 9 → OK |
| `Wall` index count | 15 (including primary key) | 15 → OK |
| Foreign keys | 7, all `CASCADE` | 7 / 7 → OK |
| `MEDIUMTEXT` columns | `Wall.BimJsonData`, `Wall.MomJsonData`, `MachiningException.Description` | 3/3 → OK |
| `timestamp` defaults | All 8 `timestamp` columns use `CURRENT_TIMESTAMP` | 8/8 → OK |
| Migration baseline | 3 rows | 3 → OK |

Column and index counts per table (indexes include the primary key):

| Table | Columns | Indexes |
| --- | --- | --- |
| `Project` | 8 | 2 |
| `Wall` | 19 | 15 |
| `DataCheckRecord` | 11 | 3 |
| `ValidationError` | 12 | 4 |
| `PlcInstruction` | 17 | 4 |
| `Opc` | 6 | 3 |
| `MachiningException` | 17 | 2 |
| `MachiningRecord` | 7 | 2 |

### 8.2 Manual Spot Checks

```sql
SHOW TABLES;                           -- expect 9 rows
SHOW CREATE TABLE `Wall`\G             -- check MEDIUMTEXT, defaults, indexes, cascades
SELECT * FROM `__EFMigrationsHistory`; -- expect 3 rows
```

### 8.3 Application-Level Verification

1. Confirm `ConnectionStrings:Default` in `CncWallStation/appsettings.json` points to the new database;
2. Start `CncWallStation.exe` and look for `数据库自动初始化已禁用（Database:AutoInitialize = false）` in `logs/log-YYYYMMDD.txt` (or `数据库初始化完成` when the switch is `true`);
3. Import a BIM data set in the application, then query:

```sql
SELECT COUNT(*) FROM `Project`;
SELECT COUNT(*) FROM `Wall`;
```

Rows present proves that read/write paths, character set and foreign keys are all correct.

---

## 9 Troubleshooting

| Error / symptom | Possible cause | Remedy |
| --- | --- | --- |
| `ERROR 2003 (HY000): Can't connect to MySQL server` | Network unreachable, wrong IP/port, firewall, MySQL not running | Re-test with `Test-NetConnection` (2.3); verify `Server`/`Port` in `appsettings.json` |
| `ERROR 1045 (28000): Access denied for user` | Wrong password, account not granted for the client host, `MYSQL_PWD` mangled by shell escaping | Re-enter the password interactively with `-p`; grant the client subnet; check PowerShell escaping |
| `ERROR 1044 (42000): Access denied for user ... to database` | Account lacks `CREATE` or privileges on the target database | Grant privileges as described in 2.4 |
| `ERROR 1049 (42000): Unknown database` | `01_create_database.sql` not executed, or database name misspelled | Create the database first; verify `DcwCncStation` |
| `ERROR 1215 (HY000): Cannot add foreign key constraint` | Wrong table creation order (parent missing), non-InnoDB engine, column type or charset mismatch | Follow the 01 → 02 order strictly; ensure InnoDB |
| `ERROR 1075 / 1005` | A legacy table with the same name but a different structure exists | Compare with `SHOW CREATE TABLE`; back up and drop that table, then recreate |
| `ERROR 1366 (HY000): Incorrect string value` | Database, table or connection charset is not utf8mb4 | Check the database collation; the connection string must contain `Charset=utf8mb4` |
| `ERROR 1267 Illegal mix of collations` | Mixed `utf8mb4_general_ci` and other collations | Use `utf8mb4_0900_ai_ci` consistently (already enforced by the scripts) |
| `ERROR 1290 --secure-file-priv` | `LOAD DATA` restricted | Not part of this setup flow; use a GUI client for imports |
| Application log `DatabaseAccessDenied` | Runtime account lacks privileges on the target database | Grant privileges per 2.4, or change the account in the connection string |
| Application log `Connect Timeout expired` | MySQL reverse DNS lookup of the client host times out | Enable `skip_name_resolve` on the server, or rely on the startup retry policy |
| Timestamps off by 8 hours | MySQL session time zone differs from the application time zone | Not affected by table creation; optionally set `default-time-zone='+08:00'` on the server |

> **Historical connection-string mismatch**: `AppDbContextFactory.cs` used to hard-code `10.34.120.31` (factory server) while `appsettings.json` used `10.33.120.30`. If a `dotnet ef` command connects to an unexpected server, check for stale code or a leftover `DCW_DB_CONNECTION` environment variable.

---

## 10 Backup, Restore and Rollback

### 10.1 Backup

If the target database already exists, **back it up first**:

```powershell
mysqldump -h 10.33.120.30 -P 3306 -u root -p `
          --single-transaction --routines --triggers `
          --default-character-set=utf8mb4 `
          DcwCncStation > DcwCncStation_backup_20261021.sql
```

### 10.2 Restore

```powershell
mysql -h 10.33.120.30 -P 3306 -u root --default-character-set=utf8mb4 DcwCncStation `
      -e "source DcwCncStation_backup_20261021.sql"
```

> `mysqldump` output does not contain `CREATE DATABASE` by default. Create the empty database first with `01_create_database.sql` before restoring.

### 10.3 Rollback (Drop the Database)

`documents/sql/05_drop_database.sql` contains:

```sql
DROP DATABASE IF EXISTS `DcwCncStation`;
```

Before executing it:

1. Complete the backup in 10.1 and verify the dump is readable;
2. Stop all CncWallStation clients and related services;
3. Obtain written approval from the site owner.

After dropping, rebuild following Chapter 4. Historical business data can only be recovered from the backup.

---

## 11 Appendices

### Appendix A Business Tables

| Table | Purpose | Key characteristics |
| --- | --- | --- |
| `Project` | A BIM import batch (project) | Auto-increment int key; stores source path, importer, total wall count |
| `Wall` | Wall master data | Auto-increment bigint key; unique on `ProjectId + WallId`; BIM/MOM JSON stored as `MEDIUMTEXT`; soft delete via `IsDeleted` |
| `ValidationError` | Data validation error details | `WallId` nullable; optionally linked to a check record via `DataCheckGroupId` |
| `DataCheckRecord` | Pre-machining data check record | `varchar(64)` key generated by the application; stores BIM/MOM scores, duration, result |
| `PlcInstruction` | PLC machining instructions | Ordered by `WallId + SortOrder`; `Side` distinguishes front/back face |
| `Opc` | OPC UA write log | No foreign key (keeps logs of deleted walls) |
| `MachiningException` | Machining exception report | Occurred time, frequency, repair method, duration, completion time |
| `MachiningRecord` | Machining record | Start/end time, total duration, status |

### Appendix B Indexes

`Wall` table (15 indexes including the primary key):

| Index | Columns | Type |
| --- | --- | --- |
| `PRIMARY` | `Id` | Primary key |
| `IX_Wall_ProjectId_WallId` | `ProjectId`, `WallId` | Unique |
| `IX_Wall_ProjectName` | `ProjectName` | Regular |
| `IX_Wall_Floor` | `Floor` | Regular |
| `IX_Wall_Status` | `Status` | Regular |
| `IX_Wall_Priority` | `Priority` | Regular |
| `IX_Wall_ImportTime` | `ImportTime` | Regular |
| `IX_Wall_EndProductionTime` | `EndProductionTime` | Regular |
| `IX_Wall_PipelineStage` | `PipelineStage` | Regular |
| `IX_Wall_AuditStatus` | `AuditStatus` | Regular |
| `IX_Wall_WallName` | `WallName` | Regular |
| `IX_Wall_IsDeleted` | `IsDeleted` | Regular |
| `IX_Wall_IsDeleted_EndProductionTime_Id` | `IsDeleted`, `EndProductionTime`, `Id` | Composite |
| `IX_Wall_ProjectName_Floor` | `ProjectName`, `Floor` | Composite |
| `IX_Wall_ProjectName_Status_Floor` | `ProjectName`, `Status`, `Floor` | Composite |

Other tables:

| Table | Indexes |
| --- | --- |
| `Project` | `IX_Project_ProjectName` |
| `DataCheckRecord` | `IX_DataCheckRecord_WallId`, `IX_DataCheckRecord_CheckTime` |
| `ValidationError` | `IX_ValidationError_WallId`, `IX_ValidationError_GroupId`, `IX_ValidationError_DataCheckGroupId` |
| `PlcInstruction` | `IX_PlcInstruction_WallId`, `IX_PlcInstruction_WallId_SortOrder`, `IX_PlcInstruction_WallId_Side` |
| `Opc` | `IX_Opc_GroupId`, `IX_Opc_WallId` |
| `MachiningException` | `IX_MachiningException_WallId` |
| `MachiningRecord` | `IX_MachiningRecord_WallId` |

### Appendix C Foreign Keys (All `ON DELETE CASCADE`)

| Foreign key | Child column → parent column |
| --- | --- |
| `FK_Wall_Project_ProjectId` | `Wall.ProjectId` → `Project.Id` |
| `FK_DataCheckRecord_Wall_WallId` | `DataCheckRecord.WallId` → `Wall.Id` |
| `FK_ValidationError_Wall_WallId` | `ValidationError.WallId` → `Wall.Id` |
| `FK_ValidationError_DataCheckRecord_DataCheckGroupId` | `ValidationError.DataCheckGroupId` → `DataCheckRecord.Id` |
| `FK_PlcInstruction_Wall_WallId` | `PlcInstruction.WallId` → `Wall.Id` |
| `FK_MachiningException_Wall_WallId` | `MachiningException.WallId` → `Wall.Id` |
| `FK_MachiningRecord_Wall_WallId` | `MachiningRecord.WallId` → `Wall.Id` |

### Appendix D Connection String Template

`CncWallStation/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Server=10.33.120.30;Port=3306;Database=DcwCncStation;User ID=root;Password=<password>;Charset=utf8mb4;Connection Timeout=30;"
  },
  "Database": {
    "AutoInitialize": false
  }
}
```

Requirements:

- `Charset=utf8mb4` is mandatory;
- `Server`, `Port` and `Database` must match section 2.1;
- Replace `User ID` / `Password` with the dedicated account in production;
- Set `Database:AutoInitialize` to `false` in production so that this manual's scripts own the schema (see Chapter 7);
- Restart the client application after changing the file.

### Appendix E Version History

| Version | Date | Change |
| --- | --- | --- |
| V1.0 (EN) | 2026-10-21 | English edition: 5 SQL scripts; `DcwCncStation` created on `10.33.120.30` (8 business tables + migration baseline); `AppDbContextFactory` switched to `appsettings.json` with a fixed `MySqlServerVersion(8,0,32)` |
| V1.1 (EN) | 2026-10-21 | Added the `Database:AutoInitialize` switch (shipped as `false`) so production skips startup-time table creation/upgrade; added the 7.1 switch reference, 7.3 log interpretation, and the reduced runtime-account privilege note |
