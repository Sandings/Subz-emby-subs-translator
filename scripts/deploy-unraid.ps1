param(
    [string]$RemoteHost = "",
    [int]$RemotePort = 0,
    [string]$RemoteUser = "",
    [string]$RemotePassword = "",
    [string]$RemoteHostKey = "",
    [string]$ConfigPath = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $ConfigPath = Join-Path $root "tmp\remote.unraid.json"
}

if (Test-Path $ConfigPath) {
    $remoteConfig = Get-Content -Raw $ConfigPath | ConvertFrom-Json
    if ($remoteConfig.unraid) {
        if ([string]::IsNullOrWhiteSpace($RemoteHost)) { $RemoteHost = [string]$remoteConfig.unraid.host }
        if ($RemotePort -le 0) { $RemotePort = [int]$remoteConfig.unraid.port }
        if ([string]::IsNullOrWhiteSpace($RemoteUser)) { $RemoteUser = [string]$remoteConfig.unraid.username }
        if ([string]::IsNullOrWhiteSpace($RemotePassword)) { $RemotePassword = [string]$remoteConfig.unraid.password }
        if ([string]::IsNullOrWhiteSpace($RemoteHostKey)) { $RemoteHostKey = [string]$remoteConfig.unraid.hostKey }
    }
}

if ([string]::IsNullOrWhiteSpace($RemoteHost)) { $RemoteHost = "sanding.life" }
if ($RemotePort -le 0) { $RemotePort = 55522 }
if ([string]::IsNullOrWhiteSpace($RemoteUser)) { $RemoteUser = "root" }

$sshTarget = "$RemoteUser@$RemoteHost"
$sshpass = Get-Command sshpass -ErrorAction SilentlyContinue
$useSshpass = -not [string]::IsNullOrWhiteSpace($RemotePassword) -and $null -ne $sshpass
$plinkCmd = Get-Command plink -ErrorAction SilentlyContinue
$pscpCmd = Get-Command pscp -ErrorAction SilentlyContinue
$usePuttyPassword = -not $useSshpass -and -not [string]::IsNullOrWhiteSpace($RemotePassword) -and $null -ne $plinkCmd -and $null -ne $pscpCmd
$hasPuttyHostKey = -not [string]::IsNullOrWhiteSpace($RemoteHostKey)

function Invoke-RemoteCommand {
    param([Parameter(Mandatory = $true)][string]$Command)

    if ($useSshpass) {
        & sshpass -p $RemotePassword ssh -p $RemotePort -o StrictHostKeyChecking=no $sshTarget $Command
    } elseif ($usePuttyPassword) {
        if ($hasPuttyHostKey) {
            & plink -batch -P $RemotePort -pw $RemotePassword -hostkey "$RemoteHostKey" $sshTarget $Command
        } else {
            & plink -batch -P $RemotePort -pw $RemotePassword $sshTarget $Command
        }
    } else {
        & ssh -p $RemotePort $sshTarget $Command
    }
}

function Copy-ToRemote {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    if ($useSshpass) {
        & sshpass -p $RemotePassword scp -P $RemotePort -o StrictHostKeyChecking=no $Source "${sshTarget}:$Destination"
    } elseif ($usePuttyPassword) {
        if ($hasPuttyHostKey) {
            & pscp -batch -P $RemotePort -pw $RemotePassword -hostkey "$RemoteHostKey" $Source "${sshTarget}:$Destination"
        } else {
            & pscp -batch -P $RemotePort -pw $RemotePassword $Source "${sshTarget}:$Destination"
        }
    } else {
        & scp -P $RemotePort $Source "${sshTarget}:$Destination"
    }
}

Write-Host "=== Step 1: Build Plugin ===" -ForegroundColor Cyan
$buildScript = Join-Path $PSScriptRoot "build-plugin.ps1"
& $buildScript
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$dllPath = Join-Path $root "SubZ.Plugin.dll"
if (-not (Test-Path $dllPath)) { throw "DLL not found at $dllPath" }

Write-Host "=== Step 2: Copy DLL to Unraid ===" -ForegroundColor Cyan
$remoteDir = "/tmp/subz_sync/"
Invoke-RemoteCommand "mkdir -p $remoteDir"
Copy-ToRemote $dllPath $remoteDir

Write-Host "=== Step 3: Deploy on Unraid ===" -ForegroundColor Cyan
$deployScriptLocal = Join-Path $env:TEMP "subz-deploy.sh"
@'
#!/bin/sh
set -e
pluginDir=""
for d in /mnt/user/DockerFile/emby/plugins /mnt/user/appdata/binhex-emby/plugins /mnt/user/appdata/emby/plugins /config/plugins /mnt/cache/appdata/binhex-emby/plugins /mnt/cache/appdata/emby/plugins; do
  if [ -d "$d" ]; then pluginDir="$d"; break; fi
done
if [ -z "$pluginDir" ]; then
  echo "PLUGIN_DIR_NOT_FOUND"
  exit 2
fi
mkdir -p "$pluginDir"
cp -f /tmp/subz_sync/SubZ.Plugin.dll "$pluginDir/SubZ.Plugin.dll"
echo "Deployed to: $pluginDir/SubZ.Plugin.dll"
ls -l "$pluginDir/SubZ.Plugin.dll"
'@ | Set-Content -Path $deployScriptLocal -Encoding Ascii

$deployScriptRemote = "/tmp/subz_sync/subz-deploy.sh"
Copy-ToRemote $deployScriptLocal $deployScriptRemote
Invoke-RemoteCommand "chmod +x $deployScriptRemote && sh $deployScriptRemote"

Write-Host "=== Step 4: Restart Emby ===" -ForegroundColor Cyan
Invoke-RemoteCommand "docker restart embyserver 2>/dev/null || docker restart emby 2>/dev/null || docker restart binhex-emby 2>/dev/null || /etc/rc.d/rc.emby restart 2>/dev/null || echo 'Please restart Emby manually'"

Write-Host "=== Done! ===" -ForegroundColor Green
