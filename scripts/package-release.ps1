param(
    [string]$Version = '',
    [string]$OutputDirectory = 'artifacts/release'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (!$Version) { $Version = (Get-Content -LiteralPath (Join-Path $projectRoot 'VERSION') -Raw).Trim() }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Expected a stable version such as 0.1.0.' }
$outputRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
$package = Join-Path $outputRoot ('build-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $package -Force | Out-Null
dotnet publish (Join-Path $projectRoot 'src/Aion2MapOverlay.App/Aion2MapOverlay.App.csproj') -c Release -r win-x64 --self-contained true -p:Version=$Version -p:InformationalVersion=$Version -p:ContinuousIntegrationBuild=true -o $package
if ($LASTEXITCODE -ne 0) { throw 'Portable publish failed.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'packaging/release-readme.txt') -Destination (Join-Path $package 'README.txt')
Copy-Item -LiteralPath (Join-Path $projectRoot 'packaging/third-party-notices.txt') -Destination (Join-Path $package 'THIRD-PARTY-NOTICES.txt')
Copy-Item -LiteralPath (Join-Path $projectRoot 'packaging/licenses') -Destination $package -Recurse
$files = @(Get-ChildItem -LiteralPath $package -File -Recurse | ForEach-Object {
    $relative = $_.FullName.Substring($package.Length + 1).Replace('\', '/')
    if ($relative -match '^(data|captures|settings\.local|\.git)(/|\.|$)') { throw "Private/local content in release: $relative" }
    [ordered]@{ path = $relative; size = $_.Length; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
})
[ordered]@{ version = $Version; files = $files } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $package 'update-manifest.json') -Encoding utf8
$checkPath = Join-Path $outputRoot 'smoke-test'
$process = Start-Process -FilePath (Join-Path $package 'aion2-map-overlay.exe') -ArgumentList @('--smoke-test', ('"' + $checkPath + '"')) -WindowStyle Hidden -PassThru
if (!$process.WaitForExit(60000)) { $process.Kill(); throw 'Packaged smoke test timed out.' }
if ($process.ExitCode -ne 0) { Get-Content -LiteralPath (Join-Path $checkPath 'error.txt'); throw 'Packaged smoke test failed.' }
& (Join-Path $projectRoot 'scripts/test-update-helper.ps1')
$zip = Join-Path $outputRoot 'aion2-map-overlay-win-x64-portable.zip'
Compress-Archive -Path (Join-Path $package '*') -DestinationPath $zip -Force -CompressionLevel Optimal
$verifyPath = Join-Path $outputRoot ('package-check-' + [Guid]::NewGuid().ToString('N'))
$verifyProcess = Start-Process -FilePath (Join-Path $package 'aion2-map-overlay.exe') -ArgumentList @('--verify-update-package', ('"' + $verifyPath + '"'), ('"' + $zip + '"'), $Version) -WindowStyle Hidden -Wait -PassThru
if ($verifyProcess.ExitCode -ne 0) { Get-Content -LiteralPath (Join-Path $verifyPath 'error.txt'); throw 'Actual release ZIP verification failed.' }
# Keep the flat archive for installed updaters; manual downloads get one named folder.
$bundleRoot = Join-Path $outputRoot ('bundle-' + [Guid]::NewGuid().ToString('N'))
$folder = Join-Path $bundleRoot 'aion2-map-overlay'
New-Item -ItemType Directory -Path $folder -Force | Out-Null
Copy-Item -Path (Join-Path $package '*') -Destination $folder -Recurse
$downloadZip = Join-Path $outputRoot 'aion2-map-overlay-win-x64.zip'
Compress-Archive -LiteralPath $folder -DestinationPath $downloadZip -Force -CompressionLevel Optimal
$flatArchive = [IO.Compression.ZipFile]::OpenRead($zip)
$folderArchive = [IO.Compression.ZipFile]::OpenRead($downloadZip)
try {
    $flatEntries = @($flatArchive.Entries | Where-Object { !$_.FullName.EndsWith('/') })
    $downloadEntries = @($folderArchive.Entries | Where-Object { !$_.FullName.EndsWith('/') })
    if ($flatEntries.Count -ne $downloadEntries.Count) { throw 'Folder package file count differs.' }
    foreach ($entry in $downloadEntries) {
        if (!$entry.FullName.StartsWith('aion2-map-overlay/')) { throw 'Manual ZIP contains files outside the application folder.' }
        $original = $flatArchive.GetEntry($entry.FullName.Substring('aion2-map-overlay/'.Length))
        if (!$original -or $original.Length -ne $entry.Length) { throw 'Folder package content differs.' }
        $originalStream = $original.Open(); $downloadStream = $entry.Open()
        try {
            $originalHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($originalStream))
            $downloadHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($downloadStream))
            if ($originalHash -ne $downloadHash) { throw 'Folder package hash differs.' }
        } finally { $originalStream.Dispose(); $downloadStream.Dispose() }
    }
} finally { $flatArchive.Dispose(); $folderArchive.Dispose() }
@($downloadZip, $zip) | ForEach-Object {
    (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($_)
} | Set-Content -LiteralPath (Join-Path $outputRoot 'SHA256SUMS') -Encoding ascii
Write-Output "Manual download (one folder): $downloadZip"
Write-Output "Public application-only portable: $zip"
