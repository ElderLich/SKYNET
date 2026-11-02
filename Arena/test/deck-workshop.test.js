'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const zlib = require('node:zlib');
const crypto = require('node:crypto');
const { createServer } = require('node:http');
const test = require('node:test');
const Database = require('better-sqlite3');
const { createWorkshop, parseYdk, inspectLua, LIMITS } = require('../server/deck-workshop');
const { createApp } = require('../server/app');

function fixture(context) {
    const workspaceRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'mdpro4-workshop-'));
    context.after(() => fs.rmSync(workspaceRoot, { recursive: true, force: true }));
    for (const format of ['standard', 'rush']) {
        const root = path.join(workspaceRoot, 'DeckWorkshop', 'Data', format);
        fs.mkdirSync(path.join(root, 'scripts'), { recursive: true });
        const db = new Database(path.join(root, 'cards.cdb'));
        db.exec(`CREATE TABLE texts (id INTEGER PRIMARY KEY, name TEXT, desc TEXT, ${Array.from({ length: 16 }, (_, index) => `str${index + 1} TEXT`).join(', ')});
            CREATE TABLE datas (id INTEGER PRIMARY KEY, alias INTEGER, type INTEGER, atk INTEGER, def INTEGER, level INTEGER, race INTEGER, attribute INTEGER);`);
        for (const [id, name, alias] of [[10001, format === 'rush' ? 'Rush One' : 'Standard One', 0], [10002, 'Card Two', 0], [10003, 'Alternate Card', 10001]]) {
            db.prepare('INSERT INTO texts (id, name, desc, str1) VALUES (?, ?, ?, ?)').run(id, name, `${name} effect text`, 'Choose one');
            db.prepare('INSERT INTO datas VALUES (?, ?, 33, 1800, 1000, 4, 1, 1)').run(id, alias);
        }
        db.close();
        fs.writeFileSync(path.join(root, 'scripts', 'c10001.lua'), `local s,id=GetID()
function s.initial_effect(c)
    local e1=Effect.CreateEffect(c)
    e1:SetDescription(aux.Stringid(id,0))
    e1:SetType(EFFECT_TYPE_IGNITION)
    e1:SetCost(s.cost)
    e1:SetTarget(s.target)
    e1:SetOperation(s.operation)
    c:RegisterEffect(e1)
end
function s.cost(e,tp,eg,ep,ev,re,r,rp,chk)
    return true
end
function s.target(e,tp,eg,ep,ev,re,r,rp,chk)
    return Duel.IsExistingMatchingCard(s.filter,tp,LOCATION_DECK,0,1,nil)
end
function s.operation(e,tp)
    Duel.SendtoHand(nil,tp,REASON_EFFECT)
end
function s.filter(c)
    return c:IsCode(10002)
end
`);
    }
    const templateDirectory = path.join(workspaceRoot, 'WindBot', 'Templates');
    fs.mkdirSync(templateDirectory, { recursive: true });
    fs.copyFileSync(path.resolve(__dirname, '../../WindBot/Templates/RdaPlannerExecutor.cs.template'), path.join(templateDirectory, 'RdaPlannerExecutor.cs.template'));
    fs.mkdirSync(path.join(workspaceRoot, 'WindBot', 'Game', 'AI', 'Decks'), { recursive: true });
    fs.writeFileSync(path.join(workspaceRoot, 'DeckWorkshop', 'data-provenance.json'), '{"schemaVersion":1}');
    return { workspaceRoot, workshop: createWorkshop({ workspaceRoot }) };
}

test('YDK parser preserves duplicate sections and copy counts without inventing legality', () => {
    const parsed = parseYdk('\uFEFF#created by player\r\n#main\r\n10001\r\n10001\r\n#extra\r\n10002\r\n!side\r\n10001\r\n');
    assert.deepEqual(parsed.deck, { main: 2, extra: 1, side: 1, total: 4, unique: 2 });
    assert.deepEqual(parsed.entries[0], { id: 10001, main: 2, extra: 0, side: 1, total: 3 });
    assert.match(parsed.normalizedYdk, /#main\n10001\n10001\n#extra\n10002\n!side\n10001\n$/);
});

test('YDK parser rejects missing headers, invalid IDs and bounded input overflows', () => {
    for (const value of ['10001', '#main\n-1', '#main\n0', '#main\n2147483648', '#main\n../secret', '#main\n', null, '#main\n10001\n!bad']) {
        assert.throws(() => parseYdk(value), (error) => error.statusCode === 400);
    }
    assert.throws(() => parseYdk(`#main\n${'10001\n'.repeat(LIMITS.deckCards + 1)}`), /limited/);
    assert.throws(() => parseYdk('x'.repeat(LIMITS.deckBytes + 1)), /bytes/);
});

test('Lua inventory ignores spoofed comments and strings and preserves literal line evidence', () => {
    const source = `-- function fake() c:IsCode(99999)
local text="function decoy() c:IsCode(88888)"
--[=[ function hidden() c:IsCode(77777) ]=]
local s,id=GetID()
function s.initial_effect(c)
 local e1=Effect.CreateEffect(c)
 e1:SetCost(s.cost)
 e1:SetDescription(aux.Stringid(id,0))
 local e2=e1:Clone()
 e2:SetOperation(s.operation)
end
function s.cost(e) return true end
function s.operation(e) return e:IsCode(10002) end
`;
    const result = inspectLua(source, 10001);
    assert.deepEqual(result.functions.map((entry) => entry.name), ['s.initial_effect', 's.cost', 's.operation']);
    assert.equal(result.functions[0].line, 5);
    assert.equal(result.effects[0].properties.SetDescription, 'aux.Stringid(id,0)');
    assert.deepEqual(result.effects[0].callbacks, [{ role: 'cost', name: 's.cost', line: 7 }]);
    assert.equal(result.effects[1].cloneOf, result.effects[0].key);
    assert.deepEqual(result.references, [{ id: 10002, line: 13, reason: 'literal card-ID call' }]);
});

test('literal references exclude expression fragments, reset flags and string-embedded includes', () => {
    const source = `local text="Duel.LoadScript('c88888.lua')"
Duel.LoadScript('c10002.lua')
c:IsCode(10003+1,10004,-10005)
c:CopyEffect(10006,10007,2)
c:ReplaceEffect(id+10008,10009)
s.listed_names={10010,10011+1,10012}
`;
    assert.deepEqual(inspectLua(source, 10001).references.map((entry) => entry.id).sort((a, b) => a - b), [10002, 10004, 10006, 10010, 10012]);
});

test('Rush helper inventory preserves lines and excludes calls written in comments or strings', () => {
    const result = inspectLua(`-- RD.Fake(c)
local text="RushDuel.Decoy(c)"
RD.AddCodeList(c,10002)
RushDuel.MaximumProcedure(c)
RD.AddEffect(c)
`, 10001);
    assert.deepEqual(result.calls, [
        { name: 'RD.AddCodeList', line: 3 },
        { name: 'RushDuel.MaximumProcedure', line: 4 },
        { name: 'RD.AddEffect', line: 5 },
    ]);
});

test('analysis keeps formats isolated and links names, prompts, counts and script evidence', (context) => {
    const { workshop } = fixture(context);
    const result = workshop.analyze({ format: 'standard', ydk: '#main\n10001\n10001\n10002\n#extra\n!side\n10003' });
    assert.equal(result.cards[0].name, 'Standard One');
    assert.equal(result.cards[0].count, 2);
    assert.equal(result.cards[0].prompts[0].descriptionId, 160016);
    assert.equal(result.cards[0].script.references[0].name, 'Card Two');
    assert.equal(result.cards[0].script.references[0].inDeck, true);
    assert.equal(result.cards[2].script.resolvedId, 10001);
    assert.ok(result.warnings.some((warning) => warning.includes('alias 10001')));
    assert.ok(result.graph.edges.some((edge) => edge.kind === 'callback' && edge.label === 'cost'));
    assert.ok(result.graph.edges.some((edge) => edge.source === 'card:10001' && edge.target === 'card:10002'));
    assert.match(result.report, /SCRIPT: DeckWorkshop\/Data\/standard\/scripts\/c10001.lua/);
    assert.match(result.report, /Lua API\/helper call L15: Duel.IsExistingMatchingCard/);
    assert.match(result.provenance.databaseSha256, /^[a-f0-9]{64}$/);
    assert.equal(workshop.analyze({ format: 'rush', ydk: '#main\n10001' }).cards[0].name, 'Rush One');
    assert.equal(workshop.status().formats[0].cardCount, 3);
    assert.throws(() => workshop.analyze({ format: '../standard', ydk: '#main\n10001' }), /Choose/);
    assert.throws(() => workshop.generate({ format: 'rush', ydk: '#main\n10001', name: 'Rush Test' }), /standard decks only/);
});

test('ambiguous scripts and missing metadata remain explicit instead of guessing', (context) => {
    const { workshop, workspaceRoot } = fixture(context);
    const scripts = path.join(workspaceRoot, 'DeckWorkshop', 'Data', 'standard', 'scripts');
    fs.mkdirSync(path.join(scripts, 'duplicate'));
    fs.copyFileSync(path.join(scripts, 'c10001.lua'), path.join(scripts, 'duplicate', 'c10001.lua'));
    const result = workshop.analyze({ format: 'standard', ydk: '#main\n10001\n77777' });
    assert.equal(result.cards[0].script.available, false);
    assert.equal(result.cards[1].found, false);
    assert.ok(result.warnings.some((warning) => warning.includes('ambiguous')));
    assert.ok(result.warnings.some((warning) => warning.includes('77777')));
});

test('oversized script is bounded without dropping the card text', (context) => {
    const { workshop, workspaceRoot } = fixture(context);
    fs.writeFileSync(path.join(workspaceRoot, 'DeckWorkshop', 'Data', 'standard', 'scripts', 'c10001.lua'), 'x'.repeat(LIMITS.scriptBytes + 1));
    const result = workshop.analyze({ format: 'standard', ydk: '#main\n10001' });
    assert.equal(result.cards[0].description, 'Standard One effect text');
    assert.equal(result.cards[0].script.available, false);
    assert.ok(result.warnings.some((warning) => warning.includes('budget')));
});

test('generated files have unique Test registrations, CRLF, reproducible provenance and disabled models', (context) => {
    const { workshop, workspaceRoot } = fixture(context);
    const result = workshop.generate({ format: 'standard', ydk: '#main\n10001\n10002', name: 'Example Deck' });
    assert.equal(result.className, 'ExampleDeckExecutor');
    const source = result.files.find((file) => file.name.endsWith('Executor.cs')).content;
    assert.match(source, /\[Deck\("ExampleDeck", "AI_ExampleDeck", "Test"\)\]/);
    assert.match(source, /protected override bool ValidateDeckAndModel\(\)[\s\S]*?return false;/);
    assert.match(source, /Card_StandardOne_10001 = 10001;/);
    assert.equal(source.replace(/\r\n/g, '').includes('\n'), false);
    const base = result.files.find((file) => file.name.endsWith('PlannerBase.cs')).content;
    assert.match(base, /class ExampleDeckPlannerBase<TState, TAction>/);
    assert.doesNotMatch(base, /RdaPlannerExecutorTemplate/);
    const manifest = JSON.parse(result.files.find((file) => file.name === 'manifest.json').content);
    assert.equal(manifest.modelImplemented, false);
    assert.match(manifest.templateSha256, /^[a-f0-9]{64}$/);
    assert.equal(manifest.cards[0].scriptSha256.length, 64);
    assert.deepEqual(workshop.generate({ format: 'standard', ydk: '#main\n10001\n10002', name: 'Example Deck' }), result);
    assert.equal(fs.readdirSync(path.join(workspaceRoot, 'WindBot', 'Game', 'AI', 'Decks')).length, 0);
    for (const name of ['../bad', '"Injected"', 'x\nclass', 'a', 'bad;name']) {
        assert.throws(() => workshop.generate({ format: 'standard', ydk: '#main\n10001', name }), /deck name/);
    }
});

test('generation detects existing class and registration collisions before export', (context) => {
    const { workshop, workspaceRoot } = fixture(context);
    fs.writeFileSync(path.join(workspaceRoot, 'WindBot', 'Game', 'AI', 'Decks', 'Existing.cs'), '[Deck("Existing", "AI_Old")] public class OtherExecutor {}');
    assert.throws(() => workshop.generate({ format: 'standard', ydk: '#main\n10001', name: 'existing' }), /conflicts/);
    fs.writeFileSync(path.join(workspaceRoot, 'WindBot', 'Game', 'AI', 'Decks', 'Collision.cs'), 'public class UniquePlannerBase<T> {}');
    assert.throws(() => workshop.generate({ format: 'standard', ydk: '#main\n10001', name: 'Unique' }), /conflicts/);
});

test('related-card index keeps categories distinct, bounds suggestions and prioritizes deck members', (context) => {
    const { workshop, workspaceRoot } = fixture(context);
    const directory = path.join(workspaceRoot, 'DeckWorkshop', 'Relations');
    fs.mkdirSync(directory, { recursive: true });
    const data = { schemaVersion: 1, format: 'standard', source: { revision: 'test' },
        categories: ['ID', 'Arch', 'Race'], cards: { 10001: {
            ID: Array.from({ length: 30 }, (_, index) => 20000 + index), Arch: [10003], Race: [10002],
        } } };
    fs.writeFileSync(path.join(directory, 'standard.json.gz'), zlib.gzipSync(JSON.stringify(data)));
    const result = workshop.analyze({ format: 'standard', ydk: '#main\n10001\n10002' });
    assert.equal(result.cards[0].related.length, 24);
    assert.equal(result.cards[0].relatedCount, 32);
    assert.deepEqual(result.cards[0].related[0], { id: 10002, kind: 'Race', name: 'Card Two', inDeck: true, reason: 'Race', source: 'RelatedCards static index' });
    assert.ok(result.cards[0].related.some((entry) => entry.kind === 'ID'));
    const edges = result.graph.edges.filter((edge) => edge.kind === 'related');
    assert.equal(edges.length, 24);
    assert.equal(edges[0].line, undefined);
    assert.equal(edges[0].evidenceSource, 'RelatedCards static index');
    assert.match(result.provenance.relatedIndexSha256, /^[a-f0-9]{64}$/);
    assert.ok(result.report.includes(`Related index SHA-256: ${result.provenance.relatedIndexSha256}`));
    assert.match(result.report, /Related index source: \{"revision":"test"\}/);
    assert.match(result.report, /Related index used: yes/);
    assert.equal(workshop.status().formats[0].relationsAvailable, true);
    assert.equal(workshop.status().formats[1].relationsAvailable, false);
    assert.deepEqual(workshop.analyze({ format: 'rush', ydk: '#main\n10001' }).cards[0].related, []);
});

test('stale relationship database identity omits links while retaining card/script analysis and provenance', (context) => {
    const { workshop, workspaceRoot } = fixture(context);
    const directory = path.join(workspaceRoot, 'DeckWorkshop', 'Relations');
    fs.mkdirSync(directory, { recursive: true });
    const indexPath = path.join(directory, 'standard.json.gz');
    const data = { schemaVersion: 1, format: 'standard', source: { databaseSha256: '0'.repeat(64) },
        categories: ['ID'], cards: { 10001: { ID: [10002] } } };
    fs.writeFileSync(indexPath, zlib.gzipSync(JSON.stringify(data)));
    const result = workshop.analyze({ format: 'standard', ydk: '#main\n10001\n10002' });
    assert.equal(result.cards[0].name, 'Standard One');
    assert.equal(result.cards[0].script.available, true);
    assert.deepEqual(result.cards[0].related, []);
    assert.equal(result.graph.edges.some((edge) => edge.kind === 'related'), false);
    assert.ok(result.warnings.some((warning) => warning.includes('different card database') && warning.includes('python DeckWorkshop/build_relations.py')));
    assert.equal(result.provenance.relatedIndexUsed, false);
    assert.equal(result.provenance.relatedSource.databaseSha256, '0'.repeat(64));
    assert.match(result.report, /Related index used: no/);
    // A matching recorded identity makes the same optional index usable.
    data.source.databaseSha256 = crypto.createHash('sha256').update(fs.readFileSync(path.join(workspaceRoot, 'DeckWorkshop', 'Data', 'standard', 'cards.cdb'))).digest('hex');
    fs.writeFileSync(indexPath, zlib.gzipSync(JSON.stringify(data)));
    const current = workshop.analyze({ format: 'standard', ydk: '#main\n10001\n10002' });
    assert.equal(current.provenance.relatedIndexUsed, true);
    assert.equal(current.cards[0].related[0].id, 10002);
});

test('workshop HTTP routes reject path-shaped formats and Rush generation without touching runtime services', async (context) => {
    const app = createApp({ rootDir: path.resolve(__dirname, '..') }, {}, {});
    const server = createServer(app);
    await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
    context.after(() => new Promise((resolve) => server.close(resolve)));
    const base = `http://127.0.0.1:${server.address().port}/api/workshop`;
    for (const [route, payload, expected] of [
        ['analyze', { format: '../../outside', ydk: '#main\n10001' }, /Choose the standard or rush/],
        ['analyze', { format: 'standard', ydk: '#main\n../../outside' }, /32-bit card ID/],
        ['generate', { format: 'rush', ydk: '#main\n10001', name: 'Example' }, /standard decks only/],
    ]) {
        const response = await fetch(`${base}/${route}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) });
        assert.equal(response.status, 400);
        assert.equal(response.headers.get('cache-control'), 'no-store');
        assert.match((await response.json()).error, expected);
    }
});
