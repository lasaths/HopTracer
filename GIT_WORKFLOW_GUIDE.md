# Git Integration Guide - How the Tool Works with Previous Git Commits

## Overview

The hoptracer tool integrates seamlessly with Git to compare Grasshopper files against their previous versions. It uses the GitWrapper service to interact with your Git repository and retrieve file content from any commit.

## How It Works

### Architecture Flow

```
User Command → GitWrapper → Git Commands → File Content Retrieval → Diff Analysis → Report Generation
```

### Step-by-Step Process

#### 1. Command Parsing
```bash
hoptracer git current.gh [--commit <hash>]
```

#### 2. Repository Validation
```csharp
var gitWrapper = new GitWrapper();
if (!gitWrapper.IsGitRepo()) {
    // Error: Not in a Git repository
}
```

#### 3. Commit Discovery
```csharp
// Gets recent commits for the file
var commits = gitWrapper.GetCommits(filePath, limit: 5);

// Or uses specific commit if provided
var commitHash = opts.Commit ?? commits[0].Hash;
```

#### 4. File Content Retrieval
```csharp
// Retrieves file content from Git at specific commit
var oldContent = gitWrapper.GetFileContentAtCommit(commitInfo.Hash, commitInfo.FilePath);

// Creates temporary file for the old version
var oldTempPath = Path.Combine(Path.GetTempPath(), $"hoptracer_old_{Guid.NewGuid():N}.ghx");
await File.WriteAllBytesAsync(oldTempPath, oldContent);
```

#### 5. Diff Analysis
```csharp
// Parse both versions
var graphOld = parser.Parse(oldTempPath);
var graphNew = parser.Parse(currentPath);

// Compute differences
var diff = differ.DiffDetailed(graphOld, graphNew);
```

#### 6. Report Generation
```csharp
// Generate report in requested format
var output = outputGenerator.GenerateTextDiff(diff, options);
```

## Git Commands Used Internally

### 1. Repository Check
```bash
git rev-parse --is-inside-work-tree
```

### 2. Commit Discovery
```bash
# Get recent commits with file details
git log --follow -n 5 --pretty=format:%h|%an|%ar|%s -- <file_path>
```

### 3. File Size Verification
```bash
# Check if file exists and get size at commit
git cat-file -s <commit_hash>:<file_path>
```

### 4. File Content Retrieval
```bash
# Get file content at specific commit
git show <commit_hash>:<file_path>
```

### 5. Repository Root Discovery
```bash
# Find repository root
git rev-parse --show-toplevel
```

## Practical Usage Examples

### Example 1: Compare with Latest Commit
```bash
# Compare current version with the most recent commit
hoptracer git current.gh

# Output explanation:
# - Tool finds the latest commit where the file exists
# - Retrieves file content from that commit
# - Compares current working directory version with commit version
# - Shows all changes made since last commit
```

### Example 2: Compare with Specific Commit
```bash
# Compare with a specific commit hash
hoptracer git current.gh --commit abc1234

# Process:
# 1. Searches for commit starting with "abc1234"
# 2. Verifies the file exists at that commit
# 3. Retrieves file content from commit abc1234
# 4. Compares with current file
# 5. Generates diff report
```

### Example 3: Compare with Previous Commits
```bash
# Compare with HEAD~1 (previous commit)
hoptracer git current.gh --commit HEAD~1

# Compare with HEAD~2 (two commits ago)
hoptracer git current.gh --commit HEAD~2

# Compare with HEAD~5 (five commits ago)
hoptracer git current.gh --commit HEAD~5
```

### Example 4: Generate Markdown Changelog
```bash
# Generate changelog comparing with last commit
hoptracer git current.gh --format markdown -o CHANGELOG.md
```

### Example 5: HTML Report for Stakeholder Review
```bash
# Create detailed HTML report comparing with previous version
hoptracer git current.gh --commit HEAD~1 \
  --verbose \
  --show-edges \
  --format html \
  -o review-report.html
```

## Advanced Git Workflows

### Workflow 1: Pre-commit Validation
```bash
#!/bin/bash
# Validate changes against previous commit

echo "Checking Grasshopper changes..."
hoptracer git current.gh --fail-on-risk

if [ $? -eq 1 ]; then
    echo "❌ Critical changes detected. Please review before committing."
    echo "Run: hoptracer git current.gh --verbose for details"
    exit 1
fi

echo "✅ Changes look safe. Proceeding with commit..."
```

### Workflow 2: Multiple Commit Comparison
```bash
# Compare current file with multiple previous versions

for commit_ref in HEAD HEAD~1 HEAD~2 HEAD~3; do
    echo "Comparing with $commit_ref..."
    hoptracer git current.gh --commit $commit_ref \
      --format markdown \
      -o changes-$commit_ref.md
done
```

### Workflow 3: Release Preparation
```bash
# Generate comprehensive report for release

hoptracer git current.gh --commit v1.0.0 \
  --verbose \
  --show-edges \
  --max-nodes 100 \
  --format html \
  -o v2.0.0-release-report.html
```

### Workflow 4: Branch Comparison
```bash
# Compare against a different branch's version

git checkout feature-branch
hoptracer ghcompare git mycomponent.gh --commit main

# This works because the tool resolves file paths
# across branches and handles renames automatically
```

## Error Handling and Troubleshooting

### Error 1: Not in a Git Repository
```
✗ ERROR: Not in a Git repository
```
**Solution**: Run the command from within a Git repository directory.

### Error 2: No Commits Found
```
✗ ERROR: No commits found for file
```
**Solution**: The file has never been committed. Use `hoptracer compare old.gh new.gh` instead.

### Error 3: Commit Not Found
```
✗ ERROR: Commit abc1234 not found in file history
```
**Solution**: The commit doesn't exist or the file wasn't present at that commit. Try using the full commit hash or a different commit.

### Error 4: File Doesn't Exist at Commit
```
✗ ERROR: File 'mycomponent.gh' does not exist in commit abc1234
```
**Solution**: The file was added after this commit. Use a more recent commit.

## File Path Resolution

### How the Tool Finds Files

The tool uses intelligent path resolution:

```csharp
// Step 1: Get repository-relative path
var relPath = ResolveTrackedPath(filePath);

// Step 2: Try exact match first
var exactMatch = trackedPaths.FirstOrDefault(p => 
    string.Equals(p, relPath, StringComparison.OrdinalIgnoreCase));

// Step 3: Fall back to filename matching and path suffix analysis
var bestMatch = trackedPaths
    .Where(p => string.Equals(Path.GetFileName(p), fileName, 
        StringComparison.OrdinalIgnoreCase))
    .Select(p => new { Path = p, Score = CalculateSuffixScore(relPath, p) })
    .Where(x => x.Score > 0)
    .OrderByDescending(x => x.Score)
    .FirstOrDefault();
```

### Handles:
- **File renames**: Uses `git log --follow` to track file renames
- **Directory moves**: Resolves paths using suffix analysis
- **Case differences**: Case-insensitive path matching
- **Mixed relative/absolute paths**: Normalizes all paths

## Commit Information Retrieved

The tool retrieves comprehensive commit information:

```csharp
public class CommitInfo
{
    public string Hash { get; set; }        // Short commit hash
    public string Author { get; set; }      // Commit author
    public string Date { get; set; }        // Relative date (e.g., "2 days ago")
    public string Message { get; set; }     // Commit message
    public string FilePath { get; set; }    // Repository-relative path
    public long FileSizeBytes { get; set; } // File size at commit
}
```

## Performance Considerations

### Memory Usage
- Only retrieves file content when needed
- Uses temporary files that are cleaned up automatically
- Limits commit history queries (default: last 5 commits)

### Speed
- Git commands are executed parallel where possible
- File parsing is optimized for large files
- Streaming output for large diff results

### Caching
- Git command results are not cached (fresh results always)
- File content is retrieved directly from Git
- Temporary files are cleaned up immediately

## Best Practices

### 1. Regular Comparisons
```bash
# Check changes regularly during development
hoptracer git current.gh --compact
```

### 2. Pre-commit Validation
```bash
# Use as pre-commit hook
hoptracer git current.gh --fail-on-risk
```

### 3. Documentation
```bash
# Generate release notes
hoptracer git current.gh --commit v1.0.0 --format markdown -o RELEASE_NOTES.md
```

### 4. Risk Monitoring
```bash
# Track risk trends over time
for commit in HEAD HEAD~1 HEAD~2; do
  hoptracer git current.gh --commit $commit --format json -o risk-$commit.json
done
```

### 5. Branch Comparison
```bash
# Compare branches
git checkout main
hoptracer git mycomponent.gh --commit feature-branch
```

## Integration with Git Workflows

### Feature Branch Development
```bash
# When working on a feature branch:
git checkout -b feature/new-component

# Make changes to mycomponent.gh

# Compare with main branch
hoptracer git mycomponent.gh --commit main

# Generate review documentation
hoptracer git mycomponent.gh --commit main --format html -o review.html
```

### Code Review Process
```bash
# During pull request review:

# 1. Get the base commit
BASE_COMMIT=$(git merge-base HEAD main)

# 2. Generate comprehensive diff
hoptracer git mycomponent.gh --commit $BASE_COMMIT \
  --verbose --show-edges \
  --format html -o pr-review.html

# 3. Check for critical risks
hoptracer git mycomponent.gh --commit $BASE_COMMIT --fail-on-risk
```

### Release Management
```bash
# When preparing a release:

# 1. Tag the release
git tag v2.0.0 -m "Release v2.0.0"

# 2. Generate release report
hoptracer git current.gh --commit v1.0.0 \
  --verbose --show-edges \
  --format html -o v2.0.0-release.html

# 3. Create changelog
hoptracer git current.gh --commit v1.0.0 \
  --format markdown -o CHANGELOG-v2.0.0.md
```

## Limitations and Caveats

### 1. File Must Be Tracked
- The tool can only compare files that are in Git history
- New files must be committed first
- Use `hoptracer compare old.gh new.gh` for untracked files

### 2. Large Files
- Very large Grasshopper files may take longer to process
- Consider using `--compact` format for faster output
- Use `--max-nodes` to limit content

### 3. Binary File Changes
- The tool works best with .ghx files
- .gh files are auto-converted to .ghx for processing
- Conversion may fail for corrupted files

### 4. Repository Size
- Very large repositories may have slower performance
- Consider using specific commits rather than searching
- Cache results in automation scenarios

## Example Real-World Scenarios

### Scenario 1: Daily Development Check
```bash
# Developer wants to check today's progress
hoptracer git current.gh --format compact

# If interesting changes found, get detailed report
hoptracer git current.gh --verbose --show-edges
```

### Scenario 2: Team Code Review
```bash
# Generate review materials for the team
hoptracer git current.gh --branch main \
  --verbose --show-edges \
  --format html -o team-review.html

# Create executive summary
hoptracer git current.gh --branch main \
  --format markdown -o executive-summary.md
```

### Scenario 3: Continuous Integration
```bash
# In CI pipeline
- name: Grasshopper Quality Check
  run: |
    hoptracer git current.gh --fail-on-risk
    hoptracer git current.gh --format json -o diff.json
```

### Scenario 4: Historical Analysis
```bash
# Analyze evolution of a component over time
for commit_ref in HEAD HEAD~1 HEAD~2 HEAD~3 HEAD~4; do
  hoptracer git current.gh --commit $commit_ref \
    --format json -o history-$commit_ref.json
done
```

## Summary

The Git integration provides a powerful way to:

- **Track changes** over time
- **Generate documentation** automatically
- **Manage risk** with automated scoring
- **Validate changes** before committing
- **Create reports** for stakeholders
- **Automate workflows** with exit codes

The tool leverages Git's robust version control to provide reliable, accurate comparisons while handling edge cases like file renames, path changes, and repository restructuring automatically.
