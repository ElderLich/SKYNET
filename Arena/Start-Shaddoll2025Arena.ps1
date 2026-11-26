param(
    [ValidateRange(1, 10000)]
    [int]$GamesPerOpponent = 1,
    [string[]]$OpponentDeck = @('Lightsworn'),
    [ValidateRange(1, 65535)]
    [int]$Port = 3000,
    [string]$ServerRuntime = (Join-Path $PSScriptRoot 'Artifacts/arena-server-v2'),
    [switch]$SkipBuild,
    [switch]$SkipChecks,
    [switch]$PreflightOnly
)

$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$windBotRoot = Join-Path $workspaceRoot 'WindBot'
$runtimeDirectory = Join-Path $windBotRoot 'bin\Release'
$databaseSource = Join-Path $workspaceRoot 'DeckWorkshop\Data\standard\cards.cdb'
$databaseDestination = Join-Path $runtimeDirectory 'cards.cdb'
$botTool = Join-Path $windBotRoot 'Tools\bot_tool.py'
$challengeTool = Join-Path $PSScriptRoot 'Tools\Executors\Shaddoll2025\run_challenge.js'
$serverTool = Join-Path $PSScriptRoot 'Tools\arena_server.py'
$serverRuntime = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ServerRuntime)
$arenaLauncher = Join-Path $PSScriptRoot 'Start-SkynetArena.ps1'
$arenaUrl = "http://127.0.0.1:$Port"
$SkipArenaBuild = $false
. (Join-Path $PSScriptRoot 'Tools/LocalArenaLauncher.ps1')

if (-not (Get-Command python -ErrorAction SilentlyContinue)) {
    throw 'Python 3 is required to build and check WindBot.'
}
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    throw 'Node.js is required to start Arena.'
}

Push-Location -LiteralPath $workspaceRoot
try {
    if (-not $SkipBuild) {
        Write-Host 'Building WindBot Release...'
        Invoke-CheckedCommand python @($botTool, 'build', '--configuration', 'Release') 'WindBot Release build failed'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $runtimeDirectory 'WindBot.exe') -PathType Leaf)) {
        throw "WindBot Release runtime is missing: $runtimeDirectory"
    }
    if (-not (Test-Path -LiteralPath $databaseDestination -PathType Leaf)) {
        if (-not (Test-Path -LiteralPath $databaseSource -PathType Leaf)) { throw "Missing Standard database: $databaseSource" }
        Copy-Item -LiteralPath $databaseSource -Destination $databaseDestination
    }

    if (-not $SkipChecks) {
        $receiptRoot = Join-Path $workspaceRoot ('Artifacts\shaddoll-2025-launcher-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
        Write-Host "Writing Shaddoll receipts under $receiptRoot"
        Invoke-CheckedCommand python @(
            $botTool, 'check', '--suite', 'runtime',
            '--configuration', 'Release',
            '--database', $databaseDestination,
            '--deck', (Join-Path $windBotRoot 'Decks\AI_Shaddoll2025.ydk'),
            '--registration', 'Shaddoll2025',
            '--output', (Join-Path $receiptRoot 'runtime')
        ) 'Shaddoll runtime check failed'
        Invoke-CheckedCommand python @(
            $botTool, 'check', '--suite', 'shaddoll-2025',
            '--configuration', 'Release',
            '--database', $databaseDestination,
            '--deck', (Join-Path $windBotRoot 'Decks\AI_Shaddoll2025.ydk'),
            '--output', (Join-Path $receiptRoot 'focused')
        ) 'Shaddoll focused checks failed'
    }
}
finally {
    Pop-Location
}

Start-ArenaIfNeeded
$public = Invoke-RestMethod "$arenaUrl/api/settings" -TimeoutSec 5
$settings = $public.settings
$current = $settings.windbots.current
if ($current.mode -ne 'local' -or -not $current.runtimeDir -or -not $current.botConfPath -or
    [IO.Path]::GetFullPath($current.runtimeDir) -ne [IO.Path]::GetFullPath($runtimeDirectory) -or
    [IO.Path]::GetFullPath($current.botConfPath) -ne [IO.Path]::GetFullPath((Join-Path $windBotRoot 'BotWrapper/bot.conf'))) {
    throw "Select the workspace Release WindBot and its BotWrapper/bot.conf as the candidate in Arena to run the checked Shaddoll build."
}
$server = Start-DedicatedArenaServer
$serverId = Set-PreparedServerSettings $settings $server
Save-LocalArenaSettings $public
Start-Process $arenaUrl

$challengeArguments = @(
    $challengeTool,
    '--url', $arenaUrl, '--srvpro', $serverId,
    '--games', [string]$GamesPerOpponent
)
foreach ($deck in $OpponentDeck) {
    $challengeArguments += @('--opponent', $deck)
}
if ($PreflightOnly) {
    $challengeArguments += '--preflight-only'
}

Write-Host 'Checking Arena and preparing the Shaddoll2025 Normal challenge...'
& node @challengeArguments
if ($LASTEXITCODE -eq 2) {
    Write-Warning 'Arena is open, but its dedicated SRVPro settings are incomplete. Configure Settings -> SRVPro, save, then run this command again.'
    exit 2
}
if ($LASTEXITCODE -ne 0) {
    throw "Shaddoll2025 Arena challenge failed (exit code $LASTEXITCODE)"
}
