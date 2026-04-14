<# PowerShell script to run the AgentContainers generator #>
param(
    [string]$Command = "generate",
    [string]$RepoRoot
)

$ErrorActionPreference = "Stop"

if (-not $RepoRoot) {
    $RepoRoot = (Get-Item "$PSScriptRoot\..").FullName
}

$GeneratorProject = Join-Path $RepoRoot "src\AgentContainers.Generator\AgentContainers.Generator.csproj"

if (-not (Test-Path $GeneratorProject)) {
    Write-Error "Generator project not found at: $GeneratorProject"
    exit 1
}

Write-Host "Running AgentContainers Generator ($Command)..." -ForegroundColor Cyan
Write-Host ""

Push-Location $RepoRoot
try {
    dotnet run --project $GeneratorProject -- $Command
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Generator exited with code $LASTEXITCODE"
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
}
