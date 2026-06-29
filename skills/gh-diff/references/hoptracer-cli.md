# hoptracer CLI Command Reference

Complete reference for the `hoptracer` command-line tool.

## Syntax

```bash
hoptracer <command> [options] [arguments]
```

## Commands

### compare

Compare two Grasshopper files and generate a diff report.

#### Syntax
```bash
hoptracer compare <oldFile> <newFile> [options]
```

#### Arguments
- `oldFile` (required): Path to the old Grasshopper file (.gh or .ghx)
- `newFile` (required): Path to the new Grasshopper file (.gh or .ghx)

#### Options
- `-f, --format <format>`: Output format (default: text)
  - `text`: Plain text output
  - `markdown` or `md`: Markdown format
  - `json`: JSON format
  - `html`: HTML format
- `-o, --output <file>`: Write output to file instead of stdout
- `--max-nodes <n>`: Maximum number of nodes to show (default: 20)
- `--max-edges <n>`: Maximum number of edges to show (default: 10)
- `--show-edges`: Show changed edges in output
- `--compact`: Use compact output format
- `-v, --verbose`: Show detailed output including port changes
- `--fail-on-risk`: Exit with code 1 if critical/high risks found
- `--no-stats`: Exclude statistics section from output
- `--no-diagnostics`: Exclude diagnostics section from output
- `--no-risk`: Exclude risk summary from output
- `--no-risks`: Exclude top risks list from output
- `--no-nodes`: Exclude changed nodes list from output

#### Exit Codes
- `0`: Success
- `1`: Error occurred or critical/high risks detected (when `--fail-on-risk` used)

#### Examples
```bash
# Basic comparison
hoptracer compare baseline.gh current.gh

# HTML output
hoptracer compare old.ghx new.ghx --format html -o report.html

# JSON with risk gating
hoptracer compare old.gh new.gh --format json -o diff.json --fail-on-risk

# Verbose with all details
hoptracer compare old.gh new.gh --verbose --show-edges --max-nodes 100
```

### git

Compare current Grasshopper file with a Git commit.

#### Syntax
```bash
hoptracer git <file> [options]
```

#### Arguments
- `file` (required): Path to the current Grasshopper file (.gh or .ghx)

#### Options
- `--commit <hash>`: Git commit hash to compare against (default: latest commit)
- `-f, --format <format>`: Output format (default: text)
  - `text`: Plain text output
  - `markdown` or `md`: Markdown format
  - `json`: JSON format
  - `html`: HTML format
- `-o, --output <file>`: Write output to file instead of stdout
- `--max-nodes <n>`: Maximum number of nodes to show (default: 20)
- `--max-edges <n>`: Maximum number of edges to show (default: 10)
- `--show-edges`: Show changed edges in output
- `--compact`: Use compact output format
- `-v, --verbose`: Show detailed output including port changes
- `--fail-on-risk`: Exit with code 1 if critical/high risks found
- `--no-stats`: Exclude statistics section from output
- `--no-diagnostics`: Exclude diagnostics section from output
- `--no-risk`: Exclude risk summary from output
- `--no-risks`: Exclude top risks list from output
- `--no-nodes`: Exclude changed nodes list from output

#### Examples
```bash
# Compare with latest commit
hoptracer git current.gh

# Compare with specific commit
hoptracer git current.gh --commit abc1234

# Markdown output for changelog
hoptracer git current.gh --format markdown -o changes.md

# Compare with specific commit and fail on risks
hoptracer git current.gh --commit HEAD~1 --fail-on-risk
```

### help

Display help information.

#### Syntax
```bash
hoptracer help [command]
```

#### Arguments
- `command` (optional): Command to get help for

#### Examples
```bash
# General help
hoptracer help

# Help for specific command
hoptracer help compare
hoptracer help git
```

## Common Options Summary

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--format` | `-f` | Output format | text |
| `--output` | `-o` | Output file path | stdout |
| `--max-nodes` | | Max nodes to show | 20 |
| `--max-edges` | | Max edges to show | 10 |
| `--show-edges` | | Show edge changes | false |
| `--compact` | | Compact format | false |
| `--verbose` | `-v` | Detailed output | false |
| `--fail-on-risk` | | Fail on critical/high risks | false |
| `--no-stats` | | Exclude statistics | false |
| `--no-diagnostics` | | Exclude diagnostics | false |
| `--no-risk` | | Exclude risk summary | false |
| `--no-risks` | | Exclude top risks | false |
| `--no-nodes` | | Exclude changed nodes | false |

## Output Formats

### Text
Human-readable plain text with sections and formatting.

### Markdown
Markdown format suitable for documentation, GitHub, and other markdown processors.

### JSON
Machine-readable JSON format with structured data for automation.

### HTML
Styled HTML report with CSS for browser viewing and printing.

## File Support

- **.gh files**: Grasshopper binary format (auto-converted to .ghx)
- **.ghx files**: Grasshopper XML format (direct parsing)

## Environment Variables

No environment variables are currently supported.

## Configuration Files

No configuration files are currently supported.

## Troubleshooting

### Exit code 1 without error message
This usually means critical or high risks were detected when using `--fail-on-risk`. Check the output for risk information.

### Slow performance on large files
Use `--compact` or reduce `--max-nodes` to improve performance.

### Missing GH_IO dependency
Run `scripts/setup_dependencies.ps1` to install the required dependency.

### Git comparison errors
Ensure you're in a Git repository and the file has commit history. Use `git log --follow <file>` to verify.

## Version Information

```bash
hoptracer --version
```
