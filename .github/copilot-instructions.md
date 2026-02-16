# Copilot Instructions — CascadeFields

## Project Overview

CascadeFields is a **Microsoft Dataverse plug-in** paired with an **XrmToolBox
Configurator**. It cascades field values from parent records to related child
records based on JSON configuration stored in plug-in step unsecure
configuration.

Two projects live in this repository:

| Project | Purpose | Key entry point |
| --- | --- | --- |
| `CascadeFields.Plugin` | Dataverse plug-in runtime | `CascadeFieldsPlugin.cs` |
| `CascadeFields.Configurator` | XrmToolBox UI for building/publishing config | `CascadeFieldsConfiguratorPlugin.cs` |

## Build & Tooling

- **Solution**: `CascadeFields.sln` (Visual Studio 2019+)
- **Target framework**: .NET Framework 4.6.2 (`net462`) for both projects
- **Plugin assembly**: strong-name signed (`CascadeFields.snk`)
- **Packaging**: `pack-nuget.ps1` — run with `-SkipPush` for local builds;
  artifacts go to `artifacts/nuget/`
- **No unit-test project exists** at this time

## C# Coding Conventions

- **Naming**: PascalCase for types, properties, and public members; `_camelCase`
  for private fields
- **XML docs**: Use `<summary>` / `<remarks>` / `<param>` / `<returns>` on all
  public and protected members
- **Access modifiers**: Always explicit (`public`, `private`, `internal`,
  `protected`)
- **Braces**: Allman style (opening brace on its own line)
- **Indentation**: 4 spaces, no tabs
- **Nullable**: The Configurator project enables `<Nullable>enable</Nullable>`;
  the Plugin project does not

## Architecture

### Plugin (`CascadeFields.Plugin`)

- Implements `IPlugin` (Dataverse SDK)
- Namespace root: `CascadeFields.Plugin`
- Sub-namespaces: `Helpers`, `Models`, `Services`
- Configuration is deserialized from the step's unsecure config JSON via
  `ConfigurationManager`
- Uses `ITracingService` for diagnostics; tracing depth controlled by
  `enableTracing` config flag

### Configurator (`CascadeFields.Configurator`)

- XrmToolBox plug-in discovered via MEF (`[Export]`, `[ExportMetadata]`)
- **MVVM pattern**: `ViewModelBase` with `SetProperty<T>` /
  `INotifyPropertyChanged`; commands via `RelayCommand` / `AsyncRelayCommand`
- Namespace root: `CascadeFields.Configurator`
- Sub-namespaces: `Controls`, `Dialogs`, `Helpers`, `Infrastructure`, `Models`,
  `Services`, `ViewModels`, `Views`
- Settings persisted through `ISettingsRepository` / `SettingsStorage`

## Key Dependencies

| Package | Version | Used by |
| --- | --- | --- |
| `Microsoft.CrmSdk.CoreAssemblies` | 9.0.2.56 | Plugin + Configurator |
| `Microsoft.CrmSdk.XrmTooling.CoreAssembly` | 9.1.1.32 | Configurator |
| `XrmToolBoxPackage` | 1.2023.10.67 | Configurator |
| `Newtonsoft.Json` | 13.0.3 | Both |

## Configuration JSON

Configuration lives in the plug-in step **Unsecure Configuration** field. The
canonical schema and patterns are documented in `CONFIGURATION.md`. Key points:

- Root properties: `parentEntity`, `isActive`, `enableTracing`,
  `cascadeOnParentUpdate`, `cascadeOnChildCreate`, `cascadeOnChildRelink`,
  `deleteAsyncOperationIfSuccessful`, `bypassCustomPluginExecution`,
  `relatedEntities[]`
- Each related entity has `entityName`, `lookupFieldName`, optional
  `filterCriteria`, and `fieldMappings[]`
- Trigger fields (`isTriggerField: true`) control which parent changes initiate
  a cascade

## Documentation Structure

| File | Audience |
| --- | --- |
| `README.md` | High-level overview |
| `QUICKSTART.md` | XrmToolBox admin guide |
| `CONFIGURATION.md` | JSON schema, patterns, reference |
| `PRODEV.md` | Build, packaging, manual registration, contributing |
| `CHOICE_FIELD_MAPPING.md` | Guide for mapping choice/lookup labels to text fields |
| `BUGFIX_CHOICE_FIELD_NAME.md` | Companion "name" field bug fix details |

When changing behavior or UI, update the relevant doc file(s).

## Contributing Guidelines

1. Branch from `master`
2. Keep changes focused; use clear commit messages
3. Update documentation when behavior or UI changes
4. Build in `Release` configuration before submitting a PR
