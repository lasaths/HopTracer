# Grasshopper Diff Skill

An agent skill for comprehensive Grasshopper definition file comparison with multi-format reporting and risk analysis.

## Quick Start

### Install the CLI Tool

```bash
# From HopTracer repository root
dotnet publish ./Source/Tools/GhDiffTool/GhDiffTool.csproj -c Release -o ./bin/hoptracer

# Or install as global tool
dotnet pack ./Source/Tools/GhDiffTool/GhDiffTool.csproj
dotnet tool install --global --add-source ./bin HopTracer.Tools.GhDiffTool
```

### Basic Usage

```bash
# Compare two files
hoptracer compare old.gh new.gh

# Generate HTML report
hoptracer compare old.gh new.gh --format html -o report.html

# Compare with Git
hoptracer git current.gh --format markdown -o changelog.md
```

## Skill Structure

```
skills/gh-diff/
├── SKILL.md                    # Main skill definition (required)
├── README.md                   # This file (optional)
└── references/                 # Additional documentation (optional)
    ├── hoptracer-cli.md           # Complete CLI reference
    ├── risk-scoring.md         # Risk assessment methodology
    └── output-formats.md       # Format specifications
```

## What This Skill Does

This skill enables Claude to:

- **Compare Grasshopper files** between versions or with Git commits
- **Generate multiple report formats** (Text, Markdown, JSON, HTML)
- **Assess change risks** with automated scoring (Critical/High/Medium/Low)
- **Provide actionable insights** for change management and quality gates
- **Support automation workflows** with JSON output and exit codes

## Activation Triggers

The skill activates when Claude detects phrases like:

- "diff grasshopper files"
- "compare gh files" or "compare .gh files"
- "grasshopper file comparison"
- "ghx diff" or ".gh diff"
- "show changes in grasshopper"
- "compare grasshopper versions"
- "grasshopper version diff"
- "delta grasshopper definition"

## Key Features

### Multi-Format Output
- **Text**: Human-readable for console viewing
- **Markdown**: Documentation and GitHub-friendly
- **JSON**: Machine-readable for automation
- **HTML**: Styled reports for presentations

### Risk Assessment
- **Critical (≥80)**: Major structural changes
- **High (60-79)**: Significant modifications
- **Medium (30-59)**: Script/code changes
- **Low (1-29)**: Minor property changes

### Git Integration
- Compare with any commit in history
- Track changes over multiple versions
- Generate changelogs automatically

### Automation Support
- Risk-based exit codes for CI/CD
- JSON output for programmatic processing
- Fail-on-risk quality gates

## Usage Examples

### For Documentation
```bash
hoptracer compare v1.gh v2.gh --format markdown -o CHANGELOG.md
```

### For Presentations
```bash
hoptracer compare baseline.gh current.gh \
  --verbose --show-edges \
  --format html -o report.html
```

### For CI/CD
```bash
hoptracer compare baseline.gh current.gh \
  --format json -o diff.json \
  --fail-on-risk
```

### For Git Workflows
```bash
hoptracer git current.gh --commit HEAD~1 \
  --format markdown -o previous-changes.md
```

## Integration Options

### Pre-commit Hooks
```bash
#!/bin/bash
hoptracer git current.gh --fail-on-risk
if [ $? -eq 1 ]; then
    echo "❌ Critical changes detected"
    exit 1
fi
```

### GitHub Actions
```yaml
- name: Check Grasshopper Changes
  run: |
    hoptracer compare baseline.gh current.gh --fail-on-risk
```

### CI/CD Pipelines
```yaml
- name: Generate Diff Report
  run: |
    hoptracer compare old.gh new.gh --format json -o diff.json
    hoptracer compare old.gh new.gh --format html -o report.html
```

## File Support

- **.gh files**: Grasshopper binary format (auto-converted)
- **.ghx files**: Grasshopper XML format (direct parsing)

## Dependencies

- **hoptracer CLI tool**: The underlying comparison engine
- **GH_IO.dll**: For .gh file format conversion

## Documentation

- `SKILL.md` - Skill definition and usage guide
- `references/hoptracer-cli.md` - Complete CLI command reference
- `references/risk-scoring.md` - Risk assessment methodology details
- `references/output-formats.md` - Format specifications

## Troubleshooting

### Common Issues

1. **"hoptracer command not found"**
   - Ensure CLI tool is built and in PATH
   - Run `dotnet tool install --global HopTracer.Tools.GhDiffTool`

2. **"GH_IO dependency unavailable"**
   - Run `scripts/setup_dependencies.ps1` in HopTracer repo

3. **"Not a Git repository"**
   - Run from within Git repository for git comparisons
   - Use `hoptracer compare old.gh new.gh` instead

4. **Exit code 1 without error**
   - This indicates critical/high risks were detected
   - Use `--fail-on-risk` only when you want strict quality gates

## Contributions

This skill is part of the HopTracer project. For issues or feature requests, please refer to the main project repository.

## License

Same as HopTracer project.

## See Also

- [HopTracer Repository](https://github.com/yourorg/HopTracer)
- [Agent Skills Documentation](https://vercel.com/academy/agent-friendly-apis)
- [Skill Anatomy](https://vercel.com/academy/agent-friendly-apis/anatomy-of-a-skill)
