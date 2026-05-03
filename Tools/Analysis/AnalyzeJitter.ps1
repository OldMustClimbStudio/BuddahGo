<#
.SYNOPSIS
  Phase 7 jitter log analyzer. Ingests Editor.log files containing
  [D-VIS HEARTBEAT] (BuddahPredictionVisualShakeProbe) and
  [D-REC HEARTBEAT] (BuddahPredictionReconcileSnapProbe) lines,
  groups by (path, peer, owner), and applies Stage 3 design Q3
  absolute gates (G3.1-G3.6) + secondary regression checks
  (G3.SEC.1/SEC.2).

.DESCRIPTION
  ASCII-only per methodology Rule 2 sub-clause "Tooling change
  execution-test" #3. Path tag (no-latency vs LatencySim) inferred
  from filename: file containing $LatencySimTag is treated as
  LatencySim run; otherwise baseline.

.PARAMETER LogPaths
  One or more Editor.log files captured during Phase 7 SMOKE.

.PARAMETER BaselineLatencyTag
  Filename tag identifying the baseline (no-LatencySim) run.
  Default: 'no-latency'. Used to label the output table column;
  not used for filename matching (any file lacking $LatencySimTag
  is treated as baseline).

.PARAMETER LatencySimTag
  Filename substring identifying the LatencySim run. Default: '100ms'.

.PARAMETER OutputJson
  Optional. If set, write the full result set as JSON to this path
  in addition to stdout markdown.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string[]] $LogPaths,
    [string] $BaselineLatencyTag = 'no-latency',
    [string] $LatencySimTag = '100ms',
    [string] $OutputJson
)

$ErrorActionPreference = 'Stop'

# Stage 3 design Q3 absolute thresholds (per design G3.1-G3.6).
$Gate_PosDp99MinusDavg = 0.30
$Gate_PosDmaxMinusDavg = 0.60
$Gate_RotDp99 = 8.0
$Gate_RotDmax = 15.0
$Gate_RecSnapP99 = 0.50
$Gate_RecSnapMax = 1.50

function Parse-Heartbeats {
    param([string[]] $Lines)
    $result = New-Object System.Collections.ArrayList
    for ($i = 0; $i -lt $Lines.Count; $i++) {
        $line = $Lines[$i]
        # [D-VIS HEARTBEAT] is a 3-line block.
        if ($line -match '\[D-VIS HEARTBEAT\] frame=(\d+) owner=(\w+) window=(\d+)') {
            $frame = [int]$Matches[1]
            $owner = ($Matches[2] -ieq 'true' -or $Matches[2] -ieq 'True')
            $window = [int]$Matches[3]
            $posLine = if ($i+1 -lt $Lines.Count) { $Lines[$i+1] } else { '' }
            $rotLine = if ($i+2 -lt $Lines.Count) { $Lines[$i+2] } else { '' }
            $posM = [regex]::Match($posLine, 'pos-dmax=([0-9.E+-]+) pos-dp99=([0-9.E+-]+) pos-davg=([0-9.E+-]+)')
            $rotM = [regex]::Match($rotLine, 'rot-dmax=([0-9.E+-]+) rot-dp99=([0-9.E+-]+) rot-davg=([0-9.E+-]+)')
            if ($posM.Success -and $rotM.Success) {
                [void]$result.Add([PSCustomObject]@{
                    Type = 'VIS'; Frame = $frame; Owner = $owner; Window = $window
                    PosDmax = [double]$posM.Groups[1].Value
                    PosDp99 = [double]$posM.Groups[2].Value
                    PosDavg = [double]$posM.Groups[3].Value
                    RotDmax = [double]$rotM.Groups[1].Value
                    RotDp99 = [double]$rotM.Groups[2].Value
                    RotDavg = [double]$rotM.Groups[3].Value
                })
            }
            continue
        }
        # [D-REC HEARTBEAT] is single-line.
        if ($line -match '\[D-REC HEARTBEAT\] frame=(\d+) events-in-window=(\d+) rec-snap-max=([0-9.E+-]+) rec-snap-p99=([0-9.E+-]+) rec-snap-avg=([0-9.E+-]+) owner=(\w+)') {
            [void]$result.Add([PSCustomObject]@{
                Type = 'REC'
                Frame = [int]$Matches[1]
                Window = [int]$Matches[2]
                RecSnapMax = [double]$Matches[3].Value
                RecSnapP99 = [double]$Matches[4].Value
                RecSnapAvg = [double]$Matches[5].Value
                Owner = ($Matches[6] -ieq 'true' -or $Matches[6] -ieq 'True')
            })
        }
    }
    return ,$result
}

function Get-PathTag {
    param([string] $Filename)
    if ($Filename -like "*$LatencySimTag*") { return $LatencySimTag } else { return $BaselineLatencyTag }
}

function Get-PeerTag {
    param([string] $Filename)
    $base = [System.IO.Path]::GetFileNameWithoutExtension($Filename).ToLowerInvariant()
    if ($base -like '*host*')    { return 'host' }
    if ($base -like '*client*')  { return 'client' }
    if ($base -like '*single*')  { return 'single' }
    return 'unknown'
}

function Check-FrameCadence {
    param([object[]] $VisEvents)
    if ($VisEvents.Count -lt 2) { return $true, 'insufficient-samples' }
    $deltas = @()
    for ($i = 1; $i -lt $VisEvents.Count; $i++) {
        $deltas += ($VisEvents[$i].Frame - $VisEvents[$i-1].Frame)
    }
    $avgDelta = ($deltas | Measure-Object -Average).Average
    # Default heartbeat is 60 frames; allow +/-10%.
    $nominal = if ($VisEvents[0].Window -gt 0) { $VisEvents[0].Window } else { 60 }
    $okLow  = $nominal * 0.9
    $okHigh = $nominal * 1.1
    if ($avgDelta -ge $okLow -and $avgDelta -le $okHigh) {
        return $true, ("avg-frame-delta=$([math]::Round($avgDelta,1)) (nominal=$nominal)")
    }
    return $false, ("avg-frame-delta=$([math]::Round($avgDelta,1)) NON-NOMINAL (nominal=$nominal)")
}

# Main pipeline.
$allEvents = New-Object System.Collections.ArrayList
foreach ($logPath in $LogPaths) {
    if (-not (Test-Path $logPath)) {
        Write-Warning "skipping missing file: $logPath"
        continue
    }
    $lines = Get-Content -LiteralPath $logPath
    $events = Parse-Heartbeats -Lines $lines
    $pathTag = Get-PathTag -Filename $logPath
    $peerTag = Get-PeerTag -Filename $logPath
    foreach ($e in $events) {
        $e | Add-Member -NotePropertyName 'PathTag' -NotePropertyValue $pathTag -Force
        $e | Add-Member -NotePropertyName 'PeerTag' -NotePropertyValue $peerTag -Force
        $e | Add-Member -NotePropertyName 'SourceFile' -NotePropertyValue ([System.IO.Path]::GetFileName($logPath)) -Force
        [void]$allEvents.Add($e)
    }
}

if ($allEvents.Count -eq 0) {
    Write-Output ''
    Write-Output '# Phase 7 jitter analysis -- NO heartbeat events parsed'
    Write-Output ''
    Write-Output 'No [D-VIS HEARTBEAT] or [D-REC HEARTBEAT] lines found. Confirm:'
    Write-Output '  1. SMOKE was run with BUDDAH_PREDICTION_VISUAL_PROBE + BUDDAH_PREDICTION_RECONCILE_PROBE both defined.'
    Write-Output '  2. Probes were attached (per Bootstrap.cs:77 wiring for VIS; manual or future Bootstrap wiring for REC).'
    Write-Output '  3. PlayMode actually ran for >= heartbeat-window worth of frames/events.'
    return
}

# Aggregate by (PathTag, PeerTag, Owner, Type).
$groups = $allEvents | Group-Object PathTag, PeerTag, Owner, Type

Write-Output ''
Write-Output '# Phase 7 jitter analysis'
Write-Output ''
Write-Output ('Files ingested: ' + ($LogPaths | ForEach-Object { [System.IO.Path]::GetFileName($_) } | Sort-Object -Unique) -join ', ')
Write-Output ('Total heartbeats parsed: ' + $allEvents.Count + ' (VIS=' + (@($allEvents | Where-Object { $_.Type -eq 'VIS' })).Count + ', REC=' + (@($allEvents | Where-Object { $_.Type -eq 'REC' })).Count + ')')
Write-Output ''

# Q3 absolute gates per (PathTag, PeerTag, Owner) -- VIS metrics.
Write-Output '## Q3 absolute gates -- visual-shake (VIS) metrics'
Write-Output ''
Write-Output '| PathTag | Peer | Owner | pos-davg | pos-dp99 | pos-dmax | rot-dp99 | rot-dmax | G3.1 dp99-davg<0.30 | G3.2 dmax-davg<0.60 | G3.3 rot-dp99<8 | G3.4 rot-dmax<15 |'
Write-Output '|---|---|---|---:|---:|---:|---:|---:|:---:|:---:|:---:|:---:|'
foreach ($g in ($groups | Where-Object { $_.Group[0].Type -eq 'VIS' } | Sort-Object Name)) {
    $events = $g.Group
    $posDavg = ($events.PosDavg | Measure-Object -Average).Average
    $posDp99 = ($events.PosDp99 | Measure-Object -Maximum).Maximum
    $posDmax = ($events.PosDmax | Measure-Object -Maximum).Maximum
    $rotDp99 = ($events.RotDp99 | Measure-Object -Maximum).Maximum
    $rotDmax = ($events.RotDmax | Measure-Object -Maximum).Maximum
    $g31 = if (($posDp99 - $posDavg) -lt $Gate_PosDp99MinusDavg) { 'PASS' } else { 'FAIL' }
    $g32 = if (($posDmax - $posDavg) -lt $Gate_PosDmaxMinusDavg) { 'PASS' } else { 'FAIL' }
    $g33 = if ($rotDp99 -lt $Gate_RotDp99) { 'PASS' } else { 'FAIL' }
    $g34 = if ($rotDmax -lt $Gate_RotDmax) { 'PASS' } else { 'FAIL' }
    Write-Output ("| {0} | {1} | {2} | {3:F4} | {4:F4} | {5:F4} | {6:F2} | {7:F2} | {8} | {9} | {10} | {11} |" -f $events[0].PathTag, $events[0].PeerTag, $events[0].Owner, $posDavg, $posDp99, $posDmax, $rotDp99, $rotDmax, $g31, $g32, $g33, $g34)
}

Write-Output ''
Write-Output '## Q3 absolute gates -- reconcile-snap (REC) metrics'
Write-Output ''
Write-Output '| PathTag | Peer | Owner | rec-snap-avg | rec-snap-p99 | rec-snap-max | events-in-window | G3.5 p99<0.50 | G3.6 max<1.50 |'
Write-Output '|---|---|---|---:|---:|---:|---:|:---:|:---:|'
foreach ($g in ($groups | Where-Object { $_.Group[0].Type -eq 'REC' } | Sort-Object Name)) {
    $events = $g.Group
    $recAvg = ($events.RecSnapAvg | Measure-Object -Average).Average
    $recP99 = ($events.RecSnapP99 | Measure-Object -Maximum).Maximum
    $recMax = ($events.RecSnapMax | Measure-Object -Maximum).Maximum
    $window = $events[0].Window
    $g35 = if ($recP99 -lt $Gate_RecSnapP99) { 'PASS' } else { 'FAIL' }
    $g36 = if ($recMax -lt $Gate_RecSnapMax) { 'PASS' } else { 'FAIL' }
    Write-Output ("| {0} | {1} | {2} | {3:F4} | {4:F4} | {5:F4} | {6} | {7} | {8} |" -f $events[0].PathTag, $events[0].PeerTag, $events[0].Owner, $recAvg, $recP99, $recMax, $window, $g35, $g36)
}

# Secondary regression checks: 100ms vs no-latency, per (peer, owner).
Write-Output ''
Write-Output '## Q3 secondary regression checks (LatencySim vs baseline)'
Write-Output ''
Write-Output '| Peer | Owner | Metric | Baseline | LatencySim | Ratio | Gate | Result |'
Write-Output '|---|---|---|---:|---:|---:|---|:---:|'
$peerOwners = $allEvents | Where-Object { $_.PathTag -in @($BaselineLatencyTag, $LatencySimTag) } | Select-Object PeerTag, Owner -Unique
foreach ($po in $peerOwners) {
    foreach ($metric in @('PosDp99', 'RecSnapP99')) {
        $type = if ($metric -eq 'PosDp99') { 'VIS' } else { 'REC' }
        $base = $allEvents | Where-Object { $_.PeerTag -eq $po.PeerTag -and $_.Owner -eq $po.Owner -and $_.Type -eq $type -and $_.PathTag -eq $BaselineLatencyTag }
        $lat  = $allEvents | Where-Object { $_.PeerTag -eq $po.PeerTag -and $_.Owner -eq $po.Owner -and $_.Type -eq $type -and $_.PathTag -eq $LatencySimTag }
        if (-not $base -or -not $lat) { continue }
        $baseVal = ($base.$metric | Measure-Object -Maximum).Maximum
        $latVal  = ($lat.$metric  | Measure-Object -Maximum).Maximum
        if ($baseVal -le 0) { $ratio = [double]::PositiveInfinity } else { $ratio = $latVal / $baseVal }
        if ($metric -eq 'PosDp99') {
            $gateLabel = 'G3.SEC.1 <=110%'
            $pass = ($ratio -le 1.10)
        } else {
            $gateLabel = 'G3.SEC.2 <=300%'
            $pass = ($ratio -le 3.00)
        }
        $result = if ($pass) { 'PASS' } else { 'FAIL' }
        Write-Output ("| {0} | {1} | {2} | {3:F4} | {4:F4} | {5:F2}x | {6} | {7} |" -f $po.PeerTag, $po.Owner, $metric, $baseVal, $latVal, $ratio, $gateLabel, $result)
    }
}

# Cadence sanity (Q0 R0.3): per (SourceFile, Owner) frame-delta sanity on VIS heartbeats.
# Grouping by Owner is required because multi-Buddah scenes (2-peer LAN) interleave
# owner=true + owner=false emits within the same log file -- a per-file delta would
# halve and false-positive NON-NOMINAL.
Write-Output ''
Write-Output '## Cadence sanity (per (SourceFile, Owner) VIS frame-delta)'
Write-Output ''
Write-Output '| SourceFile | Owner | Status | Detail |'
Write-Output '|---|---|:---:|---|'
foreach ($g in ($allEvents | Where-Object { $_.Type -eq 'VIS' } | Group-Object SourceFile, Owner)) {
    $stream = @($g.Group | Sort-Object Frame)
    $ok, $detail = Check-FrameCadence -VisEvents $stream
    $status = if ($ok) { 'OK' } else { 'NON-NOMINAL' }
    Write-Output ("| {0} | {1} | {2} | {3} |" -f $stream[0].SourceFile, $stream[0].Owner, $status, $detail)
}

if ($OutputJson) {
    $allEvents | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OutputJson -Encoding UTF8
    Write-Output ''
    Write-Output ("JSON output written to: {0}" -f $OutputJson)
}
