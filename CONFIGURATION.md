# Configuration Guide

## Configuration Best Practices

### ✅ Recommended: Explicit Lookup Fields

Always use explicit lookup field names for reliability:

```json
{
  "parentEntity": "account",
  "isActive": true,
  "relatedEntities": [
    {
      "entityName": "contact",
      "useRelationship": false,
      "lookupFieldName": "parentcustomerid",
      "fieldMappings": [...]
    }
  ]
}
```

**Benefits:**
- ✅ Works with parent updates, child creates, and child relinks
- ✅ No metadata lookups needed
- ✅ Better performance
- ✅ More reliable
- ✅ Easier to troubleshoot

### Common Lookup Field Names

| Parent Entity | Child Entity | Lookup Field |
|---------------|--------------|--------------|
| account | contact | `parentcustomerid` |
| account | opportunity | `accountid` or `parentaccountid` |
| contact | contact | `parentcontactid` |
| incident | incidentresolution | `incidentid` |
| opportunity | opportunityproduct | `opportunityid` |

**Tip:** To find lookup field names:
1. Open Advanced Find
2. Select child entity
3. Add condition for parent entity relationship
4. Field name shown in editor

## Filter Criteria

### Syntax

```text
field|operator|value;field2|operator2|value2
```

### Supported Operators

| Operator | Aliases | Description |
|----------|---------|-------------|
| `eq` | `equal`, `=` | Equal to |
| `ne` | `notequal`, `!=` | Not equal to |
| `gt` | `greaterthan`, `>` | Greater than |
| `lt` | `lessthan`, `<` | Less than |
| `null` | - | Is null |
| `notnull` | - | Is not null |
| `like` | - | Pattern match |

### Examples

**Active records only:**
```json
"filterCriteria": "statecode|eq|0"
```

**Multiple conditions:**
```json
"filterCriteria": "statecode|eq|0;revenue|gt|50000"
```

**Check for null:**
```json
"filterCriteria": "primarycontactid|notnull|null"
```

## Field Mappings

### Basic Mapping

```json
{
  "sourceField": "address1_city",
  "targetField": "address1_city",
  "isTriggerField": true
}
```

### Trigger Fields

- Set `isTriggerField: true` for fields that should trigger the cascade
- At least one trigger field recommended
- If no trigger fields specified, ALL changes trigger cascade

### Type Conversions

The plugin automatically handles type conversions:

**Lookup/OptionSet → Text:**
- Lookup: Uses display name (or formatted value, or ID as fallback)
- OptionSet: Uses label text (or numeric value as fallback)
- Automatically truncates if target field is too short

**Same-Type Mappings:**
- Text → Text
- Number → Number
- DateTime → DateTime
- Lookup → Lookup
- OptionSet → OptionSet
- Money → Money

## Configuration Patterns

### Pattern 1: Single Parent, Multiple Children

Cascade the same fields to different child entity types:

```json
{
  "parentEntity": "account",
  "relatedEntities": [
    {
      "entityName": "contact",
      "lookupFieldName": "parentcustomerid",
      "useRelationship": false,
      "fieldMappings": [
        {
          "sourceField": "address1_city",
          "targetField": "address1_city",
          "isTriggerField": true
        }
      ]
    },
    {
      "entityName": "opportunity",
      "lookupFieldName": "accountid",
      "useRelationship": false,
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

### Pattern 2: Different Mappings Per Child

Each child entity gets different fields:

```json
{
  "parentEntity": "account",
  "relatedEntities": [
    {
      "entityName": "contact",
      "lookupFieldName": "parentcustomerid",
      "useRelationship": false,
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
    },
    {
      "entityName": "opportunity",
      "lookupFieldName": "accountid",
      "useRelationship": false,
      "fieldMappings": [
        {
          "sourceField": "creditlimit",
          "targetField": "budgetamount",
          "isTriggerField": true
        }
      ]
    }
  ]
}
```

### Pattern 3: Filtered Children

Only update specific child records:

```json
{
  "entityName": "opportunity",
  "lookupFieldName": "accountid",
  "useRelationship": false,
  "filterCriteria": "statecode|eq|0;salesstage|lt|4",
  "fieldMappings": [...]
}
```

This only updates active opportunities in early sales stages.

### Pattern 4: Selective Triggers

Only cascade when specific fields change, but copy all mapped fields:

```json
{
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
  ]
}
```

Cascade only triggers when `creditonhold` changes, but all three fields are copied.

## Production Configuration

### Disable Detailed Tracing

For production environments, disable verbose tracing:

```json
{
  "parentEntity": "account",
  "isActive": true,
  "enableTracing": false,
  "relatedEntities": [...]
}
```

**Note:** Error logging is always enabled regardless of this setting.

### Configuration Validation

Before deploying:

1. ✅ Validate JSON syntax (use jsonlint.com)
2. ✅ Verify parent entity name is correct
3. ✅ Verify all child entity names are correct
4. ✅ Verify all lookup field names are correct
5. ✅ Verify source/target field names match exactly (case-sensitive)
6. ✅ Test filter criteria in Advanced Find first
7. ✅ At least one field mapping per related entity
8. ✅ Test in non-production environment

## Performance Considerations

### Optimize Query Performance

1. **Use filtering attributes** - Register plugin steps with only trigger fields
2. **Apply filter criteria** - Target specific child records
3. **Limit child record count** - Plugin has 5000 record safety limit
4. **Use batch updates** - Automatically enabled (50 records/batch)

### Monitor Performance

Check execution time in trace logs:

```text
[INFO] Update complete: 150 successful, 0 failed
[INFO] === Plugin Execution Completed Successfully === [+1250ms]
```

For large record sets (>1000 children):
- Execution time typically 2-5 seconds
- 98% reduction in API calls vs. individual updates
- Asynchronous processing prevents UI blocking

## Troubleshooting

### Plugin Doesn't Execute

**Parent Update:**
- Check filtering attributes include trigger fields
- Verify `statecode` filter matches child records
- Ensure parent record has related children

**Child Create/Update:**
- Verify `lookupFieldName` is specified and correct
- Check if child step is registered (look for "Child Create" and "Child Relink" steps)
- Ensure lookup field is populated on create or changed on update

### Fields Not Cascading

- Verify field names are exact (case-sensitive)
- Check source field exists on parent
- Check target field exists on child
- Verify field types are compatible
- Review trace logs for errors

### Permission Errors

- Ensure executing user has Read on parent entity
- Ensure executing user has Update on child entities
- Check field-level security settings

### Filter Criteria Not Working

- Test filter in Advanced Find first
- Check field names are correct (no typos)
- Verify operator syntax (use `eq`, not `equals`)
- Review trace logs for filter parsing errors

## Complete Example

```json
{
  "id": "account-to-contact-full",
  "name": "Account to Contact - Production",
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
          "sourceField": "address1_stateorprovince",
          "targetField": "address1_stateorprovince",
          "isTriggerField": true
        },
        {
          "sourceField": "address1_postalcode",
          "targetField": "address1_postalcode",
          "isTriggerField": false
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

This configuration:
- Cascades when city or state changes
- Copies all four fields when triggered
- Only updates active contacts
- Disabled verbose tracing for production
- Supports parent updates, child creates, and child relinks
