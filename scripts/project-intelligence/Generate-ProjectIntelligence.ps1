# PEMS Project Intelligence Generator
#
# Evidence-based module scan per PEMS_PROJECT_INTELLIGENCE_SPEC.md.
# Unit of analysis = one MVC Controller = one candidate "module". For each controller
# this walks the actual repository (source, DbContext, migrations, views, sidebar nav,
# tests, docs, git history, Graphify graph) and records what evidence was actually found.
#
# It does NOT invent a completion percentage. Every module gets a transparent
# evidence checklist (Present / Missing / Unknown per signal) plus a mechanically
# computed "evidence coverage" ratio (signals present / signals checked) so a human
# can see exactly how that number was derived - never a guessed score.
#
# Output:
#   graphify-out/project-intelligence.json   (source of truth, read by the Admin web page)
#   PEMS_PROJECT_INTELLIGENCE_REPORT.md      (human-readable report)
#
# Usage:
#   pwsh scripts/project-intelligence/Generate-ProjectIntelligence.ps1
#   (or from Windows PowerShell 5.1: powershell -File scripts/project-intelligence/Generate-ProjectIntelligence.ps1)

param(
    [string]$RepoRoot = (Resolve-Path "$PSScriptRoot\..\..").Path
)

$ErrorActionPreference = "Stop"
Set-Location $RepoRoot

Write-Host "PEMS Project Intelligence - scanning $RepoRoot" -ForegroundColor Cyan

# ---------------------------------------------------------------------------
# 0. Load static context: sidebar nav, doc files, DbContext, migrations, graph
# ---------------------------------------------------------------------------

function Get-TextOrEmpty([string]$path) {
    if (Test-Path $path) { return (Get-Content -Raw -Path $path -ErrorAction SilentlyContinue) }
    return ""
}

$sidebarContent = Get-TextOrEmpty "PEMS.Web\Views\Shared\_SidebarMenu.cshtml"
$dbContextContent = Get-TextOrEmpty "PEMS.Persistence\Context\PEMSDbContext.cs"

$docFiles = @(
    "ENGINEERING_AND_AI_MODULES_GUIDE.md",
    "VDR_MODULE_IMPLEMENTATION_GUIDE.md",
    "FRAMEWORK_SETUP_GUIDE.md"
)
$docsContent = ($docFiles | ForEach-Object { Get-TextOrEmpty $_ }) -join "`n---DOC---`n"

$migrationFiles = Get-ChildItem "PEMS.Persistence\Migrations" -Filter "*.cs" -File |
    Where-Object { $_.Name -notlike "*.Designer.cs" -and $_.Name -notlike "*ModelSnapshot.cs" }
$migrationsContent = ($migrationFiles | ForEach-Object { Get-Content -Raw $_.FullName }) -join "`n---MIGRATION---`n"

$entityFiles = Get-ChildItem "PEMS.Domain\Entities" -Filter "*.cs" -File -Recurse
$entityBaseNames = $entityFiles | ForEach-Object { $_.BaseName }

$testFiles = Get-ChildItem "PEMS.Tests" -Filter "*.cs" -File -Recurse -ErrorAction SilentlyContinue
$testsContent = if ($testFiles) { ($testFiles | ForEach-Object { Get-Content -Raw $_.FullName }) -join "`n---TEST---`n" } else { "" }

# Graphify cross-reference: node connectivity (in+out degree) per source file, if the graph exists
$graphDegree = @{}
$graphPath = "graphify-out\graph.json"
if (Test-Path $graphPath) {
    Write-Host "Loading graphify graph for connectivity signal..." -ForegroundColor DarkGray
    try {
        $graph = Get-Content -Raw $graphPath | ConvertFrom-Json
        $srcById = @{}
        foreach ($n in $graph.nodes) {
            if ($n.source_file) { $srcById[$n.id] = $n.source_file }
        }
        foreach ($e in $graph.edges) {
            $s = $srcById[$e.source]
            $t = $srcById[$e.target]
            if ($s) { $graphDegree[$s] = ($graphDegree[$s]) + 1 }
            if ($t -and $t -ne $s) { $graphDegree[$t] = ($graphDegree[$t]) + 1 }
        }
    } catch {
        Write-Host "  (graph.json could not be parsed - connectivity signal will be Unknown)" -ForegroundColor Yellow
    }
} else {
    Write-Host "  graphify-out/graph.json not found - connectivity signal will be Unknown" -ForegroundColor Yellow
}

# ---------------------------------------------------------------------------
# 1. Enumerate controllers = candidate modules
# ---------------------------------------------------------------------------

$controllerFiles = @()
$controllerFiles += Get-ChildItem "PEMS.Web\Controllers" -Filter "*Controller.cs" -File
$areaControllerFiles = Get-ChildItem "PEMS.Web\Areas" -Filter "*Controller.cs" -File -Recurse
$controllerFiles += $areaControllerFiles

Write-Host "Found $($controllerFiles.Count) controllers" -ForegroundColor Cyan

function Get-AreaName([System.IO.FileInfo]$file) {
    if ($file.FullName -match '\\Areas\\([^\\]+)\\Controllers\\') { return $Matches[1] }
    return ""
}

function Get-InjectedServices([string]$content) {
    $names = New-Object System.Collections.Generic.List[string]
    $fieldMatches = [regex]::Matches($content, '(?m)^\s*private\s+(readonly\s+)?(I[A-Za-z0-9_]+)\s+_[A-Za-z0-9_]+\s*;')
    foreach ($fm in $fieldMatches) {
        $t = $fm.Groups[2].Value
        if ($t -match '^(I[A-Za-z0-9_]*(Service|Repository|Broadcaster))$' -and -not $names.Contains($t)) {
            $names.Add($t)
        }
    }
    # PowerShell unwraps a returned collection through the output stream: an empty list
    # becomes $null and a single-item list becomes a bare scalar at the call site, which
    # then serializes wrong (ConvertTo-Json emits {} or a raw string instead of an array).
    # The unary comma forces the List[string] itself through as one object.
    return ,$names
}

function Get-ActionCount([string]$content) {
    $pattern = '(?m)^\s*(public|protected)\s+(async\s+)?(Task<)?(IActionResult|ActionResult(<[^>]*>)?|JsonResult|FileResult|FileStreamResult|FileContentResult|ContentResult|RedirectResult|RedirectToActionResult|PartialViewResult|ViewResult)\b[^;{]*\('
    return ([regex]::Matches($content, $pattern)).Count
}

function Test-EntityExists([string]$module) {
    if ($entityBaseNames -contains $module) { return $true }
    if ($entityBaseNames -contains ($module + "s")) { return $true }
    if ($module.EndsWith("s") -and ($entityBaseNames -contains $module.Substring(0, $module.Length - 1))) { return $true }
    $fuzzy = $entityBaseNames | Where-Object { $module.Length -ge 4 -and $_ -match [regex]::Escape($module) }
    if ($fuzzy) { return $true }
    return $false
}

function Test-DbSetExists([string]$module) {
    if ([string]::IsNullOrEmpty($dbContextContent)) { return "Unknown" }
    $dbSetMatches = [regex]::Matches($dbContextContent, 'DbSet<(\w+)>\s*(\w+)')
    foreach ($m in $dbSetMatches) {
        $typeName = $m.Groups[1].Value
        $propName = $m.Groups[2].Value
        if ($typeName -match [regex]::Escape($module) -or $propName -match [regex]::Escape($module)) { return "Yes" }
    }
    return "No"
}

function Test-MigrationEvidence([string]$module) {
    if ([string]::IsNullOrEmpty($migrationsContent)) { return "Unknown" }
    if ($module.Length -lt 4) { return "Unknown" }
    if ($migrationsContent -match [regex]::Escape($module)) { return "Yes" }
    return "Unknown"
}

function Get-GitInfo([string]$relativePath) {
    $result = [ordered]@{ lastTouched = $null; commitCount = 0 }
    try {
        $last = git log -1 --format=%ad --date=short -- "$relativePath" 2>$null
        if ($LASTEXITCODE -eq 0 -and $last) { $result.lastTouched = $last.Trim() }
        $count = git log --oneline -- "$relativePath" 2>$null
        if ($count) { $result.commitCount = ($count | Measure-Object -Line).Lines }
    } catch { }
    return $result
}

$modules = @()

foreach ($file in $controllerFiles) {
    $area = Get-AreaName $file
    $module = $file.BaseName -replace 'Controller$', ''
    $relPath = (Resolve-Path $file.FullName -Relative).Substring(2) -replace '\\', '/'
    $content = Get-Content -Raw $file.FullName

    $actionCount = Get-ActionCount $content
    $postCount = ([regex]::Matches($content, '\[HttpPost\]')).Count
    $explicitAuthorize = $content -match '\[Authorize'
    $allowAnonymous = $content -match '\[AllowAnonymous'
    $injected = Get-InjectedServices $content

    # Redirect-shim heuristic: this controller's whole job is forwarding to another
    # controller (e.g. legacy Admin-area shims) - directly observable, not inferred.
    $isRedirectShim = ($actionCount -le 2) -and ($content -match 'RedirectToAction\(')

    $serviceImplementationFound = $false
    foreach ($svc in $injected) {
        $implFiles = Get-ChildItem "PEMS.Persistence\Servises" -Filter "*.cs" -File -Recurse -ErrorAction SilentlyContinue |
            Where-Object { (Get-Content -Raw $_.FullName) -match "class\s+\w+\s*:\s*[^{]*\b$svc\b" }
        if ($implFiles) { $serviceImplementationFound = $true }
    }

    $viewsPath = if ($area) { "PEMS.Web\Areas\$area\Views\$module" } else { "PEMS.Web\Views\$module" }
    $viewCount = 0
    if (Test-Path $viewsPath) {
        $viewCount = (Get-ChildItem $viewsPath -Filter "*.cshtml" -File -ErrorAction SilentlyContinue).Count
    }

    $navPattern = 'asp-controller="' + [regex]::Escape($module) + '"'
    $inSidebarNav = $sidebarContent -match $navPattern

    $controllerClassName = $file.BaseName
    $inTests = $false
    if ($testsContent -ne "") {
        if ($testsContent -match [regex]::Escape($controllerClassName)) { $inTests = $true }
        foreach ($svc in $injected) {
            if ($testsContent -match [regex]::Escape($svc)) { $inTests = $true }
        }
    }

    $inDocs = ($module.Length -ge 3) -and ($docsContent -match [regex]::Escape($module))

    $entityFound = Test-EntityExists $module
    $dbSetFound = Test-DbSetExists $module
    $migrationEvidence = Test-MigrationEvidence $module

    $git = Get-GitInfo $relPath

    $connectivity = 0
    if ($graphDegree.ContainsKey($relPath)) { $connectivity = $graphDegree[$relPath] }

    # ---- Transparent evidence checklist (Present / Missing / Unknown) ----
    $checklist = [ordered]@{
        Controller       = "Present"
        ServiceLayer     = if ($injected.Count -eq 0) { "Missing" } elseif ($serviceImplementationFound) { "Present" } else { "Unknown" }
        EntityOrDbSet    = if ($entityFound -or $dbSetFound -eq "Yes") { "Present" } elseif ($dbSetFound -eq "Unknown") { "Unknown" } else { "Missing" }
        MigrationBacked  = $migrationEvidence
        RazorViews       = if ($viewCount -gt 0) { "Present" } else { "Missing" }
        SidebarReachable = if ($inSidebarNav) { "Present" } else { "Missing" }
        ExplicitAuthZ    = if ($explicitAuthorize) { "Present" } elseif ($allowAnonymous) { "AllowAnonymous" } else { "GlobalPolicyOnly" }
        TestCoverage     = if ($inTests) { "Present" } else { "Missing" }
        Documented       = if ($inDocs) { "Present" } else { "Missing" }
    }

    $present = ($checklist.Values | Where-Object { $_ -eq "Present" }).Count
    $unknown = ($checklist.Values | Where-Object { $_ -eq "Unknown" }).Count
    $total = $checklist.Count
    $coveragePct = [math]::Round(($present / $total) * 100)

    $modules += [ordered]@{
        module               = $module
        area                 = $area
        controllerPath       = $relPath
        actionCount          = $actionCount
        postActionCount      = $postCount
        isRedirectShim       = [bool]$isRedirectShim
        injectedServices     = $injected
        viewsPath            = ($viewsPath -replace '\\', '/')
        viewCount            = $viewCount
        inSidebarNav         = [bool]$inSidebarNav
        inTests              = [bool]$inTests
        inDocs               = [bool]$inDocs
        gitLastTouched       = $git.lastTouched
        gitCommitCount       = $git.commitCount
        graphConnectivity    = $connectivity
        evidence             = $checklist
        evidencePresent      = $present
        evidenceUnknown      = $unknown
        evidenceTotal        = $total
        evidenceCoveragePct  = $coveragePct
    }

    Write-Host ("  {0,-28} {1,3}% coverage  ({2}/{3} present, {4} unknown)" -f $module, $coveragePct, $present, $total, $unknown) -ForegroundColor DarkGray
}

# ---------------------------------------------------------------------------
# 2. Repo-level signals (git state, build health note)
# ---------------------------------------------------------------------------

$branch = (git rev-parse --abbrev-ref HEAD 2>$null)
$lastCommit = (git log -1 --format="%h %ad %s" --date=short 2>$null)
$totalCommits = (git log --oneline 2>$null | Measure-Object -Line).Lines

$sortedByCoverage = $modules | Sort-Object evidenceCoveragePct
$stubs = $modules | Where-Object { $_.isRedirectShim }
$noUi = $modules | Where-Object { $_.viewCount -eq 0 -and -not $_.isRedirectShim }
$noNav = $modules | Where-Object { -not $_.inSidebarNav -and -not $_.isRedirectShim }
$noTests = $modules | Where-Object { -not $_.inTests }

$report = [ordered]@{
    generatedAtUtc   = (Get-Date).ToUniversalTime().ToString("o")
    repoRoot         = $RepoRoot
    branch           = $branch
    lastCommit       = $lastCommit
    totalCommits     = $totalCommits
    moduleCount      = $modules.Count
    stubShimCount    = $stubs.Count
    noUiCount        = $noUi.Count
    notInNavCount    = $noNav.Count
    noTestCount      = $noTests.Count
    modules          = $modules
}

New-Item -ItemType Directory -Force -Path "graphify-out" | Out-Null
$jsonPath = "graphify-out\project-intelligence.json"
$report | ConvertTo-Json -Depth 8 | Set-Content -Encoding UTF8 -Path $jsonPath
Write-Host "`nWrote $jsonPath" -ForegroundColor Green

# ---------------------------------------------------------------------------
# 3. Markdown report
# ---------------------------------------------------------------------------

$md = New-Object System.Text.StringBuilder
[void]$md.AppendLine("# PEMS Project Intelligence Report")
[void]$md.AppendLine("")
[void]$md.AppendLine("Generated: $($report.generatedAtUtc) - Branch: ``$branch`` - Last commit: ``$lastCommit``")
[void]$md.AppendLine("")
[void]$md.AppendLine("This report is generated evidence, not a manual estimate. Each module below is a")
[void]$md.AppendLine("controller found in the repository; each signal is checked directly against source,")
[void]$md.AppendLine("EF configuration, migrations, views, sidebar navigation, tests, docs, and git history.")
[void]$md.AppendLine("`Evidence coverage %` = (signals confirmed Present) / (signals checked). It is a")
[void]$md.AppendLine("mechanical ratio over the checklist below it, not an estimate of feature completeness -")
[void]$md.AppendLine("read the checklist, not just the percentage. `Unknown` means the scan could not confirm")
[void]$md.AppendLine("either way (never treat `Unknown` as `Missing`).")
[void]$md.AppendLine("")
[void]$md.AppendLine("## Summary")
[void]$md.AppendLine("")
[void]$md.AppendLine("- Controllers scanned (candidate modules): **$($report.moduleCount)**")
[void]$md.AppendLine("- Redirect-shim controllers (no real logic, forward elsewhere): **$($report.stubShimCount)**")
[void]$md.AppendLine("- Controllers with zero Razor views found: **$($report.noUiCount)** (may be legitimate JSON/API controllers - verify before assuming broken)")
[void]$md.AppendLine("- Controllers not linked from the sidebar menu: **$($report.notInNavCount)** (may be reachable via deep link, drilldown, or another page)")
[void]$md.AppendLine("- Controllers with no reference found in PEMS.Tests: **$($report.noTestCount)**")
[void]$md.AppendLine("")
[void]$md.AppendLine("## Modules (sorted by evidence coverage, lowest first)")
[void]$md.AppendLine("")
[void]$md.AppendLine("| Module | Area | Coverage | Present/Total | Unknown | Actions | Views | Nav | Tests | Docs | Last touched |")
[void]$md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|")
foreach ($m in $sortedByCoverage) {
    $areaLabel = if ($m.area) { $m.area } else { "-" }
    $navMark = if ($m.inSidebarNav) { "yes" } else { "no" }
    $testMark = if ($m.inTests) { "yes" } else { "no" }
    $docMark = if ($m.inDocs) { "yes" } else { "no" }
    $lastTouch = if ($m.gitLastTouched) { $m.gitLastTouched } else { "-" }
    $shimMark = if ($m.isRedirectShim) { " (redirect shim)" } else { "" }
    [void]$md.AppendLine("| $($m.module)$shimMark | $areaLabel | $($m.evidenceCoveragePct)% | $($m.evidencePresent)/$($m.evidenceTotal) | $($m.evidenceUnknown) | $($m.actionCount) | $($m.viewCount) | $navMark | $testMark | $docMark | $lastTouch |")
}
[void]$md.AppendLine("")
[void]$md.AppendLine("## Detailed evidence checklist per module")
[void]$md.AppendLine("")
foreach ($m in $sortedByCoverage) {
    [void]$md.AppendLine("### $($m.module) $(if ($m.area) { "(Area: $($m.area))" })")
    [void]$md.AppendLine("")
    [void]$md.AppendLine("- Controller: ``$($m.controllerPath)``")
    [void]$md.AppendLine("- Injected services: $(if ($m.injectedServices.Count -gt 0) { ($m.injectedServices -join ', ') } else { '(none detected)' })")
    [void]$md.AppendLine("- Views path checked: ``$($m.viewsPath)`` ($($m.viewCount) .cshtml files)")
    [void]$md.AppendLine("- Git: last touched $(if ($m.gitLastTouched) { $m.gitLastTouched } else { 'unknown' }), $($m.gitCommitCount) commit(s) touching this file")
    [void]$md.AppendLine("- Graphify connectivity (edges touching this file in the graph): $($m.graphConnectivity)")
    [void]$md.AppendLine("")
    [void]$md.AppendLine("| Signal | Result |")
    [void]$md.AppendLine("|---|---|")
    foreach ($key in $m.evidence.Keys) {
        [void]$md.AppendLine("| $key | $($m.evidence[$key]) |")
    }
    [void]$md.AppendLine("")
}

[void]$md.AppendLine("## Non-negotiable reminders for anyone acting on this report")
[void]$md.AppendLine("")
[void]$md.AppendLine("1. A green checklist item means evidence was found in the repo - it does not mean the")
[void]$md.AppendLine("   feature was manually verified working end-to-end. Distinguish *Implemented* from *Verified*.")
[void]$md.AppendLine("2. `dotnet build` succeeding is a health indicator, not a completion signal.")
[void]$md.AppendLine("3. Zero views or zero nav links can be entirely correct for API/AJAX-only controllers -")
[void]$md.AppendLine("   confirm by reading the controller before treating it as unfinished.")
[void]$md.AppendLine("4. Re-run this generator after any structural change: ``pwsh scripts/project-intelligence/Generate-ProjectIntelligence.ps1``")

$mdPath = "PEMS_PROJECT_INTELLIGENCE_REPORT.md"
$md.ToString() | Set-Content -Encoding UTF8 -Path $mdPath
Write-Host "Wrote $mdPath" -ForegroundColor Green

Write-Host "`nDone. $($modules.Count) modules scanned." -ForegroundColor Cyan
