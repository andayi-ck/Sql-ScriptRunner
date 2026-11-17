# Secret Protection Scan

**Target:** `C:\Users\User\source\repos\KabarakScriptRunnerApp`
**Date:** 2026-11-16
**Baseline:** `b5699fe`
**Mode:** Read-only. No source, config, or secret material was modified.
**Scope:** All primary source — `*.cs`, `*.xaml`, `*.json`, `*.xml`, `*.config`, `*.sql`, `*.iss`, `*.yml/.yaml`, `*.csproj`, `*.user`, `*.props/.targets`. Excluded `bin/`, `obj/`, `.vs/`, `.git/`.

> **Redaction notice:** Secret values in this report are masked. Do not paste the live value back into any tracked file.

---

## 1. Verdict

| Result | Count |
|--------|-------|
| Confirmed leaked secrets | **2** (same credential, two locations) |
| Semi-secret / sensitive literals | **2** |
| Weak secret-handling patterns | **1** |
| Clean config files | 5 |
| Clean class-property surface | all models |
| API keys / tokens / private keys found | **0** |

**Bottom line:** one real production credential is exposed and is committed twice — in the installer **and** in the tracked security report. There are no leaked API keys, bearer tokens, JWTs, or plaintext cryptographic keys.

---

## 2. Confirmed findings

| ID | Severity | Finding | Location | Status |
|----|----------|---------|----------|--------|
| **SEK-01** | **Critical** | Hardcoded SQL login password `kabarak_app` / `Kab@r***!App` in the installer define block. | `KabarakScriptRunner.iss:36-37` | **OPEN** |
| **SEK-02** | **Critical** | The same credential is re-printed in cleartext inside a **tracked** audit report, so the secret is committed a second time independent of the installer. | `security/agentshield-consolidated-report.md:143` | **OPEN** |
| **SEK-03** | Medium | Sentry DSN hardcoded in application source. A DSN is a write-capable client key: anyone who obtains it can inject events into the Sentry project. Not an auth token, but it is deployment-specific and should not ship in source. | `KabarakScriptRunnerApp\App.xaml.cs:44` | **OPEN** |
| **SEK-04** | Medium | Installer writes a cleartext SQL-auth password into `appsettings.json` on every install. Any local user or backup can read it. | `KabarakScriptRunner.iss:193-209` | **OPEN** |
| **SEK-05** | Low | Password utility reads secrets with `Console.ReadLine()` — input is echoed to the terminal and may land in scrollback or session logs. | `KabarakScriptRunner.PasswordTool\Program.cs:17` | **OPEN** |

### History exposure

`git log -S` confirms the SEK-01 credential is present in history:

```
c44d6dc  Third-Party Package Dependencies, system rules and Workspace configurations...
0137895  Solved Package Dependencies and GITHUB Actions Pipeline Build
84b06f5  Updated the Left Panel with better UI
```

Deleting the line from the working tree is **insufficient**. The credential must be rotated first, then purged from history, then all clones re-synced.

---

## 3. Clean areas (verified, no secrets)

| Surface | Files checked | Result |
|---------|---------------|--------|
| `appsettings.json` | `KabarakScriptRunnerApp\appsettings.json` | **Clean** — `Integrated Security=True`; no password, no token. Git history confirms it never contained SQL-auth credentials. |
| XML configuration | `KabarakScriptRunner.Database\Scripts\app.config` | **Clean** — only `<DbProviderFactories>` registration; no connection string, no credentials. |
| Agent config | `.cursor\settings.json` | **Clean** — only a plugin enable flag. |
| SDK pin | `global.json` | **Clean** — version only. |
| User/project files | `KabarakScriptRunnerApp.csproj.user`, `KabarakScriptRunner.Database.csproj.user` | **Clean** — only `Compile` subtype metadata. |
| Class properties | `Department`, `Script`, `ScriptVersion`, `User`, `UserSummary` | **Clean** — `User.PasswordHash` stores a BCrypt hash, never plaintext. No key/secret/token properties. |
| Settings store | `app.Settings` via `GetSettingAsync`/`SetSettingAsync` | **Clean** — only `OutputFolder` and `AppTitle` are stored. |
| SQL seed scripts | 11 files under `KabarakScriptRunner.Database\Scripts` | **Clean** — seeded admin uses literal `PLACEHOLDER_HASH`, not a usable credential. No `CREATE LOGIN ... WITH PASSWORD`. |
| CI workflow | `.github\workflows\build.yml` | **Clean** — no `secrets.*` usage, no hardcoded tokens. |
| Dependency installer | `CodeDependencies.iss` | **Clean** — registry paths and signed-MSI SHA-256 checksums only. |
| Lock files | 4 × `packages.lock.json` | **Clean** — `contentHash` values are NuGet package hashes, not secrets. |

### Negative results

No matches for any of the following across primary source:

- Cloud/provider API keys: `AKIA…`, `ASIA…`, `AIza…`, `sk-…`
- VCS/chat tokens: `ghp_…`, `gho_…`, `github_pat_…`, `xox[baprs]-…`
- JWTs: `eyJ…`
- Private keys: `-----BEGIN … PRIVATE KEY-----`
- Base64/hex key material in C# (`Convert.FromBase64String`, long hex blobs)
- `<add key=…>`, `MachineKey`, `validationKey`, `decryptionKey`
- Hardcoded IP addresses outside `localhost` / the documented `192.168.1.42` example

---

## 4. Remediation

### P0 — before any deployment

1. **Rotate** the `kabarak_app` SQL login immediately. Treat the current password as compromised.
2. **Remove** `#define ServerPassword` from `KabarakScriptRunner.iss`; source the password from a per-machine secret store or switch to Windows Integrated auth.
3. **Redact** the credential from `security/agentshield-consolidated-report.md:143` (replace with `<redacted>`), then **purge both occurrences from git history** (`git filter-repo` / BFG), force-push, and have every clone re-clone.
4. **Verify:** `git grep -n 'Kab@rak' $(git rev-list --all)` returns nothing; installer contains no literal password.

### P1 — before general availability

5. **SEK-03:** move the Sentry DSN to configuration/secret storage, and rotate it. Remove the placeholder `SentryOrg`/`SentryProject` slugs or populate them from CI secrets.
6. **SEK-04:** stop writing SQL-auth passwords into `appsettings.json`; if SQL auth is retained, use a DPAPI-protected value or a credential manager, and ACL the file to Administrators/SYSTEM.

### P2 — hygiene

7. **SEK-05:** mask input in `PasswordTool` (`Console.ReadKey(intercept: true)` loop) and avoid echoing.
8. Add a pre-commit secret scanner (gitleaks / TruffleHog) and a CI gate so new secrets fail the build.
9. Add `security/` artifacts to the review process so reports are redacted before commit.

---

## 5. Verification commands

```powershell
# 1. Confirm no live credential remains in the working tree
git grep -n 'Kab@rak'

# 2. Confirm history is clean after rewrite (use the live value locally; do not commit it)
git log --all -S'<ROTATED_PASSWORD>' --oneline

# 3. Re-run the config sweep
Get-ChildItem -Recurse -File -Include *.json,*.config,*.xml,*.iss,*.yml |
  Where-Object { $_.FullName -notmatch 'bin|obj|\.vs' } |
  Select-String -Pattern 'Password=|Pwd=|User Id=|api[_-]?key|secret|token'

# 4. Provider key patterns
Get-ChildItem -Recurse -File |
  Where-Object { $_.FullName -notmatch 'bin|obj|\.vs|\.git' } |
  Select-String -Pattern 'sk-|ghp_|AKIA|AIza|-----BEGIN'
```

---

## 6. Limitations

- Static pattern matching plus targeted history search; no entropy-based binary scan of compiled artifacts (excluded by scope).
- The Sentry DSN is classified as a semi-secret, not a credential with read access; impact is event injection, not data exfiltration.
- `PLACEHOLDER_HASH` was reviewed as text only; confirm in the deployed database that the admin row was replaced with a real BCrypt hash before go-live.
