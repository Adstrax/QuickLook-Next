# Builds every QuickLookNext .NET project against .NET 10 (net10.0-windows).
# Usage: .\build.ps1

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

# v1.2.34: build the whole solution in one parallel invocation.
& dotnet build (Join-Path $root 'QuickLookNext.slnx') -c Release -v minimal --nologo
exit $LASTEXITCODE
