# Bug Fix Summary: Choice Field Name Mapping

## Issue

When mapping from a choice field's "name" companion field (e.g., `cai_modename`) to a text field (e.g., `cai_modecode`), the plugin was failing to retrieve the parent entity correctly, resulting in a NullReferenceException during Dataverse operations.

## Root Cause

The `ExpandSourceFields` method was checking entity metadata for the source field BEFORE checking if it was a virtual "name" companion field. Since companion name fields (like `cai_modename`) don't exist in entity metadata (they're runtime-only virtual attributes), the metadata lookup failed, and the plugin attempted to request the virtual field directly in the ColumnSet, which caused errors.

## Solution

Modified the `ExpandSourceFields` method in [CascadeService.cs](c:\GitHub\CascadeFields\CascadeFields.Plugin\Services\CascadeService.cs) to check for companion name fields FIRST, before looking them up in metadata. When a field ending in "name" is detected:

1. The "name" suffix is stripped to get the base field (e.g., `cai_modename` → `cai_mode`)
2. The base field is requested in the ColumnSet
3. The FormattedValues collection is automatically populated when the base field is retrieved
4. The text label is extracted and mapped to the target field

## Files Modified

### 1. CascadeService.cs (Lines 728-772)

**Method: `ExpandSourceFields`**

Added explicit check for companion name fields before metadata lookup:

```csharp
// Check if this is a companion "name" field (e.g., cai_modename, statuscodename)
// These are virtual/runtime fields that don't exist in metadata but are derived from the base field
if (TryGetCompanionNameBaseField(mapping.SourceField, out var baseFieldFromName))
{
    // Add the base field instead of the virtual name field
    // This ensures we retrieve the actual attribute (which populates FormattedValues)
    fields.Add(baseFieldFromName);
    continue;
}
```

### 2. TryGetCompanionNameBaseField (Lines 667-726)

Enhanced with comprehensive documentation explaining:
- How companion name fields work in Dataverse
- Why they don't appear in metadata
- How CascadeFields handles them
- Support for choice fields, lookups, and status fields

## Documentation Added

1. **CHOICE_FIELD_MAPPING.md** - Comprehensive guide covering:
   - How choice field name mapping works
   - Supported field types
   - Configuration examples
   - Troubleshooting steps
   - The bug fix details

2. **Examples/choice-field-name-to-text.json** - Working example configuration showing choice field name to text field mappings

## Testing Your Configuration

Your configuration should now work correctly:

```json
{
  "sourceField": "cai_modename",
  "targetField": "cai_modecode",
  "isTriggerField": true
}
```

The plugin will:
1. Recognize `cai_modename` as a virtual companion field
2. Retrieve the `cai_mode` choice field from the parent `cai_area` entity
3. Extract the selected option's text label from FormattedValues
4. Assign it to the `cai_modecode` text field on child `cai_allocation` records

## Build and Deploy

To apply this fix:

1. **Build the plugin:**
   ```powershell
   cd c:\GitHub\CascadeFields\CascadeFields.Plugin
   dotnet build -c Release
   ```

2. **Register the updated plugin** using the Plugin Registration Tool or XrmToolBox

3. **Test** by updating a `cai_area` record and changing the `cai_mode` field value

## Verification

After deployment, verify the fix by:

1. Enabling tracing in your configuration: `"enableTracing": true`
2. Updating a parent `cai_area` record
3. Changing the `cai_mode` field to a different choice value
4. Checking the plugin trace logs to confirm:
   - The base field `cai_mode` is being retrieved
   - FormattedValues contains the text label
   - Child `cai_allocation` records receive the text in `cai_modecode`

## Additional Notes

- **Automatic truncation**: If choice labels exceed the target field's MaxLength, they'll be automatically truncated with an ellipsis (…)
- **No breaking changes**: This fix only affects fields ending in "name" - all other field mappings work exactly as before
- **Performance**: The fix adds a simple string suffix check, with negligible performance impact
