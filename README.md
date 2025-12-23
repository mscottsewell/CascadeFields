# CascadeFields Plugin for Microsoft Dataverse

A flexible, configurable plugin for Microsoft Dataverse (Dynamics 365) that automatically cascades field values from parent records to related child records based on configurable rules.

## Features

- ✅ **Configurable Field Mappings**: Define which fields to cascade from parent to child records
- ✅ **Trigger Field Detection**: Only cascade when specific fields change
- ✅ **Relationship-Based**: Support for both named relationships and lookup fields
- ✅ **Filtering**: Apply filters to target only specific child records (e.g., only active records)
- ✅ **Comprehensive Logging**: Detailed tracing for debugging and monitoring
- ✅ **Error Handling**: Robust error handling to prevent data corruption
- ✅ **Performance Optimized**: Asynchronous execution to avoid blocking user operations

## How It Works

1. Plugin registers on the **Update** message of the parent entity
2. When a record is updated, the plugin checks if any configured trigger fields changed
3. If triggered, it retrieves related child records based on configured relationships and filters
4. It updates the specified fields on matching child records with values from the parent

## Configuration

The plugin uses JSON configuration stored in the plugin step's **Unsecure Configuration** field.

### Configuration Schema

```json
{
  "id": "unique-config-id",
  "name": "Configuration Name",
  "parentEntity": "account",
  "isActive": true,
  "fieldMappings": [
    {
      "sourceField": "parentfieldname",
      "targetField": "childfieldname",
      "isTriggerField": true
    }
  ],
  "relatedEntities": [
    {
      "entityName": "contact",
      "relationshipName": "account_primary_contact",
      "useRelationship": true,
      "filterCriteria": "statecode|eq|0"
    }
  ]
}
```

### Configuration Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `id` | string | No | Unique identifier for the configuration |
| `name` | string | No | Descriptive name for the configuration |
| `parentEntity` | string | **Yes** | Logical name of the parent entity being monitored |
| `isActive` | boolean | No | Whether this configuration is active (default: true) |
| `fieldMappings` | array | **Yes** | Array of field mapping definitions |
| `relatedEntities` | array | **Yes** | Array of related entity configurations |

### Field Mapping Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `sourceField` | string | **Yes** | Field name on the parent entity |
| `targetField` | string | **Yes** | Field name on the child entity |
| `isTriggerField` | boolean | No | If true, changes to this field trigger the cascade |

### Related Entity Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `entityName` | string | **Yes** | Logical name of the child entity |
| `relationshipName` | string | Conditional | Name of the relationship (required if `useRelationship` is true) |
| `useRelationship` | boolean | No | Use relationship name vs. lookup field (default: true) |
| `lookupFieldName` | string | Conditional | Lookup field name (required if `useRelationship` is false) |
| `filterCriteria` | string | No | Filter to apply to child records |

### Filter Criteria Format

Filter criteria uses a simple pipe-delimited format:

```
field|operator|value;field2|operator2|value2
```

**Supported Operators:**
- `eq`, `equal`, `=` - Equal
- `ne`, `notequal`, `!=` - Not Equal
- `gt`, `greaterthan`, `>` - Greater Than
- `lt`, `lessthan`, `<` - Less Than
- `in` - In (value list)
- `notin` - Not In
- `null` - Is Null
- `notnull` - Is Not Null
- `like` - Like (pattern matching)

**Examples:**
- `statecode|eq|0` - Active records only
- `statecode|eq|0;revenue|gt|10000` - Active records with revenue > 10000
- `primarycontactid|notnull|null` - Records with a primary contact

## Configuration Examples

### Example 1: Account to Contact Cascade

Cascade account status and primary industry to all active contacts:

```json
{
  "id": "account-to-contact",
  "name": "Cascade Account Fields to Contacts",
  "parentEntity": "account",
  "isActive": true,
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
  ],
  "relatedEntities": [
    {
      "entityName": "contact",
      "useRelationship": false,
      "lookupFieldName": "parentcustomerid",
      "filterCriteria": "statecode|eq|0"
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
  "fieldMappings": [
    {
      "sourceField": "estimatedclosedate",
      "targetField": "scheduledeliverydate",
      "isTriggerField": true
    }
  ],
  "relatedEntities": [
    {
      "entityName": "opportunityproduct",
      "useRelationship": false,
      "lookupFieldName": "opportunityid",
      "filterCriteria": "statecode|eq|0"
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
  ],
  "relatedEntities": [
    {
      "entityName": "new_childtype1",
      "useRelationship": false,
      "lookupFieldName": "new_parentid",
      "filterCriteria": "statecode|eq|0;new_type|eq|1"
    },
    {
      "entityName": "new_childtype2",
      "useRelationship": false,
      "lookupFieldName": "new_parentid",
      "filterCriteria": "statecode|eq|0"
    }
  ]
}
```

## Installation & Deployment

### Prerequisites

- Visual Studio 2019 or later
- .NET Framework 4.6.2 or later
- Plugin Registration Tool
- Access to a Dataverse environment

### Build the Plugin

1. Clone or download this repository
2. Open `CascadeFields.sln` in Visual Studio
3. Restore NuGet packages
4. Build the solution in **Release** mode
5. The compiled assembly will be in `CascadeFields.Plugin\bin\Release\net462\CascadeFields.Plugin.dll`

### Register the Plugin

1. Open the **Plugin Registration Tool** and connect to your environment
2. Click **Register** > **Register New Assembly**
3. Select the `CascadeFields.Plugin.dll` file
4. Choose **Sandbox** isolation mode
5. Choose **Database** for storage
6. Click **Register Selected Plugins**

### Register Plugin Steps

For each parent entity you want to monitor:

1. Right-click the `CascadeFields.Plugin` assembly
2. Select **Register New Step**
3. Configure the step:
   - **Message**: `Update`
   - **Primary Entity**: Your parent entity (e.g., `account`)
   - **Event Pipeline Stage**: `PostOperation` (40)
   - **Execution Mode**: `Asynchronous`
   - **Unsecure Configuration**: Paste your JSON configuration (see examples above)
   - **Filtering Attributes**: Select the trigger fields to improve performance
4. Register a **PreImage**:
   - **Name**: `PreImage`
   - **Entity Alias**: `PreImage`
   - **Parameters**: Select **All Attributes** or specific fields needed
5. Click **Register New Step**

### Important Registration Notes

- ⚠️ **Always use Asynchronous mode** to avoid blocking user operations
- ⚠️ **Always register a PreImage** named "PreImage" for change detection
- ⚠️ **Use Filtering Attributes** to optimize performance (select only trigger fields)
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
|-------|----------|
| Plugin not executing | Verify plugin step is registered on correct entity and message |
| Fields not cascading | Check field names match exactly (case-sensitive) |
| No child records updated | Verify filter criteria and relationship configuration |
| Performance issues | Add filtering attributes, optimize filter criteria |
| Infinite loop errors | Check cascade depth settings and circular references |

### Debug Logging

The plugin provides detailed trace logging:

```
[timestamp] [INFO] [CascadeFieldsPlugin] [+0ms] === Plugin Execution Started ===
[timestamp] [INFO] [CascadeFieldsPlugin] [+5ms] Execution Context - Message: Update | Stage: 40
[timestamp] [INFO] [CascadeFieldsPlugin] [+12ms] Configuration loaded: Account to Contact
[timestamp] [INFO] [CascadeFieldsPlugin] [+15ms] Trigger field 'customstatusfield' changed
[timestamp] [INFO] [CascadeFieldsPlugin] [+120ms] Found 15 related contact records
[timestamp] [INFO] [CascadeFieldsPlugin] [+450ms] Update complete: 15 successful, 0 failed
```

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

This project is provided as-is for use in Microsoft Dataverse environments.

## Version History

- **1.0.0** - Initial release
  - Configurable field cascading
  - Relationship and lookup field support
  - Filter criteria support
  - Comprehensive logging and error handling
