param([Parameter(Mandatory)][string]$InstallPath, [Parameter(Mandatory)][string]$PayloadPath, [int]$ParentProcessId = 0, [switch]$NoRestart)
$ErrorActionPreference = 'Stop'
$installRoot = [IO.Path]::GetFullPath($InstallPath).TrimEnd('\') + '\'
$payloadRoot = [IO.Path]::GetFullPath($PayloadPath).TrimEnd('\') + '\'
$backupRoot = Join-Path (Split-Path $payloadRoot.TrimEnd('\') -Parent) 'backup'
$changed = [Collections.Generic.List[object]]::new()
function Get-Sha256([string]$path) {
    $stream = [IO.File]::OpenRead($path)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
    finally { $stream.Dispose(); $sha.Dispose() }
}
function Resolve-PackagePath([string]$root, [string]$relative) {
    $parts = $relative.Replace('\', '/').Split('/')
    foreach ($part in $parts) {
        if (!$part -or $part -in @('.', '..') -or $part.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0 -or $part.EndsWith('.') -or $part.EndsWith(' ')) { throw 'Invalid update path.' }
    }
    if ($parts[0] -match '^(data|captures)$|^settings\.local|^\.') { throw 'Update cannot change local data/settings.' }
    $resolved = [IO.Path]::GetFullPath((Join-Path $root $relative))
    if (!$resolved.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw 'Update path escapes target folder.' }
    # Reject existing junctions/symlinks in the installation path.
    $walk = [IO.Path]::GetDirectoryName($resolved)
    while ($walk -and ($walk + '\').StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
        if ((Test-Path -LiteralPath $walk) -and ((Get-Item -LiteralPath $walk).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Linked install directories are unsupported.' }
        $walk = [IO.Path]::GetDirectoryName($walk)
    }
    return $resolved
}
try {
    if ($ParentProcessId -gt 0) {
        $parent = Get-Process -Id $ParentProcessId -ErrorAction SilentlyContinue
        if ($parent -and !$parent.WaitForExit(60000)) { throw 'Application did not exit; update canceled.' }
    }
    $manifest = Get-Content -LiteralPath (Join-Path $payloadRoot 'update-manifest.json') -Raw | ConvertFrom-Json
    if (!$manifest.files -or $manifest.version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid update manifest.' }
    foreach ($file in $manifest.files) {
        $source = Resolve-PackagePath $payloadRoot $file.path
        $destination = Resolve-PackagePath $installRoot $file.path
        if ((Get-Item -LiteralPath $source).Length -ne $file.size -or (Get-Sha256 $source) -ne $file.sha256) { throw 'Update verification failed.' }
        if (Test-Path -LiteralPath $destination) {
            if ((Get-Item -LiteralPath $destination).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked application files are unsupported.' }
            $backup = Join-Path $backupRoot $file.path
            New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($backup)) -Force | Out-Null
            Copy-Item -LiteralPath $destination -Destination $backup
        }
    }
    foreach ($file in $manifest.files) {
        $source = Resolve-PackagePath $payloadRoot $file.path
        $destination = Resolve-PackagePath $installRoot $file.path
        $changed.Add([pscustomobject]@{ Destination = $destination; Backup = (Join-Path $backupRoot $file.path); Existed = (Test-Path -LiteralPath $destination) })
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $destination -Force
    }
    $installedManifest = Join-Path $installRoot 'update-manifest.json'
    $manifestBackup = Join-Path $backupRoot 'update-manifest.json'
    if (Test-Path -LiteralPath $installedManifest) { Copy-Item -LiteralPath $installedManifest -Destination $manifestBackup -Force }
    $changed.Add([pscustomobject]@{ Destination = $installedManifest; Backup = $manifestBackup; Existed = (Test-Path -LiteralPath $installedManifest) })
    Copy-Item -LiteralPath (Join-Path $payloadRoot 'update-manifest.json') -Destination $installedManifest -Force
    if (!$NoRestart) { Start-Process -FilePath (Join-Path $installRoot 'aion2-map-overlay.exe') -WorkingDirectory $installRoot }
} catch {
    $failure = $_.ToString()
    foreach ($item in $changed) {
        try {
            if ($item.Existed) { Copy-Item -LiteralPath $item.Backup -Destination $item.Destination -Force }
            elseif (Test-Path -LiteralPath $item.Destination) { Remove-Item -LiteralPath $item.Destination -Force }
        } catch { $failure += "`r`nRollback failed: $_" }
    }
    $failure | Set-Content -LiteralPath (Join-Path $installRoot 'update-error.txt')
    exit 1
}
