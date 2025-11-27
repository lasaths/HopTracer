# Git Interface UI/UX Issues & Improvements

## Current Issues Identified

### 1. **Missing File Metadata Display**
- ❌ Diff viewer doesn't show which commit is being compared
- ❌ No display of "OLD file @ commit" vs "NEW file (current)"
- ❌ User has no context about what they're looking at

### 2. **Commit List UX Problems**
- ✅ FIXED: Now filters commits where file doesn't exist
- ❌ No visual indication that commits are filtered
- ❌ No empty state message if no commits found
- ❌ No loading state while fetching commits

### 3. **Git Modal Flow Issues**
- ❌ Three buttons (Use Current, Browse, Load Commits) is confusing
- ❌ Manual path input is awkward and error-prone
- ❌ No clear indication of what state the modal is in
- ❌ Status messages appear but are easy to miss

### 4. **Missing Visual Feedback**
- ❌ No indication that file was selected successfully
- ❌ No clear feedback when Git operations are happening
- ❌ Error messages appear as alerts (not integrated)
- ❌ No progress indicators

## Proposed Solutions

### Solution 1: Add File Metadata Header in Diff Viewer

**Location**: `diff_viewer.html`

Add a header banner showing:
```
Comparing: filename.gh @ abc1234 → filename.gh (current)
Commit: abc1234 - "Commit message here" by Author (Date)
```

**Implementation**:
- Extract metadata from `diffData.Meta`
- Display prominently at top of diff viewer
- Style with contrasting background

### Solution 2: Simplify Git Modal Workflow

**Remove complexity**:
- Remove manual path input field
- Remove "Use Current" button (redundant)
- Keep only: "Browse…" and "Load Commits" buttons
- Auto-load commits after Browse

**New Flow**:
1. User clicks "Browse for OLD file"
2. Native picker opens → file selected
3. Commits auto-load immediately
4. User selects commit → clicks "Load Selected"

### Solution 3: Better Status/Feedback

**Add visual states**:
- **Empty**: "No commits found for this file"
- **Loading**: Spinner + "Loading commits..."
- **Loaded**: "X commits found where file exists"
- **Error**: Prominent error message with retry button

**Implement**:
- Replace `gitStatus` text with styled status cards
- Add loading spinner during operations
- Show success/error toasts instead of alerts

### Solution 4: Improve Commit List Display

**Enhancements**:
- Show commit count: "Showing 8 commits (filtered 2)"
- Add tooltip explaining filtering
- Better visual hierarchy (larger message, smaller metadata)
- Highlight selected commit more prominently

## Priority Fixes

### HIGH PRIORITY
1. ✅ Display file metadata in diff viewer header
2. ✅ Simplify Git modal (remove manual input)
3. ✅ Auto-load commits after file selection
4. ✅ Better error messages with context

### MEDIUM PRIORITY
5. Add loading states/spinners
6. Styled status messages (not plain text)
7. Show filtered commit count
8. Better empty states

### LOW PRIORITY
9. Keyboard shortcuts (Enter to confirm)
10. Remember last used repo
11. Commit message search/filter
12. Diff statistics in sidebar

## Next Steps

Choose which fixes to implement first. I recommend:
1. Display file metadata (quick win, high impact)
2. Simplify Git modal workflow (better UX)
3. Add loading/empty states (polish)

Let me know which approach you'd like to take!
