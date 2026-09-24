# HopTracer CLI Skill

Agent skill for the `hoptracer` CLI — compare Grasshopper definitions (`.gh` / `.ghx`), git history, baselines, forensic reports, and agent-oriented diff JSON.

## Install skill (agents)

```bash
npx skills add lasaths/HopTracer@hoptracer -g -y
```

## Install CLI

Send `hoptracer-win-x64.zip` from GitHub release **v1.3.1**. Unzip it and run `hoptracer.exe`.

The repository is private, so send the file unless the recipient can access the repo. WinGet needs a public installer URL; `wingetcreate new` gets HTTP 404 on this release.

- **Microsoft Store**: `hoptracer` is on PATH after the desktop app install. That copy can be older.
- **From source**: `dotnet publish ./Source/Tools/GhDiffTool/GhDiffTool.csproj -c Release -o ./bin/hoptracer`

For `.gh` files: `scripts/setup_dependencies.ps1` (installs `GH_IO.dll`).

## Usage

```bash
hoptracer doctor
hoptracer compare old.ghx new.ghx
hoptracer compare old.gh new.gh --format agent -o diff-agent.json
hoptracer compare old.gh new.gh --format html -o report.html
hoptracer git current.gh --commit HEAD~1 --format markdown -o changes.md
hoptracer git current.gh --fail-on-risk
hoptracer baseline save current.gh --name release-1.0
hoptracer baseline compare current.gh --name release-1.0 --fail-on-risk
hoptracer report old.ghx new.ghx -o ./reports
hoptracer --version
```

## Docs

- [`SKILL.md`](SKILL.md) — skill definition and workflows
- [`references/hoptracer-cli.md`](references/hoptracer-cli.md) — full CLI reference
- [`references/agent-schema.json`](references/agent-schema.json) — JSON Schema for `--format agent`
- [`references/risk-scoring.md`](references/risk-scoring.md) — risk methodology
- [`references/output-formats.md`](references/output-formats.md) — format specs
