# CascadeFields for Microsoft Dataverse

<img width="180" height="180" alt="CascadeFieldsConfigurator" src="https://github.com/user-attachments/assets/c6fcb5fa-de3e-41c1-b4a4-fb2d8863097e" align="left" style="margin-right: 10px;" />


A powerful, no-code solution for Microsoft Dataverse (Dynamics 365) that automatically cascades field values from parent records to related child records. Keep your data synchronized across relationships without writing a single line of code.

CascadeFields provides an **XrmToolBox** interface to configure a Dataverse plugin. The plugin will: ➡️Automatically cascade field changes from parent to child records. ➡️Populate child records when created or re-linked to a parent ➡️Filter which child records should be updated ➡️Control exactly which field changes trigger cascades ➡️Handle complex scenarios with multiple child entity types
✅ Achieve **50-98%** performance improvement over PA flows.

<div style="clear: both;"></div>

## 🚀 Quick Start

**The easiest way to get started:**

1. Open **XrmToolBox**
2. Search for **CascadeFields Configurator** in the Tool Library
   <img width="800" alt="image" src="https://github.com/user-attachments/assets/53cf2e1f-7382-4c84-b6b0-c3183e310903" />

4. Install and launch the tool
5. Connect to your Dataverse environment
6. Configure your cascades using the visual interface
7. Click **Publish** - done!

No manual plugin registration, no JSON editing, no deployment hassles. The Configurator handles everything for you.

<img width="1400" height="789" alt="image" src="https://github.com/user-attachments/assets/957b0391-5e44-4189-8fa2-d37ce081a1b8" />


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
- ✅ Achieve 50-98% performance improvement over traditional flows

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

Keep contact information synchronized with their parent account:

- Cascade address changes to all contacts
- Update industry, status, or classification fields
- Maintain consistent branding or territory assignments

### Opportunity-to-Product Cascading

Ensure products stay aligned with opportunity details:

- Cascade expected close dates
- Update discount tiers or pricing changes
- Synchronize sales territories

### Case-to-Activity Propagation

Keep activities in sync with their parent case:

- Cascade priority changes to all related activities
- Update subject or category information
- Maintain consistent SLA tracking

### Multi-Level Hierarchies

Handle complex organizational structures:

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

**Requirements:**

- Visual Studio 2019 or later
- .NET Framework 4.6.2 SDK
- .NET 8.0 SDK (for build tools)

## Configuration Made Easy

### Using the Configurator (Recommended)

The visual interface guides you through configuration:

1. **Select Solution** - Choose where components will be added
2. **Choose Parent Entity** - Pick the entity to monitor for changes
3. **Add Relationships** - Select which child entities to cascade to
4. **Map Fields** - Drag and drop to map parent fields to child fields
5. **Set Filters** - Optionally filter which children receive updates
6. **Mark Triggers** - Specify which parent field changes trigger cascades
7. **Publish** - One click deploys everything

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

1. Go to **Settings** → **Plug-in Trace Log** in Dataverse
2. Filter by plugin name: `CascadeFields.Plugin`
3. Review execution details and timing

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

- Visual relationship configuration
- Real-time JSON preview
- Pre-publish validation
- Session persistence
- Solution component management
- Plugin version checking
- Progress reporting

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
