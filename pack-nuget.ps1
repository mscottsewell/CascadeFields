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
    $nugetExe = $null

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

Write-Info "Building solution ($Configuration)..."
& dotnet build (Join-Path $PSScriptRoot "CascadeFields.sln") -c $Configuration

$projDir = Join-Path $PSScriptRoot "CascadeFields.Configurator"
$buildOutput = Join-Path $projDir "bin/$Configuration/net48"
$assemblyPath = Join-Path $buildOutput "CascadeFields.Configurator.dll"
$assetsPluginDir = Join-Path $projDir "Assets/plugin"
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
    if (-not (Test-Path $XrmToolBoxPluginsPath)) {
        throw "XrmToolBox plugins path not found: $XrmToolBoxPluginsPath"
    }

    $payload = @()
    $payload += Get-Item (Join-Path $buildOutput "CascadeFields.Configurator.dll")
    if (Test-Path (Join-Path $buildOutput "CascadeFields.Configurator.pdb")) {
        $payload += Get-Item (Join-Path $buildOutput "CascadeFields.Configurator.pdb")
    }
    $payload += Get-ChildItem $assetsPluginDir -File

    foreach ($file in $payload) {
        try {
            Copy-Item $file.FullName -Destination $XrmToolBoxPluginsPath -Force
        } catch {
            Write-Warning "Could not copy $($file.Name): $($_.Exception.Message)"
        }
    }
    Write-Info "Attempted to copy $($payload.Count) files to $XrmToolBoxPluginsPath"
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
