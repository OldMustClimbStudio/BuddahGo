param(
    [ValidateSet("debug", "refactor", "feature", "unknown")]
    [string]$Intent = "unknown",

    [string[]]$Systems,

    [string[]]$Files,

    [string]$Query,

    [switch]$AsJson
)

function Normalize-Text([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        return ""
    }

    return $Value.Replace('\', '/').ToLowerInvariant()
}

function Get-SystemMatch([object]$System, [string[]]$NormalizedFiles, [string]$SearchText) {
    $score = 0
    $reasons = [System.Collections.Generic.List[string]]::new()

    foreach ($file in $NormalizedFiles) {
        foreach ($path in @($System.paths)) {
            $normalizedPath = Normalize-Text([string]$path)
            if (-not [string]::IsNullOrWhiteSpace($normalizedPath) -and $file.StartsWith($normalizedPath)) {
                $score += 3
                [void]$reasons.Add("path:$path")
            }
        }
    }

    foreach ($keyword in @($System.keywords)) {
        $normalizedKeyword = Normalize-Text([string]$keyword)
        if (-not [string]::IsNullOrWhiteSpace($normalizedKeyword) -and $SearchText.Contains($normalizedKeyword)) {
            $score += 1
            [void]$reasons.Add("keyword:$keyword")
        }
    }

    [pscustomobject]@{
        system = $System
        score = $score
        reasons = @($reasons | Select-Object -Unique)
    }
}

function Resolve-ConditionalValidationRules([object]$Manifest, [string[]]$NormalizedFiles, [string]$SearchText) {
    $resolved = [System.Collections.Generic.List[object]]::new()

    foreach ($rule in @($Manifest.conditionalValidations)) {
        $matched = $false

        foreach ($keyword in @($rule.queryKeywords)) {
            $normalizedKeyword = Normalize-Text([string]$keyword)
            if (-not [string]::IsNullOrWhiteSpace($normalizedKeyword) -and $SearchText.Contains($normalizedKeyword)) {
                $matched = $true
                break
            }
        }

        if (-not $matched) {
            foreach ($file in $NormalizedFiles) {
                foreach ($extension in @($rule.fileExtensions)) {
                    if ($file.EndsWith(([string]$extension).ToLowerInvariant())) {
                        $matched = $true
                        break
                    }
                }

                if ($matched) {
                    break
                }

                foreach ($pathKeyword in @($rule.pathKeywords)) {
                    $normalizedPathKeyword = Normalize-Text([string]$pathKeyword)
                    if (-not [string]::IsNullOrWhiteSpace($normalizedPathKeyword) -and $file.Contains($normalizedPathKeyword)) {
                        $matched = $true
                        break
                    }
                }

                if ($matched) {
                    break
                }
            }
        }

        if ($matched) {
            $resolved.Add([pscustomobject]@{
                name = $rule.name
                sequence = $rule.sequence
                triggerDescription = $rule.triggerDescription
                steps = @($Manifest.validationSequences.($rule.sequence))
            })
        }
    }

    return @($resolved)
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$manifestPath = Join-Path $repoRoot "Docs\harness-manifest.json"

if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "Harness manifest not found at '$manifestPath'."
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$allSystems = @($manifest.systems)

$normalizedFiles = @(
    foreach ($file in @($Files)) {
        Normalize-Text([string]$file)
    }
)

$searchTextParts = [System.Collections.Generic.List[string]]::new()
if (-not [string]::IsNullOrWhiteSpace($Query)) {
    [void]$searchTextParts.Add((Normalize-Text $Query))
}
foreach ($file in $normalizedFiles) {
    [void]$searchTextParts.Add($file)
}
$searchText = ($searchTextParts -join " ").Trim()

$selectionMode = "explicit"
$systemMatches = @()

if ($Systems -and $Systems.Count -gt 0) {
    $selectedSystems = foreach ($systemId in $Systems) {
        $match = $allSystems | Where-Object { $_.id -eq $systemId } | Select-Object -First 1
        if (-not $match) {
            throw "Unknown system id '$systemId'. Valid ids: $($allSystems.id -join ', ')"
        }

        $match
    }
}
elseif ($normalizedFiles.Count -gt 0 -or -not [string]::IsNullOrWhiteSpace($searchText)) {
    $selectionMode = "auto-detected"
    $systemMatches = @(
        foreach ($system in $allSystems) {
            Get-SystemMatch -System $system -NormalizedFiles $normalizedFiles -SearchText $searchText
        }
    ) | Sort-Object -Property @{ Expression = { $_.score }; Descending = $true }, @{ Expression = { $_.system.id }; Descending = $false }

    $selectedSystems = @($systemMatches | Where-Object { $_.score -gt 0 } | ForEach-Object { $_.system })

    if (-not $selectedSystems -or $selectedSystems.Count -eq 0) {
        $selectionMode = "auto-detected-fallback"
        $selectedSystems = $allSystems
    }
}
else {
    $selectionMode = "all-systems"
    $selectedSystems = $allSystems
}

$route = $manifest.taskRouting | Where-Object { $_.intent -eq $Intent } | Select-Object -First 1

$requiredDocs = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$protectedFiles = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$highRiskFiles = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$validationNames = [System.Collections.Generic.List[string]]::new()
$stopConditions = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

if ($route) {
    foreach ($doc in @($route.requiredDocs)) {
        if (-not [string]::IsNullOrWhiteSpace([string]$doc)) {
            [void]$requiredDocs.Add([string]$doc)
        }
    }
}

foreach ($system in $selectedSystems) {
    foreach ($doc in @($system.requiredDocs)) {
        if (-not [string]::IsNullOrWhiteSpace([string]$doc)) {
            [void]$requiredDocs.Add([string]$doc)
        }
    }

    foreach ($file in @($system.protectedFiles)) {
        if (-not [string]::IsNullOrWhiteSpace([string]$file)) {
            [void]$protectedFiles.Add([string]$file)
        }
    }

    foreach ($file in @($system.highRiskFiles)) {
        if (-not [string]::IsNullOrWhiteSpace([string]$file)) {
            [void]$highRiskFiles.Add([string]$file)
        }
    }

    foreach ($name in @($system.validationSequence)) {
        if (-not $validationNames.Contains([string]$name)) {
            [void]$validationNames.Add([string]$name)
        }
    }

    foreach ($stop in @($system.stopConditions)) {
        [void]$stopConditions.Add([string]$stop)
    }
}

$conditionalValidations = Resolve-ConditionalValidationRules -Manifest $manifest -NormalizedFiles $normalizedFiles -SearchText $searchText

$validationDetails = foreach ($name in $validationNames) {
    [pscustomobject]@{
        name = $name
        steps = @($manifest.validationSequences.$name)
    }
}

$summary = [pscustomobject]@{
    project = $manifest.project
    intent = $Intent
    selectionMode = $selectionMode
    query = $Query
    files = @($Files)
    recommendedSkill = if ($route) { $route.skill } else { $null }
    systems = @(
        foreach ($system in $selectedSystems) {
            $matchReasons = @()
            if ($systemMatches.Count -gt 0) {
                $matchReasons = @(
                    $systemMatches |
                        Where-Object { $_.system.id -eq $system.id } |
                        Select-Object -First 1 |
                        ForEach-Object { $_.reasons }
                )
            }

            [pscustomobject]@{
                id = $system.id
                name = $system.name
                layer = $system.layer
                ownerSummary = $system.ownerSummary
                entryPoints = @($system.entryPoints)
                paths = @($system.paths)
                rules = @($system.rules)
                protectedFiles = @($system.protectedFiles)
                highRiskFiles = @($system.highRiskFiles)
                matchReasons = @($matchReasons)
            }
        }
    )
    requiredDocs = @($requiredDocs)
    protectedFiles = @($protectedFiles)
    highRiskFiles = @($highRiskFiles)
    validation = @($validationDetails)
    conditionalValidation = @($conditionalValidations)
    stopConditions = @($stopConditions)
}

if ($AsJson) {
    $summary | ConvertTo-Json -Depth 10
    exit 0
}

Write-Output ("Project: {0}" -f $summary.project.name)
Write-Output ("Intent: {0}" -f $summary.intent)
Write-Output ("Selection mode: {0}" -f $summary.selectionMode)
if ($summary.query) {
    Write-Output ("Query: {0}" -f $summary.query)
}
if ($summary.files.Count -gt 0) {
    Write-Output ("Files: {0}" -f ($summary.files -join ", "))
}
if ($summary.recommendedSkill) {
    Write-Output ("Recommended skill: {0}" -f $summary.recommendedSkill)
}

Write-Output ""
Write-Output "Systems:"
foreach ($system in $summary.systems) {
    Write-Output ("- [{0}] {1}" -f $system.id, $system.name)
    Write-Output ("  Layer: {0}" -f $system.layer)
    Write-Output ("  Owner: {0}" -f $system.ownerSummary)
    Write-Output ("  Paths: {0}" -f ($system.paths -join ", "))
    if ($system.matchReasons.Count -gt 0) {
        Write-Output ("  Match reasons: {0}" -f ($system.matchReasons -join ", "))
    }
}

Write-Output ""
Write-Output "Required docs:"
foreach ($doc in $summary.requiredDocs) {
    Write-Output ("- {0}" -f $doc)
}

Write-Output ""
Write-Output "Protected files:"
foreach ($file in $summary.protectedFiles) {
    Write-Output ("- {0}" -f $file)
}

Write-Output ""
Write-Output "High-risk files:"
foreach ($file in $summary.highRiskFiles) {
    Write-Output ("- {0}" -f $file)
}

Write-Output ""
Write-Output "Validation:"
foreach ($item in $summary.validation) {
    Write-Output ("- {0}" -f $item.name)
    foreach ($step in $item.steps) {
        Write-Output ("  * {0}" -f $step)
    }
}

if ($summary.conditionalValidation.Count -gt 0) {
    Write-Output ""
    Write-Output "Conditional validation:"
    foreach ($item in $summary.conditionalValidation) {
        Write-Output ("- {0}: {1}" -f $item.name, $item.triggerDescription)
        foreach ($step in $item.steps) {
            Write-Output ("  * {0}" -f $step)
        }
    }
}

Write-Output ""
Write-Output "Stop conditions:"
foreach ($stop in $summary.stopConditions) {
    Write-Output ("- {0}" -f $stop)
}

# Active Phase Gate surface (added 2026-05-03 from V5 closeout retrospective).
# Auto-discovers active phase contract(s) and prints status + last ledger row.
# Helps any new session immediately know which phase + stage is in flight,
# without having to read the full contract upfront.
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$activeContractDir = Join-Path $repoRoot "Docs/phase-gates/active"

if (Test-Path $activeContractDir) {
    $activeContracts = @(Get-ChildItem -Path $activeContractDir -Filter "*-contract.md" -ErrorAction SilentlyContinue)

    if ($activeContracts.Count -gt 0) {
        Write-Output ""
        Write-Output "Active Phase Gate:"
        foreach ($contract in $activeContracts) {
            $relativePath = $contract.FullName.Substring($repoRoot.Length).TrimStart('\', '/').Replace('\', '/')
            Write-Output ("- contract: {0}" -f $relativePath)

            $contractContent = Get-Content -Path $contract.FullName -ErrorAction SilentlyContinue
            if ($contractContent) {
                $statusLine = $contractContent | Where-Object { $_ -match '^\*\*Status:\*\*' } | Select-Object -First 1
                if ($statusLine) {
                    Write-Output ("  * {0}" -f $statusLine.Trim())
                }

                # Last non-empty sign-off ledger row (table row starting with "|")
                # that has a non-empty Date cell. Skips header + separator rows.
                $ledgerRows = $contractContent | Where-Object { $_ -match '^\| ' -and $_ -notmatch '^\|---' -and $_ -notmatch '^\| Stage \|' }
                $lastSignedRow = $ledgerRows | Where-Object { $_ -match '^\| [A-Za-z]+ \| [0-9]{4}-' } | Select-Object -Last 1
                if ($lastSignedRow) {
                    # Truncate ledger row to first 200 chars to keep helper output readable.
                    $rowDisplay = $lastSignedRow.Trim()
                    if ($rowDisplay.Length -gt 200) {
                        $rowDisplay = $rowDisplay.Substring(0, 197) + "..."
                    }
                    Write-Output ("  * last signed: {0}" -f $rowDisplay)
                }
            }
        }

        Write-Output ""
        Write-Output "Verify discipline (Phase Gate System):"
        Write-Output "- Negative claims: use git plumbing (git show HEAD:<path>, git status --short, git diff origin/<base>) — NOT Read/Grep on working tree (per L22 + methodology Rule 2)"
        Write-Output "- Stage 6 first command: git status --short filtered to phase scope; discriminate CRLF/mount-truncation drift before halting"
        Write-Output "- Verify report template: Docs/phase-gates/templates.md Template 4 (post-L22)"
    }
}
