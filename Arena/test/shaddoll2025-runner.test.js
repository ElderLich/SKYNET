'use strict';

const assert = require('node:assert/strict');
const test = require('node:test');
const http = require('node:http');
const {
    main,
    parseArguments,
    printRunSummary,
    selectNormalOpponents,
} = require('../Tools/Executors/Shaddoll2025/run_challenge');

const catalog = [
    { aiLevel: 4, deck: 'Classic' },
    { aiLevel: 3, deck: 'Lightsworn' },
    { aiLevel: 3, deck: 'Shaddoll2025' },
    { aiLevel: 3, deck: 'SkyStriker' },
    { aiLevel: 2, deck: 'Easy' },
    { aiLevel: 3, deck: 'RushNormal', format: 'rush' },
];

test('Shaddoll runner defaults to one match against every other Normal bot', () => {
    assert.deepEqual(parseArguments([]), {
        arenaUrl: 'http://127.0.0.1:3000',
        gamesPerOpponent: 1,
        opponents: [],
        pollSeconds: 5,
        preflightOnly: false,
    });
    assert.deepEqual(selectNormalOpponents(catalog), ['Lightsworn', 'SkyStriker']);
});

test('Shaddoll runner accepts selected Normal opponents and rejects other levels', () => {
    const options = parseArguments([
        '--url', 'http://127.0.0.1:3137/',
        '--games', '3',
        '--opponent', 'Lightsworn',
        '--preflight-only',
    ]);
    assert.equal(options.arenaUrl, 'http://127.0.0.1:3137');
    assert.equal(options.gamesPerOpponent, 3);
    assert.deepEqual(options.opponents, ['Lightsworn']);
    assert.equal(options.preflightOnly, true);
    assert.deepEqual(selectNormalOpponents(catalog, options.opponents), ['Lightsworn']);
    assert.throws(() => selectNormalOpponents(catalog, ['Classic']), /not AI_LV3/);
    assert.throws(() => selectNormalOpponents(catalog, ['RushNormal']), /not AI_LV3/);
});

test('Shaddoll runner requires a Normal-classified Shaddoll target', () => {
    assert.throws(
        () => selectNormalOpponents(catalog.filter((item) => item.deck !== 'Shaddoll2025')),
        /missing from Arena/,
    );
    assert.throws(
        () => selectNormalOpponents(catalog.map((item) => (
            item.deck === 'Shaddoll2025' ? { ...item, aiLevel: null } : item
        ))),
        /classified as AI_LV3/,
    );
});

test('Shaddoll result summary uses the observed matchup count', () => {
    const originalTable = console.table;
    let rows;
    console.table = (value) => { rows = value; };
    try {
        printRunSummary({
            matchups: [{
                label: 'Lightsworn',
                observedGames: 1,
                competitors: [
                    { source: 'target', win: 0, flee: 0 },
                    { source: 'opponent', win: 1, flee: 0 },
                ],
            }],
        });
    } finally {
        console.table = originalTable;
    }
    assert.equal(rows[0].observed, 1);
    assert.equal(rows[0].opponentWins, 1);
});

test('Shaddoll validates and launches on the selected SRVPro', async (context) => {
    const requests = [];
    const server = http.createServer(async (request, response) => {
        response.setHeader('Content-Type', 'application/json');
        if (request.url === '/api/system') {
            response.end(JSON.stringify({
                configuration: { modes: { challenge: { valid: false } } },
                configurationBySrvpro: { selected: { modes: { challenge: { valid: true } } } },
            }));
        } else if (request.url === '/api/decks') {
            response.end(JSON.stringify({ currentDecks: catalog }));
        } else if (request.url === '/api/runs') {
            let body = '';
            for await (const chunk of request) body += chunk;
            requests.push(JSON.parse(body));
            response.end(JSON.stringify({ run: { id: 'fixture' } }));
        } else if (request.url === '/api/runs/fixture') {
            response.end(JSON.stringify({ run: { status: 'completed', launchedGames: 1, totalGames: 1, matchups: [] } }));
        } else {
            response.end('{}');
        }
    });
    await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
    context.after(() => new Promise((resolve) => server.close(resolve)));
    const url = 'http://127.0.0.1:' + server.address().port;
    await assert.rejects(main(['--url', url, '--srvpro', 'unknown']), /preflight is incomplete/);
    await main(['--url', url, '--srvpro', 'selected', '--preflight-only']);
    assert.equal(requests.length, 0);
    await main(['--url', url, '--srvpro', 'selected', '--opponent', 'Lightsworn']);
    assert.equal(requests.length, 1);
    assert.equal(requests[0].srvproId, 'selected');
});
