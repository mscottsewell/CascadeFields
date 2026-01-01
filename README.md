# CascadeFields for Microsoft Dataverse

<img width="180" height="180" alt="CascadeFieldsConfigurator" src="https://github.com/user-attachments/assets/c6fcb5fa-de3e-41c1-b4a4-fb2d8863097e" align="left" style="margin-right: 10px;" />


A powerful, no-code solution for Microsoft Dataverse (Dynamics 365) that automatically cascades field values from parent records to related child records. Keep your data synchronized across relationships without writing a single line of code.

CascadeFields provides an **XrmToolBox** interface to configure a Dataverse plugin. The plugin will: ➡️Automatically cascade field changes from parent to child records. ➡️Populate child records when created or re-linked to a parent ➡️Filter which child records should be updated ➡️Control exactly which field changes trigger cascades ➡️Handle complex scenarios with multiple child entity types
✅ Achieve **50-98%** performance improvement over implementing the same feature using Power Automate.

<div style="clear: both;"></div>

## 🚀 Quick Start

**The easiest way to get started:**

1. Open **XrmToolBox**
2. Search for **CascadeFields Configurator** in the Tool Library
   
   <img width="800" alt="image" src="https://github.com/user-attachments/assets/53cf2e1f-7382-4c84-b6b0-c3183e310903" />

3. Install and launch the tool
4. Connect to your Dataverse environment
5. Choose an unmanaged solution containing the parent and child entities
6. Select a **Parent Entity** - the tool will automatically prompt you to add a child relationship if none are configured
7. Configure your fields to cascade using the visual interface
8. Add filters and trigger fields as needed
9. Click **Publish Configuration and Plug-in** - done!

No manual plugin registration, no JSON editing, no deployment hassles. The Configurator handles everything for you.

<img width="1300" alt="image" src="https://github.com/user-attachments/assets/d8008705-dd1c-4508-a830-3e2eae02a1e4" />


## Why CascadeFields?

### The Problem

In Dataverse, keeping related records in sync is challenging:

- Manual updates across hundreds of child records are error-prone
- Workflows and Power Automate can be slow and complex for large datasets
- Custom plugins require development expertise and ongoing maintenance
- Changes to parent records don't automatically propagate to children

### The Solution

CascadeFields provides a **visual, no-code** interface to:

- ✅ Automatically cascade field changes from parent to child records
- ✅ Populate child records when created or re-linked to a parent
- ✅ Filter which child records should be updated
- ✅ Control exactly which field changes trigger cascades
- ✅ Handle complex scenarios with multiple child entity types
- ✅ Achieve 50-98% performance improvement over traditional Power Automate flows
- ✅ Deploy quickly with minimal effort using the XrmToolBox Configurator
- ✅ Maintain easily with built-in validation, session persistence, and update management
- ✅ Leverage open-source code for customization if needed
- ✅ Benefit from comprehensive documentation and examples
- ✅ Ensure security with user-context execution and injection protection
- ✅ Monitor performance with detailed tracing and logging
- ✅ Avoid infinite loops with automatic depth protection
- ✅ Keep everything solution-aware for easy deployment and versioning
- ✅ Save time and resources compared to custom development

### Perfect For

- **Admins** who need to keep related data synchronized
- **ProDevs** who want a faster alternative to custom code
- **Consultants** delivering solutions without development costs
- **Power Users** managing complex data relationships

## 📚 Documentation

- **[Quick Start Guide](QUICKSTART.md)** - Get running in 5 minutes
- **[Configuration Guide](CONFIGURATION.md)** - Best practices and advanced scenarios
- **[Examples Folder](Examples/)** - Ready-to-use templates for common use cases

## Key Features

### For End Users

- **Zero Code Required**: Visual configuration interface - no JSON, no coding
- **Real-Time Sync**: Changes cascade immediately (async by default for performance)
- **Smart Filtering**: Target specific child records with simple filter criteria
- **Bulk Operations**: Efficiently updates hundreds of child records
- **Audit Trail**: Comprehensive logging for compliance and troubleshooting

### For Administrators

- **One-Click Publishing**: Configurator automatically creates all required plugin steps
- **Solution-Aware**: Components automatically added to your solution
- **Session Restore**: Resume where you left off across sessions
- **Validation**: Pre-publish checks prevent configuration errors
- **Update Management**: Built-in plugin version checking and updates

### For Developers

- **Comprehensive Documentation**: Microsoft-quality XML documentation throughout
- **Open Source**: Full source code available for customization
- **Extensible Architecture**: Clean MVVM pattern for easy modifications
- **Well-Tested**: Battle-tested in production environments
- **Performance Optimized**: Batch operations and efficient caching

## How It Works

CascadeFields operates on three core mechanisms:

### 1. Parent Updates → Child Cascades

When a parent record is updated:

1. Plugin detects changes to configured trigger fields
2. Retrieves related child records (filtered if specified)
3. Updates mapped fields on all matching children
4. Uses batch operations for optimal performance

### 2. Child Creation → Parent Population

When a child record is created with a parent lookup:

1. Plugin retrieves parent record
2. Copies configured field values to the new child
3. Executes synchronously in the same transaction

### 3. Child Re-linking → Parent Synchronization

When a child's parent lookup is changed:

1. Plugin detects the lookup change
2. Loads new parent record
3. Updates child fields to match new parent
4. Executes synchronously in the same transaction

## Common Use Cases

### Account-to-Contact Synchronization

Keep contact information synchronized with their parent account for simplicity:

- Cascade address changes to all contacts
- Update industry, status, credit, or classification fields
- Maintain consistent branding or territory assignments

### Multi-Level Hierarchies

Handle complex organizational structures by cascading references enabling easier filtering and navigability visibility:

- Department → Team → Employee cascades
- Project → Phase → Task synchronization
- Product Line → Product → SKU updates

## Installation Options

### Option 1: XrmToolBox Configurator (Recommended)

**For most users - no compilation required:**

1. Open XrmToolBox (download from [xrmtoolbox.com](https://www.xrmtoolbox.com) if needed)
2. Click **Tool Library** (puzzle piece icon)
3. Search for **CascadeFields Configurator**
4. Click **Install**
5. Launch the tool and connect to your environment
6. Start configuring cascades immediately

**What you get:**

- Visual configuration interface
- Automatic plugin deployment
- Built-in validation
- Session persistence
- Update notifications

### Option 2: Build from Source (For Customization)

**For developers who want to customize the tool:**

```bash
# Clone the repository
git clone https://github.com/mscottsewell/CascadeFields.git
cd CascadeFields

# Build the solution
dotnet build -c Release

# Outputs:
# Plugin: CascadeFields.Plugin\bin\Release\net462\CascadeFields.Plugin.dll
# Configurator: CascadeFields.Configurator\bin\Release\...
```

**Build Script:**

The `pack-nuget.ps1` script handles building, versioning, and packaging:

```powershell
# Standard build (increments both Configurator and Plugin versions)
.\pack-nuget.ps1 -SkipPush

# Smart build (only increments Plugin version if it has changes)
.\pack-nuget.ps1 -SkipPush

# Skip Plugin rebuild if unchanged (uses existing binaries)
.\pack-nuget.ps1 -SkipPush -SkipPluginRebuildIfUnchanged
```

The script automatically detects changes to the Plugin project using git and:

- Only increments the Plugin's file version when there are actual code changes
- Optionally skips rebuilding the Plugin entirely if unchanged (with `-SkipPluginRebuildIfUnchanged`)
- Always increments the Configurator version (unless `-SkipVersionBump` is used)

**Requirements:**

- Visual Studio 2019 or later
- .NET Framework 4.6.2 SDK
- .NET 8.0 SDK (for build tools)

## Configuration Made Easy

### Using the Configurator (Recommended)

The visual interface guides you through configuration:
<img width="1212" height="483" alt="Interface" src="https://github.com/user-attachments/assets/99cb1296-dd45-4c97-b05c-96cdb24f8171" />

1. **Select Solution** - Choose the unmanaged solution containing the parent and child entities you're configuring.  
Note: The plugin assembly and related step components will be added to this solution upon publishing.
2. **Choose Parent Entity** - Pick the entity to monitor for changes. The tool automatically loads any existing configuration for that entity. If no child relationships are configured, you'll be prompted to add one immediately.
3. **Add Relationships** - Click **Add Relationship** in the ribbon to select which child entities to cascade to. The list is filtered to entities in the solution with a many-to-one relationship to the parent.
4. **Map Fields** - Select the parent fields you want copied to child fields
5. **Set Filters** - Optionally filter which children receive updates.  
Note: Use criteria like 'statecode = 0' (Status = Active) to limit updates to only active records.
6. **Publish Configuration and Plug-in** - One click deploys everything

### User Interface Overview

The Configurator is divided into two main panes:

**Ribbon Buttons:**

The toolbar ribbon at the top contains the following buttons:

<img width="885" height="70" alt="Toolbar" src="https://github.com/user-attachments/assets/64d06ce0-d835-474c-a5f8-e432053df4d0" />

| Button | Purpose |
| --- | --- |
| **Retrieve Configured Entity** | Load an existing configuration from Dataverse. Auto-loads if only one parent is configured; shows picker for multiple. |
| **Export JSON** | Save the current configuration to a JSON file for backup, sharing, or version control. |
| **Import JSON** | Load a configuration from a JSON file. Useful for restoring backups or sharing between environments. |
| **Add Relationship** | Add a new child entity relationship to the current parent configuration. |
| **Remove Relationship** | Remove the currently selected child entity relationship tab. |
| **Publish Configuration and Plug-in** | Validate and publish the configuration. Creates or updates the plugin assembly and all plugin steps. |

**Left Pane - Configuration & Logging:**

The left pane contains three tabs:

<img width="1000" alt="Thre Tabs" src="https://github.com/user-attachments/assets/2743847e-8301-4bf3-8360-ecc7d363a8d6" />


| Tab | Description |
| --- | --- |
| **Configuration** | Main configuration controls: select Solution, select Parent Entity, and manage configuration checkboxes. |
| **Log** | Real-time activity log showing operations, metadata loading, and status messages. Useful for troubleshooting. |
| **JSON Preview** | Live preview of the generated JSON configuration. Updates automatically as you make changes. |

**Configuration Tab Controls:**

- **Solution Selector:** Dropdown to choose the unmanaged solution where plugin components will be added.
- **Parent Entity Selector:** Dropdown to select the parent entity to configure. Filters to entities in the selected solution.

Below these controls are three important checkboxes:

| Checkbox | Description | Recommendation |
| --- | --- | --- |
| **Is Active** | When checked, the configuration is active and the plugin will process cascades. | Uncheck and publish to temporarily disable cascading without removing the configuration. |
| **Auto-delete Successful System Jobs** | Automatically deletes successful async operations (parent update step) from System Jobs to prevent System Job storage bloat. (Only applies to parent step since child steps run synchronously.) | ✅ Disable during development and testing to monitor successful jobs. <br> ⚠️ **Enable in production** to avoid System Job bloat |
| **Enable Detailed Tracing** | When checked, the plugin writes verbose trace logs for every execution. <br> > **Note (Org Setting Required):** This checkbox only controls how much the plug-in writes to Dataverse tracing (`ITracingService`). To actually capture and view these traces, your Dataverse environment must have **Plug-in trace log** enabled (see “Plugin Trace Logs” below). | ✅ Enable during development and testing. <br> ⚠️ **Disable in production** to reduce log volume and improve performance. |



**Right Pane - Relationship Configuration:**

The right pane displays tabs for each configured child relationship. Each tab contains:

- **Field Mappings Grid** - Map source (parent) fields to target (child) fields, with a checkbox to mark trigger fields
- **Filter Criteria Grid** - Define conditions to limit which child records are updated

### Retrieve Configured Entity

Click **Retrieve Configured Entity** in the ribbon to load an existing configuration:

- If **only one parent entity** has a configuration, it loads automatically without prompting
- If **multiple parent entities** are configured, a selection dialog appears showing all configured parents with their child relationships
- Select a parent row to load its complete configuration (all child relationships, field mappings, and filters)

> **Note:** Clicking a child row in the selector will automatically highlight its parent row, ensuring you always load the complete parent configuration.

The Configurator automatically:

- Creates parent Update step (async, post-operation)
- Creates child Create steps (sync, pre-operation)
- Creates child Relink steps (sync, pre-operation)
- Adds all components to your selected solution
- Validates configuration before publishing
- Provides detailed progress feedback

### Advanced: Manual Configuration

For those who prefer working with JSON or need programmatic control, see [CONFIGURATION.md](CONFIGURATION.md) for complete JSON schema and manual registration instructions.

## What Gets Deployed

When you publish, the Configurator creates these plugin steps:

| Step | Entity | Message | Stage | Mode | Trigger |
| --- | --- | --- | --- | --- | --- |
| Parent Update | (Your parent entity) | Update | Post-Operation | Async | Trigger field changes |
| Child Create | (Each child entity) | Create | Pre-Operation | Sync | Record creation |
| Child Relink | (Each child entity) | Update | Pre-Operation | Sync | Parent lookup change |

**All steps:**

- Are added to your selected solution
- Include proper pre-images for change detection
- Use appropriate filtering for optimal performance
- Include your complete configuration as secure JSON

## Performance & Security

### Performance Optimizations

- **Batch Updates**: Uses ExecuteMultiple for up to 98% speed improvement
- **Smart Caching**: Metadata cached for faster execution
- **Async by Default**: Parent cascades don't block user operations
- **Filtering**: Only processes records that match your criteria
- **Change Detection**: Only cascades when trigger fields actually change

### Security Features

- **User Context**: Executes with user's permissions (no privilege escalation)
- **Field Validation**: Prevents injection attacks in filter criteria
- **Depth Protection**: Automatic loop detection and prevention
- **Audit Compliance**: Full tracing for compliance requirements
- **Solution-Aware**: All components tracked in your solution

## Troubleshooting

### Using the Configurator

The Configurator includes built-in diagnostics:

- Pre-publish validation catches configuration errors
- Real-time status messages during operations
- Detailed error messages with resolution hints
- Plugin version checking and update recommendations

### Plugin Trace Logs

For detailed execution information:

1. Ensure your environment has **Plug-in trace log** enabled:
   - **Off**: no trace logs are stored
   - **Exception**: logs are stored only when the plug-in throws an exception
   - **All**: logs are stored for all executions (recommended while troubleshooting)
1. Enable **Enable Detailed Tracing** in the Configurator for verbose `INFO/WARNING/DEBUG` logs (errors are always written).
1. View trace logs in Dataverse:
   - **Modern**: Power Platform admin center → Environments → (your environment) → Settings → Plug-in trace log / Plug-in trace logs
   - **Classic (legacy UI)**: **Settings** → **Plug-in Trace Log** in Dataverse
1. Filter by plugin name: `CascadeFields.Plugin`
1. Review execution details and timing

**Where logs appear:**

- **Configurator Log tab**: local, real-time UI log from the Configurator (useful while publishing/configuring)
- **Dataverse Plug-in Trace Log**: server-side execution traces emitted via `ITracingService` (what “Enable Detailed Tracing” affects)
- **System Jobs**: only for async executions (Parent Update step). Helpful to confirm success/failure and timing.

### Common Issues

| Symptom | Likely Cause | Solution |
| --- | --- | --- |
| Nothing cascades | No trigger field changes | Verify mapped fields are marked as triggers |
| Some children not updated | Filter criteria too restrictive | Review filter settings in Configurator |
| Slow performance | No filtering attributes | Use Configurator to set filtering properly |
| Plugin not found | Assembly not registered | Use Configurator to update/republish plugin |

## Best Practices

### Configuration

1. ✅ **Start Simple**: Configure one relationship first, test, then expand
2. ✅ **Use Filters**: Limit updates to only the children that need them
3. ✅ **Mark Triggers**: Only mark fields as triggers if they should cascade
4. ✅ **Test First**: Always test in development before production
5. ✅ **Document**: Use the configuration name field to describe purpose

### Deployment

1. ✅ **Use Solutions**: Always add components to a managed solution
2. ✅ **Version Control**: Export your solution regularly
3. ✅ **Monitor Traces**: Check plugin traces after initial deployment
4. ✅ **Stage Rollout**: Deploy to UAT before production
5. ✅ **Keep Updated**: Check for Configurator updates in XrmToolBox

### Maintenance

1. ✅ **Review Regularly**: Audit configurations quarterly
2. ✅ **Monitor Performance**: Check execution times in traces
3. ✅ **Clean Up**: Remove obsolete configurations
4. ✅ **Stay Current**: Update plugin when new versions available

## Contributing

We welcome contributions! The codebase includes:

- **Comprehensive XML Documentation**: All public APIs documented
- **Clean Architecture**: MVVM pattern for easy understanding
- **Modern C#**: Leverages latest language features
- **Automated Build**: PowerShell scripts for packaging

To contribute:

1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Ensure XML documentation is complete
5. Submit a pull request

## Support

- **Documentation**: Start with [QUICKSTART.md](QUICKSTART.md) and [CONFIGURATION.md](CONFIGURATION.md)
- **Examples**: Check the [Examples folder](Examples/) for templates
- **Issues**: Report bugs or request features via GitHub Issues
- **Community**: Share your use cases and configurations

## Technical Details

### Architecture

- **Plugin**: .NET Framework 4.6.2, runs in Dataverse sandbox
- **Configurator**: WinForms application integrated with XrmToolBox
- **Services**: Clean separation of concerns (Metadata, Configuration, Settings)
- **Models**: Domain models shared between plugin and configurator
- **Documentation**: Microsoft-quality XML comments throughout

### Plugin Capabilities

- Change detection with pre-image comparison
- Batch operations with ExecuteMultiple
- Filter criteria with injection protection
- Metadata caching for performance
- Comprehensive tracing and error handling
- Loop detection and prevention

### Configurator Features

- Visual relationship configuration with intuitive tabbed interface
- Real-time JSON preview with live updates
- Three-tab left pane: Configuration, Log, and JSON Preview
- Pre-publish validation catches errors before deployment
- Session persistence resumes where you left off
- Solution component management with automatic additions
- Plugin version checking and update recommendations
- Progress reporting with detailed status messages
- Auto-load configuration when switching parent entities
- Auto-prompt to add relationships for unconfigured parents
- Smart retrieve functionality (auto-loads single configurations, shows picker for multiple)

## License

Licensed under the [MIT License](LICENSE). Free for commercial and personal use.

Copyright © 2026 Scott Sewell

## Version History

### Latest Release

- Full-featured XrmToolBox Configurator
- Visual configuration interface
- Automatic plugin deployment
- Session persistence
- Comprehensive validation
- Solution-aware component management
- Microsoft-quality documentation

### Previous Releases

See [CHANGELOG.md](CHANGELOG.md) for complete version history.

---

**Ready to get started?** Install the CascadeFields Configurator from XrmToolBox and configure your first cascade in minutes!
