# Mapping Choice Field Labels to Text Fields

## Overview

CascadeFields supports mapping choice (OptionSet) field **labels** (the human-readable text) to text fields by using the virtual "name" companion fields that Dataverse provides at runtime.

## How It Works

For choice fields, Dataverse provides two ways to access the value:
1. **Base Field** (e.g., `cai_mode`) - Contains the numeric OptionSetValue
2. **Name Companion Field** (e.g., `cai_modename`) - Virtual field that provides the text label

When you configure a field mapping with a source field ending in "name", CascadeFields:
1. Recognizes it as a virtual companion field
2. Retrieves the base field instead (which you CAN request in a query)
3. Extracts the text label from the FormattedValues collection
4. Maps it to your target text field

## Example Configuration

```json
{
  "sourceField": "cai_modename",
  "targetField": "cai_modecode",
  "isTriggerField": true
}
```

This will:
- Retrieve the `cai_mode` choice field from the parent
- Extract the selected option's label (e.g., "Standard Mode", "Advanced Mode")
- Set the `cai_modecode` text field on the child to that label

## Supported Field Types with Name Companions

This works for:
- **Choice/OptionSet fields**: `statuscodename`, `industrycodename`, custom choice fields
- **Lookup fields**: `parentcustomeridname`, `owneridname`
- **Status fields**: `statuscodename`, `statecodename`

## Important Notes

### Virtual Fields Don't Exist in Metadata

The "name" fields (like `cai_modename`) are NOT real attributes in the Dataverse metadata. They're runtime-only virtual attributes. This means:
- You cannot request them directly in a `ColumnSet`
- They don't appear in the entity's attribute metadata
- They're populated automatically when you retrieve the base field

### How CascadeFields Handles This

The plugin automatically:
1. Detects when a source field ends with "name"
2. Strips the "name" suffix to get the base field
3. Requests the base field in the retrieve operation
4. Accesses the formatted value from either:
   - The `FormattedValues` collection, OR
   - The `Name` property (for lookups), OR  
   - The OptionSetValue with a fallback to the numeric value

### Truncation

If the choice field label exceeds the target text field's maximum length, CascadeFields will automatically truncate it and append an ellipsis character (…).

## Bug Fix (January 2026)

A bug was fixed where companion "name" fields weren't being handled correctly during parent entity retrieval. The issue was:

**Problem**: When mapping from `cai_modename` → `cai_modecode`, the plugin tried to look up `cai_modename` in the entity metadata first. Since companion name fields don't exist in metadata, this lookup failed, and the plugin attempted to request the virtual field directly in the ColumnSet, which caused errors.

**Solution**: The `ExpandSourceFields` method now checks for companion name fields BEFORE looking them up in metadata. When it detects a field ending in "name", it immediately strips the suffix and requests the base field instead.

### Code Changes

The fix was made in `CascadeFields.Plugin/Services/CascadeService.cs`:

1. **Updated `ExpandSourceFields` method** (lines ~587-620)
   - Added explicit check for companion name fields using `TryGetCompanionNameBaseField`
   - Prioritizes this check before the metadata lookup
   - Ensures the base field is requested in the ColumnSet

2. **Enhanced `TryGetCompanionNameBaseField` method** (lines ~664-719)
   - Added comprehensive documentation
   - Explained how companion fields work in Dataverse
   - Documented that these fields are runtime-only and don't exist in metadata

### Before Fix
```csharp
// Old logic checked metadata first, failed for companion name fields
if (entityAttributes != null && entityAttributes.TryGetValue(mapping.SourceField, out var sourceMeta))
{
    // This failed for cai_modename because it doesn't exist in metadata
}
```

### After Fix
```csharp
// New logic checks for companion name fields first
if (TryGetCompanionNameBaseField(mapping.SourceField, out var baseFieldFromName))
{
    // Strips "name" suffix: cai_modename → cai_mode
    fields.Add(baseFieldFromName);
    continue;
}
```

## Testing Your Configuration

To test a choice field name mapping:

1. Create or update a parent record
2. Change the choice field value (e.g., set `cai_mode` to a different option)
3. Verify that related child records receive the **text label** of the selected option
4. Check that truncation occurs if labels are longer than the target field's max length

## Example: Your Configuration

Based on your provided configuration:

```json
{
  "sourceField": "cai_modename",
  "targetField": "cai_modecode",
  "isTriggerField": true
}
```

This should now work correctly. The plugin will:
1. Detect that `cai_modename` is a companion field
2. Retrieve `cai_mode` from the parent `cai_area` entity
3. Extract the choice field's selected option label
4. Assign it to the `cai_modecode` text field on the child `cai_allocation` entity

## Troubleshooting

If you're still seeing errors:

1. **Verify the base field exists**: Make sure `cai_mode` is a valid choice field on `cai_area`
2. **Check field security**: Ensure your plugin execution user has read access to `cai_mode`
3. **Enable tracing**: Set `"enableTracing": true` in your configuration to see detailed logs
4. **Check target field length**: If the choice labels are very long, ensure `cai_modecode` has sufficient MaxLength

## Related Examples

See [Examples/choice-field-name-to-text.json](Examples/choice-field-name-to-text.json) for a complete working example using standard Account and Contact entities.
