# Builds every QuickLook .NET project against .NET 8 (net8.0-windows).
# Usage: .\build-net8.ps1

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

$projects = @(
    (Join-Path $root 'QuickLook.Common\QuickLook.Common.csproj'),
    (Join-Path $root 'QuickLook\QuickLook.csproj')
)
$projects += Get-ChildItem (Join-Path $root 'QuickLook.Plugin') -Recurse -Filter *.csproj |
    Select-Object -ExpandProperty FullName

$failed = @()
foreach ($p in $projects) {
    Write-Host "Building $([IO.Path]::GetFileName($p))..."
    & dotnet build $p -c Release -v minimal --nologo
    if ($LASTEXITCODE -ne 0) {
        $failed += $p
    }
}

if ($failed.Count -gt 0) {
    Write-Host "FAILED projects:" -ForegroundColor Red
    $failed | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}

Write-Host "All $($projects.Count) projects built successfully." -ForegroundColor Green
