[CmdletBinding()]
param(
    [string]$Python = 'python',
    [string]$MSBuild,
    [Parameter(Mandatory = $true)][string]$DataRoot,
    [Parameter(Mandatory = $true)][string]$Server,
    [string]$OutputDirectory,
    [int]$MirrorGames = 10
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$windBotRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../..')).Path
$repo = Split-Path -Parent $windBotRoot
$bot = Join-Path $windBotRoot 'bin/Debug/WindBot.exe'
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $repo ('Artifacts/skystriker-check-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
$outputPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
if (Test-Path -LiteralPath $outputPath) { throw 'Use a new output directory.' }
& $Python -c 'import sys; sys.exit(0 if sys.version_info >= (3,11) else 1)'
if ($LASTEXITCODE -ne 0) { throw 'Native runner requires Python 3.11 or newer; pass -Python with its executable path.' }
$database = (Resolve-Path -LiteralPath (Join-Path $DataRoot 'locales/en-US/cards.cdb')).Path
$serverPath = (Resolve-Path -LiteralPath $Server).Path
$buildArgs = @((Join-Path $windBotRoot 'Tools/bot_tool.py'), 'build', '--configuration', 'Both')
if ($MSBuild) {
    $buildArgs += @('--msbuild', (Get-Command $MSBuild -CommandType Application).Source)
}
& $Python @buildArgs
if ($LASTEXITCODE -ne 0) { throw 'WindBot build failed.' }

& (Join-Path $PSScriptRoot 'Invoke-SkyStrikerPlannerChecks.ps1') `
    -BotExecutable $bot -CardDatabase $database -OutputDirectory (Join-Path $outputPath 'checks')
& (Join-Path $PSScriptRoot 'Invoke-SkyStrikerRecoveryChecks.ps1') `
    -BotExecutable $bot -CardDatabase $database -OutputDirectory (Join-Path $outputPath 'recovery-checks')
& $Python (Join-Path $PSScriptRoot 'skystriker_recovery_native.py') `
    --bot-exe $bot --data-root $DataRoot --server $serverPath `
    --output-dir (Join-Path $outputPath 'recovery-native')
if ($LASTEXITCODE -ne 0) { throw "Recovery native checks failed. Inspect $outputPath" }
& $Python (Join-Path $PSScriptRoot 'skystriker_native_checks.py') `
    --bot-exe $bot --data-root $DataRoot --server $serverPath `
    --deck (Join-Path $windBotRoot 'Decks/AI_SkyStriker2026.ydk') --output-dir (Join-Path $outputPath 'native') `
    --mirror-games $MirrorGames
if ($LASTEXITCODE -ne 0) { throw "Native behavior checks failed. Inspect $outputPath" }
Write-Output "Build, focused checks and selected native regressions passed. Results in $outputPath"
