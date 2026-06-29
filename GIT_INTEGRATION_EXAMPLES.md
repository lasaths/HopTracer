# Git Integration - Practical Examples and Demonstrations

## How the Git Integration Works

The hoptracer tool's git command integrates with Git repositories to compare Grasshopper files against previous versions. Here's how it works step by step:

### Architecture Overview

```
User Command: hoptracer git current.gh [--commit <hash>]
    ↓
Git Initialization: Create GitWrapper instance
    ↓
Repository Check: Verify we're in a Git repo
    ↓
Commit Discovery: Find commits for the file
    ↓
Content Retrieval: Get file content from specified commit
    ↓
Diff Analysis: Compare old vs current versions
    ↓
Report Generation: Create formatted output
```

## Practical Example Scenario

### Scenario: You're Working on a Grasshopper Component

Let's say you're working on a file called `my_component.gh` and you want to see how it's changed since your last commit.

#### Step 1: Check What Commits Exist

```bash
# See the git history for your file
git log --follow --oneline my_component.gh

# Output might be:
# abc1234 Update component logic for new requirements
# def5678 Fix connection issue with cluster
# ghi9012 Initial component implementation
```

#### Step 2: Compare with Latest Commit

```bash
# Simple comparison with latest commit
hoptracer git my_component.gh

# This does:
# 1. Finds the latest commit where my_component.gh exists (abc1234)
# 2. Retrieves the file content from abc1234
# 3. Compares it with your current working directory version
# 4. Shows all changes made since abc1234
```

#### Step 3: Compare with Specific Previous Commit

```bash
# Compare with a specific commit hash
hoptracer git my_component.gh --commit def5678

# This shows changes since commit def5678, skipping the most recent abc1234
```

#### Step 4: Generate Different Formats

```bash
# Generate HTML report for presentation
hoptracer git my_component.gh --format html -o changes.html

# Generate Markdown for documentation
hoptracer git my_component.gh --format markdown -o CHANGES.md

# Generate JSON for automation
hoptracer git my_component.gh --format json -o changes.json
```

## Real-World Usage Examples

### Example 1: Development Workflow

```bash
# Typical development workflow:

# 1. Make changes to your Grasshopper file
# Edit my_component.gh...

# 2. See what changed since last commit
hoptracer git my_component.gh --compact

# 3. Get detailed view if needed
hoptracer git my_component.gh --verbose

# 4. Check for risky changes
hoptracer git my_component.gh --fail-on-risk

# 5. If everything looks good, commit
git add my_component.gh
git commit -m "Update component logic"

# 6. Compare with previous commit to verify
hoptracer git my_component.gh --commit HEAD~1
```

### Example 2: Code Review Process

```bash
# During code review or pull request:

# 1. Get the base commit for comparison
BASE_COMMIT=$(git merge-base HEAD main)

# 2. Generate comprehensive review report
hoptracer git my_component.gh --commit $BASE_COMMIT \
  --verbose \
  --show-edges \
  --max-nodes 50 \
  --format html \
  -o pr-review.html

# 3. Create executive summary for stakeholders
hoptracer git my_component.gh --commit $BASE_COMMIT \
  --format markdown \
  -o executive-summary.md

# 4. Check for critical changes that need approval
hoptracer git my_component.gh --commit $BASE_COMMIT --fail-on-risk
if [ $? -eq 1 ]; then
  echo "⚠️  Critical changes detected - requires review approval"
fi
```

### Example 3: Release Preparation

```bash
# Preparing for v2.0.0 release:

# 1. Compare with v1.0.0 release tag
hoptracer git my_component.gh --commit v1.0.0 \
  --verbose \
  --show-edges \
  --format html \
  -o v2.0.0-release-report.html

# 2. Generate changelog
hoptracer git my_component.gh --commit v1.0.0 \
  --format markdown \
  -o CHANGELOG-v2.0.0.md

# 3. Check for regressions
hoptracer git my_component.gh --commit v1.0.0 --fail-on-risk

# 4. Tag the release
git tag v2.0.0 -m "Release v2.0.0 - Component Updates"
```

### Example 4: Historical Analysis

```bash
# Tracking how a component evolved over time:

# Compare with last 5 commits
for commit_ref in HEAD HEAD~1 HEAD~2 HEAD~3 HEAD~4; do
  echo "Comparing with commit: $commit_ref"
  hoptracer git my_component.gh --commit $commit_ref --format compact
  echo "---"
done

# Generate JSON for each commit for analysis
for commit_ref in HEAD HEAD~1 HEAD~2 HEAD~3 HEAD~4; do
  hoptracer git my_component.gh --commit $commit_ref \
    --format json \
    -o history-$commit_ref.json
done

# Analyze risk trends over time
echo "Risk progression over 5 commits:"
for commit_ref in HEAD HEAD~1 HEAD~2 HEAD~3 HEAD~4; do
  hoptracer git my_component.gh --commit $commit_ref --format json | \
    jq '.summary.riskSummary'
done
```

## Git Integration Features

### 1. Automatic File Path Resolution

The tool handles complex Git scenarios:

```bash
# Works even if file was renamed or moved
hoptracer git my_component.gh

# The tool uses git log --follow to track file renames
# It resolves paths intelligently even across reorganization
```

### 2. Branch Comparison

```bash
# Compare current file with different branch version
hoptracer git my_component.gh --commit feature-branch

# Compare with main branch
hoptracer git my_component.gh --commit main

# Compare with specific commit from another branch
hoptracer git my_component.gh --commit origin/feature/new-stuff
```

### 3. Behind-the-Scenes Git Commands

The tool uses these git commands:

```bash
# Check if we're in a git repository
git rev-parse --is-inside-work-tree

# Find repository root
git rev-parse --show-toplevel

# Get commit history for a file
git log --follow -n 5 --pretty=format:%h|%an|%ar|%s -- file.gh

# Get file content at specific commit
git show abc1234:file.gh

# Check file size at commit
git cat-file -s abc1234:file.gh

# List tracked files
git ls-files
```

### 4. File Format Handling

```bash
# The tool handles both .gh and .ghx files

# .gh files (binary format) are auto-converted to .ghx
hoptracer git my_component.gh

# .ghx files (XML format) are parsed directly
hoptracer git my_component.ghx

# Conversion happens automatically when retrieving from Git
```

## Error Handling Examples

### Example: File Not in Git History

```bash
# If the file was never committed:
hoptracer git new-component.gh

# Output:
# ✗ ERROR: No commits found for file

# Solution: Commit the file first, then use hoptracer compare
git add new-component.gh
git commit -m "Add new component"
hoptracer git new-component.gh --commit HEAD~1

# Or use direct comparison:
hoptracer compare old-version.gh new-component.gh
```

### Example: Wrong Commit Hash

```bash
# If you specify wrong commit:
hoptracer git my_component.gh --commit wrong123

# Output:
# ✗ ERROR: Commit wrong123 not found in file history

# Solution: Use correct commit hash or use HEAD~N notation
hoptracer git my_component.gh --commit HEAD~1
```

### Example: Working Outside Git Repository

```bash
# If you're not in a git repository:
hoptracer git my_component.gh

# Output:
# ✗ ERROR: Not in a Git repository

# Solution: Run from within git repository or use direct comparison
cd /path/to/git/repo
hoptracer git my_component.gh
```

## Advanced Git Workflows

### Workflow 1: Feature Branch Development

```bash
# Create feature branch
git checkout -b feature/update-component

# Make changes to my_component.gh

# Compare with main branch daily
hoptracer git my_component.gh --commit main

# Before merging, do thorough comparison
hoptracer git my_component.gh --commit main \
  --verbose --show-edges \
  --format html \
  -o merge-review.html

# Merge if all looks good
git checkout main
git merge feature/update-component
```

### Workflow 2: Hotfix Branch

```bash
# Create hotfix from release
git checkout -b hotfix/fix-component v1.2.0

# Make fixes to my_component.gh

# Compare with fixed version
hoptracer git my_component.gh --commit v1.2.0

# Generate fix report
hoptracer git my_component.gh --commit v1.2.0 \
  --format markdown \
  -o HOTFIX-FIXES.md

# Tag the hotfix
git tag v1.2.1 -m "Hotfix: Component fixes"
```

### Workflow 3: Continuous Integration

```bash
# In CI pipeline (example GitHub Actions):

- name: Check Grasshopper Changes
  run: |
    # Get the base commit
    BASE_COMMIT=$(git merge-base HEAD $GITHUB_BASE_REF)
    
    # Generate comparison report
    hoptracer git my_component.gh --commit $BASE_COMMIT \
      --format json \
      -o diff.json
    
    # Check for critical changes
    hoptracer git my_component.gh --commit $BASE_COMMIT \
      --fail-on-risk
    
    # Store artifacts
    mkdir -p artifacts
    cp diff.json artifacts/
    hoptracer git my_component.gh --commit $BASE_COMMIT \
      --format html \
      -o artifacts/diff-report.html
```

### Workflow 4: Multi-File Comparison

```bash
# Compare multiple files at once
for file in component1.gh component2.gh component3.gh; do
  echo "=== Comparing $file ==="
  hoptracer git $file --format compact
  echo ""
done

# Or create a script to handle multiple files
#!/bin/bash
FILES="component1.gh component2.gh component3.gh"
COMMIT="HEAD~1"

for file in $FILES; do
  REPORT="diff-${file%.gh}.html"
  hoptracer git $file --commit $COMMIT --format html -o $REPORT
  echo "Generated $REPORT"
done
```

## Performance Tips

### 1. Use Compact Format for Large Files

```bash
# Faster processing and smaller output
hoptracer git large-component.gh --compact
```

### 2. Limit Content for Quick Checks

```bash
# Show only key changes initially
hoptracer git my-component.gh --max-nodes 10 --compact

# Then get full details if needed
hoptracer git my-component.gh --verbose
```

### 3. Use Specific Commits

```bash
# Faster than searching for commits
hoptracer git my-component.gh --commit abc1234
```

## Summary

The Git integration provides powerful capabilities:

- **Historical tracking** comparing with any previous commit
- **Automatic path resolution** handling renames and moves
- **Multi-format output** for different use cases
- **Risk assessment** for quality control
- **Automation support** with exit codes and JSON output
- **Branch comparison** for review workflows

The integration leverages Git's robust version control to give you accurate, reliable comparisons while handling edge cases automatically.
