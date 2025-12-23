# Quick Start Guide

## 1. Build the Plugin

```powershell
# Navigate to the solution directory
cd c:\GitHub\CascadeFields

# Restore NuGet packages
dotnet restore

# Build in Release mode
dotnet build -c Release
```

The compiled plugin will be located at:
`CascadeFields.Plugin\bin\Release\net462\CascadeFields.Plugin.dll`

**Important**: The plugin is strongly signed as required by Dataverse. The `CascadeFields.snk` key file must be present in the project directory for the build to succeed.

## 2. Prepare Your Configuration

Choose or create a configuration JSON file:

- **Account to Contact**: See `Examples\account-to-contact.json`
- **Opportunity to Products**: See `Examples\opportunity-to-products.json`
- **Case to Activities**: See `Examples\case-to-activities.json`

Copy the JSON content - you'll need it for plugin registration.

## 3. Register the Plugin Assembly

1. Open **Plugin Registration Tool**
2. Connect to your Dataverse environment
3. Click **Register** > **Register New Assembly**
4. Select `CascadeFields.Plugin.dll`
5. Settings:
   - ✅ Isolation Mode: **Sandbox**
   - ✅ Location: **Database**
6. Click **Register Selected Plugins**

## 4. Register Plugin Step

1. Find the registered assembly in the tree
2. Right-click **CascadeFieldsPlugin** > **Register New Step**

### Step Configuration:

| Setting | Value |
|---------|-------|
| Message | `Update` |
| Primary Entity | Your parent entity (e.g., `account`) |
| Event Pipeline Stage | `PostOperation` |
| Execution Mode | `Asynchronous` |
| Unsecure Configuration | *Paste your JSON configuration here* |
| Filtering Attributes | Select your trigger fields |

### Important:
✅ Click **Add** in Filtering Attributes section
✅ Select the fields that should trigger the plugin (your `isTriggerField` fields)

## 5. Register PreImage

1. In the same dialog, go to the **Images** section
2. Click **Add** to add a new image

### Image Configuration:

| Setting | Value |
|---------|-------|
| Image Type | `PreImage` |
| Name | `PreImage` |
| Entity Alias | `PreImage` |
| Parameters | Select **All Attributes** (or specific fields needed) |

## 6. Test the Plugin

1. Navigate to your parent entity (e.g., Account)
2. Open a record
3. Update a trigger field
4. Save the record
5. Wait a few seconds (asynchronous processing)
6. Check related records to verify fields were updated

## 7. Monitor Execution

### View Plugin Trace Logs:

1. Go to **Settings** > **Plug-in Trace Log**
2. Filter by Type Name: `CascadeFieldsPlugin`
3. View the most recent trace log
4. Look for success indicators:
   - "Plugin Execution Started"
   - "Configuration loaded"
   - "Found X related records"
   - "Update complete: X successful, 0 failed"
   - "Plugin Execution Completed Successfully"

### Check System Jobs:

1. Go to **Settings** > **System Jobs**
2. Filter by:
   - **System Job Type**: Plug-in
   - **Regarding**: Your parent record
3. Verify status is **Succeeded**

## Common Setup Issues

### Issue: Plugin doesn't execute
**Solution**: Verify filtering attributes match your trigger fields

### Issue: Configuration error
**Solution**: Validate JSON using a JSON validator (jsonlint.com)

### Issue: No child records updated
**Solution**: 
- Check filter criteria matches your child records
- Verify lookup field name is correct
- Ensure child records exist and match filters

### Issue: Permission errors
**Solution**: Ensure the user has update permissions on child entities

## Example Test Scenario

**Using Account to Contact configuration:**

1. Open an Account record
2. Change the **City** field (this is a trigger field)
3. Save the record
4. Wait 5-10 seconds
5. Open related Contact records
6. Verify the **City** field was updated on active contacts

## Next Steps

- Review the [README.md](../README.md) for detailed documentation
- Customize configurations for your specific needs
- Monitor trace logs for the first few executions
- Test with different filter criteria
- Create additional configurations for other entities

## Getting Help

If you encounter issues:

1. ✅ Check Plugin Trace Logs (most detailed information)
2. ✅ Check System Jobs for errors
3. ✅ Verify configuration JSON is valid
4. ✅ Ensure PreImage is registered correctly
5. ✅ Confirm filtering attributes are set
6. ✅ Test in a development environment first
