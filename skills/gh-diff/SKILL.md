---
name: gh-diff
description: |
  Compare Grasshopper definition files (.gh/.ghx) and generate detailed diff reports. Key trigger phrases: "diff grasshopper files", "compare gh files", "grasshopper file comparison", "ghx diff", ".gh diff", "show changes in grasshopper", "compare grasshopper versions", "grasshopper version diff", "delta grasshopper definition". Use when user wants to compare two Grasshopper definitions, see changes between versions, compare with git commits, or analyze change impact and risks.
---

# Grasshopper Diff Skill

This skill enables comparison of Grasshopper definition files with comprehensive change analysis, risk assessment, and multi-format reporting.

## Prerequisites

The `hoptracer` CLI tool must be available in your PATH. It provides the underlying diff computation and formatting capabilities.

### Installing hoptracer CLI

From the HopTracer repository root:

```bash
dotnet publish ./Source/Tools/GhDiffTool/GhDiffTool.csproj -c Release -o ./bin/hoptracer
```

Or install as a global .NET tool:

```bash
dotnet pack ./Source/Tools/GhDiffTool/GhDiffTool.csproj
dotnet tool install --global --add-source ./bin HopTracer.Tools.GhDiffTool
```

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
✗ ERROR: Old file not found: old.gh
```
**Solution**: Verify the file path and extension.

### Git Repository Error
```
✗ ERROR: Not in a Git repository
```
**Solution**: Run from within a Git repository for git comparisons.

### Missing GH_IO Dependency
```
✗ ERROR: GH_IO dependency unavailable
```
**Solution**: Run `scripts/setup_dependencies.ps1` to install dependencies.

### Commit Not Found
```
✗ ERROR: Commit abc1234 not found in file history
```
**Solution**: Verify the commit hash exists in the file's git history, or use `ghtdiff git <file>` without commit to compare against the latest version.

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
- `references/risk-scoring.md` - Risk assessment methodology
- `references/output-formats.md` - Detailed format specifications
