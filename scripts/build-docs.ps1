param(
    [switch]$Serve,
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot\..

if (-not $NoRestore)
{
    dotnet restore
    dotnet tool restore
    dotnet build --configuration Release
}

$arguments = @('docs\docfx.json', '--warningsAsErrors')
if ($Serve)
{
    $arguments += '--serve'
}

dotnet tool run docfx @arguments
