# CascadeFields Configurator - Build, Pack, and Deploy

This guide covers how to build the solution, create a NuGet package, and deploy locally for XrmToolBox testing using the provided script [pack-nuget.ps1](pack-nuget.ps1).

## Prerequisites

- PowerShell 7+ (pwsh)
- .NET SDK (to run `dotnet build`)
- Close XrmToolBox before local deploy to avoid locked plugin files.
- Optional: NuGet API key (for pushes) set via `-NugetApiKey` or environment variable `NUGET_API_KEY`.

## Core Script

[pack-nuget.ps1](pack-nuget.ps1) builds the solution, packs the configurator, optionally deploys DLLs to your local XrmToolBox plugins folder, and optionally pushes to a NuGet feed. It auto-downloads `nuget.exe` if needed.

Common parameters:

- `-Configuration` (default `Release`)
- `-SkipDeploy` to skip copying DLLs to the local XrmToolBox plugins folder.
- `-SkipPush` to skip pushing the created package.
- `-NugetApiKey` and `-NugetSource` for publishing.
- `-XrmToolBoxPluginsPath` to override the plugins folder (default uses `%APPDATA%/MscrmTools/XrmToolBox/Plugins`).

## Usage Recipes

### 1) Build + Local Deploy + Pack (no push)

For iterative testing with XrmToolBox:

```pwsh
pwsh -File ./pack-nuget.ps1 -Configuration Release -SkipPush
```

Results:

- Builds both projects.
- Creates NuGet at `artifacts/nuget/CascadeFields.Configurator.<version>.nupkg`.
- Copies DLLs into your XrmToolBox plugins folder (skips locked files with warnings).

### 2) Pack Only (no deploy, no push)

For generating the package without touching XrmToolBox:

```pwsh
pwsh -File ./pack-nuget.ps1 -Configuration Release -SkipDeploy -SkipPush
```

Result: NuGet at `artifacts/nuget/`.

### 3) Pack and Push to NuGet (skip local deploy)

Publish to nuget.org or another feed:

```pwsh
pwsh -File ./pack-nuget.ps1 -Configuration Release \ 
     -SkipDeploy \ 
     -NugetApiKey "<your-key>" \ 
     -NugetSource "https://api.nuget.org/v3/index.json"
```

Notes:

- You can omit `-NugetApiKey` if `NUGET_API_KEY` is set.
- Use a different `-NugetSource` for private feeds.

### 4) Local Deploy Only

If you only want to copy the latest DLLs into XrmToolBox:

```pwsh
pwsh -File ./pack-nuget.ps1 -Configuration Release -SkipPush
```

(Closes XrmToolBox to avoid lock warnings.)

## Outputs

- NuGet package: `artifacts/nuget/CascadeFields.Configurator.<version>.nupkg`
- Built assemblies: `CascadeFields.Configurator/bin/<Configuration>/net48/` and `CascadeFields.Plugin/bin/<Configuration>/net462/`

## Tips

- If files are locked during deploy, close XrmToolBox and rerun with `-SkipPush`.
- Version comes from the configurator assembly; build metadata (+suffix) is stripped to satisfy NuGet versioning.
- The nuspec is at [CascadeFields.Configurator/CascadeFields.Configurator.nuspec](CascadeFields.Configurator/CascadeFields.Configurator.nuspec).
