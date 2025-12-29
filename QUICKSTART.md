# Quick Start Guide

Get up and running with CascadeFields in minutes using the XrmToolBox Configurator.

## Prerequisites

- XrmToolBox with CascadeFields Configurator plugin installed
- Access to a Dataverse environment
- Security roles with update permissions on parent and child entities

## Option 1: Using the Configurator Tool (Recommended)

### 1. Install the Configurator

```powershell
# Install from NuGet in XrmToolBox
# Search for: CascadeFields.Configurator
```

Or build and deploy locally:

```powershell
cd c:\GitHub\CascadeFields
pwsh -File ./pack-nuget.ps1 -Configuration Release -SkipPush
```

### 2. Configure Your Cascade

1. Open **XrmToolBox** and launch **CascadeFields Configurator**
2. Connect to your environment
3. Select your **parent entity** (e.g., Account)
4. Click **Add Related Entity**
5. Select your **child entity** (e.g., Contact)
6. Choose the **lookup field** that links child to parent (e.g., `parentcustomerid`)
7. Add **field mappings**:
   - Source field: parent field name
   - Target field: child field name
   - Trigger: check if this field should trigger the cascade
8. Optionally add **filter criteria** (e.g., `statecode|eq|0` for active only)

### 3. Publish Configuration

1. Click **Publish Configuration** in the ribbon
2. Optionally select a solution to add components
3. Wait for confirmation: "Publish complete: parent and child steps upserted"

**What gets created:**
- Parent Update step (Post-operation, Async) + PreImage
- Child Create step (Pre-operation, Sync) per related entity
- Child Update step (Pre-operation, Sync) per related entity with lookup field

### 4. Test It

**Parent Update Test:**
1. Open a parent record (e.g., Account)
2. Change a trigger field
3. Save and wait 5-10 seconds (async)
4. Check related child records - fields should be updated

**Child Create Test:**
1. Create a new child record (e.g., Contact)
2. Set the parent lookup field (e.g., Account)
3. Save
4. Mapped fields should be populated immediately

**Child Relink Test:**
1. Open an existing child record
2. Change the parent lookup to a different parent
3. Save
4. Mapped fields should update to new parent's values

### 5. Monitor Execution

**View Trace Logs:**
1. Settings → Plug-in Trace Log
2. Filter by: `CascadeFieldsPlugin`
3. Look for: "Plugin Execution Completed Successfully"

**Check System Jobs (for parent updates):**
1. Settings → System Jobs
2. Filter by: System Job Type = "Plug-in"
3. Verify status: Succeeded

## Option 2: Manual Registration (Advanced)

If you prefer manual setup without the Configurator:

### 1. Build the Plugin

```powershell
cd c:\GitHub\CascadeFields
dotnet build -c Release
```

Output: `CascadeFields.Plugin\bin\Release\net462\CascadeFields.Plugin.dll`

### 2. Register Assembly

1. Open Plugin Registration Tool
2. Register → Register New Assembly
3. Select `CascadeFields.Plugin.dll`
4. Isolation: Sandbox, Location: Database

### 3. Prepare Configuration JSON

See `Examples\` folder for templates. Example:

```json
{
  "parentEntity": "account",
  "isActive": true,
  "relatedEntities": [
    {
      "entityName": "contact",
      "useRelationship": false,
      "lookupFieldName": "parentcustomerid",
      "filterCriteria": "statecode|eq|0",
      "fieldMappings": [
        {
          "sourceField": "address1_city",
          "targetField": "address1_city",
          "isTriggerField": true
        }
      ]
    }
  ]
}
```

### 4. Register Parent Step

- **Message**: Update
- **Primary Entity**: account
- **Stage**: PostOperation (40)
- **Mode**: Asynchronous
- **Unsecure Config**: [paste JSON]
- **Filtering Attributes**: [select trigger fields]
- **PreImage**: Name=PreImage, Alias=PreImage, Attributes=[source fields]

### 5. Register Child Steps (per related entity)

**Create Step:**
- **Message**: Create
- **Primary Entity**: contact
- **Stage**: PreOperation (20)
- **Mode**: Synchronous
- **Unsecure Config**: [same JSON]

**Update Step (for relink):**
- **Message**: Update
- **Primary Entity**: contact
- **Stage**: PreOperation (20)
- **Mode**: Synchronous
- **Unsecure Config**: [same JSON]
- **Filtering Attributes**: parentcustomerid
- **PreImage**: Name=PreImage, Alias=PreImage, Attributes=parentcustomerid

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Plugin doesn't fire | Check filtering attributes match trigger fields |
| Child records not updating | Verify lookup field name is correct |
| Permission errors | Ensure users have update rights on child entities |
| Values not copying | Check field names are exact (case-sensitive) |

## Next Steps

- Review [README.md](README.md) for detailed documentation
- Check [CONFIGURATION.md](CONFIGURATION.md) for advanced patterns
- Explore `Examples\` folder for more configurations
- Set `enableTracing: false` in production for better performance

## Getting Help

1. ✅ Check Plugin Trace Logs (most detailed)
2. ✅ Verify JSON configuration is valid
3. ✅ Confirm `lookupFieldName` is specified
4. ✅ Test in dev environment first
