param(
    [ValidateRange(1, 65535)]
    [int]$Port = 3000,
    [string]$Database = '',
    [switch]$Build,
    [switch]$InstallDependencies,
    [switch]$OpenBrowser
)
$ErrorActionPreference = 'Stop'
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    throw 'Install Node.js 24 LTS, then double-click Skynet Arena.cmd again.'
}
Push-Location -LiteralPath $PSScriptRoot
$previousPort = $env:SKYNET_ARENA_PORT
$previousDatabase = $env:SKYNET_ARENA_DATABASE
$browserJob = $null
$arenaUrl = "http://127.0.0.1:$Port"
try {
    # A second double-click should reopen the existing default instance.
    if ($OpenBrowser -and -not $Database) {
        try {
            $existing = Invoke-RestMethod "$arenaUrl/api/system" -TimeoutSec 2
            if ($existing.endpoints.web -eq $arenaUrl -and $existing.windbots -and $existing.srvpros) {
                Write-Host "Arena is already running at $arenaUrl"
                Start-Process $arenaUrl
                return
            }
        }
        catch { }
    }
    $dependencyCheck = @'
try {
    require('express');
    require.resolve('vite');
    require.resolve('@vitejs/plugin-vue');
    require.resolve('vue');
    require.resolve('naive-ui');
    const Database = require('better-sqlite3');
    const database = new Database(':memory:');
    database.close();
} catch (error) {
    console.log(error.message);
    process.exit(1);
}
'@
    $dependencyOutput = & node -e $dependencyCheck 2>&1
    if ($LASTEXITCODE -ne 0 -and $InstallDependencies) {
        if (-not (Get-Command npm.cmd -ErrorAction SilentlyContinue)) {
            throw 'npm is missing. Repair the Node.js installation, then double-click Skynet Arena.cmd again.'
        }
        Write-Host 'Installing Arena dependencies. This first-time repair can take a few minutes.'
        & npm.cmd ci
        if ($LASTEXITCODE -ne 0) {
            throw 'Dependency installation failed. If npm reports EPERM on better_sqlite3.node, close all Arena consoles using this checkout (Ctrl+C), then double-click Skynet Arena.cmd again. Keep this console output for other npm errors.'
        }
        $dependencyOutput = & node -e $dependencyCheck 2>&1
    }
    if ($LASTEXITCODE -ne 0) {
        throw "Arena dependencies are missing, incomplete, or incompatible with this Node version. Stop every Arena console using this checkout (Ctrl+C), then run npm ci here and retry. Windows cannot replace better_sqlite3.node while Arena is running. Details: $dependencyOutput"
    }
    if ($Build -or -not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'dist/index.html'))) {
        & npm.cmd run build
        if ($LASTEXITCODE -ne 0) { throw 'Skynet Arena frontend build failed.' }
    }
    $env:SKYNET_ARENA_PORT = [string]$Port
    if ($Database) { $env:SKYNET_ARENA_DATABASE = $Database }
    Write-Host "Skynet Arena - open $arenaUrl"
    Write-Host 'Keep this console open. Press Ctrl+C to stop Arena and its owned duel processes.'
    if ($OpenBrowser) {
        $browserJob = Start-Job -ArgumentList $arenaUrl -ScriptBlock {
            param($Url)
            for ($attempt = 0; $attempt -lt 30; $attempt++) {
                try {
                    $system = Invoke-RestMethod "$Url/api/system" -TimeoutSec 1
                    if ($system.endpoints.web -eq $Url -and $system.windbots -and $system.srvpros) {
                        Start-Process $Url
                        return
                    }
                }
                catch { }
                Start-Sleep -Milliseconds 500
            }
        }
    }
    & node server/index.js
    if ($LASTEXITCODE -ne 0) { throw "Skynet Arena exited with code $LASTEXITCODE." }
}
finally {
    if ($browserJob) {
        Stop-Job $browserJob -ErrorAction SilentlyContinue
        Remove-Job $browserJob -Force -ErrorAction SilentlyContinue
    }
    $env:SKYNET_ARENA_PORT = $previousPort
    $env:SKYNET_ARENA_DATABASE = $previousDatabase
    Pop-Location
}
