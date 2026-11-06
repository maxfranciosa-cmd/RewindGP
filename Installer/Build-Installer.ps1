# Builds the Rewind GP setup exe locally: a Release build of the app, then Inno Setup's compiler
# over Installer\RewindGP.iss. The version comes from the built AMS2ChEd.exe (the csproj's
# FileVersion) unless given. Output: Installer\Output\RewindGP-Setup-v<version>.exe
#
#   powershell -ExecutionPolicy Bypass -File Installer\Build-Installer.ps1 [-Tag v2.4] [-SkipBuild]

param(
    [string]$Tag,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceDir = Join-Path $repoRoot 'AMS2ChEd\bin\Release\net8.0-windows'
$script = Join-Path $PSScriptRoot 'RewindGP.iss'
$outputDir = Join-Path $PSScriptRoot 'Output'

$iscc = @(
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    throw 'Inno Setup 6 not found. Install it from https://jrsoftware.org/isdl.php and run this again.'
}

if (-not $SkipBuild) {
    & dotnet build (Join-Path $repoRoot 'AMS2ChEd\AMS2ChEd.csproj') -c Release
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed ($LASTEXITCODE)" }
}

$info = (Get-Item (Join-Path $sourceDir 'AMS2ChEd.exe')).VersionInfo
$version = '{0}.{1}.{2}' -f $info.FileMajorPart, $info.FileMinorPart, $info.FileBuildPart
if (-not $Tag) { $Tag = 'v{0}.{1}' -f $info.FileMajorPart, $info.FileMinorPart }

& $iscc "/DSourceDir=$sourceDir" "/DAppVersion=$version" "/DTag=$Tag" "/O$outputDir" $script
if ($LASTEXITCODE -ne 0) { throw "ISCC failed ($LASTEXITCODE)" }

Write-Host "Setup written to $(Join-Path $outputDir "RewindGP-Setup-$Tag.exe")"
