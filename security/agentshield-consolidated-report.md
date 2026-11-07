# AgentShield + Dependency / Workspace Security Scan

**Target:** `C:\Users\User\source\repos\KabarakScriptRunnerApp`
**Date:** 2026-11-07
**Tool:** `ecc-agentshield` 1.6.0 (AgentShield)
**Mode:** Read-only. No source, configuration, or dependency files were modified.

---

## 1. Executive summary

| Area | Result |
|------|--------|
| AgentShield configuration grade | **A (98/100)** |
| AgentShield findings | 2 Medium, 0 High, 0 Critical |
| AgentShield coverage | **1 file only** (`.cursor/settings.json`) |
| NuGet vulnerable packages | **None reported** across all 5 projects |
| NuGet health / drift | 7 outdated direct packages, 5 `NU1701` compatibility warnings |
| Workspace secrets | **1 Critical**: hardcoded SQL password in `KabarakScriptRunner.iss` |
| CI / supply-chain hardening | Weak: mutable action tags, unpinned Chocolatey, no lock file |

> **Important scope caveat:** AgentShield is an **AI-agent configuration auditor**. It does not scan
> NuGet packages, C# source, SQL scripts, or GitHub Actions workflows. The `A` grade reflects a single
> `.cursor/settings.json` file and **must not** be read as an application security clearance. The
> application-level findings from the prior white-box review (F-01 … F-16) still apply.

---

## 2. AgentShield results

Command executed:

```powershell
npx.cmd ecc-agentshield scan --path . --format terminal --supply-chain --supply-chain-online --compliance all -v
```

| Metric | Value |
|--------|-------|
| Grade | A (98/100) |
| Files scanned | 1 |
| Findings | 2 Medium |
| Harness adapters matched | 0 / 9 |
| Supply-chain packages analysed | 0 (no MCP package references found) |
| Injection payloads blocked | 70 / 70 |
| Sandbox hooks executed | 0 |
| Taint flows | 0 |

### Findings

| # | Severity | Finding | File |
|---|----------|---------|------|
| A-01 | Medium | No `permissions` block configured; agent falls back to default permissions | `.cursor/settings.json` |
| A-02 | Medium | No `PreToolUse` security hooks configured | `.cursor/settings.json` |

Compliance mapping produced by AgentShield: SOC 2 CC6.1/CC6.3/CC7.1, PCI DSS Req 2/Req 7,
ISO/IEC 27001 A.5.15/A.8.2/A.8.9.

### Why the score is misleading

* `.cursor/settings.json` contains only a plugin enable flag:
  `{"plugins": {"affaan-m-ecc/ecc": {"enabled": true}}}`.
* There is **no** `.claude/` directory, `CLAUDE.md`, `AGENTS.md`, `.cursorrules`, `mcp.json`,
  or hooks directory in the repository. AgentShield matched **0 of 9** harness adapters.
* No MCP servers are configured, so the supply-chain verifier had nothing to inspect.
* The scan therefore cannot detect the repository's real structural risks, which are covered in
  sections 3–5 below.

---

## 3. Third-party package dependency audit

Commands executed per project (the `.Database` SSDT project cannot be evaluated by `dotnet list`
because it requires Visual Studio SSDT targets):

```powershell
dotnet list <project>.csproj package --vulnerable --include-transitive
dotnet list <project>.csproj package --outdated
dotnet list KabarakScriptRunnerApp\KabarakScriptRunnerApp.csproj package --include-transitive
```

### 3.1 Known vulnerabilities

`dotnet list package --vulnerable --include-transitive` reported **no vulnerable packages** for:

* `KabarakScriptRunner.Core`
* `KabarakScriptRunner.Data`
* `KabarakScriptRunnerApp`
* `KabarakScriptRunner.PasswordTool`
* `tests\KabarakScriptRunner.Tests`

This is an improvement over the earlier audit state: `System.Security.Cryptography.Pkcs` is now
**9.0.21** in both `Core` and `Data` (previously 9.0.18, affected by GHSA-846f-f764-76mx and
GHSA-2fmv-rv2c-59x7). The current dependency graph no longer triggers those advisories.

> Advisory databases only cover published CVEs. "No vulnerable packages" does **not** cover
> abandoned packages, .NET Framework-only binaries, or unused dependencies.

### 3.2 Outdated direct packages

| Project | Package | Current | Latest | Risk |
|---------|---------|---------|--------|------|
| Core | Microsoft.Data.SqlClient | 7.1.0 | 7.1.1 | Patch — apply |
| Core | OpenAI | 2.14.0 | 2.15.0 | **Unused dependency — remove** |
| Core | Polly | 8.8.0 | 8.9.0 | Minor — apply |
| Core | System.Security.Cryptography.Pkcs | 9.0.21 | 10.0.14 | Major — plan |
| Data | Microsoft.Data.SqlClient | 7.1.0 | 7.1.1 | Patch — apply |
| Data | Microsoft.Extensions.Configuration.Json | 9.0.0 | 10.0.14 | Major — plan |
| Data | Polly | 8.8.0 | 8.9.0 | Minor — apply |
| Data | System.Security.Cryptography.Pkcs | 9.0.21 | 10.0.14 | Major — plan |
| App | Microsoft.Extensions.Hosting | 9.0.0 | 10.0.14 | Major — plan |
| Tests | coverlet.collector | 6.0.4 | 10.1.0 | Major — plan |

### 3.3 Structural package risks

| # | Severity | Finding | Evidence |
|---|----------|---------|----------|
| D-01 | High | **Unused `OpenAI` 2.14.0 dependency** in `KabarakScriptRunner.Core` — no source file references `OpenAI`, `ChatClient`, or `OpenAIClient`. Unused packages enlarge the attack surface and are not audited at runtime. | `Core.csproj:16`; grep returned no matches |
| D-02 | Medium | **.NET Framework-only packages restored into a `net10.0-windows` app** (`NU1701`): `OpenTK 3.3.1`, `OpenTK.GLWpfControl 3.3.0`, `SkiaSharp.Views.WPF 3.119.0`, `ToastNotifications 2.5.1`, `ToastNotifications.Messages 2.5.1`. These are not validated against the .NET 10 runtime. | Build restore warnings |
| D-03 | Medium | **No `packages.lock.json` and no `RestorePackagesWithLockFile`** — transitive versions can drift between developer and CI restores. | No lock file found |
| D-04 | Medium | **No `global.json`** — local SDK is `10.0.401`, CI requests floating `10.0.x`. Builds are not SDK-reproducible. | Repo root; `build.yml:23` |
| D-05 | Low | **No `NuGet.config`** — restore relies entirely on the machine's configured feeds. | No NuGet config found |
| D-06 | Low | **Mixed major versions in the graph** (`Microsoft.Extensions.*` 9.0.0 and 10.0.x side by side) increases the chance of binding/behaviour drift. | Transitive package listing |
| D-07 | Info | `Microsoft.Bcl.Cryptography 9.0.18` and `System.Threading.RateLimiting 9.0.18` are pulled transitively and are behind the .NET 10 servicing line. | Transitive package listing |

---

## 4. Agent rules / system configuration audit

| # | Severity | Finding | Evidence |
|---|----------|---------|----------|
| R-01 | Medium | `.cursor/settings.json` has **no `permissions` block**. The agent relies on defaults instead of an explicit allow/deny list. | AgentShield A-01 |
| R-02 | Medium | **No `PreToolUse` hooks** to block destructive shell/file operations. | AgentShield A-02 |
| R-03 | Medium | **No repository-level agent rules** (`.claude/`, `CLAUDE.md`, `AGENTS.md`, `.cursorrules`) — no documented guardrails for shell execution, secrets handling, or dependency changes. | Directory scan |
| R-04 | Medium | **`.cursor/` is untracked and not git-ignored.** It will be committed by a broad `git add .`, creating unreviewed agent configuration drift. | `git check-ignore` returned nothing |
| R-05 | Low | AgentShield report artifacts under `security/` are also untracked and not ignored. Decide whether to commit them as audit evidence or ignore them. | `git check-ignore` returned nothing |

---

## 5. Workspace / configuration audit

| # | Severity | Finding | Evidence |
|---|----------|---------|----------|
| W-01 | **Critical** | **Hardcoded SQL credential** `Kab@rak#2026!App` for login `kabarak_app` in the installer script. | `KabarakScriptRunner.iss:37` |
| W-02 | **Critical** | The same credential is present in **git history** (commits `0137895`, `84b06f5`), so deleting the line is not sufficient — history must be purged and the password rotated. | `git log -S` |
| W-03 | High | Installer writes a **cleartext SQL-auth password into `appsettings.json`** on every install. | `KabarakScriptRunner.iss:193-209` |
| W-04 | High | `TrustServerCertificate=True` disables certificate validation for SQL connections. | `KabarakScriptRunner.iss:201`, `appsettings.json:3` |
| W-05 | Medium | CI actions pinned to **mutable major tags**, not commit SHAs: `actions/checkout@v5`, `actions/setup-dotnet@v5`, `actions/upload-artifact@v5` and `@v4`. | `.github/workflows/build.yml:17,21,51,75` |
| W-06 | Medium | `choco install innosetup --yes --no-progress` is **unpinned** — CI executes whatever version Chocolatey serves. | `.github/workflows/build.yml:59` |
| W-07 | Medium | CI has **no dependency vulnerability gate** (`dotnet list package --vulnerable`, NuGet audit-as-error, or lock-file verification). | `.github/workflows/build.yml` |
| W-08 | Low | `actions/upload-artifact` uses **v5 in one step and v4 in another** — inconsistent action versions. | `.github/workflows/build.yml:51,75` |
| W-09 | Low | `.gitignore` does not ignore `.cursor/` or `security/`; `appsettings.json` is intentionally shipped but must never contain a real credential. | `.gitignore` |
| W-10 | Info | `.vs/`, `bin/`, `obj/` are correctly ignored; the audit excluded them as requested. | `.gitignore:2-4` |

---

## 6. Priority remediation

1. **Immediately (blocking):** rotate the `kabarak_app` SQL password, remove `ServerPassword` from
   `KabarakScriptRunner.iss`, purge the secret from git history, and move the credential to a
   per-machine secret store or Windows-integrated auth.
2. **Before release:** stop writing SQL-auth passwords into `appsettings.json`; set
   `TrustServerCertificate=False` and provision a trusted certificate.
3. **Before release:** add an explicit `permissions` block and `PreToolUse` deny rules to
   `.cursor/settings.json`; decide whether `.cursor/` is committed or ignored.
4. **Next dependency pass:** remove the unused `OpenAI` package, replace .NET Framework-only UI
   packages, add `packages.lock.json` + `global.json`, and update patch/minor packages.
5. **CI hardening:** pin actions to SHAs, pin the Inno Setup version, add a vulnerability gate, and
   normalise `upload-artifact` to one version.

---

## 7. Evidence artifacts

| Artifact | Path |
|----------|------|
| AgentShield terminal/markdown report | `security/agentshield-report.md` |
| AgentShield JSON report | `security/agentshield-report.json` |
| Transitive package inventory | `security/transitive-packages.txt` |
| This consolidated report | `security/agentshield-consolidated-report.md` |

Reproduce:

```powershell
npx.cmd ecc-agentshield scan --path . --format markdown --supply-chain --compliance all
dotnet list KabarakScriptRunnerApp\KabarakScriptRunnerApp.csproj package --vulnerable --include-transitive
dotnet list KabarakScriptRunnerApp\KabarakScriptRunnerApp.csproj package --outdated
```
