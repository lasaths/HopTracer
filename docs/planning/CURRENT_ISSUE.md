# Current Issue: Git Diff Metadata Not Displaying

## Problem Summary
After implementing Git integration for HopTracer, the file metadata header is not displaying in the diff viewer when comparing Git commits, despite the backend correctly sending the metadata.

## Expected Behavior
When using Git History to compare a commit against the current file:
1. User clicks "Browse for OLD file" → selects file
2. User clicks "📜 View Git History" → commits auto-load
3. User selects a commit → clicks "Load Selected"
4. Diff viewer opens with a **prominent header banner** showing:
   ```
   Comparing: filename.gh @ abc1234 → filename.gh (current)
   ```

## Current Behavior
- The diff viewer opens successfully
- The comparison works correctly
- **BUT: The file metadata header is not visible/showing**

## What's Already Working
- ✅ Backend sends metadata: `FileOld`, `FileNew`, `CommitHash` in `DiffResponse.Meta`
- ✅ GitController populates: 
  ```csharp
  FileOld = $"{Path.GetFileName(path)} @ {hash_old.Substring(0, 7)}",
  FileNew = $"{Path.GetFileName(path)} (current)",
  CommitHash = hash_old
  ```
- ✅ HTML has the header element with proper styling
- ✅ JavaScript code to populate the header exists in `populateMetadata()`

## Technical Details

### Backend (Working)
**File**: `GitController.cs` → `ViewDiff` method
```csharp
Meta = new DiffMeta {
    FileOld = $"{Path.GetFileName(path)} @ {hash_old.Substring(0, 7)}",
    FileNew = $"{Path.GetFileName(path)} (current)",
    CommitHash = hash_old
}
```

### Frontend HTML (Added)
**File**: `diff_viewer.html`
```html
<div id="file-metadata-header">
    <div class="metadata-row">
        <div class="metadata-item">
            <span class="metadata-label">Comparing:</span>
            <span class="metadata-value" id="meta-file-old">Loading...</span>
            <span class="metadata-arrow">→</span>
            <span class="metadata-value" id="meta-file-new">Loading...</span>
        </div>
    </div>
</div>
```

### Frontend JavaScript (Added)
**File**: `diff_viewer.html` → `populateMetadata()` function
```javascript
// Populate file metadata header
if (GRAPH_DATA.meta) {
    const metaHeader = document.getElementById('file-metadata-header');
    const metaFileOld = document.getElementById('meta-file-old');
    const metaFileNew = document.getElementById('meta-file-new');

    if (GRAPH_DATA.meta.fileOld && GRAPH_DATA.meta.fileNew) {
        metaFileOld.textContent = GRAPH_DATA.meta.fileOld;
        metaFileNew.textContent = GRAPH_DATA.meta.fileNew;
        metaHeader.style.display = 'block'; // Show the header
    }
}
```

## Possible Issues to Investigate

1. **JavaScript not executing?**
   - Is `populateMetadata()` being called?
   - Check browser console for errors

2. **Data not reaching frontend?**
   - Verify `GRAPH_DATA.meta` contains the new properties
   - Check JSON serialization (DiffMeta has properties: `FileOld`, `FileNew`, `CommitHash`)

3. **CSS issue?**
   - Header has `display: none` by default
   - JavaScript should set `display: block` when metadata exists
   - Could be z-index or positioning issue?

4. **JSON serialization issue?**
   - `DiffMeta` properties might not be serialized (need JsonPropertyName attributes?)
   - Check if `AppJsonContext` includes the new properties

## Files Modified
1. `Source/HopTracer.Web/Controllers/GitController.cs` - Added metadata population
2. `Source/HopTracer.Web/Models/DiffResponse.cs` - Added FileOld, FileNew, CommitHash to DiffMeta
3. `Source/HopTracer.Web/wwwroot/diff_viewer.html` - Added header HTML, CSS, and JavaScript

## Testing Steps
1. Start HopTracer
2. Click "Browse for OLD file" → select a Git-tracked .gh/.ghx file
3. Click "📜 View Git History"
4. Select any commit
5. Click "Load Selected"
6. **Check**: Does the header banner appear at the top showing file comparison info?

## Request for Help
Need to debug why the metadata header is not displaying. The backend is sending the data correctly, the HTML/CSS is in place, and the JavaScript exists - but something is preventing the header from showing. 

**What's the missing piece?**
