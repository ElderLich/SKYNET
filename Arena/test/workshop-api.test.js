'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const modulePromise = import('../client/src/workshop-api.mjs');

test('Workshop explains stale HTML backend errors and static-server responses', async () => {
    const { requestWorkshop } = await modulePromise;
    for (const status of [404, 200]) {
        await assert.rejects(requestWorkshop('/api/workshop/status', undefined, async () => new Response(
            '<!DOCTYPE html><title>Error</title>', { status, headers: { 'Content-Type': 'text/html' } },
        )), (error) => error.message.includes(`HTTP ${status}`)
            && error.message.includes('Restart Arena') && !error.message.includes("Unexpected token"));
    }
});

test('Workshop keeps real JSON validation errors and diagnoses malformed JSON', async () => {
    const { requestWorkshop } = await modulePromise;
    await assert.rejects(requestWorkshop('/api/workshop/analyze', { ydk: '' }, async () => Response.json(
        { error: 'The deck contains no card IDs.' }, { status: 400 },
    )), /The deck contains no card IDs/);
    await assert.rejects(requestWorkshop('/api/workshop/status', undefined, async () => new Response(
        '{', { headers: { 'Content-Type': 'application/json' } },
    )), /returned invalid JSON/);
});

test('Workshop preserves successful analysis requests', async () => {
    const { requestWorkshop } = await modulePromise;
    const input = { format: 'standard', ydk: '#main\n19434243' };
    const result = await requestWorkshop('/api/workshop/analyze', input, async (url, options) => {
        assert.equal(url, '/api/workshop/analyze');
        assert.equal(options.method, 'POST');
        assert.equal(options.headers['Content-Type'], 'application/json');
        assert.deepEqual(JSON.parse(options.body), input);
        return Response.json({ cards: [{ id: 19434243 }] });
    });
    assert.equal(result.cards[0].id, 19434243);
});
