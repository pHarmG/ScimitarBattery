Param(
    [string]$Runtime = "win-x64",
    [string]$Framework = "net8.0-windows10.0.19041.0"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $repoRoot "ScimitarBattery\\ScimitarBattery.csproj"
$artifactsDir = Join-Path $repoRoot "artifacts"
$publishDir = Join-Path $artifactsDir "ScimitarBattery-$Runtime"
$zipPath = Join-Path $artifactsDir "ScimitarBattery-$Runtime.zip"

New-Item -ItemType Directory -Force -Path $artifactsDir | Out-Null
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }

dotnet publish $project `
  -c Release `
  -f $Framework `
  -r $Runtime `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o $publishDir

if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

Write-Host "Created: $zipPath"
