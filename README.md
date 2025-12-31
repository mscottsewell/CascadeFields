# CascadeFields Plugin for Microsoft Dataverse

A flexible, configurable plugin for Microsoft Dataverse (Dynamics 365) that automatically cascades field values from parent records to related child records, and populates child records when created or relinked to a parent. You can run the plugin on its own via the Plugin Registration Tool, but the fastest path is the XrmToolBox **CascadeFields Configurator**, which handles step creation, publishing, and validation for you.

## Overview: Why It Helps

- End-to-end path from configuration design to deployment without writing code or manually wiring steps
- Keeps parent and child records in sync with trigger-aware cascades and safe filtering
- Ready-to-use templates and examples so you can start from proven setups and tweak as needed
- Easiest path: open XrmToolBox → CascadeFields Configurator → author mappings in the UI → click Publish. The tool writes the JSON, registers steps, and validates lookup fields automatically.

## 📚 Documentation

- **[Quick Start Guide](QUICKSTART.md)** - Get started in minutes with XrmToolBox Configurator
- **[Configuration Guide](CONFIGURATION.md)** - Best practices, patterns, and examples
- **[Examples Folder](Examples/)** - Ready-to-use configuration templates

## Features

- ✅ **Parent & Child Triggering**: Cascade on parent updates AND when children are created/relinked
- ✅ **Automatic Deployment**: XrmToolBox Configurator publishes parent and child plugin steps
- ✅ **Configurable Field Mappings**: Define which fields to cascade from parent to child records
- ✅ **Entity-Specific Mappings**: Each related entity can have its own unique field mappings
- ✅ **Trigger Field Detection**: Only cascade when specific fields change
- ✅ **Explicit Lookup Fields**: Reliable child detection using explicit lookup field names
- ✅ **Filtering**: Apply filters to target only specific child records
- ✅ **Batch Updates**: ExecuteMultipleRequest for 50-98% faster execution
- ✅ **Security**: Field validation and injection protection in filter criteria
- ✅ **Comprehensive Logging**: Detailed tracing for debugging and monitoring

## How It Works

### Parent-Side Cascade (Parent Update → Children)

1. Plugin registers on **Update** message of parent entity (Post-operation, Async)
2. When parent is updated, plugin checks if any configured trigger fields changed
3. If triggered, retrieves related child records based on lookup fields and filters
4. Updates specified fields on matching child records with values from parent

### Child-Side Population (Child Created/Relinked → Copy from Parent)

1. Plugin registers on **Create** and **Update** messages of child entities (Pre-operation, Sync)
2. When child is created with parent lookup, or parent lookup changes, plugin detects it
3. Retrieves parent record and extracts mapped field values
4. Applies values directly to child in same transaction

## Configuration

See **[CONFIGURATION.md](CONFIGURATION.md)** for detailed configuration guide including:

- Best practices and recommended patterns
- Filter criteria syntax and examples
- Field mapping strategies
- Production optimization
- Complete examples

### Quick Configuration Reference

Basic JSON structure:

### Configuration Schema

```json
{
  "id": "unique-config-id",
  "name": "Configuration Name",
  "parentEntity": "account",
  "isActive": true,
  "relatedEntities": [
    {
      "entityName": "contact",
      "relationshipName": "account_primary_contact",
      "useRelationship": true,
      "filterCriteria": "statecode|eq|0",
      "fieldMappings": [
        {
          "sourceField": "parentfieldname",
          "targetField": "childfieldname",
          "isTriggerField": true
        }
      ]
    }
  ]
}
```

### Configuration Properties

| Property | Type | Required | Description |
| ---------- | ------ | ---------- | ------------- |
| `id` | string | No | Unique identifier for the configuration |
| `name` | string | No | Descriptive name for the configuration |
| `parentEntity` | string | **Yes** | Logical name of the parent entity being monitored |
| `isActive` | boolean | No | Whether this configuration is active (default: true) |
| `enableTracing` | boolean | No | Enable detailed tracing/logging (default: true) |
| `relatedEntities` | array | **Yes** | Array of related entity configurations |

### Related Entity Properties

| Property | Type | Required | Description |
| ---------- | ------ | ---------- | ------------- |
| `entityName` | string | **Yes** | Logical name of the child entity |
| `lookupFieldName` | string | **Recommended** | Child's lookup field pointing to parent (e.g., `parentcustomerid`) |
| `relationshipName` | string | Optional | Name of the relationship (legacy, not recommended) |
| `useRelationship` | boolean | No | Use relationship name vs. lookup field (default: true, **set to false**) |
| `filterCriteria` | string | No | Filter to apply to child records |
| `fieldMappings` | array | **Yes** | Array of field mapping definitions for this entity |

**Important**: Always specify `lookupFieldName` and set `useRelationship: false` for reliable child-side plugin support and relink handling.

### Field Mapping Properties

| Property | Type | Required | Description |
| ---------- | ------ | ---------- | ------------- |
| `sourceField` | string | **Yes** | Field name on the parent entity |
| `targetField` | string | **Yes** | Field name on the child entity |
| `isTriggerField` | boolean | No | If true, changes to this field trigger the cascade |

**Note**: Field mappings are defined **within each related entity**, allowing different entities to have different field mappings from the same parent.

### Lookup / Option Set to Text Targets

When the target field is text and source is a lookup or option set:

- **Lookup**: Uses display name, falls back to formatted value or GUID
- **OptionSet**: Uses label text, falls back to numeric value
- Automatically truncates with ellipsis (…) if too long for target field
- Attribute metadata is cached for performance

## Configuration Examples

### Example 1: Account to Contact Cascade

Cascade account status and primary industry to all active contacts:

```json
{
  "id": "account-to-contact",
  "name": "Cascade Account Fields to Contacts",
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
          "sourceField": "customstatusfield",
          "targetField": "customstatusfield",
          "isTriggerField": true
        },
        {
          "sourceField": "industrycode",
          "targetField": "industrycode",
          "isTriggerField": false
        }
      ]
    }
  ]
}
```

### Example 2: Opportunity to Opportunity Product

Cascade opportunity expected close date to all active opportunity products:

```json
{
  "id": "opportunity-to-products",
  "name": "Cascade Opportunity Dates to Products",
  "parentEntity": "opportunity",
  "isActive": true,
  "relatedEntities": [
    {
      "entityName": "opportunityproduct",
      "useRelationship": false,
      "lookupFieldName": "opportunityid",
      "filterCriteria": "statecode|eq|0",
      "fieldMappings": [
        {
          "sourceField": "estimatedclosedate",
          "targetField": "scheduledeliverydate",
          "isTriggerField": true
        }
      ]
    }
  ]
}
```

### Example 3: Custom Parent to Multiple Child Entities

Cascade custom fields to multiple child entities with different filters:

```json
{
  "id": "parent-to-children",
  "name": "Cascade Parent to Multiple Children",
  "parentEntity": "new_parent",
  "isActive": true,
  "relatedEntities": [
    {
      "entityName": "new_childtype1",
      "useRelationship": false,
      "lookupFieldName": "new_parentid",
      "filterCriteria": "statecode|eq|0;new_type|eq|1",
      "fieldMappings": [
        {
          "sourceField": "new_customfield",
          "targetField": "new_customfield",
          "isTriggerField": true
        },
        {
          "sourceField": "new_category",
          "targetField": "new_category",
          "isTriggerField": true
        }
      ]
    },
    {
      "entityName": "new_childtype2",
      "useRelationship": false,
      "lookupFieldName": "new_parentid",
      "filterCriteria": "statecode|eq|0",
      "fieldMappings": [
        {
          "sourceField": "new_customfield",
          "targetField": "new_customfield",
          "isTriggerField": true
        },
        {
          "sourceField": "new_category",
          "targetField": "new_category",
          "isTriggerField": true
        }
      ]
    }
  ]
}
```

### Example 5: Production Configuration with Minimal Tracing

Same as Example 1 but with tracing disabled for production environments:

```json
{
  "id": "prod-account-contact",
  "name": "Production: Account to Contact (Minimal Tracing)",
  "parentEntity": "account",
  "isActive": true,
  "enableTracing": false,
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
        },
        {
          "sourceField": "telephone1",
          "targetField": "telephone1",
          "isTriggerField": false
        }
      ]
    }
  ]
}
```

**Additional Examples**: See the `Examples` folder for more configuration samples including:

- `account-to-contact.json` - Address and phone cascading
- `opportunity-to-products.json` - Date field cascading
- `case-to-activities.json` - Priority cascading to multiple activity types
- `multi-entity-different-mappings.json` - Different field mappings per entity
- `production-with-minimal-tracing.json` - Production configuration with tracing disabled

## Installation & Deployment

### Quick Start

See **[QUICKSTART.md](QUICKSTART.md)** for step-by-step setup using the XrmToolBox Configurator tool.

### Prerequisites

- Visual Studio 2019 or later (for building from source)
- .NET Framework 4.6.2 or later
- XrmToolBox with CascadeFields Configurator (recommended)
- Plugin Registration Tool (for manual registration)
- Access to a Dataverse environment

### Build from Source

```powershell
git clone https://github.com/mscottsewell/CascadeFields.git
cd CascadeFields
dotnet build -c Release
```

Output: `CascadeFields.Plugin\bin\Release\net462\CascadeFields.Plugin.dll`

**Note**: The assembly is strongly signed using `CascadeFields.snk`.

### Register the Plugin

1. Open the **Plugin Registration Tool** and connect to your environment
2. Click **Register** > **Register New Assembly**
3. Select the `CascadeFields.Plugin.dll` file
4. Choose **Sandbox** isolation mode
5. Choose **Database** for storage
6. Click **Register Selected Plugins**

### Automatic Publishing with the Configurator Tool

**Recommended**: Use the CascadeFields Configurator tool (XrmToolBox plugin) to automatically register and publish configurations:

1. Open **XrmToolBox** and launch the **CascadeFields Configurator** tool
2. Connect to your environment
3. Configure your parent entity and child relationships in the UI
4. Click **Publish Configuration** button in the ribbon
5. The tool automatically creates:
   - **Parent Update step** (PostOperation, Async) with PreImage
   - **Child Create steps** (PreOperation, Sync) for each related entity
   - **Child Update steps** (PreOperation, Sync) for each related entity when `lookupFieldName` is specified
6. Optionally select a solution to add all components

**What Gets Published:**

| Step Type | Entity | Message | Stage | Mode | Purpose | PreImage |
|-----------|--------|---------|-------|------|---------|----------|
| Parent | `<ParentEntity>` | Update | PostOperation (40) | Async | Cascade changes to children | Yes (trigger fields) |
| Child Create | `<ChildEntity>` | Create | PreOperation (20) | Sync | Populate child on creation | No |
| Child Relink | `<ChildEntity>` | Update | PreOperation (20) | Sync | Update child when parent changes | Yes (lookup field) |

**Child Relink Step Requirements:**

- Only published when `lookupFieldName` is explicitly set in the configuration
- Filters on changes to the lookup field only (no unnecessary triggers)
- Recommended over `useRelationship: true` for reliability

**Important:** Ensure each `relatedEntities` entry includes `lookupFieldName` (e.g., `parentcustomerid` for contact → account) to enable child relink handling. The Configurator will warn if this is missing.

### Manual Registration (Advanced)

For manual step registration without the Configurator:

#### Parent Step

1. Right-click the `CascadeFields.Plugin` assembly
2. Select **Register New Step**
3. Configure the step:
   - **Message**: `Update`
   - **Primary Entity**: Your parent entity (e.g., `account`)
   - **Event Pipeline Stage**: `PostOperation` (40)
   - **Execution Mode**: `Asynchronous`
   - **Unsecure Configuration**: Paste your JSON configuration
   - **Filtering Attributes**: Select the trigger fields
4. Register a **PreImage**:
   - **Name**: `PreImage`
   - **Entity Alias**: `PreImage`
   - **Parameters**: Include source fields from mappings

#### Child Create Step (Per Related Entity)

1. Right-click the `CascadeFields.Plugin` assembly
2. Select **Register New Step**
3. Configure the step:
   - **Message**: `Create`
   - **Primary Entity**: Your child entity (e.g., `contact`)
   - **Event Pipeline Stage**: `PreOperation` (20)
   - **Execution Mode**: `Synchronous`
   - **Unsecure Configuration**: Same JSON as parent step
4. No PreImage required for Create

#### Child Update Step (Per Related Entity, Optional)

1. Right-click the `CascadeFields.Plugin` assembly
2. Select **Register New Step**
3. Configure the step:
   - **Message**: `Update`
   - **Primary Entity**: Your child entity (e.g., `contact`)
   - **Event Pipeline Stage**: `PreOperation` (20)
   - **Execution Mode**: `Synchronous`
   - **Unsecure Configuration**: Same JSON as parent step
   - **Filtering Attributes**: The lookup field (e.g., `parentcustomerid`)
4. Register a **PreImage**:
   - **Name**: `PreImage`
   - **Entity Alias**: `PreImage`
   - **Parameters**: Include the lookup field

### Important Registration Notes

- ⚠️ **Use the Configurator tool** for automatic deployment with proper stage/mode settings
- ⚠️ **Parent steps use Asynchronous mode** to avoid blocking user operations
- ⚠️ **Child steps use Synchronous PreOperation** to write values in the same transaction
- ⚠️ **Always include `lookupFieldName`** in configuration for child relink support
- ⚠️ **Test in a non-production environment first**

## Security Considerations

1. **Permissions**: The plugin executes under the context of the user who triggered it
2. **Security Roles**: Ensure users have appropriate permissions on both parent and child entities
3. **Cascade Depth**: Plugin checks depth to prevent infinite loops (max depth: 2)
4. **Sensitive Data**: If using secure configuration, store sensitive data there instead of unsecure

## Monitoring & Troubleshooting

### Viewing Plugin Traces

1. In Dataverse, navigate to **Settings** > **Plug-in Trace Log**
2. Filter by plugin name: `CascadeFields.Plugin`
3. Review trace logs for execution details

### Common Issues

| Issue | Solution |
| ------- | ---------- |
| Plugin not executing | Verify plugin step is registered on correct entity and message |
| Fields not cascading | Check field names match exactly (case-sensitive) |
| No child records updated | Verify filter criteria and relationship configuration |
| Performance issues | Add filtering attributes, optimize filter criteria |
| Infinite loop errors | Check cascade depth settings and circular references |

### Debug Logging

The plugin provides detailed trace logging that can be controlled via the `enableTracing` configuration property:

#### Enable Detailed Tracing (Development/Debugging)

```json
{
  "parentEntity": "account",
  "enableTracing": true,
  ...
}
```

#### Disable Detailed Tracing (Production)

```json
{
  "parentEntity": "account",
  "enableTracing": false,
  ...
}
```

When tracing is enabled, you'll see detailed logs like:

``` text
[timestamp] [INFO] [CascadeFieldsPlugin] [+0ms] === Plugin Execution Started ===
[timestamp] [INFO] [CascadeFieldsPlugin] [+5ms] Execution Context - Message: Update | Stage: 40
[timestamp] [INFO] [CascadeFieldsPlugin] [+12ms] Configuration loaded: Account to Contact
[timestamp] [INFO] [CascadeFieldsPlugin] [+15ms] Trigger field 'customstatusfield' changed
[timestamp] [INFO] [CascadeFieldsPlugin] [+120ms] Found 15 related contact records
[timestamp] [INFO] [CascadeFieldsPlugin] [+450ms] Update complete: 15 successful, 0 failed
```

**Note**: Error logging is always enabled regardless of the `enableTracing` setting to ensure critical issues are captured.

## Performance Considerations

- Use **Asynchronous** execution mode (recommended for all scenarios)
- Apply **Filtering Attributes** on plugin step registration
- Use specific **filter criteria** to limit child records
- Monitor **execution time** via trace logs
- Consider **batch size** for large record sets

## Limitations

- Maximum execution depth: 2 (prevents infinite loops)
- Filter criteria uses simplified format (not full FetchXML)
- Requires PreImage for change detection
- Plugin registration must be done manually (no automated deployment)

## Best Practices

1. ✅ Always test in a development environment first
2. ✅ Use meaningful configuration names and IDs
3. ✅ Document your cascade rules in a central location
4. ✅ Monitor plugin trace logs regularly
5. ✅ Use filtering attributes to optimize performance
6. ✅ Keep configurations focused (one parent entity per configuration)
7. ✅ Use trigger fields to avoid unnecessary executions
8. ✅ Apply filters to child records to minimize updates

## Support & Contributing

For issues, questions, or contributions:

- Review trace logs for detailed error information
- Ensure configuration JSON is valid
- Verify all required fields are present in configuration

## License

This project is licensed under the [MIT License](LICENSE) - see the LICENSE file for details.

Copyright (c) 2025 mscottsewell

## Version History

- **1.0.0** - Initial release
  - Configurable field cascading
  - Relationship and lookup field support
  - Filter criteria support
  - Comprehensive logging and error handling
