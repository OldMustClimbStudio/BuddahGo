<#
.SYNOPSIS
  Phase 7 Path C SMOKE pre-flight automation. Adds Phase 7 probe defines to
  ProjectSettings.asset Standalone scripting-define-symbols, cleans Editor.log,
  and prints driver next-steps.

.DESCRIPTION
  Idempotent (safe to re-run). ASCII-only per methodology Rule 2 sub-clause #3.
  Writes ProjectSettings.asset using UTF-8 without BOM (Unity's serialization
  format) -- using Set-Content with default encoding on PowerShell 5.1 would
  corrupt the file (UTF-16 BOM).

.PARAMETER SkipGitPull
  Skip the initial git pull. Useful when caller has already pulled or when
  testing the script in isolation.
#>
[CmdletBinding()]
param(
    [switch] $SkipGitPull
)

$ErrorActionPreference = 'Stop'

Write-Host 'Phase 7 Path C SMOKE -- Pre-flight'
Write-Host ''

# 1. git pull (unless caller skipped).
if (-not $SkipGitPull) {
    Write-Host '  [git]   pull origin'
    git pull
}

# 2. Add Phase 7 probe defines to ProjectSettings.asset Standalone scripting-define-symbols.
# Idempotent: skip if already present. Append `;DEFINE` to the end of the Standalone line.
$projectSettings = 'ProjectSettings/ProjectSettings.asset'
if (-not (Test-Path $projectSettings)) {
    Write-Error "ProjectSettings.asset not found at $projectSettings"
    exit 1
}

$content = Get-Content -LiteralPath $projectSettings -Raw

$defines = @(
    'BUDDAH_PREDICTION_RECONCILE_PROBE',
    'BUDDAH_PREDICTION_FRAMETIME_PROBE'
)

# Anchor pattern to the scripting-define-symbols Standalone line specifically.
# The .asset file has multiple unrelated `Standalone:` YAML keys (applicationIdentifier,
# buildNumber, etc.); only the scripting-define-symbols one contains FISHNET in this
# project. Matching on FISHNET avoids leaking defines into unrelated keys.
$sdLinePattern = '(?m)^(    Standalone: [^\r\n]*FISHNET[^\r\n]*)$'

if ($content -notmatch $sdLinePattern) {
    Write-Error 'Could not locate the scripting-define-symbols Standalone line in ProjectSettings.asset (expected to contain FISHNET). Aborting; manual investigation required before re-running pre-flight.'
    exit 1
}

$mutated = $false
foreach ($def in $defines) {
    if ($content -match [regex]::Escape($def)) {
        Write-Host "  [skip]  $def already present"
    } else {
        $content = $content -replace $sdLinePattern, ('$1;' + $def)
        Write-Host "  [add]   $def"
        $mutated = $true
    }
}

if ($mutated) {
    # UTF-8 without BOM matches Unity's serialization. Set-Content on PS 5.1
    # defaults to UTF-16-with-BOM and would corrupt the .asset file.
    [System.IO.File]::WriteAllText(
        (Resolve-Path -LiteralPath $projectSettings).Path,
        $content,
        (New-Object System.Text.UTF8Encoding $false))
    Write-Host '  [write] ProjectSettings.asset (UTF-8 no BOM)'
} else {
    Write-Host '  [write] no changes; file untouched'
}

# 3. Clean Editor.log per Rule 1 session-scoping discipline. Non-fatal: if Editor
# is already running, the file is locked; warn and continue (caller should close
# the Editor before running pre-flight, or accept that the log will append rather
# than start fresh -- post-flight grep counts still work either way, but session
# scoping is cleaner with a fresh log).
$editorLog = Join-Path $env:LOCALAPPDATA 'Unity\Editor\Editor.log'
if (Test-Path $editorLog) {
    try {
        Remove-Item -LiteralPath $editorLog -Force -ErrorAction Stop
        Write-Host "  [clean] Editor.log removed ($editorLog)"
    } catch {
        Write-Warning "Editor.log is locked (Unity Editor running?). Could not remove. Close the Editor and re-run pre-flight, OR accept that Editor.log will append rather than reset."
        Write-Warning "Locked path: $editorLog"
    }
} else {
    Write-Host "  [skip]  Editor.log not present at $editorLog"
}

Write-Host ''
Write-Host 'Pre-flight done. Now:'
Write-Host '  1. Open Unity Editor; wait for recompile to finish.'
Write-Host '  2. Enter PlayMode (host-mode start; no peer joins).'
Write-Host '  3. Drive 90s of continuous active play -- driving + random skill cast (NO idle).'
Write-Host '  4. Exit PlayMode.'
Write-Host '  5. Run: pwsh Tools/Phase7-SMOKE-PathC-PostFlight.ps1'
