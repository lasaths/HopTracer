# Output Format Specifications

Detailed technical specifications for each output format.

## Text Format

### Structure

```
============================================================
HOPTRACER DIFF REPORT
============================================================
<header information>

STATISTICS
----------------------------------------
<statistics table>

RISK SUMMARY
----------------------------------------
<risk breakdown>

TOP RISK ITEMS
----------------------------------------
<top risks list>

CHANGED NODES
----------------------------------------
<node details>

DIAGNOSTICS
----------------------------------------
<diagnostic messages>

============================================================
```

### Character Encoding
- UTF-8
- Line endings: CRLF (Windows)/LF (Unix) - system dependent
- Use system console encoding for display

### Sections

#### Header
- 60-character equal sign divider
- Tool name and format type
- Files being compared
- Comparison timestamp (in verbose mode)

#### Statistics
- 40-character divider
- Key-value pairs for metrics
- Right-aligned numeric values
- Sorted by category (nodes, edges, risks)

#### Risk Summary
- 40-character divider
- Categorized risk counts
- Score ranges displayed

#### Top Risks
- 40-character divider
- Limited to 10 highest-risk items
- Format: `[score] status: name (id)`
- Compact mode: abbreviated node IDs

#### Changed Nodes
- Shows node count and total
- Format: `[STATUS] Name (ID)`
- In verbose mode: includes position, movement, properties, ports

#### Diagnostics
- Severity indicators: `[INFO]`, `[WARNING]`, `[ERROR]`
- Diagnostic codes with messages
- Limited to 25 messages to prevent overflow

## Markdown Format

### Structure

```markdown
# HopTracer Diff Report

<header section>

## 📊 Statistics
<table>
</table>

## 🎯 Risk Summary
- **Critical (≥80):** <count>
- **High (60-79):** <count>
- ...

## ⚠️ Top Risk Items
<emoji> **[<score>]** <status>: `<name>` (`<id>`)

## 🔧 Changed Nodes
<changes>

## 🔗 Changed Edges
<edges>

## 📋 Diagnostics
<diagnostics>
```

### Markdown Extensions
- Standard GitHub Flavored Markdown (GFM)
- Tables supported
- Emojis for visual indicators
- Code blocks with syntax highlighting

### Typography
- Headers: `#`, `##`, `###` levels
- Bold: `**text**`
- Code: `` `code` `` and ` ```code``` `
- Lists: `*` and `1.` formats
- Horizontal rules: `---`

### Escape Rules
- HTML entities for special characters: `&`, `<`, `>`, `"`, `'`
- Backslash escapes for reserved characters: `\`, `*`, `#`, `|`
- Code blocks avoid escaping issues

### Responsive Design
- Tables with responsive scrolling
- Code blocks with line wrapping
- Mobile-friendly emoji size

## JSON Format

### Root Structure

```json
{
  "generatedAt": "ISO8601 timestamp",
  "fileOld": "path/to/old.gh",
  "fileNew": "path/to/new.gh",
  "options": {
    "fileOld": "path/to/old.gh",
    "fileNew": "path/to/new.gh",
    "includeStatistics": true,
    "includeDiagnostics": true,
    "includeRiskSummary": true,
    "includeTopRisks": true,
    "includeChangedNodes": true,
    "includeChangedEdges": false,
    "includeNodeDetails": false,
    "maxNodesToShow": 20,
    "maxEdgesToShow": 10,
    "compactFormat": false
  },
  "summary": {
    "statistics": { /* ... */ },
    "riskSummary": { /* ... */ }
  },
  "topRisks": [ /* ... */ ],
  "changedNodes": [ /* ... */ ],
  "changedEdges": [ /* ... */ ],
  "diagnostics": [ /* ... */ ]
}
```

### Data Types

#### String Types
- **Standard strings**: UTF-8 encoded
- **Paths**: String with `fileOld`, `fileNew`, etc.
- **Identifiers**: Node IDs, component names, etc.
- **Enumerations**: "added", "removed", "modified", "same"

#### Numeric Types
- **Integers**: Node counts, risk scores, edge counts
- **Floats**: Coordinates (X, Y positions), sizes (W, H)
- All negative numbers allowed for Deltas (Dx, Dy)

#### Boolean Types
- All boolean values use lowercase: `true`, `false`

####NullOrMissing Values
- Objects may be `null` if not included
- Arrays are always present but may be empty
- Strings are never `null` (use empty string)

#### Timestamp Format
- ISO 8601: `"2026-06-29T08:39:09.5890079+00:00"`
- Always UTC timezone
- Millisecond precision maintained

### Array Handling
- **Always present**: Even when empty `[]`
- **Preserve order**: Maintain original sort order
- **No null elements**: Skip null values entirely

### Special Values
- **Node IDs**: UUID format string
- **Coordinates**: Double precision floating point
- **Risk scores**: Integer 0-100
- **Status codes**: Enumerated strings

### JSON Schema Validation

The output follows this schema structure:

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "required": ["generatedAt", "summary"],
  "properties": {
    "generatedAt": {"type": "string", "format": "date-time"},
    "fileOld": {"type": "string"},
    "fileNew": {"type": "string"},
    "summary": {
      "type": "object",
      "properties": {
        "statistics": {"type": "object"},
        "riskSummary": {"type": "object"}
      }
    }
  }
}
```

### Unicode and Encoding
- **Encoding**: UTF-8
- **Unicode**: Full Unicode support
- **Escaping**: Standard JSON escaping rules
- **Special chars**: Properly escaped in strings

## HTML Format

### Document Structure

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>HopTracer Diff Report</title>
    <style>/* embedded CSS */</style>
</head>
<body>
    <div class="container">
        <!-- sections -->
    </div>
</body>
</html>
```

### CSS Styling

#### Reset and Base Styles
- Font: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif
- Background: `#f5f7fa`
- Container: White background, rounded corners, shadow

#### Typography
- Headings: `#2c3e50` color, dark text
- Body text: `#555` color, readable contrast
- Inline code: Monospace, light background
- Pre blocks: Monospace, styled for code display

#### Component Styling

##### Tables
- Responsive width: 100% with max-width
- Borders: 1px solid borders
- Headers: `#34495e` background, white text
- Cells: Padding 8px, left-aligned text
- Striped: Alternating row colors optional

##### Risk Bars
- Flex container with proportional coloring
- Critical: `#e74c3c` (red)
- High: `#f39c12` (orange)
- Medium: `#f1c40f` (yellow)
- Low: `#27ae60` (green)

##### Node Cards
- Rounded corners, left border for status
- Status colors: Added=green, Removed=red, Modified=orange
- Hover effects for interactive elements
- Collapsible details for verbose information

##### Edge Rows
- Flex layout for_alignment
- Status-based left borders
- Padding and margin for readability
- Connector arrows visual design

##### Diagnostic Messages
- Severity-based backgrounds and borders
- Error: `#fadbd8` background, `#e74c3c` border
- Warning: `#fef9e7` background, `#f39c12` border
- Info: `#ebf5fb` background, `#3498db` border

#### Responsive Design
- Mobile breakpoint: 768px
- Tablet breakpoint: 1024px
- Desktop breakpoint: 1200px
- Scrollable tables for mobile
- Collapsible sections for small screens

#### Print Styles
- Hide interactive elements in print
- Expand all collapsible content
- Use black text on white background
- Optimize for A4 portrait orientation
- Remove shadow and extra spacing

### Interactive Elements

#### Collapsible Sections
- JavaScript not required (CSS-only)
- Maximum-height transitions
- Chevron rotation indicators
- Preserved state across interactions

#### Hover Effects
- Button hover states
- Card elevation changes
- Border darkening
- Color transitions

### Accessibility

#### Semantic HTML
- Proper heading hierarchy (h1-h3)
- Meaningful link text
- Alt text for images
- Proper table headers

#### ARIA Attributes
- `aria-expanded` for collapsible elements
- `aria-label` for interactive elements
- `role` attributes where appropriate
- Focus indicators for navigation

#### Keyboard Navigation
- Tab navigation through interactive elements
- Enter/Space for toggling
- Focus visible states
- Skip to main content link

### Browser Support
- Modern browsers (Chrome, Firefox, Safari, Edge)
- IE11+ with polyfills (if needed)
- Mobile browsers (iOS Safari, Chrome Mobile)
- Progressive enhancement principles

### Customization

#### CSS Variables
Define variables for easy theming:
```css
:root {
    --primary-color: #3498db;
    --danger-color: #e74c3c;
    --warning-color: #f39c12;
    --success-color: #27ae60;
    --background-color: #f5f7fa;
    --text-color: #2c3e50;
}
```

#### Logo and Branding
- Logo URL can be customized in header
- Company name substitution
- Custom footer content
- Theme color overrides

## Agent Format

AI-oriented JSON for LLM review workflows. Recommended invocation:

```bash
hoptracer compare old.ghx new.ghx --format agent -o diff-agent.json
```

### Design goals

- Resolved wire labels (`Src.O → Snk.I`) instead of raw GUIDs
- Text/script property deltas with `old` / `new` strings
- Opaque geometry/cache blobs listed separately (not inlined)
- Risk summary and diagnostics for triage
- Truncation metadata when node/edge/property lists are capped

### Root structure

```json
{
  "generatedAt": "2026-07-09T12:00:00+00:00",
  "fileOld": "compare-old.ghx",
  "fileNew": "compare-new.ghx",
  "summary": "1 added, 0 removed, 0 modified nodes.",
  "statistics": {},
  "riskSummary": {},
  "topRisks": [],
  "wireChanges": [],
  "propertyChanges": [],
  "opaqueChanges": [],
  "nodeChanges": [],
  "diagnostics": [],
  "totalChangedNodes": 1,
  "totalChangedEdges": 0,
  "truncatedNodes": false,
  "truncatedEdges": false
}
```

### Key fields

| Field | Purpose |
| ----- | ------- |
| `summary` | One-line human/LLM rollup |
| `wireChanges` | Changed connections with `wire` label and node GUIDs |
| `propertyChanges` | Text/script deltas; `nodeId` + `truncated` when capped |
| `opaqueChanges` | `ON_Data` / cluster hash changes without blob payload |
| `nodeChanges` | Per-node status, risk, reasons, 1-hop neighbors |
| `totalChangedNodes` / `totalChangedEdges` | Full counts before `--max-nodes` / `--max-edges` cap |
| `truncatedNodes` / `truncatedEdges` | `true` when output lists were capped |

### Defaults (agent mode)

- `--show-edges` enabled automatically
- `maxNodesToShow`: 100 (override with `--max-nodes`)
- `maxEdgesToShow`: 50 (override with `--max-edges`)
- Text/script property limit: 4000 characters (`truncated: true` when shorter)

### Schema

Machine-readable contract: [`agent-schema.json`](agent-schema.json)

### CLI errors (agent/json formats)

When `--format agent` or `--format json`, failures emit structured JSON on stderr:

```json
{"error":"file_not_found","message":"File not found: C:\\path\\old.ghx","path":"C:\\path\\old.ghx"}
```

Plain-text errors remain the default for `text`, `markdown`, and `html` formats.

## Format Selection Guide

### When to Use Each Format

#### Text Format
- Console output
- Terminal display
- Simple log files
- Basic human reading

#### Markdown Format
- Documentation generation
- GitHub/GitLab display
- Version control files
- Technical documentation

#### JSON Format
- Automation scripts
- CI/CD pipelines
- Data processing
- Third-party integration

#### HTML Format
- Stakeholder presentations
- Email reports
- Web dashboards
- Print documentation

#### Agent Format
- Automation and AI agent review
- LLM workflows with resolved wires and classified property changes
- CI gates combined with `--fail-on-risk`

### Conversion Between Formats

```bash
# Convert between formats
hoptracer compare old.gh new.gh --format markdown -o diff.md
hoptracer compare old.gh new.gh --format html -o diff.html
hoptracer compare old.gh new.gh --format json -o diff.json
hoptracer compare old.gh new.gh --format text -o diff.txt
```

All formats contain the same core data, just organized differently for their target use case.
