<#
.SYNOPSIS
  Phase 7 Path C SMOKE post-flight automation. Captures Editor.log to the
  agent-exchange raw/ folder, removes Phase 7 probe defines from
  ProjectSettings.asset, verifies alpha-discipline net-zero diff, and commits
  the captured log.

.DESCRIPTION
  ASCII-only per methodology Rule 2 sub-clause #3. ProjectSettings.asset
  written as UTF-8 without BOM (matches Unity serialization). HARD FAIL if
  alpha-discipline check shows ProjectSettings.asset diff vs origin/dev is
  non-empty after define removal.

.PARAMETER SkipGitPush
  Skip the final git push. Commit still happens; caller pushes manually.

.PARAMETER DryRun
  Skip git add / git commit / git push entirely. Used by author-side
  execution-test (Rule 2 sub-clause #1) and reviewer cross-execution
  (Rule 2 sub-clause #2). All other steps -- copy, define removal,
  alpha-discipline verify -- still run.

.PARAMETER EditorLogPath
  Override the Editor.log source path. Default: %LOCALAPPDATA%\Unity\Editor\Editor.log.
  Used by tests to point at a synthetic log; in production the default is
  correct on Windows.

.PARAMETER TargetLogPath
  Override the captured-log target path. Default:
  agent-exchange/console/raw/2026-05-03-phase7-host-only-jitter.log.
#>
[CmdletBinding()]
param(
    [switch] $SkipGitPush,
    [switch] $DryRun,
    [string] $EditorLogPath,
    [string] $TargetLogPath = 'agent-exchange/console/raw/2026-05-03-phase7-host-only-jitter.log'
)

$ErrorActionPreference = 'Stop'

Write-Host 'Phase 7 Path C SMOKE -- Post-flight'
if ($DryRun) { Write-Host '  (DryRun: no git operations will run)' }
Write-Host ''

# 1. Resolve Editor.log source.
if (-not $EditorLogPath) {
    $EditorLogPath = Join-Path $env:LOCALAPPDATA 'Unity\Editor\Editor.log'
}
if (-not (Test-Path -LiteralPath $EditorLogPath)) {
    Write-Error "Editor.log not found at $EditorLogPath. Did Unity Editor run?"
    exit 1
}

# Ensure target directory exists.
$targetDir = Split-Path -Parent $TargetLogPath
if ($targetDir -and -not (Test-Path -LiteralPath $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
}

Copy-Item -LiteralPath $EditorLogPath -Destination $TargetLogPath -Force
$logSize = (Get-Item -LiteralPath $TargetLogPath).Length
Write-Host "  [copy]  Editor.log -> $TargetLogPath ($logSize bytes)"

# 2. Sanity stats on captured log.
$logContent = Get-Content -LiteralPath $TargetLogPath -Raw
$visCount = ([regex]::Matches($logContent, '\[D-VIS HEARTBEAT\]')).Count
$recCount = ([regex]::Matches($logContent, '\[D-REC HEARTBEAT\]')).Count
$ftCount  = ([regex]::Matches($logContent, '\[D-FT HEARTBEAT\]')).Count
Write-Host "  [stat]  VIS HBs=$visCount  REC HBs=$recCount  FT HBs=$ftCount"

if ($visCount -lt 30 -or $ftCount -lt 30) {
    Write-Warning 'Low HB count -- confirm 90s active play actually ran. ~90 HBs each expected at 60-frame default window.'
}
if ($recCount -eq 0) {
    Write-Warning 'No REC HBs -- ReconcileSnapProbe may not have wired. Check Bootstrap runtime-attach + define gates.'
}

# 3. Remove Phase 7 probe defines from ProjectSettings.asset (alpha discipline).
$projectSettings = 'ProjectSettings/ProjectSettings.asset'
if (-not (Test-Path -LiteralPath $projectSettings)) {
    Write-Error "ProjectSettings.asset not found at $projectSettings"
    exit 1
}

$content = Get-Content -LiteralPath $projectSettings -Raw

$defines = @(
    'BUDDAH_PREDICTION_RECONCILE_PROBE',
    'BUDDAH_PREDICTION_FRAMETIME_PROBE'
)

$mutated = $false
foreach ($def in $defines) {
    if ($content -match [regex]::Escape($def)) {
        # Try `;DEFINE` first (most common form), then `DEFINE;`, then bare DEFINE.
        $content = $content -replace [regex]::Escape(';' + $def), ''
        $content = $content -replace [regex]::Escape($def + ';'), ''
        $content = $content -replace [regex]::Escape($def), ''
        Write-Host "  [remove] $def"
        $mutated = $true
    } else {
        Write-Host "  [skip]   $def not present"
    }
}

if ($mutated) {
    [System.IO.File]::WriteAllText(
        (Resolve-Path -LiteralPath $projectSettings).Path,
        $content,
        (New-Object System.Text.UTF8Encoding $false))
    Write-Host '  [write]  ProjectSettings.asset (UTF-8 no BOM)'
}

# 4. Alpha-discipline verify: ProjectSettings.asset must net-zero vs origin/dev.
$diff = git diff origin/dev -- ProjectSettings/ProjectSettings.asset 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Warning "git diff origin/dev failed (exit $LASTEXITCODE) -- verify origin/dev is fetched. Skipping alpha verify."
} elseif ($diff) {
    Write-Error "ALPHA DISCIPLINE VIOLATED: ProjectSettings.asset has unexpected diff vs origin/dev:`n$diff"
    Write-Error 'Phase 7 must NOT alter ProjectSettings.asset net of pre-flight + post-flight cycle. Investigate before push.'
    exit 1
} else {
    Write-Host '  [verify] ProjectSettings.asset net-zero vs origin/dev OK (alpha preserved)'
}

# 5. Git operations (unless dry-run).
if ($DryRun) {
    Write-Host ''
    Write-Host 'DryRun complete. No git operations performed.'
    return
}

git add -- $TargetLogPath
git commit -m 'phase7 Stage 5 SMOKE Path C: host-only 90s active play raw log'

if (-not $SkipGitPush) {
    git push
    Write-Host '  [push]  commit pushed to origin'
}

Write-Host ''
Write-Host 'Post-flight done. Next:'
Write-Host '  - Ping CC to run AnalyzeJitter on the new log + write the SMOKE digest.'
Write-Host '  - CC commits AnalyzeJitter output + SMOKE digest, then pings cowork-reviewer for Stage 6 VERIFY.'
