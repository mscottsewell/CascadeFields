param(
    [string]$Configuration = "Release",
    [string]$NugetSource = "https://api.nuget.org/v3/index.json",
    [string]$NugetApiKey = $env:NUGET_API_KEY,
    [switch]$SkipPush,
    [switch]$SkipDeploy,
    [string]$XrmToolBoxPluginsPath = "$env:APPDATA\MscrmTools\XrmToolBox\Plugins"
)

$ErrorActionPreference = "Stop"

function Write-Info($message) {
    Write-Host $message -ForegroundColor Cyan
}

function Ensure-NugetExe {
    $minimumNugetVersion = [Version]"5.10.0"

    $nuget = Get-Command nuget.exe -ErrorAction SilentlyContinue
    if ($nuget) {
        try {
            $nugetVersion = [Version]([System.Diagnostics.FileVersionInfo]::GetVersionInfo($nuget.Source).FileVersion)
            if ($nugetVersion -ge $minimumNugetVersion) {
                return $nuget.Source
            }
        } catch {
            Write-Warning "Unable to read nuget.exe version from $($nuget.Source); falling back to local copy."
        }
    }

    $toolsDir = Join-Path $PSScriptRoot ".nuget"
    New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null
    $nugetPath = Join-Path $toolsDir "nuget.exe"

    $downloadRequired = $true
    if (Test-Path $nugetPath) {
        try {
            $localVersion = [Version]([System.Diagnostics.FileVersionInfo]::GetVersionInfo($nugetPath).FileVersion)
            if ($localVersion -ge $minimumNugetVersion) {
                $downloadRequired = $false
            }
        } catch {
            Write-Warning "Unable to read nuget.exe version from $nugetPath; re-downloading."
        }
    }

    if ($downloadRequired) {
        Write-Info "Downloading nuget.exe..."
        Invoke-WebRequest -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile $nugetPath
    }

    return $nugetPath
}

Write-Info "Restoring and building solution ($Configuration)..."
& dotnet restore (Join-Path $PSScriptRoot "CascadeFields.sln")
& dotnet build (Join-Path $PSScriptRoot "CascadeFields.sln") -c $Configuration

$projDir = Join-Path $PSScriptRoot "CascadeFields.Configurator"
$buildOutput = Join-Path $projDir "bin/$Configuration/net462"
$assemblyPath = Join-Path $buildOutput "CascadeFields.Configurator.dll"
$assetsPluginDir = Join-Path $projDir "Assets/DataversePlugin"
if (-not (Test-Path $assemblyPath)) {
    throw "Build output not found at $assemblyPath"
}

$versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($assemblyPath)
$version = $versionInfo.ProductVersion
if ([string]::IsNullOrWhiteSpace($version)) {
    $version = $versionInfo.FileVersion
}
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Unable to determine version from $assemblyPath"
}

# NuGet does not accept build metadata (+suffix); strip anything after '+'
if ($version -like "*+*") {
    $version = $version.Split('+')[0]
}

# NuGet requires SemVer (three numeric segments). Trim a 4th segment if present.
try {
    $v = [Version]$version
    $version = "$($v.Major).$($v.Minor).$($v.Build)"
} catch {
    # If parsing fails, leave as-is
}

$nugetExe = Ensure-NugetExe

$outputDir = Join-Path $PSScriptRoot "artifacts/nuget"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

# Refresh plugin payload into Assets/plugin for packaging and deployment
New-Item -ItemType Directory -Force -Path $assetsPluginDir | Out-Null
$pluginBuild = Join-Path $PSScriptRoot "CascadeFields.Plugin/bin/$Configuration/net462"
if (-not (Test-Path $pluginBuild)) {
    throw "Plugin build output not found at $pluginBuild"
}
Copy-Item (Join-Path $pluginBuild "CascadeFields.Plugin.dll") -Destination $assetsPluginDir -Force
if (Test-Path (Join-Path $pluginBuild "CascadeFields.Plugin.pdb")) {
    Copy-Item (Join-Path $pluginBuild "CascadeFields.Plugin.pdb") -Destination $assetsPluginDir -Force
}

$nuspecPath = Join-Path $projDir "CascadeFields.Configurator.nuspec"
if (-not (Test-Path $nuspecPath)) {
    throw "Nuspec not found at $nuspecPath"
}

Write-Info "Packing CascadeFields.Configurator v$version..."
& $nugetExe pack $nuspecPath -Version $version -OutputDirectory $outputDir -BasePath $projDir -NoPackageAnalysis
if ($LASTEXITCODE -ne 0) {
    throw "nuget.exe pack failed with exit code $LASTEXITCODE"
}

$packagePath = Join-Path $outputDir "CascadeFields.Configurator.$version.nupkg"
if (-not (Test-Path $packagePath)) {
    throw "NuGet package not created at $packagePath"
}

if (-not $SkipDeploy) {
    Write-Info "Deploying locally for XrmToolBox testing..."
    
    # Ensure root plugins path exists
    if (-not (Test-Path $XrmToolBoxPluginsPath)) {
        New-Item -ItemType Directory -Force -Path $XrmToolBoxPluginsPath | Out-Null
    }
    
    # Deploy to CascadeFieldsConfigurator subdirectory for Assets
    $pluginSubfolder = Join-Path $XrmToolBoxPluginsPath "CascadeFieldsConfigurator"
    if (-not (Test-Path $pluginSubfolder)) {
        New-Item -ItemType Directory -Force -Path $pluginSubfolder | Out-Null
    }

    # Copy main DLL and PDB to ROOT plugins folder only (XrmToolBox discovers plugins here)
    try {
        Copy-Item (Join-Path $buildOutput "CascadeFields.Configurator.dll") -Destination $XrmToolBoxPluginsPath -Force
        Write-Info "Copied CascadeFields.Configurator.dll to Plugins root"
    } catch {
        Write-Warning "Could not copy CascadeFields.Configurator.dll: $($_.Exception.Message)"
    }
    
    if (Test-Path (Join-Path $buildOutput "CascadeFields.Configurator.pdb")) {
        try {
            Copy-Item (Join-Path $buildOutput "CascadeFields.Configurator.pdb") -Destination $XrmToolBoxPluginsPath -Force
            Write-Info "Copied CascadeFields.Configurator.pdb to Plugins root"
        } catch {
            Write-Warning "Could not copy CascadeFields.Configurator.pdb: $($_.Exception.Message)"
        }
    }
    
    # Copy Plugin DLL to root Plugins folder so XrmToolBox can resolve the assembly reference
    $pluginDll = Join-Path $pluginBuild "CascadeFields.Plugin.dll"
    if (Test-Path $pluginDll) {
        try {
            Copy-Item $pluginDll -Destination $XrmToolBoxPluginsPath -Force
            Write-Info "Copied CascadeFields.Plugin.dll to Plugins root"
        } catch {
            Write-Warning "Could not copy CascadeFields.Plugin.dll: $($_.Exception.Message)"
        }
    }

    # Copy Assets folder to CascadeFieldsConfigurator subfolder
    $assetsDestination = Join-Path $pluginSubfolder "Assets"
    if (Test-Path $assetsDestination) {
        Remove-Item $assetsDestination -Recurse -Force -ErrorAction SilentlyContinue
    }
    
    $assetsSource = Join-Path $projDir "Assets"
    if (Test-Path $assetsSource) {
        try {
            Copy-Item $assetsSource -Destination $assetsDestination -Recurse -Force
            Write-Info "Copied Assets folder to $assetsDestination"
        } catch {
            Write-Warning "Could not copy Assets folder: $($_.Exception.Message)"
        }
    } else {
        Write-Warning "Assets folder not found at $assetsSource"
    }
    
    Write-Info "Deployment complete: DLL in Plugins root, Assets in CascadeFieldsConfigurator subfolder"
}

if (-not $SkipPush) {
    if ([string]::IsNullOrWhiteSpace($NugetApiKey)) {
        throw "NuGet API key not provided. Supply -NugetApiKey or set NUGET_API_KEY."
    }

    Write-Info "Pushing package to $NugetSource..."
    & $nugetExe push $packagePath -ApiKey $NugetApiKey -Source $NugetSource
    if ($LASTEXITCODE -ne 0) {
        throw "nuget.exe push failed with exit code $LASTEXITCODE"
    }
    Write-Info "Package pushed successfully."
} else {
    Write-Info "SkipPush enabled; package available at $packagePath"
}
