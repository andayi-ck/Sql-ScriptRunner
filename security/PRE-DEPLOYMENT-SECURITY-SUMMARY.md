# Pre-Deployment Security Summary

**Document ID:** KSR-SEC-001
**Classification:** Internal — Release Record
**Version:** 1.0
**Date:** 2026-11-15
**Repository:** `andayi-ck/Sql-ScriptRunner` (`KabarakScriptRunnerApp`)
**Baseline commit:** `b5699fe` — *Implemented Refactor Agent*
**Working tree:** Clean
**Prepared by:** Security review (white-box audit + AgentShield + refactor sweep)
**Release decision:** **BLOCK — not cleared for deployment**

---

## 1. Document control

| Field | Value |
|-------|-------|
| Application | Kabarak Script Runner (WPF, `net10.0-windows`) |
| Solution | `KabarakScriptRunnerApp.slnx` — App, Core, Data, Database (SSDT), PasswordTool, Tests |
| Review type | White-box source audit, dependency/supply-chain scan, dead-code/logical-trap sweep |
| Review mode | Read-only. No source changes made by this review. |
| Scope | Primary source (`*.cs`, `*.xaml`, `*.csproj`, `*.sql`, `*.iss`, `.github/workflows/*.yml`). Excluded `bin/`, `obj/`, `.vs/`. |
| Prior artifacts | `security/agentshield-consolidated-report.md`, `security/refactor-cleaner-sweep.md` |
| Build evidence | `dotnet build` App Release — **succeeded, 0 errors, 10 warnings** |
| Test evidence | `dotnet test` — **15 passed, 0 failed** |

**Purpose.** This document is the security release record. It lists every verified risk, the structural fixes already applied, the outstanding remediation required, and the objective gate for shipping.

---

## 2. Release decision

> **BLOCK.** The application must not be deployed until the three **P0** items in §5 are closed and independently verified. The current build is functionally shippable but security-incomplete.

| Severity | Open | Partial | Closed | Total |
|----------|------|---------|--------|-------|
| Critical | 2 | 0 | 0 | 2 |
| High | 7 | 1 | 0 | 8 |
| Medium | 12 | 1 | 0 | 13 |
| Low | 8 | 0 | 2 | 10 |
| **Total** | **29** | **2** | **2** | **33** |

> 31 risks remain live (29 open + 2 partial). Two prior findings were closed as false positives. Twenty structural fixes are recorded separately in §4.

**Headline risks**

1. A production SQL credential is committed in the installer **and in git history**.
2. The SQL guard is a type denylist; a large set of destructive statements still executes.
3. Department isolation is advertised in the UI but **not enforced** in the query.

---

## 3. Verified risk register

Status legend: **OPEN** = present in `b5699fe` · **PARTIAL** = control exists but is incomplete · **FIXED** = verified remediated · **CLOSED** = false positive / no longer applicable.

### 3.1 Critical

| ID | Risk | Evidence | Status |
|----|------|----------|--------|
| **SEC-01** | Hardcoded SQL login `kabarak_app` / `Kab@rak#2026!App` in the installer; the same secret exists in git history, so deleting the line is insufficient. | `KabarakScriptRunner.iss:36-37`; history commits `c44d6dc`, `0137895`, `84b06f5` (`git log -S`) | **OPEN** |
| **SEC-02** | SQL validator is a denylist over ~200 ScriptDom statement types. Destructive and exfiltrating statements still pass. The 2026-11-14 refactor **removed** the `CREATE PROCEDURE`, `GRANT`, `BACKUP`, and `RESTORE` guards. | `ScriptValidator.cs:51-85`; error text at `:42`; `ScriptExecutorService.cs:26,59` | **OPEN (regressed)** |

**SEC-01 — exploit path.** Anyone with repository read access (contractor, CI log, fork, backup) obtains a working SQL login. If that login is reused in production, the credential is already compromised. Rotating the password without purging history leaves the old secret recoverable.

**SEC-02 — exploit path.** `ScriptExecutorService` validates, then executes with `Integrated Security=True`. On a DBA/developer workstation the interactive user is frequently `sysadmin`. A stored script containing any of the following passes validation and executes:

```sql
DROP DATABASE Kabarak;
ALTER DATABASE Kabarak SET READ_ONLY;
CREATE DATABASE Evil;
USE master;
SHUTDOWN;
KILL 51;
RECONFIGURE;
DBCC FREEPROCCACHE;
WAITFOR DELAY '00:00:10';
BULK INSERT uni.Students FROM 'C:\temp\x.csv';
CREATE VIEW / FUNCTION / TRIGGER / SCHEMA / LOGIN ...
ALTER LOGIN ... WITH PASSWORD = '...';
DENY SELECT ON uni.Students TO public;
EXECUTE AS LOGIN = 'sa';
SELECT * FROM OPENROWSET('SQLNCLI','Server=evil;...','SELECT 1');
```

The comment at `ScriptValidator.cs:55` claims *"We ONLY allow SelectStatement. Everything else is blocked by default."* This is false: the visitor flags only 11 specific node types.

### 3.2 High

| ID | Risk | Evidence | Status |
|----|------|----------|--------|
| **SEC-03** | Department isolation is not enforced. `GetScriptsAsync(departmentId, isAdmin)` ignores both parameters; every active script is returned to every user. | `ScriptRepository.cs:88-104` (`WHERE s.IsActive = 1` at `:99`); callers pass both at `MainWindow.xaml.cs:110-112` | **OPEN** |
| **SEC-04** | The "Test Syntax" button is treated as a security gate, but it calls the same incomplete validator. It proves parseability, not safety. | `ScriptEditorWindow.xaml.cs:37-58`; `ScriptValidator.cs` | **OPEN** |
| **SEC-05** | Admin authorization is UI-only. Buttons are disabled/hidden, but repository methods accept no caller identity, so any code path that reaches a repository can mutate users, scripts, and settings. | `MainWindow.xaml.cs:62-70,327-377`; `ScriptRepository.cs:254-337` | **OPEN** |
| **SEC-06** | Lockout exists but is non-atomic and caller-driven. The counter is read once and passed in; concurrent attempts can defeat or mis-time the lockout. No password complexity/rotation policy. | `ScriptRepository.cs:172-190`; `LoginWindow.xaml.cs:84-86` | **PARTIAL** |
| **SEC-07** | TLS certificate validation is disabled for SQL connections. | `appsettings.json:3`; `KabarakScriptRunner.iss:201` | **OPEN** |
| **SEC-08** | The installer writes a cleartext SQL-auth password into `appsettings.json` on every install. | `KabarakScriptRunner.iss:193-209` | **OPEN** |
| **SEC-09** | Exported PDF/Excel artifacts are unencrypted and written to a user-chosen folder. Exports can contain student and fee data. | `PdfReportService.cs:42-76`; `ExcelExportService`; `BatchExportWindow.xaml.cs:292-331` | **OPEN** |
| **SEC-10** | "Run At" scheduling is an in-process `Task.Delay`; closing the app silently loses the job. | `BatchExportWindow.xaml.cs:215-233` | **OPEN** |

### 3.3 Medium

| ID | Risk | Evidence | Status |
|----|------|----------|--------|
| **SEC-11** | Error leakage: some UI paths still render raw `ex.Message`; others correctly log and show generic text. Inconsistent. | `UsersWindow.xaml.cs:33`; `AccessLogWindow.xaml.cs:280`; good examples `LoginWindow.xaml.cs:41,128` | **PARTIAL** |
| **SEC-12** | Audit reliability: audit writes are fire-and-forget (`_ = LogAuditAsync`), and run-state updates are wrapped in empty `catch {}`. Audit state can silently diverge from reality. | `MainWindow.xaml.cs:284,589`; `:246,554,561,592`; `BatchExportWindow.xaml.cs:337` | **OPEN** |
| **SEC-13** | CI supply chain is unpinned: mutable action tags, unpinned Chocolatey Inno Setup, inconsistent `upload-artifact` versions, no vulnerability gate. | `.github/workflows/build.yml:17,21,51,59,75` | **OPEN** |
| **SEC-14** | Log rows are anonymous objects read by reflection (`GetProperty("UserId")`); a property rename fails at runtime, not compile time. | `AccessLogWindow.xaml.cs:79-83,239-245` | **OPEN** |
| **SEC-15** | .NET Framework-only packages restored into a `net10.0-windows` app (`NU1701`): `OpenTK`, `OpenTK.GLWpfControl`, `SkiaSharp.Views.WPF`, `ToastNotifications`, `ToastNotifications.Messages`. | Build restore output | **OPEN** |
| **SEC-16** | Configuration is reloaded on change but the file has no ACL hardening and sits beside the executable. | `StartupValidator.cs:32` (`reloadOnChange: true`) | **OPEN** |
| **SEC-17** | `TotalScripts` is populated from `ActiveScripts`; PDF/dashboard totals can understate the true count. | `DashboardWindow.xaml.cs:76` | **OPEN** |
| **SEC-18** | Parallel dashboard DTOs (`MonthlyTotal`/`MonthTotal`, `YearlyTotal`/`YearTotal`, `RecentActivity`/`ActivityRow`) with manual mapping — silent field drift. | `DashboardRepository.cs:235+`; `PdfReportService.cs:407-429` | **OPEN** |
| **SEC-19** | Retry/transient logic duplicated in repositories and executor; fixing one copy does not fix the others. | `ScriptRepository.cs:546-574`; `DashboardRepository.cs:215-229`; `ScriptExecutorService.cs:33-43` | **OPEN** |
| **SEC-20** | Process-wide static service locator (`App.ServiceProvider`, `App.Configuration`) increases attack surface and complicates shutdown ordering. | `App.xaml.cs:18-19` | **OPEN** |
| **SEC-21** | Agent configuration has no explicit `permissions` block and no `PreToolUse` deny hooks. | `.cursor/settings.json` (AgentShield A-01/A-02) | **OPEN** |
| **SEC-22** | No repository-level agent rules (`CLAUDE.md`, `AGENTS.md`, `.cursorrules`) documenting secret-handling or destructive-command guardrails. | Repository scan (AgentShield R-03) | **OPEN** |
| **SEC-23** | Unused dependency surface: `Notification.Wpf`, `ToastNotifications*`, `Serilog.Extensions.Logging`, `Serilog.Sinks.Console`, direct `BCrypt` in App, `Pkcs` in Core/Data/Database, `Microsoft.Extensions.Configuration.Json` in Data. | Project files; zero source references | **OPEN** |

### 3.4 Low

| ID | Risk | Evidence | Status |
|----|------|----------|--------|
| **SEC-24** | Sentry DSN is hardcoded in source; csproj still has placeholder org/project slugs. | `App.xaml.cs:44`; `KabarakScriptRunnerApp.csproj:37-39` | **OPEN** |
| **SEC-25** | PasswordTool reads the password with `Console.ReadLine()` — visible echo, no masking. | `KabarakScriptRunner.PasswordTool\Program.cs:18` | **OPEN** |
| **SEC-26** | `Process.Start` launches `explorer.exe` with paths derived from user-selected folders; one call is unquoted. | `BatchExportWindow.xaml.cs:336`; `AccessLogWindow.xaml.cs:275`; `DashboardWindow.xaml.cs:122`; `OptionsWindow.xaml.cs:83` | **OPEN** |
| **SEC-27** | Database provider is inferred by substring matching; unknown providers silently default to SQL Server. | `App.xaml.cs:26-36` | **OPEN** |
| **SEC-28** | Batch export always requests `isAdmin: true`, bypassing the intended department filter. | `BatchExportWindow.xaml.cs:50` | **OPEN** |
| **SEC-29** | Validation failure reason is discarded (`catch { return false; }`). | `Validation\ScriptRunValidator.cs:38` | **OPEN** |
| **SEC-30** | Debug artifact: `await Task.Delay(5000)` in the close/logout flow. | `MainWindow.xaml.cs:496` | **OPEN** |
| **SEC-31** | Unused scaffolding: `AppTitle` setting is never applied; `ICurrentUserService` is registered and populated but never read. | `OptionsWindow.xaml.cs:26,54`; `App.xaml.cs:150,175` | **OPEN** |
| **SEC-32** | `security/` audit artifacts are tracked; confirm this is intended for the release record (it now is). | `git ls-files security` | **CLOSED** |
| **SEC-33** | `AssignScriptsWindow` `null` → `0` was reported as a defect. Verified false positive: `0` is converted back to `null` at save, and the schema allows NULL. | `AssignScriptsWindow.xaml.cs:74`; schema | **CLOSED** |

---

## 4. Structural fixes applied

These are verified in `b5699fe` and its two immediate predecessors. They are retained as part of the release record.

| # | Fix | Evidence | Verification |
|---|-----|----------|--------------|
| 1 | **Password reset cast corrected** — `List<UserSummary>` is now cast to `UserSummary`, not `User`. | `ChangePasswordWindow.xaml.cs:43`; `ChangePasswordValidator.cs` | Compiles; selection path reachable |
| 2 | **User activate/deactivate cast corrected** — same `UserSummary` fix. | `UsersWindow.xaml.cs:60,73` | Compiles; toggle path reachable |
| 3 | **PDF font made portable** — `Lato` replaced with system `Arial`, removing a clean-machine render failure. | `PdfReportService.cs:51,100` | Build + code review |
| 4 | **Patch hygiene** — stale `// FIXED:` comment removed. | `UsersWindow.xaml.cs:72` (commit `b5699fe`) | Diff verified |
| 5 | **Password hashing standardized** — BCrypt with work factor 11, single hasher. | `Core\Security\PasswordHasher.cs:9,19` | Unit tests |
| 6 | **Login enumeration resistance** — generic failure text for unknown user, wrong password, and department mismatch. | `LoginWindow.xaml.cs:67,93,105` | Code review |
| 7 | **Account lockout implemented** — 5 failures trigger a 5-minute lockout, with audit event. | `ScriptRepository.cs:172-190`; `LoginWindow.xaml.cs:84-89` | Code review (race remains, SEC-06) |
| 8 | **Login department check** — non-admin users must match the selected department. | `LoginWindow.xaml.cs:100-111` | Code review (listing still unfiltered, SEC-03) |
| 9 | **Audit persistence** — admin and auth actions written to `app.AdminActions`. | `ScriptRepository.cs:499-517`; call sites in Login, Users, UserEdit, Assign, ChangePassword, BatchExport | Code review |
| 10 | **Sentry PII suppressed** — `SendDefaultPii = false`; global mode enabled. | `App.xaml.cs:48-49` | Code review |
| 11 | **Startup fail-fast** — missing/invalid `appsettings.json` aborts startup with a controlled error instead of running unconfigured. | `StartupValidator.cs:14-70` | Code review |
| 12 | **Parameterized SQL** — Dapper parameters used across repositories; no string-concatenated user input found in query paths. | `ScriptRepository.cs`, `DashboardRepository.cs` | Repository-wide review |
| 13 | **ScriptDom parsing introduced** — `TSql150Parser` rejects malformed SQL before execution. | `ScriptValidator.cs:21-34` | 15 unit tests |
| 14 | **SDK pinned** — `global.json` pins `10.0.401`. | `global.json` | File present |
| 15 | **Restore locked (App)** — `RestorePackagesWithLockFile=true` and `packages.lock.json` committed. | `KabarakScriptRunnerApp.csproj`; `packages.lock.json` | File present |
| 16 | **Unused `OpenAI` dependency removed** from Core. | `Core.csproj` | No `OpenAI` reference remains |
| 17 | **`Pkcs` advisory cleared** — `System.Security.Cryptography.Pkcs` upgraded to 9.0.21; `dotnet list --vulnerable` reports none. | Core/Data/Database csproj | Scan output |
| 18 | **`.cursor/` ignored** — agent configuration cannot be committed accidentally. | `.gitignore` | File review |
| 19 | **Audit reports retained** under `security/` as release evidence. | `git ls-files security` | 5 artifacts tracked |
| 20 | **`AssignScriptsWindow` false positive corrected** — `null`→`0` round-trips correctly. | `AssignScriptsWindow.xaml.cs:74` | Code + schema review |

**Not a fix:** AgentShield grade **A (98/100)** is scoped to a single `.cursor/settings.json` and is **not** an application security clearance. It must not be cited as such in the release record.

---

## 5. Outstanding remediation plan

### P0 — Release blockers (must close before any deployment)

| # | Task | Acceptance criteria | Closes |
|---|------|---------------------|--------|
| P0-1 | Rotate the `kabarak_app` SQL credential; remove `ServerPassword` from `KabarakScriptRunner.iss`; purge the secret from git history; move auth to Windows Integrated or a per-machine secret store. | `git log -S` finds no credential; installer contains no literal password; new credential verified against DB; history rewrite coordinated with all clones. | SEC-01, SEC-08 |
| P0-2 | Replace the validator denylist with a positive allow-list: every top-level statement must be `SelectStatement`; reject `Into != null`, `OpenRowsetTableReference`, `OpenQueryTableReference`, `BulkOpenRowset`. Rewrite the error message. | One negative test per statement family (`DROP/ALTER/CREATE DATABASE`, `USE`, `SHUTDOWN`, `KILL`, `RECONFIGURE`, `DBCC`, `WAITFOR`, `BULK INSERT`, `CREATE/ALTER/DROP VIEW/FUNCTION/TRIGGER/SCHEMA/LOGIN`, `DENY`, `REVOKE`, `EXECUTE AS`, `OPENROWSET`, `OPENQUERY`) all fail validation. Confirm temp-table policy first. | SEC-02, SEC-04 |
| P0-3 | Enforce department isolation in `GetScriptsAsync`: `AND (@IsAdmin = 1 OR s.DepartmentId = @DepartmentId)`. | Non-admin user receives only own-department scripts; admin receives all; regression test added. | SEC-03, SEC-28 |

### P1 — Required before general availability

| # | Task | Acceptance criteria | Closes |
|---|------|---------------------|--------|
| P1-1 | Set `TrustServerCertificate=False`; provision and pin a trusted SQL certificate. | Connection succeeds with validation enabled. | SEC-07 |
| P1-2 | Make lockout atomic in SQL (`UPDATE ... SET FailedAttempts = FailedAttempts + 1, LockoutEnd = CASE ...`), wrapped in the retry helper; remove caller-supplied count. | Concurrent-attempt test cannot exceed the threshold or skip the lockout. | SEC-06 |
| P1-3 | Add server-side authorization: pass caller identity/role into repository methods or enforce via DB roles. | Non-admin cannot invoke user/script/settings mutations even if UI guards are bypassed. | SEC-05 |
| P1-4 | Protect exports: restrict output folder ACLs, add optional password/encryption, or remove bulk export of sensitive columns. | Exported artifacts are access-controlled at rest. | SEC-09 |
| P1-5 | Replace in-process scheduling with a persisted job runner, or remove the "Run At" control. | Scheduled job survives app restart; no silent loss. | SEC-10 |
| P1-6 | Introduce a typed `AccessLogRow` DTO and bind the grid to it. | No reflection property lookup remains in `AccessLogWindow`. | SEC-14 |
| P1-7 | Harden CI: pin actions to commit SHAs, pin Inno Setup version, add `dotnet list package --vulnerable` as a failing gate, normalize `upload-artifact`. | Workflow diff reviewed; vulnerable-package step fails the build on findings. | SEC-13 |
| P1-8 | Move the Sentry DSN to configuration/secret storage and rotate it. | No DSN literal in source. | SEC-24 |
| P1-9 | Add ACL hardening for `appsettings.json` and validate file ownership at startup. | Config file writable only by Administrators/SYSTEM. | SEC-16 |
| P1-10 | Fix audit reliability: await or queue audit writes; replace empty `catch {}` with logged failures. | No `catch {}` remains in audit/run-state paths; audit write failures are observable. | SEC-12 |

### P2 — Hardening and cleanup

| # | Task | Closes |
|---|------|--------|
| P2-1 | Remove dead code: `RetryPolicyFactory.cs`, `PdfExportService.cs`, `ErrorHandler.cs`, versioning methods, `DatabaseContext.ConnectionString`, `ToastService.Initialize`, no-op `ScrollChanged`, duplicate `TextChanged`, warning brushes. | SEC-23 |
| P2-2 | Prune unused packages; keep `Microsoft.Extensions.Hosting`; swap Data's `Configuration.Json` for `Configuration.Abstractions`. | SEC-23 |
| P2-3 | Consolidate retry helpers and dashboard DTOs; remove manual mapping. | SEC-18, SEC-19 |
| P2-4 | Replace static service locator with injected abstractions. | SEC-20 |
| P2-5 | Standardize error handling: log detail, show generic text everywhere. | SEC-11 |
| P2-6 | Remove `Task.Delay(5000)`; apply or remove `AppTitle`; remove or use `ICurrentUserService`. | SEC-30, SEC-31 |
| P2-7 | Mask password input in PasswordTool; quote all `Process.Start` arguments. | SEC-25, SEC-26 |
| P2-8 | Fix `TotalScripts`; replace provider substring sniffing with an explicit setting. | SEC-17, SEC-27 |
| P2-9 | Add `.cursor/settings.json` `permissions` + `PreToolUse` deny hooks; add repository agent rules. | SEC-21, SEC-22 |
| P2-10 | Surface validation failure reasons instead of `catch { return false; }`. | SEC-29 |

---

## 6. Dependency and supply-chain posture

| Check | Result |
|-------|--------|
| Known vulnerable NuGet packages | **None reported** (`dotnet list package --vulnerable --include-transitive`) |
| Previously vulnerable `Pkcs` | Upgraded 9.0.18 → **9.0.21**; advisories GHSA-846f-f764-76mx and GHSA-2fmv-rv2c-59x7 no longer trigger |
| SDK reproducibility | **Pinned** via `global.json` (`10.0.401`) |
| Restore reproducibility | **App only** — `packages.lock.json` + `RestorePackagesWithLockFile=true`; other projects unlocked |
| Framework compatibility | 5 packages emit `NU1701` (restored as .NET Framework binaries) |
| CI supply chain | **Weak** — mutable action tags, unpinned Chocolatey, no vulnerability gate |
| Unused dependencies | 9+ references with zero source use (see SEC-23) |

**Caveat:** advisory databases cover published CVEs only. "No vulnerable packages" does not cover abandoned packages, framework-only binaries, or unused dependencies.

---

## 7. Verification evidence

| Command | Result |
|---------|--------|
| `dotnet build KabarakScriptRunnerApp\KabarakScriptRunnerApp.csproj -c Release` | **Succeeded** — 0 errors, 10 warnings (`NU1701`) |
| `dotnet test tests\KabarakScriptRunner.Tests\KabarakScriptRunner.Tests.csproj -c Release` | **Passed** — 15/15, 0 failed, 0 skipped |
| `dotnet list ... package --vulnerable --include-transitive` | **No vulnerable packages** |
| `dotnet build KabarakScriptRunnerApp.slnx` | **Not supported** — `MSB4278`; SSDT `Database` project requires Visual Studio targets |
| AgentShield `scan --supply-chain --supply-chain-online --compliance all` | **A (98/100)** on 1 file; **not** an app clearance |

**Test coverage gap (release-relevant):** 15 tests cover Excel export, password hashing, and validator basics. There are **no** repository, authorization, lockout-concurrency, or end-to-end tests. SEC-01 through SEC-03 are therefore unguarded by regression tests.

---

## 8. Release gate checklist

Deployment is approved only when every line is true and evidenced:

- [ ] P0-1 closed — credential rotated, purged from history, installer literal removed
- [ ] P0-2 closed — allow-list validator merged with per-family negative tests
- [ ] P0-3 closed — department filter enforced with a non-admin regression test
- [ ] `dotnet build` and `dotnet test` green on the release commit
- [ ] No new `--vulnerable` findings
- [ ] P1 items either closed or formally risk-accepted by the product owner with a dated exception
- [ ] This document re-issued as v1.1+ with updated statuses and the release commit hash

**Current status: 0 of 3 P0 gates met.**

---

## 9. Evidence index

| Artifact | Path |
|----------|------|
| This summary | `security/PRE-DEPLOYMENT-SECURITY-SUMMARY.md` |
| AgentShield consolidated report | `security/agentshield-consolidated-report.md` |
| AgentShield markdown / JSON | `security/agentshield-report.md`, `security/agentshield-report.json` |
| Refactor / dead-code sweep | `security/refactor-cleaner-sweep.md` |
| Transitive package inventory | `security/transitive-packages.txt` |

---

## 10. Limitations

- Static analysis and targeted execution only; the application was not launched, and no database or installer was executed.
- The SSDT `Database` project was not built by `dotnet`; its schema and permissions were reviewed as text only.
- WPF resource keys, Dapper name-based mapping, DI, and reflection can create references invisible to static search; affected items are marked in the sweep report.
- Secret-history exposure (SEC-01) is asserted from `git log -S`; a full history rewrite was not performed by this review.
- Severity reflects confidentiality/integrity/availability impact in the Kabarak deployment context (student and fee data, DBA workstations with elevated SQL rights).

---

## 11. Sign-off

| Role | Name | Decision | Date |
|------|------|----------|------|
| Security reviewer | ____________________ | BLOCK / APPROVE | __________ |
| Engineering lead | ____________________ | ____________________ | __________ |
| Product owner | ____________________ | ____________________ | __________ |

*Exceptions to any P0/P1 item must be recorded here with a compensating control and an expiry date.*
