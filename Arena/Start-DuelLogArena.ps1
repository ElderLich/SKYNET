param(
    [ValidateRange(1, 65535)]
    [int]$Port = 3000,
    [string]$TraceDirectory = '',
    [string]$ServerRuntime = (Join-Path $PSScriptRoot 'Artifacts/arena-server-v2'),
    [switch]$SkipWindBotBuild,
    [switch]$SkipArenaBuild
)

$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$windBotRoot = Join-Path $workspaceRoot 'WindBot'
$windBotRuntime = Join-Path $windBotRoot 'bin\Release'
$windBotExecutable = Join-Path $windBotRuntime 'WindBot.exe'
$botTool = Join-Path $windBotRoot 'Tools\bot_tool.py'
$databaseSource = Join-Path $workspaceRoot 'DeckWorkshop\Data\standard\cards.cdb'
$databaseDestination = Join-Path $windBotRuntime 'cards.cdb'
$arenaLauncher = Join-Path $PSScriptRoot 'Start-SkynetArena.ps1'
$arenaUrl = "http://127.0.0.1:$Port"
$serverTool = Join-Path $PSScriptRoot 'Tools\arena_server.py'
$serverRuntime = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ServerRuntime)
. (Join-Path $PSScriptRoot 'Tools/LocalArenaLauncher.ps1')

function Get-ExplicitTraceDirectory {
    param([string]$RequestedDirectory)

    $candidate = $RequestedDirectory
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        $candidate = Join-Path $workspaceRoot 'Artifacts\windbot-duel-traces'
    }
    if ($candidate -notmatch '^(?:[A-Za-z]:[\\/]|[\\/]{2}[^\\/]+[\\/][^\\/]+)') {
        throw 'TraceDirectory must be an absolute non-root directory.'
    }
    $fullPath = [IO.Path]::GetFullPath($candidate)
    $root = [IO.Path]::GetPathRoot($fullPath)
    $normalizedPath = $fullPath.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $normalizedRoot = $root.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    if ([string]::IsNullOrWhiteSpace($root) -or
        [string]::Equals($normalizedPath, $normalizedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'TraceDirectory must be an absolute non-root directory.'
    }
    return $fullPath
}

function Set-LocalArenaSettings {
    param([string]$OutputDirectory)

    $public = Invoke-RestMethod "$arenaUrl/api/settings" -TimeoutSec 5
    $settings = $public.settings
    $enabled = 0
    foreach ($name in @('current', 'old')) {
        $instance = $settings.windbots.$name
        if ($instance.mode -ne 'local') {
            Write-Host "Skipping $name agent because its mode is '$($instance.mode)'."
            continue
        }
        $instance.traceEnabled = $true
        $instance.traceDirectory = $OutputDirectory
        $enabled++
    }
    if ($enabled -eq 0) {
        throw 'No Local WindBot agent is configured. Select Local WindBot under Settings -> Agents and retry.'
    }
    $server = Start-DedicatedArenaServer
    $serverId = Set-PreparedServerSettings $settings $server
    Save-LocalArenaSettings $public
    Write-Host "Arena now uses the prepared local SRVPro; structured duel traces are enabled for $enabled Local WindBot agent(s). Select server $serverId for this runtime."
}

if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    throw 'Install Node.js 24 LTS, then double-click Duel Log Arena.bat again.'
}
if (-not (Get-Command python -ErrorAction SilentlyContinue)) {
    throw 'Python 3 is required to build WindBot.'
}
if (-not (Test-Path -LiteralPath $botTool -PathType Leaf)) {
    throw "WindBot build tool is missing: $botTool"
}
if (-not (Test-Path -LiteralPath $arenaLauncher -PathType Leaf)) {
    throw "Arena launcher is missing: $arenaLauncher"
}

$resolvedTraceDirectory = Get-ExplicitTraceDirectory $TraceDirectory
[void](New-Item -ItemType Directory -Path $resolvedTraceDirectory -Force)

if (-not $SkipWindBotBuild) {
    Write-Host 'Building normal WindBot Release with structured trace support...'
    Invoke-CheckedCommand python @($botTool, 'build', '--configuration', 'Release') 'WindBot Release build failed. Close any running WindBot server and retry'
}
if (-not (Test-Path -LiteralPath $windBotExecutable -PathType Leaf)) {
    throw "WindBot Release runtime is missing: $windBotExecutable"
}
if (-not (Test-Path -LiteralPath $databaseDestination -PathType Leaf)) {
    if (-not (Test-Path -LiteralPath $databaseSource -PathType Leaf)) {
        throw "WindBot cards.cdb is missing and no versioned Standard database is available: $databaseSource"
    }
    Copy-Item -LiteralPath $databaseSource -Destination $databaseDestination
}

Start-ArenaIfNeeded
Set-LocalArenaSettings $resolvedTraceDirectory
Write-Host "Duel logs will be written below: $resolvedTraceDirectory"
Write-Host 'Each Arena run is separated by experiment, room and participant.'
Start-Process $arenaUrl
