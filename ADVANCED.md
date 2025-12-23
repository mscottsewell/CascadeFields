# Advanced Configuration Guide

## Advanced Filter Scenarios

### Complex Filter Combinations

Combine multiple conditions using semicolon separation:

```json
"filterCriteria": "statecode|eq|0;statuscode|eq|1;createdon|gt|2024-01-01"
```

This filters for records that are:
- Active (statecode = 0)
- AND have status reason = 1
- AND created after January 1, 2024

### Using NULL Filters

Check for null or non-null values:

```json
"filterCriteria": "primarycontactid|notnull|null"
```

Or to find records with null values:

```json
"filterCriteria": "parentaccountid|null|null"
```

### Numeric Comparisons

Use comparison operators with numeric fields:

```json
"filterCriteria": "revenue|gt|50000;numberofemployees|lt|100"
```

### Using IN Operator

Filter by multiple possible values (not yet implemented in current version, but can be added):

```json
"filterCriteria": "industrycode|in|1,2,3"
```

## Multiple Configuration Patterns

### Pattern 1: Single Parent, Multiple Child Types

One parent entity cascading to different types of child entities:

```json
{
  "id": "account-multi-cascade",
  "name": "Account to Multiple Entities",
  "parentEntity": "account",
  "isActive": true,
  "fieldMappings": [
    {
      "sourceField": "address1_city",
      "targetField": "address1_city",
      "isTriggerField": true
    }
  ],
  "relatedEntities": [
    {
      "entityName": "contact",
      "useRelationship": false,
      "lookupFieldName": "parentcustomerid",
      "filterCriteria": "statecode|eq|0"
    },
    {
      "entityName": "opportunity",
      "useRelationship": false,
      "lookupFieldName": "parentaccountid",
      "filterCriteria": "statecode|eq|0"
    },
    {
      "entityName": "quote",
      "useRelationship": false,
      "lookupFieldName": "customerid",
      "filterCriteria": "statecode|eq|0"
    }
  ]
}
```

### Pattern 2: Conditional Field Mapping

Different child entities get different fields:

```json
{
  "id": "conditional-mapping",
  "name": "Conditional Field Distribution",
  "parentEntity": "account",
  "isActive": true,
  "fieldMappings": [
    {
      "sourceField": "creditlimit",
      "targetField": "creditlimit",
      "isTriggerField": true
    },
    {
      "sourceField": "paymenttermscode",
      "targetField": "paymenttermscode",
      "isTriggerField": true
    }
  ],
  "relatedEntities": [
    {
      "entityName": "opportunity",
      "useRelationship": false,
      "lookupFieldName": "parentaccountid",
      "filterCriteria": "statecode|eq|0;salesstage|lt|4"
    },
    {
      "entityName": "quote",
      "useRelationship": false,
      "lookupFieldName": "customerid",
      "filterCriteria": "statecode|eq|1"
    }
  ]
}
```

### Pattern 3: Selective Trigger Fields

Only cascade when specific fields change, but copy all mapped fields:

```json
{
  "id": "selective-trigger",
  "name": "Selective Trigger Pattern",
  "parentEntity": "account",
  "isActive": true,
  "fieldMappings": [
    {
      "sourceField": "creditonhold",
      "targetField": "creditonhold",
      "isTriggerField": true
    },
    {
      "sourceField": "creditlimit",
      "targetField": "creditlimit",
      "isTriggerField": false
    },
    {
      "sourceField": "paymenttermscode",
      "targetField": "paymenttermscode",
      "isTriggerField": false
    }
  ],
  "relatedEntities": [
    {
      "entityName": "opportunity",
      "useRelationship": false,
      "lookupFieldName": "parentaccountid",
      "filterCriteria": "statecode|eq|0"
    }
  ]
}
```

This only executes when `creditonhold` changes, but copies all three fields.

## Performance Optimization Tips

### 1. Use Filtering Attributes

Always specify filtering attributes when registering the plugin step:

- Reduces unnecessary plugin executions
- Improves overall system performance
- Minimizes async job queue

### 2. Specific Column Sets

Modify the `CascadeService.cs` to use specific column sets instead of `new ColumnSet(true)`:

```csharp
var query = new QueryExpression(relatedConfig.EntityName)
{
    ColumnSet = new ColumnSet("statecode", "statuscode"), // Only needed columns
    NoLock = true
};
```

### 3. Batch Updates (Future Enhancement)

For large-scale updates, consider implementing batch operations using `ExecuteMultipleRequest`.

### 4. Caching Configuration

For high-volume scenarios, cache the configuration in memory to avoid repeated JSON parsing.

## Custom Field Type Handling

### Handling Money Fields

Money fields are automatically handled, but you can customize precision:

```csharp
if (value is Money money)
{
    updateEntity[kvp.Key] = new Money(Math.Round(money.Value, 2));
}
```

### Handling EntityReference Fields

EntityReference fields work automatically:

```json
{
  "sourceField": "primarycontactid",
  "targetField": "regardingobjectid",
  "isTriggerField": false
}
```

### Handling OptionSet Fields

OptionSet fields work automatically:

```json
{
  "sourceField": "industrycode",
  "targetField": "industrycode",
  "isTriggerField": true
}
```

## Error Handling Strategies

### Continue on Error (Current Implementation)

The plugin continues updating other records if one fails:

```csharp
foreach (var relatedRecord in relatedRecords)
{
    try
    {
        UpdateRelatedRecord(relatedRecord, values);
        successCount++;
    }
    catch (Exception ex)
    {
        errorCount++;
        _tracer.Error($"Failed to update record {relatedRecord.Id}", ex);
        // Continues with next record
    }
}
```

### Stop on Error (Modification)

To stop on first error, modify the `CascadeService.cs`:

```csharp
foreach (var relatedRecord in relatedRecords)
{
    UpdateRelatedRecord(relatedRecord, values);
    successCount++;
}
// Let exception propagate up
```

## Security Considerations

### Impersonation

To run updates as a specific user, modify the service creation:

```csharp
IOrganizationService service = serviceFactory.CreateOrganizationService(specificUserId);
```

### Auditing

All updates are automatically audited by Dataverse if auditing is enabled on the entities.

### Field-Level Security

The plugin respects field-level security. If the executing user doesn't have permission to a field, the update will fail for that record.

## Extending the Plugin

### Adding Custom Validation

Add validation logic in `CascadeService.cs` before updating:

```csharp
private void UpdateRelatedRecord(Entity relatedRecord, Dictionary<string, object> values)
{
    // Custom validation
    if (values.ContainsKey("creditlimit"))
    {
        var creditLimit = (Money)values["creditlimit"];
        if (creditLimit.Value < 0)
        {
            _tracer.Warning($"Skipping record {relatedRecord.Id} - negative credit limit");
            return;
        }
    }

    var updateEntity = new Entity(relatedRecord.LogicalName, relatedRecord.Id);
    // ... rest of method
}
```

### Adding Transformation Logic

Transform values before cascading:

```csharp
private Dictionary<string, object> GetValuesToCascade(Entity target, Entity preImage, CascadeConfiguration config)
{
    var values = new Dictionary<string, object>();

    foreach (var mapping in config.FieldMappings)
    {
        if (target.Contains(mapping.SourceField))
        {
            var value = target[mapping.SourceField];
            
            // Apply transformation
            if (mapping.SourceField == "revenue")
            {
                var money = (Money)value;
                value = new Money(money.Value * 1.1m); // 10% markup
            }
            
            values[mapping.TargetField] = value;
        }
    }

    return values;
}
```

### Adding Conditional Logic

Add conditions to field mappings:

```json
{
  "sourceField": "creditlimit",
  "targetField": "creditlimit",
  "isTriggerField": true,
  "condition": "revenue|gt|100000"
}
```

Then implement in code:

```csharp
private bool EvaluateCondition(Entity record, string condition)
{
    // Parse and evaluate condition
    // Return true if condition met
}
```

## Integration with Other Systems

### Logging to External System

Add custom logging to Application Insights, Azure Monitor, etc.:

```csharp
public void CascadeFieldValues(Entity target, Entity preImage, CascadeConfiguration config)
{
    _tracer.StartOperation("CascadeFieldValues");
    
    // Log to external system
    LogToApplicationInsights("CascadeStarted", new {
        EntityName = target.LogicalName,
        RecordId = target.Id,
        ConfigurationId = config.Id
    });

    try
    {
        // ... existing logic
    }
    catch (Exception ex)
    {
        LogToApplicationInsights("CascadeError", new {
            EntityName = target.LogicalName,
            Error = ex.Message
        });
        throw;
    }
}
```

### Triggering External Workflows

Trigger Power Automate flows after cascade:

```csharp
// After successful cascade
var request = new ExecuteWorkflowRequest
{
    WorkflowId = new Guid("your-workflow-id"),
    EntityId = target.Id
};
_service.Execute(request);
```

## Testing Strategies

### Unit Testing

Create unit tests using fake/mock services:

```csharp
[TestMethod]
public void TestCascadeFieldValues()
{
    // Arrange
    var fakeService = new FakeOrganizationService();
    var fakeTracer = new FakeTracingService();
    var cascadeService = new CascadeService(fakeService, new PluginTracer(fakeTracer, "Test"));
    
    // Act
    // ... execute cascade
    
    // Assert
    // ... verify results
}
```

### Integration Testing

Test in a sandbox environment with real data:

1. Create test parent record
2. Create test child records
3. Update parent record
4. Verify child records updated
5. Check trace logs

### Load Testing

For high-volume scenarios:

1. Create bulk test data
2. Use Plugin Profiler to measure performance
3. Monitor async job processing time
4. Adjust batch sizes if needed

## Troubleshooting Advanced Scenarios

### Circular Reference Detection

If parent-child relationships can be circular, add detection:

```csharp
private static readonly HashSet<Guid> _processingRecords = new HashSet<Guid>();

public void Execute(IServiceProvider serviceProvider)
{
    // ... setup code
    
    if (_processingRecords.Contains(context.PrimaryEntityId))
    {
        tracer.Warning("Circular reference detected, stopping execution");
        return;
    }
    
    _processingRecords.Add(context.PrimaryEntityId);
    
    try
    {
        // ... normal execution
    }
    finally
    {
        _processingRecords.Remove(context.PrimaryEntityId);
    }
}
```

### Large Record Sets

For parents with thousands of child records, implement pagination:

```csharp
private List<Entity> RetrieveRelatedRecords(Guid parentId, RelatedEntityConfig relatedConfig, CascadeConfiguration config)
{
    var allRecords = new List<Entity>();
    var query = BuildQuery(parentId, relatedConfig, config);
    query.PageInfo = new PagingInfo { Count = 5000, PageNumber = 1 };
    
    while (true)
    {
        var results = _service.RetrieveMultiple(query);
        allRecords.AddRange(results.Entities);
        
        if (!results.MoreRecords) break;
        
        query.PageInfo.PageNumber++;
        query.PageInfo.PagingCookie = results.PagingCookie;
    }
    
    return allRecords;
}
```

## Version Control & Configuration Management

### Managing Configurations

Store configurations in source control:

1. Create a `Configurations` folder in your repository
2. Save each configuration as a separate JSON file
3. Use naming convention: `{entity}-{purpose}.json`
4. Document in README which configurations are deployed to which environments

### Environment-Specific Configurations

Maintain different configurations per environment:

```
Configurations/
  Dev/
    account-to-contact.json
  Test/
    account-to-contact.json
  Prod/
    account-to-contact.json
```

### Configuration Versioning

Include version in configuration:

```json
{
  "id": "account-contact-v2",
  "name": "Account to Contact Cascade v2.0",
  "version": "2.0",
  "parentEntity": "account",
  ...
}
```

## Support & Maintenance

### Regular Monitoring

Set up scheduled checks:

- Daily: Review failed system jobs
- Weekly: Analyze trace logs for errors
- Monthly: Review performance metrics

### Configuration Updates

When updating configuration:

1. Export current configuration (backup)
2. Test new configuration in dev/test
3. Update plugin step configuration
4. Monitor for 24-48 hours
5. Document changes

### Plugin Updates

When updating plugin code:

1. Version the assembly (increment version number)
2. Test in non-production environment
3. Schedule deployment during low-usage period
4. Update assembly in Plugin Registration Tool
5. Verify existing plugin steps still work
6. Monitor trace logs for issues
