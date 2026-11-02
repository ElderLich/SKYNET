'use strict';

const path = require('node:path');

function buildWindBotCommand(instance, platform = process.platform) {
    const executable = path.join(instance.runtimeDir, 'WindBot.exe');
    const args = ['ServerMode=True', `ServerPort=${instance.port}`, 'Chat=False'];
    return {
        executable: platform === 'win32' ? executable : 'mono',
        args: platform === 'win32' ? args : [executable, ...args],
        encoding: platform === 'win32' ? 'gbk' : 'utf-8',
    };
}

module.exports = { buildWindBotCommand };
