$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
dotnet publish (Join-Path $projectRoot 'src/Aion2MapOverlay.App/Aion2MapOverlay.App.csproj') -c Release -r win-x64 --self-contained true -o (Join-Path $projectRoot 'artifacts/portable')
if ($LASTEXITCODE -ne 0) { throw 'Portable publish failed.' }
