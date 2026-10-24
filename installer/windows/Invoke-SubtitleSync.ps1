[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SubtitlePath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms

function ConvertTo-SubtitleSeconds([string]$Timestamp) {
    if ($Timestamp -notmatch '^(?<hours>\d+):(?<minutes>\d{2}):(?<seconds>\d{2})[,.](?<milliseconds>\d{3})$') {
        throw "Invalid SRT timestamp: $Timestamp"
    }

    return ([int64]$Matches.hours * 3600) + ([int64]$Matches.minutes * 60) +
        [int64]$Matches.seconds + ([int64]$Matches.milliseconds / 1000.0)
}

function ConvertFrom-SubtitleSeconds([double]$Seconds) {
    $milliseconds = [math]::Round($Seconds * 1000, 0, [MidpointRounding]::AwayFromZero)
    if ($milliseconds -lt 0) {
        throw 'The estimated correction would create a timestamp before 00:00:00,000.'
    }

    $milliseconds = [int64]$milliseconds
    $hours = [math]::Floor($milliseconds / 3600000); $milliseconds %= 3600000
    $minutes = [math]::Floor($milliseconds / 60000); $milliseconds %= 60000
    $wholeSeconds = [math]::Floor($milliseconds / 1000); $milliseconds %= 1000
    return ('{0:00}:{1:00}:{2:00},{3:000}' -f $hours, $minutes, $wholeSeconds, $milliseconds)
}

function Read-SubtitleText([string]$Path) {
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $offset = 0
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        $encoding = [System.Text.UTF8Encoding]::new($true); $offset = 3
    }
    elseif ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) {
        $encoding = [System.Text.Encoding]::Unicode; $offset = 2
    }
    elseif ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF) {
        $encoding = [System.Text.Encoding]::BigEndianUnicode; $offset = 2
    }
    else {
        $encoding = [System.Text.UTF8Encoding]::new($false)
    }

    return [pscustomobject]@{ Text = $encoding.GetString($bytes, $offset, $bytes.Length - $offset); Encoding = $encoding }
}

function Get-CorrectedSubtitlePath([string]$Path) {
    $directory = Split-Path -Parent $Path
    $stem = [System.IO.Path]::GetFileNameWithoutExtension($Path)
    $extension = [System.IO.Path]::GetExtension($Path)
    for ($index = 0; ; $index++) {
        $suffix = if ($index -eq 0) { '-synced' } else { "-synced-$($index + 1)" }
        $candidate = Join-Path $directory "$stem$suffix$extension"
        if (-not (Test-Path -LiteralPath $candidate)) { return $candidate }
    }
}

if (-not (Test-Path -LiteralPath $SubtitlePath -PathType Leaf)) {
    [System.Windows.Forms.MessageBox]::Show('The selected subtitle file no longer exists.', 'Subtitle Syncer', 'OK', 'Error') | Out-Null
    exit 1
}

$dialog = New-Object System.Windows.Forms.OpenFileDialog
$dialog.Title = 'Choose the movie for this subtitle'
$dialog.Filter = 'Video files|*.mkv;*.mp4;*.avi;*.mov;*.webm;*.mpeg;*.mpg;*.wmv|All files|*.*'
if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
    exit 0
}

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$syncScript = Join-Path $repositoryRoot 'python\duration_alignment\sync.py'
if (-not (Test-Path -LiteralPath $syncScript)) {
    [System.Windows.Forms.MessageBox]::Show("Could not find the synchronization command at:`n$syncScript", 'Subtitle Syncer', 'OK', 'Error') | Out-Null
    exit 1
}

try {
    $output = & python $syncScript --video $dialog.FileName --subtitles $SubtitlePath 2>&1
    if ($LASTEXITCODE -ne 0) { throw ($output | Out-String) }
    $result = $output | Out-String | ConvertFrom-Json
    $source = Read-SubtitleText $SubtitlePath
    $timestampPattern = '(?m)^(?<start>\d{2,}:\d{2}:\d{2}[,.]\d{3})(?<arrow>\s+-->\s+)(?<end>\d{2,}:\d{2}:\d{2}[,.]\d{3})(?<suffix>[^\r\n]*)$'
    $replacementCount = 0
    $evaluator = [System.Text.RegularExpressions.MatchEvaluator]{
        param([System.Text.RegularExpressions.Match]$Match)
        $start = (ConvertTo-SubtitleSeconds $Match.Groups['start'].Value) + [double]$result.offset_seconds
        $end = (ConvertTo-SubtitleSeconds $Match.Groups['end'].Value) + [double]$result.offset_seconds
        if ($end -le $start) { throw 'The estimated correction would create an invalid subtitle interval.' }
        $script:replacementCount++
        return "$(ConvertFrom-SubtitleSeconds $start)$($Match.Groups['arrow'].Value)$(ConvertFrom-SubtitleSeconds $end)$($Match.Groups['suffix'].Value)"
    }
    $corrected = [System.Text.RegularExpressions.Regex]::Replace($source.Text, $timestampPattern, $evaluator)
    if ($replacementCount -eq 0) { throw 'No valid SRT timestamps were found.' }
    $correctedPath = Get-CorrectedSubtitlePath $SubtitlePath
    [System.IO.File]::WriteAllText($correctedPath, $corrected, $source.Encoding)
    [System.Windows.Forms.MessageBox]::Show("Created a corrected subtitle file:`n$correctedPath", 'Subtitle Syncer', 'OK', 'Information') | Out-Null
}
catch {
    [System.Windows.Forms.MessageBox]::Show("Synchronization could not finish.`n`n$($_.Exception.Message)", 'Subtitle Syncer', 'OK', 'Error') | Out-Null
    exit 1
}
