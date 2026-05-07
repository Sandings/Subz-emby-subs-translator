param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\SubZ.Plugin\SubZ.Plugin.csproj"
$outDir = Join-Path $root "artifacts\plugin"
$zipDir = Join-Path $root "artifacts"
$rootDll = Join-Path $root "SubZ.Plugin.dll"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet SDK is required. Install .NET SDK first."
}

Write-Host "Building plugin..."
dotnet build $project -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

$binDir = Join-Path $root "src\SubZ.Plugin\bin\$Configuration\netstandard2.0"
$dllPath = Join-Path $binDir "SubZ.Plugin.dll"

if (-not (Test-Path $dllPath)) {
    throw "Build completed but SubZ.Plugin.dll not found: $dllPath"
}

Remove-Item -Recurse -Force $outDir -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $outDir | Out-Null
Copy-Item $dllPath -Destination $outDir
Copy-Item $dllPath -Destination $rootDll -Force

$zipPath = Join-Path $zipDir "SubZ.Plugin-$Configuration.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $outDir "*") -DestinationPath $zipPath

Write-Host "Done."
Write-Host "DLL(bin): $dllPath"
Write-Host "DLL(root): $rootDll"
Write-Host "ZIP: $zipPath"
