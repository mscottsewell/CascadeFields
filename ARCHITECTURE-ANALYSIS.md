# Architecture Analysis: Why Settings/Configuration Changes Are Disruptive

## Executive Summary

The CascadeFields Configurator app has **5 fundamental architectural problems** that cause small changes to create cascading failures. These issues stem from **conflicting design assumptions** that evolved over time without refactoring.

---

## Problem 1: Multiple Sources of Truth for Configuration

**The Core Issue:**
Configuration data exists in 5 different places simultaneously:

```
1. SessionSettings.ConfigurationJson (string) 
   └─ Source of truth on disk, not in memory
   
2. UI Controls (Combo boxes, Tabs, DataGrids)
   └─ "Live" state, event-driven updates
   
3. In-Memory Dictionaries
   ├─ _filterCriteriaPerTab[tabKey] = List<SavedFilterCriteria>
   ├─ _additionalChildMappings[tabKey] = BindingList<MappingRow>
   ├─ _childRelationships[tabKey] = RelationshipItem
   └─ _selectedRelationship (single relationship)
   
4. filterControl (Single Shared UI Control)
   └─ Represents ONE tab at a time, but manages ALL tabs' data
   
5. Dataverse SDK Message Processing Steps
   └─ Published configurations (source of truth in production)
```

**Why This Breaks:**
- When user modifies a tab, which source gets updated?
- When loading, which source is authoritative?
- Desynchronization happens silently (filters lost, dropdowns empty)
- Example: `UpdateJsonPreview()` reads from `_filterCriteriaPerTab`, but if filterControl wasn't synced first, it's stale data

**Symptom Examples:**
- Filters appear in JSON but not in UI (filterControl not loaded)
- Destination fields appear in JSON but dropdowns empty (per-tab attributes not loaded)
- Second tab loses filters during restoration (filterControl only shows tab 0)

---

## Problem 2: Bidirectional Synchronization with No Clear Owner

**The Pattern:**
```
User modifies filterControl 
  → Event fires
    → SaveCurrentTabFilters() saves to _filterCriteriaPerTab
      → UpdateJsonPreview() reads from _filterCriteriaPerTab
        → Previously called SaveCurrentTabFilters() (infinite loop!)
```

**The Fundamental Conflict:**
- During EDITING: UI is source of truth → push to dictionaries → regenerate JSON
- During RESTORATION: JSON is source of truth → push to dictionaries → push to UI

No clear "owner" of data during each phase.

**Why This Breaks:**
- Same method (`UpdateJsonPreview`) has different semantics depending on app state
- Changes to one sync path require coordinating changes across all 5 sources
- Adding features (e.g., per-tab filters) requires finding and updating all sync paths
- Event handlers fire in unexpected orders during restoration

**Recent Failures:**
- Added `SaveCurrentTabFilters()` call to `UpdateJsonPreview()` → caused infinite loop
- Fixed infinite loop by removing the call → now manual filter changes don't sync
- Had to add `SaveCurrentTabFilters()` back to ensure current tab filters are saved before reading

---

## Problem 3: Tab Management Complexity (Evolved from Single to Multiple)

**Original Assumption (in design):**
- One parent entity
- One child relationship
- One grid (gridMappings)
- One filterControl

**Current Reality:**
- One parent entity
- Multiple child relationships (each as a tab)
- Multiple grids (one per tab, stored in tab.Controls)
- One filterControl shared across all tabs

**The Symptom:**
Every tab-related feature has two implementations:
```csharp
// Grid 0 (first child)
gridMappings.DataSource = _mappingRows;

// Grids 1+ (additional children)
foreach (var tab in _childEntityTabs)
{
    var grid = tab.Controls.OfType<DataGridView>().FirstOrDefault();
    grid.DataSource = _additionalChildMappings[tabKey];
}
```

**This causes:**
- Dropdowns don't populate correctly for tab 1+ because code was written for grid 0
- Filters only load for current tab, not all tabs at once
- Attributes only load for `_selectedRelationship`, not all child entities

**Example From Recent Bug:**
```csharp
RefreshDestinationFieldDropdowns(); // Only refreshes gridMappings (first grid)
RefreshDestinationFieldDropdownsForAllTabs(); // Method exists but wasn't called

// Had to manually iterate all tabs in ApplyConfiguration:
foreach (var kvp in _childEntityTabs)
{
    var grid = kvp.Value.Controls.OfType<DataGridView>().FirstOrDefault();
    // ... manually populate dropdowns
}
```

---

## Problem 4: Session Restoration State Machines with Overlapping Flags

**Current Flags:**
```csharp
private bool _isRestoringSession;  // "Are we restoring from JSON?"
private bool _isApplyingSession;   // "Are we applying a configuration?"
```

**The Problem:**
These flags were added incrementally to prevent event handlers from firing during restoration:
1. Set flags in `LoadEntitiesForSolutionAsync()`
2. Clear flags if no configuration found
3. Keep flags set if configuration exists
4. Later, moved flag-setting to `RestoreSessionStateIfNeeded()`
5. But old code still sets flags in `LoadSolutionsAsync()`

**Current State Machine:**
```
App Start
  → LoadSolutionsAsync() sets _isApplyingSession = true
    → LoadEntitiesForSolutionAsync() 
      → RestoreSessionStateIfNeeded() sets both flags again
        → OnParentEntityChangedAsync() checks flags, returns early
      → ApplyConfiguration() tries to select parent entity
        → Event fires (but handler returns early due to flags)
          → BindChildRelationships() called manually
            → Attributes loaded for first tab only

Manual Parent Selection (after app running)
  → Flags are now FALSE
    → OnParentEntityChangedAsync() proceeds normally
      → Calls BindChildRelationships()
      → Calls TryLoadExistingParentConfigurationAsync()
        → Calls ApplyConfiguration()
          → Flags FALSE, so parent entity set fires event
            → Returns early again (conflict!)
```

**Why This Breaks:**
- Flags create implicit state that's easy to forget to update
- Event handlers have dual behavior (one when flags set, one when not)
- Adding new features requires understanding flag semantics
- Recent bugs: filters lost, dropdowns empty because restoration code paths weren't fully refactored

---

## Problem 5: No Separation Between UI Binding and Data Persistence

**Current Architecture:**
```
Configurator UI Control
  ├─ Session Settings (save to disk)
  ├─ In-Memory Configuration
  ├─ Event Handlers (UI ↔ Memory)
  ├─ Metadata Loading
  ├─ Configuration Publishing (to Dataverse)
  └─ All mixed together in one 2300+ line file
```

**Ideal Architecture:**
```
Model (ConfigurationModel)
  └─ Represents actual configuration

ViewModel (ConfigurationViewModel)
  ├─ Holds in-memory state
  ├─ Manages property changes
  └─ Notifies view of changes

View (UI Controls)
  ├─ Displays ViewModel
  ├─ Captures user input
  └─ Sends commands to ViewModel

Persistence (SettingsStorage)
  └─ Saves/loads ViewModel to/from disk
```

**Current Reality:**
All of this is mixed in `CascadeFieldsConfiguratorControl` with scattered sync points.

**Why This Breaks:**
- No clear contract for "what state needs to be saved?"
- Changes to UI control → need to update 3+ sync methods
- Hard to test restoration logic independently
- Restoration requires calling methods in specific order with flags set

---

## Evidence From Recent Failures

### Failure 1: Filters Lost During Restoration
**Root Cause:** Filters loaded into `_filterCriteriaPerTab` during `ApplyConfiguration()`, but only first tab's filters loaded into `filterControl`. When `UpdateJsonPreview()` called, it tried to read from `filterControl` (which wasn't synced) instead of `_filterCriteriaPerTab`.

**Fix Attempted:** Call `SaveCurrentTabFilters()` before `UpdateJsonPreview()` to sync filterControl state.

**Unintended Consequence:** Created infinite loop because `SaveCurrentTabFilters()` called `UpdateJsonPreview()`.

**Root Issue:** No clear data flow. Unclear which source should be read at each point.

### Failure 2: Destination Field Dropdowns Empty
**Root Cause:** `RefreshDestinationFieldDropdowns()` only refreshes first grid. Multiple tabs have their own grids, but code was written assuming one grid.

**Fix:** Manually iterate all tabs in `ApplyConfiguration()` to populate dropdowns.

**Root Issue:** Tab feature evolved from single grid to multiple grids, but restoration code wasn't refactored to handle multiple tabs.

### Failure 3: App Hangs During Load
**Root Cause:** `SaveCurrentTabFilters()` calling `UpdateJsonPreview()`, which calls `SaveCurrentTabFilters()`.

**Root Issue:** No guards against recursive calls. Methods don't document their caller expectations.

---

## Detailed Design Flaws by Component

### MetadataService
**Good:** Proper async/await, caching, batch operations
**Bad:** None currently

### ConfigurationService  
**Good:** Proper async/await, separation of concerns
**Bad:** None currently

### CascadeFieldsConfiguratorControl (2300+ lines)
**Good:** 
- Comments document intent sometimes
- Async patterns used

**Bad:**
- Single class manages: UI, state, persistence, metadata loading, restoration
- No ViewModel/Model separation
- 15+ private fields with overlapping responsibilities
- Event handlers scattered throughout (~30+ event handlers)
- Sync methods without clear ordering requirements
- Two parallel tab management systems (gridMappings vs _childEntityTabs)
- Restoration logic spread across 4 methods with implicit state (flags)

### Session Settings
**Good:** Simple structure
**Bad:** Single ConfigurationJson field stores everything; no granular control

---

## Recommendations for Long-Term Stability

### Immediate (< 1 day):
1. Add logging to all sync paths to understand data flow
2. Document which sources are authoritative at each app state
3. Add guard conditions to prevent recursive calls

### Short-Term (1-2 weeks):
1. Extract a `ConfigurationViewModel` class
   - Holds all in-memory state (tabs, mappings, filters)
   - Provides methods to update state
   - Raises events when state changes
   - Clear single source of truth

2. Separate concerns:
   - `UIBinder` - binds ViewModel to controls
   - `SettingsRepository` - loads/saves from disk
   - `DataversePublisher` - publishes to plugin steps
   - `MetadataLoader` - loads metadata

3. Refactor restoration:
   - Single method: `ApplyConfigurationViewModel(ConfigurationModel)`
   - Clears and rebuilds ViewModel
   - UIBinder listens to ViewModel changes
   - No flags needed

### Medium-Term (2-4 weeks):
1. Unit test restoration logic in isolation
2. Move logic from control to ViewModel
3. Consider MVVM pattern for long-term maintainability

---

## Why Small Changes Introduce Bugs

Currently, changing even one feature (e.g., "save filters to disk") requires touching:

1. **SessionSettings** - add a field
2. **SettingsStorage** - update serialization
3. **Control.LoadSettings()** - deserialize new field
4. **Control.SaveSettings()** - serialize new field
5. **Control.RestoreSessionStateIfNeeded()** - restore field
6. **Control.UpdateJsonPreview()** - read field
7. **Control.ApplyConfiguration()** - apply field to UI
8. **Multiple Tab handlers** - sync field when tab changes
9. **Multiple Event handlers** - update field when UI changes

If any of these 9 places is missed or done incorrectly, data is lost.

With a proper ViewModel, it would be:
1. **ConfigurationModel** - add a field
2. **ConfigurationViewModel** - add property, raise NotifyPropertyChanged
3. **UIBinder** - bind property to control

---

## Conclusion

The app is difficult to modify because:

1. **No single source of truth** - configuration exists in 5 places
2. **Implicit state machines** - flags control behavior invisibly  
3. **Bidirectional sync** - unclear who updates whom
4. **Evolved design** - original single-relationship design inadequate for multi-relationship reality
5. **Monolithic control** - everything in one 2300-line class

Each small change must coordinate across all 5 sources and multiple sync methods. Missing any one causes silent data loss.

The fix is architectural, not tactical: separate concerns into Model/ViewModel/View with clear data ownership.
