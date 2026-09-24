---
name: hoptracer
description: |
  Compare Grasshopper definition files (.gh/.ghx) with the hoptracer CLI — diff reports, git history, baselines, forensic export, and agent-oriented JSON. Key trigger phrases: "diff grasshopper files", "compare gh files", "hoptracer", "ghx diff", ".gh diff", "grasshopper version diff", "forensic report", "baseline compare". Use when comparing Grasshopper definitions, running CI diff gates, or analyzing change risk.
---

# HopTracer CLI Skill

This skill enables comparison of Grasshopper definition files with comprehensive change analysis, risk assessment, and multi-format reporting.

## Install skill (agents)

```bash
npx skills add lasaths/HopTracer@hoptracer -g -y
```

## Prerequisites

Download, unzip, and run `hoptracer.exe`. Keep the unzipped folder together:

https://github.com/lasaths/HopTracer/releases/download/v1.3.1/hoptracer-win-x64.zip

- **WinGet**: `winget install lasaths.HopTracer.CLI` after the official manifest pull request is merged. The Store listing can be older than this zip.
- **Microsoft Store**: `hoptracer` is on PATH after the desktop app install.
- **From source**: `dotnet publish ./Source/Tools/GhDiffTool/GhDiffTool.csproj -c Release -o ./bin/hoptracer`

For `.gh` file support, `GH_IO.dll` must be present (Rhino install or `scripts/setup_dependencies.ps1` before building).

## Core Commands

### Basic File Comparison

Use when comparing two Grasshopper definition files:

```bash
hoptracer compare <oldFile> <newFile>
```

**Example:**
```bash
hoptracer compare baseline.gh current.gh
```

### Git-Based Comparison

Use when comparing a file with its previous version in Git history:

```bash
hoptracer git <currentFile>
hoptracer git <currentFile> --commit <hash>
```

### Environment Check

```bash
hoptracer doctor
```

### Baselines

Save, list, and compare against known-good snapshots (shared with the desktop app at `%LOCALAPPDATA%\\HopTracer\\baselines`):

```bash
hoptracer baseline save current.gh --name release-1.0
hoptracer baseline list
hoptracer baseline compare current.gh --name release-1.0 --fail-on-risk
```

### Forensic Report

Export signed JSON + HTML forensic artifacts:

```bash
hoptracer report old.ghx new.ghx -o ./reports
hoptracer report old.ghx new.ghx -o ./reports --baseline release-1.0
```

**Examples:**
```bash
hoptracer git current.gh
hoptracer git current.gh --commit abc1234
```

## Output Formats

The tool supports multiple output formats for different use cases:

### Text Format (default)

Human-readable output for console viewing:

```bash
hoptracer compare old.gh new.gh
```

### Markdown Format

Formatted for documentation, changelogs, or GitHub:

```bash
hoptracer compare old.gh new.gh --format markdown -o CHANGELOG.md
```

### JSON Format

Machine-readable for automation and CI/CD integration:

```bash
hoptracer compare old.gh new.gh --format json -o diff.json
```

### HTML Format

Styled reports for presentations and stakeholder communication:

```bash
hoptracer compare old.gh new.gh --format html -o report.html
```

### Agent Format (recommended for AI)

Compact JSON with resolved wire labels, classified property diffs, and a template summary. Omits opaque geometry blobs.

```bash
hoptracer compare old.gh new.gh --format agent -o diff-agent.json
hoptracer git current.gh --commit HEAD~1 --format agent
```

Key fields: `summary`, `wireChanges`, `propertyChanges`, `opaqueChanges`, `nodeChanges`, `diagnostics`.

## Common Options

### Verbosity and Detail

```bash
--verbose           # Show detailed information including port changes
--compact           # Use abbreviated format for quick overview
--show-edges        # Include edge (connection) changes in output
```

### Output Control

```bash
-o <file>           # Write output to file instead of stdout
--max-nodes <n>     # Maximum number of nodes to show (default: 20)
--max-edges <n>     # Maximum number of edges to show (default: 10)
```

### Content Filtering

```bash
--no-stats          # Exclude statistics section
--no-diagnostics    # Exclude diagnostics section  
--no-risk           # Exclude risk summary
--no-risks          # Exclude top risks list
--no-nodes          # Exclude changed nodes list
```

### Automation

```bash
--fail-on-risk      # Exit with code 1 if critical/high risks found
```

## Risk Assessment

The tool automatically scores changes on a 0-100 scale:

| Level | Score | Meaning |
|-------|-------|---------|
| 🔴 Critical | ≥80 | Removed components or major structural changes |
| 🟠 High | 60-79 | Added components or significant modifications |
| 🟡 Medium | 30-59 | Script/code changes or cluster modifications |
| 🟢 Low | 1-29 | Minor property changes or movements |

## Workflow Integration

### Pre-commit Checks

```bash
hoptracer git current.gh --fail-on-risk
# Exits with 1 if critical/high risks found, preventing commits
```

### CI/CD Integration

```yaml
# GitHub Actions example
- name: Check Grasshopper Changes
  run: |
    hoptracer compare baseline.gh current.gh --format json -o diff.json
    hoptracer compare baseline.gh current.gh --fail-on-risk
```

### Documentation Generation

```bash
# Generate markdown changelog
hoptracer compare v1.gh v2.gh --format markdown -o CHANGELOG.md

# Generate HTML report for stakeholders
hoptracer compare baseline.gh current.gh --format html -o report.html
```

## File Type Conversion

The tool automatically handles both Grasshopper file formats:

- **.gh files** (binary): Automatically converted to .ghx format
- **.ghx files** (XML): Directly parsed without conversion

Conversion requires `GH_IO.dll` dependency - see troubleshooting section.

## Error Handling

### File Not Found
```
File not found: old.gh
```
With `--format agent` or `--format json`, stderr is structured JSON:
```json
{"error":"file_not_found","message":"File not found: old.gh","path":"..."}
```
**Solution**: Verify the file path and extension.

### Git Repository Error
```
Not a git repository: /path/to/dir
```
Structured (`agent`/`json`): `{"error":"not_git_repository","message":"..."}`

**Solution**: Run from within a Git repository for git comparisons.

### Missing GH_IO Dependency
```
Error: GH_IO dependency unavailable
```
Structured (`agent`/`json`): `{"error":"error","message":"..."}`

**Solution**: Run `scripts/setup_dependencies.ps1` to install dependencies.

### Commit Not Found
```
Error: Commit abc1234 not found in file history
```
**Solution**: Verify the commit hash exists in the file's git history, or use `hoptracer git <file>` without `--commit` to compare against the latest version.

## Performance Considerations

- Use `--compact` for large files to reduce output size
- Limit `--max-nodes` for faster processing of complex definitions
- Use JSON format for programmatic processing (less overhead than HTML)
- Cache baseline files for repeated comparisons

## Best Practices

1. **Start with text format** to understand the scope of changes
2. **Use markdown for documentation** that needs to be version-controlled
3. **Use HTML for presentations** and stakeholder communication
4. **Use JSON for automation** and CI/CD integration
5. **Always review critical/high risks** before merging changes
6. **Use git comparisons** during development to track progress
7. **Generate multiple formats** when sharing with different audiences

## Advanced Examples

### Comprehensive Analysis
```bash
hoptracer compare old.gh new.gh \
  --verbose \
  --show-edges \
  --max-nodes 50 \
  --max-edges 20 \
  --format html \
  -o comprehensive-report.html
```

### CI/CD Quality Gate
```bash
# In CI pipeline, fail if critical changes detected
hoptracer compare baseline.gh current.gh \
  --format json \
  -o diff.json \
  --fail-on-risk
EXIT_CODE=$?
if [ $EXIT_CODE -eq 1 ]; then
  echo "❌ Critical changes detected in Grasshopper definition"
  exit 1
fi
```

### Multi-Format Export
```bash
# Generate all formats from a single comparison
for format in text markdown json html; do
  hoptracer compare old.gh new.gh \
    --format $format \
    -o diff.$format
done
```

### Git History Analysis
```bash
# Compare with multiple commits to understand progression
hoptracer git current.gh --commit HEAD~1 --format markdown -o HEAD-1.md
hoptracer git current.gh --commit HEAD~2 --format markdown -o HEAD-2.md
hoptracer git current.gh --commit HEAD~3 --format markdown -o HEAD-3.md
```

## See Also

- `references/hoptracer-cli.md` - Complete CLI command reference
- `references/agent-schema.json` - JSON Schema for `--format agent` output
- `references/risk-scoring.md` - Risk assessment methodology
- `references/output-formats.md` - Detailed format specifications
