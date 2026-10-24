[CmdletBinding()]
param(
    [switch]$Remove
)

$ErrorActionPreference = 'Stop'
$verbKey = 'HKCU:\Software\Classes\SystemFileAssociations\.srt\shell\SubtitleSyncer'
$scriptPath = Join-Path $PSScriptRoot 'Invoke-SubtitleSync.ps1'

if ($Remove) {
    Remove-Item -LiteralPath $verbKey -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host 'Removed the Subtitle Syncer context-menu entry for SRT files.'
    exit 0
}

if (-not (Test-Path -LiteralPath $scriptPath)) {
    throw "The context-menu command was not found: $scriptPath"
}

New-Item -Path $verbKey -Force | Out-Null
New-ItemProperty -Path $verbKey -Name 'MUIVerb' -Value 'Sync with movie using Subtitle Syncer' -PropertyType String -Force | Out-Null
New-Item -Path (Join-Path $verbKey 'command') -Force | Out-Null
$command = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$scriptPath`" `"%1`""
New-ItemProperty -Path (Join-Path $verbKey 'command') -Name '(default)' -Value $command -PropertyType String -Force | Out-Null
Write-Host 'Added the Subtitle Syncer context-menu entry for SRT files.'
