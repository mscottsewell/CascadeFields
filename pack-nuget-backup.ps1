param(
    [string]$Configuration = "Release",
    [string]$NugetSource = "https://api.nuget.org/v3/index.json",
    [string]$NugetApiKey = $env:NUGET_API_KEY,
    # Only push when explicitly requested.
    [switch]$Push,
    [switch]$SkipPush,
    [switch]$SkipDeploy,
    [switch]$SkipVersionBump,
    [switch]$SkipPluginRebuildIfUnchanged,
    [string]$XrmToolBoxPluginsPath = "$env:APPDATA\MscrmTools\XrmToolBox\Plugins"
)

# Builds, packs, and optionally deploys the configurator + plugin payload.
# When SkipVersionBump is set, the AssemblyInfo version is reused (helpful for CI repeatability).
# SkipDeploy keeps artifacts local; Push enables pushing to NuGet; SkipPush forces skipping push (overrides -Push).
# SkipPluginRebuildIfUnchanged skips rebuilding the Plugin project if it has no git changes since last commit.

$ErrorActionPreference = "Stop"

function Write-Info($message) {
    Write-Host $message -ForegroundColor Cyan
}

# Ensures we have a nuget.exe meeting the minimum supported version; downloads locally if missing/old.
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

function Update-ConfiguratorVersion {
    param(
        [string]$AssemblyInfoPath
    )

    if (-not (Test-Path $AssemblyInfoPath)) {
        throw "AssemblyInfo not found at $AssemblyInfoPath"
    }

    $content = Get-Content -Path $AssemblyInfoPath -Raw
    $assemblyVersionMatch = [regex]::Match($content, 'AssemblyVersion\("(?<ver>[^"]+)"\)')

    if (-not $assemblyVersionMatch.Success) {
        throw "AssemblyVersion attribute not found in $AssemblyInfoPath"
    }

    $currentVersion = $assemblyVersionMatch.Groups['ver'].Value

    try {
        $v = [Version]$currentVersion
    } catch {
        throw "Invalid version format '$currentVersion' in $AssemblyInfoPath"
    }

    $nextRevision = [Math]::Max(0, $v.Revision) + 1
    $nextVersion = [Version]::new($v.Major, $v.Minor, $v.Build, $nextRevision)
    $nextVersionString = "$($nextVersion.Major).$($nextVersion.Minor).$($nextVersion.Build).$($nextVersion.Revision)"

    $content = $content -replace 'AssemblyVersion\("[^"]*"\)', "AssemblyVersion(`"$nextVersionString`")"
    $content = $content -replace 'AssemblyFileVersion\("[^"]*"\)', "AssemblyFileVersion(`"$nextVersionString`")"

    Set-Content -Path $AssemblyInfoPath -Value $content -Encoding ascii

    Write-Info "Incremented Configurator version: $currentVersion -> $nextVersionString"

    return $nextVersionString
}

function Test-PluginHasChanges {
    param(
        [string]$PluginProjectDir
    )

    # Check if we're in a git repository
    try {
        $null = & git rev-parse --show-toplevel 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Not in a git repository; assuming Plugin has changes"
            return $true
        }
    } catch {
        Write-Warning "Git not available; assuming Plugin has changes"
        return $true
    }

    # Get relative path from git root to plugin directory
    $pluginPath = "CascadeFields.Plugin"

    # Check for uncommitted changes in the Plugin directory
    $status = & git status --porcelain -- $pluginPath 2>&1
    if ($status) {
        Write-Info "Plugin has uncommitted changes"
        return $true
    }

    # Check if Plugin files were modified in the last commit
    # This helps catch the case where changes were just committed
    try {
        $lastCommitFiles = & git diff-tree --no-commit-id --name-only -r HEAD -- $pluginPath 2>&1
        if ($LASTEXITCODE -eq 0 -and $lastCommitFiles) {
            Write-Info "Plugin was modified in the last commit"
            return $true
        }
    } catch {
        # If we can't determine, assume changes exist
        Write-Warning "Could not check last commit; assuming Plugin has changes"
        return $true
    }

    Write-Info "Plugin has no changes since last commit"
    return $false
}

function Update-PluginFileVersion {
    param(
        [string]$AssemblyInfoPath
    )

    if (-not (Test-Path $AssemblyInfoPath)) {
        throw "AssemblyInfo not found at $AssemblyInfoPath"
    }

    $content = Get-Content -Path $AssemblyInfoPath -Raw

    $fileVersionMatch = [regex]::Match($content, 'AssemblyFileVersion\("(?<ver>[^"]+)"\)')
    if (-not $fileVersionMatch.Success) {
        throw "AssemblyFileVersion attribute not found in $AssemblyInfoPath"
    }

    $currentFileVersion = $fileVersionMatch.Groups['ver'].Value

    try {
        $v = [Version]$currentFileVersion
    } catch {
        throw "Invalid file version format '$currentFileVersion' in $AssemblyInfoPath"
    }

    $nextRevision = [Math]::Max(0, $v.Revision) + 1
    $nextVersion = [Version]::new($v.Major, $v.Minor, $v.Build, $nextRevision)
    $nextVersionString = "$($nextVersion.Major).$($nextVersion.Minor).$($nextVersion.Build).$($nextVersion.Revision)"

    # Preserve AssemblyVersion; only bump AssemblyFileVersion
    $content = $content -replace 'AssemblyFileVersion\("[^"]*"\)', "AssemblyFileVersion(`"$nextVersionString`")"

    Set-Content -Path $AssemblyInfoPath -Value $content -Encoding ascii

    Write-Info "Incremented Plugin file version: $currentFileVersion -> $nextVersionString"

    return $nextVersionString
}

$projDir = Join-Path $PSScriptRoot "CascadeFields.Configurator"
$assemblyInfoPath = Join-Path $projDir "Properties/AssemblyInfo.cs"
$pluginProjDir = Join-Path $PSScriptRoot "CascadeFields.Plugin"
$pluginAssemblyInfoPath = Join-Path $pluginProjDir "Properties/AssemblyInfo.cs"

# Detect if Plugin has changes
$pluginHasChanges = Test-PluginHasChanges -PluginProjectDir $pluginProjDir
$skipPluginBuild = $SkipPluginRebuildIfUnchanged -and (-not $pluginHasChanges)

if (-not $SkipVersionBump) {
    Update-ConfiguratorVersion -AssemblyInfoPath $assemblyInfoPath | Out-Null

    # Only increment Plugin version if it has changes
    if ($pluginHasChanges) {
        Update-PluginFileVersion -AssemblyInfoPath $pluginAssemblyInfoPath | Out-Null
    } else {
        Write-Info "Plugin unchanged; skipping version increment"
    }
} else {
    Write-Info "SkipVersionBump enabled; using existing AssemblyInfo version."
}

Write-Info "Restoring and building solution ($Configuration)..."
& dotnet restore (Join-Path $PSScriptRoot "CascadeFields.sln")

if ($skipPluginBuild) {
    Write-Info "Plugin unchanged; skipping Plugin build (using existing binaries)"
    # Only build the Configurator project
    & dotnet build (Join-Path $projDir "CascadeFields.Configurator.csproj") -c $Configuration
} else {
    # Build entire solution (includes Plugin)
    & dotnet build (Join-Path $PSScriptRoot "CascadeFields.sln") -c $Configuration
}

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

# Preserve 4th segment when present (NuGet accepts 4-part numeric versions)
try {
    $v = [Version]$version
    if ($v.Revision -ge 0) {
        $version = "$($v.Major).$($v.Minor).$($v.Build).$($v.Revision)"
    } else {
        $version = "$($v.Major).$($v.Minor).$($v.Build)"
    }
} catch {
    # If parsing fails, leave as-is
}

$nugetExe = Ensure-NugetExe

$outputDir = Join-Path $PSScriptRoot "artifacts/nuget"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

# Refresh plugin payload into Assets/plugin for packaging and deployment
New-Item -ItemType Directory -Force -Path $assetsPluginDir | Out-Null
# Ensure legacy Assets\plugin folder is removed so we only ship Assets/DataversePlugin
$legacyPluginDir = Join-Path $projDir "Assets/plugin"
if (Test-Path $legacyPluginDir) {
    Remove-Item $legacyPluginDir -Recurse -Force -ErrorAction SilentlyContinue
}
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

if ($Push -and -not $SkipPush) {
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
    Write-Info "NuGet push skipped; package available at $packagePath"
}
