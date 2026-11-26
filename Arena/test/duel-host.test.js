'use strict';

const assert = require('node:assert/strict');
const { execFileSync } = require('node:child_process');
const path = require('node:path');
const test = require('node:test');

test('native room bind policy accepts only IPv4 loopback and preserves caller memory', { skip: process.platform !== 'win32' }, () => {
    // Use a managed recorder in place of Winsock. This creates no listener.
    const script = `
        $ErrorActionPreference = 'Stop'
        $types = Add-Type -Path './Tools/SkynetDuelHost.cs' -PassThru -WarningAction SilentlyContinue
        Add-Type 'public static class BindProbe {
            public static int Capture(System.IntPtr socket, System.IntPtr address, int length) {
                return System.Runtime.InteropServices.Marshal.ReadInt32(address, 4);
            }
        }'
        $type = $types | Where-Object FullName -eq 'Skynet.Tools.SkynetDuelHost'
        $binding = [Reflection.BindingFlags]'NonPublic, Static'
        $field = $type.GetField('_originalBind', $binding)
        $field.SetValue($null, [Delegate]::CreateDelegate($field.FieldType, [BindProbe].GetMethod('Capture')))
        $guard = $type.GetMethod('GuardedBind', $binding)
        $addr = [Runtime.InteropServices.Marshal]::AllocHGlobal(32)
        try {
            foreach ($row in @(@(2,0,16), @(2,16777343,16), @(2,167772161,16), @(23,0,28), @(2,0,3))) {
                [Runtime.InteropServices.Marshal]::WriteInt16($addr, 0, [int16]$row[0])
                [Runtime.InteropServices.Marshal]::WriteInt32($addr, 4, $row[1])
                $result = $guard.Invoke($null, @([IntPtr]::Zero, $addr, $row[2]))
                if ([Runtime.InteropServices.Marshal]::ReadInt32($addr, 4) -ne $row[1]) { throw 'Caller address changed' }
                $result
            }
        } finally { [Runtime.InteropServices.Marshal]::FreeHGlobal($addr) }
    `;
    const output = execFileSync('powershell.exe', ['-NoProfile', '-Command', script], {
        cwd: path.join(__dirname, '..'),
        encoding: 'utf8',
    });
    assert.deepEqual(output.trim().split(/\s+/).map(Number), [16777343, 16777343, -1, -1, -1]);
});
