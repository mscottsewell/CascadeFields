# Quick Start Guide

Get up and running with CascadeFields in minutes using the XrmToolBox Configurator.

## Prerequisites

- XrmToolBox with CascadeFields Configurator plugin installed
- Access to a Dataverse environment
- Security roles with update permissions on parent and child entities
- An **unmanaged solution** containing the entities you want to map (both parent and child entities)

> **💡 Pro Tip:** Start simple with a single field mapping and evaluate the behavior to ensure it's what you're looking for before adding more complex configurations.

## Setup Steps

### 1. Install the Configurator

1. Open **XrmToolBox**
2. Go to **Tools Library** (or Plugin Store)
3. Search for **"CascadeFields Configurator"**
4. Click **Install**

### 2. Configure Your Cascade

1. Open **XrmToolBox** and launch **CascadeFields Configurator**
2. Connect to your environment
3. Select your **unmanaged solution** containing the entities you want to map
   - This solution must include both parent and child entities
   - The plugin assembly and plugin steps will be added to this solution when published
4. Select your **parent entity** (e.g., Account) from the entities in the solution
   - If the entity has an existing configuration, it loads automatically
   - If no child relationships are configured, you'll be prompted to add one immediately
5. Select your **child entity and relationship** (e.g., Contact : Parent Account)
   - Note: This list is filtered to entities in the solution that have a many-to-one relationship to the parent
6. Add **field mappings**:
   - Source field: parent field name
   - Target field: child field name
   - Trigger: check if this field should trigger the cascade when changed on the parent
   - **Start with just one or two mappings** to verify behavior first
7. Add **filter criteria** to limit which child records are updated:
   - Recommended: `statecode|eq|0` (active records only)
   - This ensures only active and editable records are affected
   - Additional filters can be added (e.g., `statuscode|eq|1`)

### 3. Publish Configuration

1. Click **Publish Configuration and Plug-in** in the ribbon
2. The selected solution will be updated to include:
   - Plugin assembly (CascadeFields.Plugin)
   - Plugin steps (parent and child steps)
3. Wait for confirmation: "Publish complete: parent and child steps upserted"

**What gets created:**

- **Parent Update step**: Post-operation, Asynchronous (updates child records after parent changes)
- **Child Create step**: Pre-operation, Synchronous (populates fields when child is created)
- **Child Update step**: Pre-operation, Synchronous (updates fields when child is relinked to different parent)

## Understanding the User Interface

### Ribbon Buttons

The toolbar ribbon at the top of the Configurator contains the following buttons (left to right):

| Button | Purpose |
| --- | --- |
| **Retrieve Configured Entity** | Load an existing configuration from Dataverse. If only one parent entity is configured, it loads automatically. If multiple are configured, a selection dialog appears. |
| **Export JSON** | Save the current configuration to a JSON file for backup, sharing, or version control. |
| **Import JSON** | Load a configuration from a JSON file. Useful for restoring backups or sharing configurations between environments. |
| **Add Relationship** | Add a new child entity relationship to the current parent entity configuration. Opens a dialog to select the child entity and lookup field. |
| **Remove Relationship** | Remove the currently selected child entity relationship tab from the configuration. |
| **Publish Configuration and Plug-in** | Validate and publish the configuration to Dataverse. Creates or updates the plugin assembly and all plugin steps. |

### Left Pane: Tabs and Controls

The left pane is organized into three tabs:

| Tab | Contents & Purpose |
| --- | --- |
| **Configuration** | Main configuration controls: Solution selector, Parent Entity selector, and configuration checkboxes. |
| **Log** | Real-time activity log showing operations, metadata loading progress, and status messages for troubleshooting. |
| **JSON Preview** | Live preview of the generated JSON configuration. Updates automatically as you make changes. |

**Configuration Tab Controls:**

- **Solution Selector:** Dropdown to choose the unmanaged solution where plugin components will be added.
- **Parent Entity Selector:** Dropdown to select the parent entity to configure. Filters to entities in the selected solution.

Below these controls are three important checkboxes:

| Checkbox | What it does | When to use |
| --- | --- | --- |
| **Is Active** | Controls whether the plugin processes cascades for this configuration. | Leave checked for normal operation. Uncheck to temporarily pause cascading without removing the configuration. |
| **Auto-delete Successful System Jobs** | Automatically deletes successful async operations (parent update step) from System Jobs to prevent clutter. Only applies to the parent update step since child steps run synchronously. | Leave checked to keep System Jobs clean. Uncheck if you want to monitor successful jobs. |
| **Enable Detailed Tracing** | Writes verbose trace logs for every plugin execution to the Plugin Trace Log. | ✅ Enable during development and testing. ⚠️ Disable in production to reduce log volume and improve performance. |

### Right Pane

The right pane shows a tab for each configured child relationship. Each tab contains:

- **Field Mappings Grid** (top) - Map source fields from the parent to target fields on the child. Check "Trigger Field" for fields that should initiate cascades when changed.
- **Filter Criteria Grid** (bottom) - Define conditions to limit which child records receive updates (e.g., only active records).

## Retrieve Configured Entity

To load an existing configuration:

1. Click **Retrieve Configured Entity** in the ribbon
2. **If only one parent entity** has a configuration, it loads automatically
3. **If multiple parent entities** are configured, a selection dialog appears:
   - Shows all configured parent entities with their child relationships
   - Click a parent row (or any of its child rows) to select it
   - Click **OK** to load the complete configuration

> **Tip:** When you select a parent entity from the dropdown, any existing configuration for that entity loads automatically. If no child relationships exist, you'll be prompted to add one.

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

### 5. Monitor and Debug

**View Plugin Trace Logs:**

Plugin trace logs are your best tool for debugging and understanding what CascadeFields is doing.

1. In your Dataverse environment, navigate to:
   - **Settings** → **Customizations** → **Plug-in Trace Log**
   - Or search for "Plugin Trace Log" in the search bar
2. Filter the view:
   - **Type Name** contains `CascadeFieldsPlugin`
   - Sort by **Created On** (newest first)
3. Open a log entry to see detailed execution information:
   - Configuration being used
   - Records being processed
   - Fields being updated
   - Any errors or warnings

**Enable Detailed Tracing (Development Only):**

For more detailed logs during testing:

1. Open your configuration in the Configurator
2. Check the **Enable Detailed Tracing** checkbox in the Configuration tab
3. Click **Publish Configuration and Plug-in** to apply the change
4. **⚠️ Important:** Do not leave detailed tracing enabled in production environments as it impacts performance and creates excessive log records

**Check System Jobs (for parent updates):**

Since parent updates run asynchronously:

1. Navigate to **Settings** → **System Jobs**
2. Filter by:
   - **System Job Type** = "Plug-in"
   - **Regarding** = your parent record
3. Verify status: **Succeeded**
4. If failed, click the job to see error details

## Removing or Modifying Mappings

### Remove a Mapping

#### Using the Configurator

1. Open **CascadeFields Configurator** in XrmToolBox
2. Connect to your environment
3. Load the configuration for the parent entity
4. Select the child relationship tab you want to remove
5. Click **Remove Relationship** in the ribbon, or modify field mappings as needed
6. Click **Publish Configuration and Plug-in** to update

#### Uninstalling Completely

To remove the plugin assembly and all associated steps:

#### Option 1: Using Power Apps Maker Portal

1. Navigate to [make.powerapps.com](https://make.powerapps.com)
2. Select your environment
3. Go to **Solutions** → select your solution
4. Find **Plugin Assemblies** → select **CascadeFields.Plugin**
5. Click **Remove** → **Delete** (this will also remove all associated steps)

#### Option 2: Using Plugin Registration Tool

1. Open the **Plugin Registration Tool**
2. Connect to your environment
3. Find **CascadeFields.Plugin** in the assembly list
4. Right-click → **Unregister**
5. Confirm to remove the assembly and all steps

> **Note:** Always test removal in a development environment first. Removing the plugin will stop all cascade operations immediately.

## Troubleshooting

| Issue | Solution |
| --- | --- |
| Plugin doesn't fire | Check filtering attributes match trigger fields |
| Child records not updating | Verify lookup field name is correct |
| Permission errors | Ensure users have update rights on child entities |
| Values not copying | Check field names are exact (case-sensitive) |

## Best Practices

✅ **Do:**

- Start with simple mappings and test thoroughly
- Use filter criteria to limit updates to active/editable records only
- Test in a development environment first
- Monitor plugin trace logs during initial testing
- Keep your unmanaged solution up to date with entity changes

⚠️ **Don't:**

- Leave detailed tracing enabled in production
- Create circular cascades (A → B → A)
- Map fields with different data types without testing
- Deploy directly to production without testing

## Next Steps

- Review [README.md](README.md) for detailed documentation and architecture
- Check [CONFIGURATION.md](CONFIGURATION.md) for advanced configuration patterns
- Explore the `Examples\` folder for more sample configurations

## Getting Help

1. ✅ Check Plugin Trace Logs (most detailed)
2. ✅ Verify JSON configuration is valid
3. ✅ Confirm `lookupFieldName` is specified
4. ✅ Test in dev environment first
