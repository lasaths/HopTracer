# Grasshopper Diff Skill

Agent skill for comparing Grasshopper definition files (`.gh` / `.ghx`) with multi-format reports and risk scoring.

## Install

- **Microsoft Store**: `hoptracer` is on PATH after install.
- **Portable**: `Release/HopTracer_Portable/tools/hoptracer/hoptracer.exe`
- **From source**: `dotnet publish ./Source/Tools/GhDiffTool/GhDiffTool.csproj -c Release -o ./bin/hoptracer`

For `.gh` files: `scripts/setup_dependencies.ps1` (installs `GH_IO.dll`).

## Usage

```bash
hoptracer compare old.ghx new.ghx
hoptracer compare old.gh new.gh --format agent -o diff-agent.json
hoptracer compare old.gh new.gh --format html -o report.html
hoptracer git current.gh --commit HEAD~1 --format markdown -o changes.md
hoptracer git current.gh --fail-on-risk
hoptracer --version
```

## Docs

- [`SKILL.md`](SKILL.md) — skill definition and workflows
- [`references/hoptracer-cli.md`](references/hoptracer-cli.md) — full CLI reference
- [`references/agent-schema.json`](references/agent-schema.json) — JSON Schema for `--format agent`
- [`references/risk-scoring.md`](references/risk-scoring.md) — risk methodology
- [`references/output-formats.md`](references/output-formats.md) — format specs
