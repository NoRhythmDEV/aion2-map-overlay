$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$testRoot = Join-Path $projectRoot ('artifacts/updater-helper-' + [Guid]::NewGuid().ToString('N'))
$install = Join-Path $testRoot 'install'
$payload = Join-Path $testRoot 'payload'
New-Item -ItemType Directory -Path (Join-Path $install 'data'),$payload -Force | Out-Null
Set-Content -LiteralPath (Join-Path $install 'data/cache.txt') -Value 'preserve data'
Set-Content -LiteralPath (Join-Path $install 'settings.local.json') -Value 'preserve settings'
function Make-Payload {
    Set-Content -LiteralPath (Join-Path $payload 'app.txt') -Value 'new application'
    Set-Content -LiteralPath (Join-Path $payload 'locked.txt') -Value 'new secondary file'
    $files = @(Get-ChildItem -LiteralPath $payload -File | Where-Object Name -ne 'update-manifest.json' | ForEach-Object {
        @{ path = $_.Name; size = $_.Length; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
    @{ version = '0.1.0'; files = $files } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $payload 'update-manifest.json')
}
function Run-Helper {
    $helper = Join-Path $PSScriptRoot 'ApplyUpdate.ps1'
    $powershell = Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe'
    $process = Start-Process -FilePath $powershell -ArgumentList @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', ('"' + $helper + '"'), '-InstallPath', ('"' + $install + '"'), '-PayloadPath', ('"' + $payload + '"'), '-NoRestart') -WindowStyle Hidden -Wait -PassThru
    return $process.ExitCode
}
Make-Payload
if ((Run-Helper) -ne 0) { Get-Content (Join-Path $install 'update-error.txt'); throw 'Successful update test failed.' }
if ((Get-Content (Join-Path $install 'app.txt') -Raw).Trim() -ne 'new application') { throw 'Update was not applied.' }
if ((Get-Content (Join-Path $install 'data/cache.txt') -Raw).Trim() -ne 'preserve data' -or (Get-Content (Join-Path $install 'settings.local.json') -Raw).Trim() -ne 'preserve settings') { throw 'User files changed.' }
Set-Content -LiteralPath (Join-Path $install 'app.txt') -Value 'previous application'
$lock = [IO.File]::Open((Join-Path $install 'locked.txt'), [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
try {
    if ((Run-Helper) -eq 0) { throw 'Locked destination should fail.' }
    if ((Get-Content (Join-Path $install 'app.txt') -Raw).Trim() -ne 'previous application') { throw 'Rollback did not restore the application.' }
} finally { $lock.Dispose() }
$manifest = Get-Content -LiteralPath (Join-Path $payload 'update-manifest.json') -Raw | ConvertFrom-Json
$manifest.files[0].path = 'data/cache.txt'
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $payload 'update-manifest.json')
if ((Run-Helper) -eq 0) { throw 'Protected data path was accepted.' }
if ((Get-Content (Join-Path $install 'data/cache.txt') -Raw).Trim() -ne 'preserve data') { throw 'Protected data was modified.' }
Write-Output 'Update apply, data/settings preservation, rollback, and protected-path checks passed.'
