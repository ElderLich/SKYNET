'use strict';

const assert = require('node:assert/strict');
const { execFileSync } = require('node:child_process');
const path = require('node:path');
const test = require('node:test');
const { createDefaultArenaSettings } = require('../server/arena-settings');

test('local launchers preserve server profiles and reuse the same prepared endpoint', { skip: process.platform !== 'win32' }, () => {
    const initial = createDefaultArenaSettings();
    const remote = { ...initial.srvpros[0], host: 'saved.example', username: 'owner', id: 'frozen' };
    const draft = { ...initial.srvpros[0], name: 'Unfinished profile' };
    initial.srvpros = [remote, draft];
    const script = `
        $ErrorActionPreference = 'Stop'
        . './Tools/LocalArenaLauncher.ps1'
        $settings = $env:ARENA_LAUNCHER_TEST_SETTINGS | ConvertFrom-Json
        $server = [pscustomobject]@{host='127.0.0.1'; duelPort=19700; statusPort=19701; username='skynet-local'; password='fixture'}
        $first = Set-PreparedServerSettings $settings $server
        $settings.srvpros[-1].name = 'Kept name'
        $settings.srvpros[-1].maxRooms = 1
        $second = Set-PreparedServerSettings $settings $server
        [pscustomobject]@{first=$first; second=$second; profiles=$settings.srvpros} | ConvertTo-Json -Depth 5
    `;
    const result = JSON.parse(execFileSync('powershell.exe', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', script], {
        cwd: path.join(__dirname, '..'),
        encoding: 'utf8',
        env: { ...process.env, ARENA_LAUNCHER_TEST_SETTINGS: JSON.stringify(initial) },
    }));
    assert.equal(result.first, result.second);
    assert.equal(result.profiles.length, 3);
    assert.deepEqual(result.profiles.slice(0, 2), [remote, draft]);
    assert.equal(result.profiles[2].name, 'Kept name');
    assert.equal(result.profiles[2].maxRooms, 1);
    assert.equal(result.profiles[2].password, 'fixture');
});

test('launcher checks the prepared listener owner before sending credentials', { skip: process.platform !== 'win32' }, () => {
    const script = `
        $ErrorActionPreference = 'Stop'
        . './Tools/LocalArenaLauncher.ps1'
        $serverRuntime = 'C:/fixture'
        $server = [pscustomobject]@{statusPort=19701; password='fixture'}
        function Test-LocalTcpPort { return $true }
        function Test-Path { return $true }
        function Get-Content { param($LiteralPath, [switch]$Raw, $ErrorAction)
            if ($LiteralPath -like '*node.pid') { return '41' }; return '42'
        }
        function Get-NetTCPConnection { return [pscustomobject]@{OwningProcess=$owner; LocalAddress='127.0.0.1'} }
        function Get-CimInstance { return [pscustomobject]@{Name='node.exe'; ParentProcessId=42} }
        function Invoke-RestMethod { $script:count += 1; return [pscustomobject]@{count=0; serverInstanceId='fixture'} }
        $count = 0
        $owner = 99
        if (Test-PreparedServerReady $server) { throw 'Accepted an unrelated process' }
        if ($count -ne 0) { throw 'Sent credentials to an unrelated process' }
        $owner = 41
        if (-not (Test-PreparedServerReady $server)) { throw 'Rejected the owned process' }
        if ($count -ne 1) { throw 'Missing authentication probe' }
    `;
    execFileSync('powershell.exe', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', script], {
        cwd: path.join(__dirname, '..'),
    });
});
