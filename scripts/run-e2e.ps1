<#
.SYNOPSIS
    AgentContainers E2E Test Runner (PowerShell)
.DESCRIPTION
    Builds generated Docker images and validates runtime behavior using
    manifest-defined validation commands.
.PARAMETER Scope
    Test scope: quick, bases, agents, compose, full
.PARAMETER Filter
    Comma-separated image IDs to test
.PARAMETER NoCleanup
    Skip Docker image cleanup after tests
.EXAMPLE
    .\scripts\run-e2e.ps1 -Scope quick
    .\scripts\run-e2e.ps1 -Scope bases -Filter python,node-bun
#>
param(
    [ValidateSet("quick", "bases", "agents", "compose", "full")]
    [string]$Scope = "quick",
    [string]$Filter = "",
    [switch]$NoCleanup
)

$ErrorActionPreference = "Continue"
$RepoRoot = (Get-Item "$PSScriptRoot\..").FullName
$GenProj = Join-Path $RepoRoot "src\AgentContainers.Generator\AgentContainers.Generator.csproj"

$script:Pass = 0
$script:Fail = 0
$script:Skip = 0
$script:Errors = @()
$script:Tags = @()

function Log-Pass  { param($m) Write-Host "  [PASS] $m" -ForegroundColor Green;  $script:Pass++ }
function Log-Fail  { param($m) Write-Host "  [FAIL] $m" -ForegroundColor Red;    $script:Errors += $m; $script:Fail++ }
function Log-Skip  { param($m) Write-Host "  [SKIP] $m" -ForegroundColor Yellow; $script:Skip++ }
function Log-Info  { param($m) Write-Host "  [INFO] $m" -ForegroundColor Cyan }
function Log-Hdr   { param($m) Write-Host "`n=== $m ===" -ForegroundColor White }

function Test-Filter { param($id)
    if ([string]::IsNullOrEmpty($Filter)) { return $true }
    return ($Filter -split ",") -contains $id
}

function Invoke-Build { param($ctx, $tag)
    $full = Join-Path $RepoRoot $ctx
    if (-not (Test-Path (Join-Path $full "Dockerfile"))) {
        Log-Fail "Build $tag - Dockerfile not found"
        return $false
    }
    Log-Info "Building $tag from $ctx..."
    docker build -t $tag $full 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        $script:Tags += $tag
        return $true
    }
    return $false
}

function Invoke-Validate { param($tag, $cmd)
    docker run --rm $tag bash -c $cmd 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) { Log-Pass "$tag - $cmd" }
    else { Log-Fail "$tag - $cmd" }
}

# --- Generate E2E Plan ---
Log-Hdr "Generating E2E Test Plan"
Push-Location $RepoRoot
try {
    $raw = dotnet run --project $GenProj --configuration Release -- emit-e2e-plan 2>$null
    if (-not $raw) { Write-Error "Failed to emit e2e plan"; exit 1 }
    $plan = $raw | ConvertFrom-Json
    Log-Info ("Plan: {0} bases, {1} combos, {2} agents, {3} compose" -f $plan.bases.Count, $plan.combos.Count, $plan.agents.Count, $plan.compose_stacks.Count)
} finally { Pop-Location }

# --- Test Bases ---
function Test-Bases {
    Log-Hdr "Testing Base Images"
    foreach ($b in $plan.bases) {
        if (-not (Test-Filter $b.id)) { Log-Skip "Base $($b.id) filtered"; continue }
        if ($Scope -eq "quick" -and $b.id -ne "python") { Log-Skip "Base $($b.id) quick-skip"; continue }

        Write-Host "`n  Base: $($b.display_name) [$($b.size_class)]" -ForegroundColor White
        if (Invoke-Build $b.build_context $b.tag) {
            Log-Pass "Build: $($b.tag)"
            foreach ($c in $b.validation_commands) { Invoke-Validate $b.tag $c }
            foreach ($c in $b.common_tool_validations) { Invoke-Validate $b.tag $c }
        } else { Log-Fail "Build: $($b.tag)" }
    }
}

# --- Test Agents ---
function Test-Agents {
    Log-Hdr "Testing Agent Overlay Images"
    $comboIds = @()
    if ($plan.combos) { $comboIds = $plan.combos | ForEach-Object { $_.id } }

    foreach ($a in $plan.agents) {
        if (-not (Test-Filter $a.id)) { Log-Skip "Agent $($a.id) filtered"; continue }
        if ($Scope -eq "quick" -and $a.id -ne "node-bun-claude") { Log-Skip "Agent $($a.id) quick-skip"; continue }
        if ($comboIds -contains $a.base_id) { Log-Skip "Agent $($a.id) combo-skip"; continue }

        Write-Host "`n  Agent: $($a.display_name) [$($a.size_class)]" -ForegroundColor White
        docker image inspect $a.base_tag 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { Log-Skip "Agent $($a.id) - base not built"; continue }

        if (Invoke-Build $a.build_context $a.tag) {
            Log-Pass "Build: $($a.tag)"
            foreach ($c in $a.validation_commands) { Invoke-Validate $a.tag $c }
        } else { Log-Fail "Build: $($a.tag)" }
    }
}

# --- Test Compose ---
function Test-Compose {
    Log-Hdr "Testing Compose Stack Readiness"
    $imgTag = "agentcontainers/node-bun-claude:latest"
    docker image inspect $imgTag 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { Log-Skip "Compose: $imgTag not available"; return }

    Write-Host "`n  Compose: e2e-health-validation" -ForegroundColor White
    $dir = Join-Path $RepoRoot "generated\compose\e2e-validation"
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    $file = Join-Path $dir "docker-compose.yaml"

    # Build compose YAML line by line to avoid parse issues
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine("services:")
    [void]$sb.AppendLine("  agent-health:")
    [void]$sb.AppendLine("    image: $imgTag")
    [void]$sb.AppendLine("    command: [""sleep"", ""infinity""]")
    [void]$sb.AppendLine("    healthcheck:")
    [void]$sb.AppendLine("      test: [""CMD"", ""node"", ""--version""]")
    [void]$sb.AppendLine("      interval: 5s")
    [void]$sb.AppendLine("      timeout: 3s")
    [void]$sb.AppendLine("      retries: 10")
    [void]$sb.AppendLine("      start_period: 3s")
    [void]$sb.AppendLine("    restart: ""no""")
    [System.IO.File]::WriteAllText($file, $sb.ToString())

    $proj = "e2e-ac-$PID"
    Log-Info "Starting compose stack..."
    docker compose -f $file -p $proj up -d 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Log-Fail "Compose: stack failed to start"
        docker compose -f $file -p $proj down -v 2>&1 | Out-Null
        Remove-Item -Recurse -Force $dir -ErrorAction SilentlyContinue
        return
    }

    Log-Info "Waiting for healthy..."
    $ok = $false
    for ($i = 1; $i -le 12; $i++) {
        Start-Sleep -Seconds 5
        $st = docker compose -f $file -p $proj ps --format json 2>$null
        if ($st) {
            $parsed = $st | ConvertFrom-Json -ErrorAction SilentlyContinue
            if ($parsed -and $parsed.Health -eq "healthy") { $ok = $true; break }
        }
        Log-Info "  Attempt $i/12"
    }

    if ($ok) {
        Log-Pass "Compose: service healthy"
        docker compose -f $file -p $proj exec -T agent-health node --version 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) { Log-Pass "Compose: node --version" }
        else { Log-Fail "Compose: node --version" }
        docker compose -f $file -p $proj exec -T agent-health claude --version 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) { Log-Pass "Compose: claude --version" }
        else { Log-Fail "Compose: claude --version" }
    } else {
        Log-Fail "Compose: not healthy within timeout"
    }

    Log-Info "Tearing down..."
    docker compose -f $file -p $proj down -v 2>&1 | Out-Null
    Remove-Item -Recurse -Force $dir -ErrorAction SilentlyContinue
}

# === Main ===
Log-Hdr "AgentContainers E2E Tests"
Write-Host "  Scope: $Scope"
if ($Filter) { Write-Host "  Filter: $Filter" }

$sw = [System.Diagnostics.Stopwatch]::StartNew()

switch ($Scope) {
    "quick"   { Test-Bases; Test-Agents; Test-Compose }
    "bases"   { Test-Bases }
    "agents"  { Test-Bases; Test-Agents }
    "compose" { Test-Bases; Test-Agents; Test-Compose }
    "full"    { Test-Bases; Test-Agents; Test-Compose }
}

if (-not $NoCleanup -and $script:Tags.Count -gt 0) {
    Log-Hdr "Cleanup"
    foreach ($t in $script:Tags) { docker rmi -f $t 2>&1 | Out-Null }
    Log-Info "Removed $($script:Tags.Count) image(s)"
}

$sw.Stop()
Log-Hdr "E2E Results"
Write-Host "  Passed:  $($script:Pass)" -ForegroundColor Green
Write-Host "  Failed:  $($script:Fail)" -ForegroundColor Red
Write-Host "  Skipped: $($script:Skip)" -ForegroundColor Yellow
Write-Host "  Duration: $([math]::Round($sw.Elapsed.TotalSeconds))s"

if ($script:Fail -gt 0) {
    Write-Host "`n  Failures:" -ForegroundColor Red
    foreach ($e in $script:Errors) { Write-Host "    - $e" -ForegroundColor Red }
    exit 1
}

Write-Host "`n  All e2e tests passed!" -ForegroundColor Green
exit 0
