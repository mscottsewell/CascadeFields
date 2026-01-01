# ProDev Guide (Build, Manual Setup, Contributing)

This guide is for pro developers and contributors. It covers:

- Building the solution from source
- Packaging and release workflow
- Manual Dataverse plug-in registration (without the XrmToolBox Configurator)
- Contributing guidelines

> If you are an admin using XrmToolBox, start with [QUICKSTART.md](QUICKSTART.md).

## Building the Solution

### Prerequisites

- Visual Studio 2019+ (or equivalent MSBuild tooling)
- .NET Framework 4.6.2 targeting pack/developer pack
- PowerShell (for build/pack scripts)
- Git

### Build (Visual Studio)

1. Open `CascadeFields.sln`.
2. Build the solution in `Release`.

### Build Outputs

- Configurator (XrmToolBox tool): `CascadeFields.Configurator/bin/Release/`
- Plug-in assembly: `CascadeFields.Plugin/bin/Release/` (targeting .NET Framework 4.6.2)

## Packaging (NuGet)

This repository includes a packaging script:

- `pack-nuget.ps1`

Typical usage:

- Build/package without pushing: `./pack-nuget.ps1 -SkipPush`

Artifacts are placed under:

- `artifacts/nuget/`

## Manual Dataverse Plug-in Registration (Without XrmToolBox)

The XrmToolBox Configurator is the recommended way to deploy because it:

- Registers the plug-in assembly
- Creates/updates the required steps
- Adds components to your selected solution
- Writes the JSON configuration into the correct step configuration

Only use manual registration when you explicitly need it (e.g., custom deployment automation).

### Where the JSON Configuration Lives

CascadeFields loads configuration JSON from the **plug-in step Unsecure Configuration** field.

See the implementation in [CascadeFields.Plugin/Helpers/ConfigurationManager.cs](CascadeFields.Plugin/Helpers/ConfigurationManager.cs).

### Step Overview (Conceptual)

A complete deployment typically includes:

- A **Parent Update** step (post-operation, asynchronous)
- A **Child Create** step (pre-operation, synchronous)
- A **Child Update** step used for *relink* scenarios (pre-operation, synchronous)

These recommendations align with the plugin’s built-in guidance in [CascadeFields.Plugin/CascadeFieldsPlugin.cs](CascadeFields.Plugin/CascadeFieldsPlugin.cs).

### Recommended Step Registration

Parent-side cascade:

- **Message**: `Update`
- **Primary entity**: your configured parent entity (e.g., `account`)
- **Stage**: Post-operation (40)
- **Mode**: Asynchronous (recommended for performance)
- **Filtering attributes**: the trigger fields you expect to cascade from (limits executions)

Child-side population/relink:

- **Message**: `Create` and `Update`
- **Primary entity**: each configured child entity (e.g., `contact`)
- **Stage**: Pre-operation (20)
- **Mode**: Synchronous (required so values are applied in-transaction)
- **Filtering attributes**: include only the lookup field that references the parent entity (so only relinks trigger on `Update`)

### Recommended Registration Notes

- Use filtering attributes on steps to reduce executions (limit to trigger fields and lookup fields as appropriate).
- Ensure required pre-images are present for change detection (parent step) and relink detection (child update step).
- Store the configuration JSON in the step **Unsecure Configuration**.

### Tracing and Log Storage

The configuration flag `enableTracing` controls how much the plug-in writes to `ITracingService`.

To actually **store and view** Dataverse traces, the environment setting **Plug-in trace log** must be enabled:

- **Off**: no traces stored
- **Exception**: traces stored only when the plug-in throws
- **All**: traces stored for all executions (recommended while troubleshooting)

To view Plug-in Trace Logs:

- **Modern**: Power Platform admin center → Environments → (your environment) → Settings → Plug-in trace log / Plug-in trace logs
- **Classic (legacy UI)**: Dataverse **Settings** → **Customizations** → **Plug-in Trace Log**

## Contributing

### Workflow

1. Fork the repository.
2. Create a feature branch from `master`.
3. Make focused changes with clear commit messages.
4. Update documentation when behavior or UI changes.
5. Open a pull request.

### Documentation Structure

- [README.md](README.md): high-level overview only
- [QUICKSTART.md](QUICKSTART.md): complete XrmToolBox admin guide
- [CONFIGURATION.md](CONFIGURATION.md): JSON schema, patterns, and configuration reference
- [PRODEV.md](PRODEV.md): build/pack/manual registration/contributing

### Repo Layout (High Level)

- `CascadeFields.Configurator/`: XrmToolBox UI and publishing logic
- `CascadeFields.Plugin/`: Dataverse plug-in runtime
- `Examples/`: sample configuration JSON files
