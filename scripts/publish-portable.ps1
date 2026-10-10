$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$sourceData = Join-Path $projectRoot 'data/processed/questlog'
foreach ($required in @('map-1010.json', 'reference/World_L_A.webp', 'reference/verteron-title.png', 'icons/manifest.json')) {
    if (!(Test-Path -LiteralPath (Join-Path $sourceData $required))) { throw "Missing local data: $required" }
}
dotnet publish (Join-Path $projectRoot 'src/Aion2MapOverlay.App/Aion2MapOverlay.App.csproj') -c Release -r win-x64 --self-contained true -o (Join-Path $projectRoot 'artifacts/portable')
if ($LASTEXITCODE -ne 0) { throw 'Portable publish failed.' }
$destination = Join-Path $projectRoot 'artifacts/portable/data'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $sourceData 'map-1010.json') -Destination $destination -Force
Copy-Item -LiteralPath (Join-Path $sourceData 'reference'),(Join-Path $sourceData 'icons') -Destination $destination -Recurse -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/portable-readme.txt') -Destination (Join-Path $projectRoot 'artifacts/portable/README.txt') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/third-party-notices.txt') -Destination (Join-Path $projectRoot 'artifacts/portable/THIRD-PARTY-NOTICES.txt') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/licenses') -Destination (Join-Path $projectRoot 'artifacts/portable') -Recurse -Force
# This local package contains Questlog assets. Do not upload it as a public release
# until redistribution permission has been established.
Write-Output 'Local portable build: artifacts/portable/aion2-map-overlay.exe'
