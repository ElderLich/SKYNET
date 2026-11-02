'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const { buildWindBotCommand } = require('../server/windbot-command');

test('Windows local WindBot launches directly with separate arguments', () => {
    const command = buildWindBotCommand({ runtimeDir: 'runtime with spaces', port: 2399 }, 'win32');
    assert.equal(command.executable, path.join('runtime with spaces', 'WindBot.exe'));
    assert.deepEqual(command.args, ['ServerMode=True', 'ServerPort=2399', 'Chat=False']);
    assert.equal(command.encoding, 'gbk');
});

test('Linux local WindBot launches under Mono without a shell', () => {
    const command = buildWindBotCommand({ runtimeDir: '/tmp/bot with spaces', port: 2398 }, 'linux');
    assert.equal(command.executable, 'mono');
    assert.deepEqual(command.args, [path.join('/tmp/bot with spaces', 'WindBot.exe'),
        'ServerMode=True', 'ServerPort=2398', 'Chat=False']);
    assert.equal(command.encoding, 'utf-8');
});
