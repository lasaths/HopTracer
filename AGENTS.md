# AGENTS.md

This root file is a quick agent-facing entrypoint. Full project guidance lives in:

- `docs/AGENTS.md`

## Required Build Command (Agents)
Run builds from the repository root using the PowerShell script:

```powershell
.\scripts\build.ps1
```

If needed for faster iteration:

```powershell
.\scripts\build.ps1 -SkipTests
```

```powershell
.\scripts\build.ps1 -SkipClean
```

If script execution is blocked by policy, use:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

Note: Prefer `scripts/build.ps1` over ad-hoc `dotnet build` for final validation, because the script runs the full clean/test/build/publish flow used for releases.

## CLI for Agents (`hoptracer`)

Headless Grasshopper diffing for CI and AI workflows. Skill docs: [`skills/gh-diff/SKILL.md`](skills/gh-diff/SKILL.md).

```powershell
dotnet publish ./Source/Tools/GhDiffTool/GhDiffTool.csproj -c Release -o ./bin/hoptracer
./bin/hoptracer/hoptracer.exe compare old.ghx new.ghx --format agent
```

Use `--format agent` for AI-oriented JSON. Portable release: `Release/HopTracer_Portable/tools/hoptracer/hoptracer.exe`.
