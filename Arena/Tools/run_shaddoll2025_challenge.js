'use strict';

const runner = require('./Executors/Shaddoll2025/run_challenge');

if (require.main === module) {
    runner.main().catch((error) => {
        console.error(error.message);
        process.exitCode = error.exitCode || 1;
    });
}

module.exports = runner;
