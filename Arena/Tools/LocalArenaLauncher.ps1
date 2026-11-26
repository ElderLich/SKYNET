# Shared by the local duel-log and Shaddoll launchers.

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$FailureMessage
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage (exit code $LASTEXITCODE)"
    }
}

function Test-ArenaReady {
    try {
        $system = Invoke-RestMethod "$arenaUrl/api/system" -TimeoutSec 2
        return $system.endpoints.web -eq $arenaUrl -and $null -ne $system.configurationBySrvpro
    }
    catch {
        return $false
    }
}

function Test-LocalTcpPort {
    param([int]$TcpPort)

    $client = [System.Net.Sockets.TcpClient]::new()
    try {
        $pending = $client.BeginConnect('127.0.0.1', $TcpPort, $null, $null)
        if (-not $pending.AsyncWaitHandle.WaitOne(500)) {
            return $false
        }
        $client.EndConnect($pending)
        return $true
    }
    catch {
        return $false
    }
    finally {
        $client.Dispose()
    }
}

function Start-ArenaIfNeeded {
    if (Test-ArenaReady) {
        Write-Host "Using the Arena instance already running at $arenaUrl"
        return
    }

    if (Test-LocalTcpPort $Port) { throw "Port $Port is occupied by a different service." }
    $arguments = @(
        '-NoLogo', '-NoProfile', '-ExecutionPolicy', 'Bypass',
        '-File', ('"' + $arenaLauncher + '"'),
        '-Port', [string]$Port,
        '-InstallDependencies'
    )
    if (-not $SkipArenaBuild) {
        $arguments += '-Build'
    }
    Write-Host "Starting Arena in a separate console at $arenaUrl ..."
    $dashboard = Start-Process -FilePath 'powershell.exe' -ArgumentList $arguments -WindowStyle Normal -PassThru
    for ($attempt = 0; $attempt -lt 360 -and -not (Test-ArenaReady); $attempt++) {
        if ($dashboard.HasExited) { throw "Dashboard launcher exited before Arena was ready at $arenaUrl." }
        Start-Sleep -Milliseconds 500
    }
    if (-not (Test-ArenaReady)) {
        throw "Arena did not become ready within 180 seconds: $arenaUrl"
    }
}

function Test-PreparedServerReady {
    param($Server)
    if (-not (Test-LocalTcpPort $Server.statusPort)) { return $false }

    # Verify the port owner before presenting the generated management credential.
    $nodePidFile = Join-Path $serverRuntime 'node.pid'
    $leaseFile = Join-Path $serverRuntime 'supervisor.lock'
    if (-not (Test-Path -LiteralPath $nodePidFile -PathType Leaf) -or
        -not (Test-Path -LiteralPath $leaseFile -PathType Leaf)) { return $false }
    $nodePid = 0
    $supervisorPid = 0
    try {
        $nodePidText = Get-Content -LiteralPath $nodePidFile -Raw -ErrorAction Stop
        $leaseText = Get-Content -LiteralPath $leaseFile -Raw -ErrorAction Stop
        if (-not [int]::TryParse($nodePidText, [ref]$nodePid) -or
            -not [int]::TryParse($leaseText, [ref]$supervisorPid)) { return $false }
    } catch { return $false }
    $listeners = @(Get-NetTCPConnection -State Listen -LocalPort $Server.statusPort -ErrorAction SilentlyContinue)
    $node = Get-CimInstance Win32_Process -Filter "ProcessId=$nodePid" -ErrorAction SilentlyContinue
    if ($nodePid -le 0 -or $supervisorPid -le 0 -or $null -eq $node -or
        $node.ParentProcessId -ne $supervisorPid -or $node.Name -ne 'node.exe' -or
        $listeners.Count -ne 1 -or $listeners[0].OwningProcess -ne $nodePid -or
        $listeners[0].LocalAddress -ne '127.0.0.1') { return $false }
    try {
        $password = [Uri]::EscapeDataString($Server.password)
        $url = "http://127.0.0.1:$($Server.statusPort)/api/getroomscount?username=skynet-local&pass=$password"
        $rooms = Invoke-RestMethod $url -TimeoutSec 2 -MaximumRedirection 0
        return $null -ne $rooms.PSObject.Properties['count'] -and [long]$rooms.count -ge 0 -and
            -not [string]::IsNullOrWhiteSpace([string]$rooms.serverInstanceId)
    } catch { return $false }
}

function Start-DedicatedArenaServer {
    $config = Get-Content -LiteralPath (Join-Path $serverRuntime 'skynet-server.json') -Raw | ConvertFrom-Json
    $users = Get-Content -LiteralPath (Join-Path $serverRuntime 'config/admin_user.json') -Raw | ConvertFrom-Json
    $server = [pscustomobject]@{
        host = '127.0.0.1'
        duelPort = [int]$config.port
        statusPort = [int]$config.adminPort
        username = 'skynet-local'
        password = $users.users.'skynet-local'.password
    }
    if ($config.format -ne 'skynet-arena-server-v1' -or $config.listenAddress -ne $server.host -or
        $server.duelPort -lt 1024 -or $server.duelPort -gt 65535 -or
        $server.statusPort -lt 1024 -or $server.statusPort -gt 65535 -or
        $server.duelPort -eq $server.statusPort -or [string]::IsNullOrWhiteSpace($server.password)) {
        throw "Expected a prepared loopback Arena server and its credentials under $serverRuntime"
    }
    if (Test-PreparedServerReady $server) { return $server }
    if ((Test-LocalTcpPort $server.statusPort) -or (Test-LocalTcpPort $server.duelPort)) {
        throw "A configured port is in use and the prepared server could not be verified: $serverRuntime"
    }
    foreach ($marker in @('supervisor.lock', 'stop.request')) {
        if (Test-Path -LiteralPath (Join-Path $serverRuntime $marker)) {
            throw "The runtime contains $marker. Check the supervisor before restarting it. See Arena/README.md."
        }
    }
    $nodePath = (Get-Command node -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
    $pythonPath = (Get-Command python -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
    Write-Host "Starting the prepared loopback duel server on ports $($server.duelPort)/$($server.statusPort) ..."
    $supervisor = Start-Process -FilePath $pythonPath -WorkingDirectory $serverRuntime -WindowStyle Hidden -PassThru -ArgumentList @(
        ('"' + $serverTool + '"'), 'serve', '--runtime-dir', ('"' + $serverRuntime + '"'),
        '--node', ('"' + $nodePath + '"'), '--minutes', '720'
    )
    try {
        for ($attempt = 0; $attempt -lt 120; $attempt++) {
            if (Test-PreparedServerReady $server) { return $server }
            if ($supervisor.HasExited) { throw "Supervisor exited. Check $serverRuntime/server.log and the staged runtime." }
            Start-Sleep -Milliseconds 500
        }
        throw "The prepared server did not become ready: $serverRuntime"
    } catch {
        if (-not $supervisor.HasExited) {
            # Retire only our child. Once it exits, its job also retires Node.
            $supervisor.Kill()
            $supervisor.WaitForExit()
            $lease = Join-Path $serverRuntime 'supervisor.lock'
            if (Test-Path -LiteralPath $lease -PathType Leaf) {
                $owner = Get-Content -LiteralPath $lease -Raw -ErrorAction SilentlyContinue
                if ($owner -eq [string]$supervisor.Id) { Remove-Item -LiteralPath $lease }
            }
        }
        throw
    }
}

function Set-PreparedServerSettings {
    param($Settings, $Server)
    $entry = @($Settings.srvpros | Where-Object {
        $_.host -in @('127.0.0.1', 'localhost') -and $_.statusPort -eq $Server.statusPort
    }) | Select-Object -First 1
    if (-not $entry) {
        $entry = $Settings.srvpros | Where-Object { $_.id -eq 'srvpro-1' -and $_.name -eq 'SRVPro 1' -and -not $_.host -and -not $_.username } | Select-Object -First 1
        if (-not $entry) {
            $entry = [pscustomobject]@{
                id = ('local-' + [Guid]::NewGuid().ToString('N'))
                name = ''; host = ''; username = ''; password = ''; duelPort = 0; statusPort = 0
                maxRooms = 2; roomsPerSecond = 1
            }
            $Settings.srvpros = @($Settings.srvpros) + $entry
        }
        $entry.name = 'Local MDPro4'
        $entry.maxRooms = 2
    }
    foreach ($field in @('host', 'duelPort', 'statusPort', 'username', 'password')) {
        $entry.$field = $Server.$field
    }
    return $entry.id
}

function Save-LocalArenaSettings {
    param($Public)
    $Public.settings | Add-Member -NotePropertyName expectedUpdatedAt -NotePropertyValue $Public.updatedAt
    $body = [Text.Encoding]::UTF8.GetBytes(($Public.settings | ConvertTo-Json -Depth 20))
    [void](Invoke-RestMethod "$arenaUrl/api/settings" -Method Put -ContentType 'application/json; charset=utf-8' -Body $body -TimeoutSec 15 -MaximumRedirection 0)
}
