'use strict';

const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const zlib = require('node:zlib');
const Database = require('better-sqlite3');
const { generateFiles } = require('./deck-workshop-generator');

const LIMITS = Object.freeze({ deckBytes: 32768, deckCards: 200, scriptBytes: 262144, sourceBytes: 4194304 });
const FORMATS = Object.freeze({ standard: 'Standard', rush: 'Rush Duel' });
const STATIC_NOTICE = 'Static Lua evidence only: dynamic helpers, cloned effects and runtime branches may be incomplete. Edges are source relationships, not verified combos.';

function fail(message, statusCode = 400) {
    const error = new Error(message);
    error.statusCode = statusCode;
    throw error;
}

function sha256(value) {
    return crypto.createHash('sha256').update(value).digest('hex');
}

function parseYdk(value) {
    if (typeof value !== 'string' || Buffer.byteLength(value, 'utf8') > LIMITS.deckBytes) {
        fail(`Paste a .ydk deck of at most ${LIMITS.deckBytes} bytes.`);
    }
    const sections = { main: [], extra: [], side: [] };
    let section = null;
    let total = 0;
    const lines = value.replace(/^\uFEFF/, '').split(/\r?\n/);
    for (let index = 0; index < lines.length; index++) {
        const line = lines[index].trim();
        if (!line || /^#created by\b/i.test(line) || line.startsWith('//')) continue;
        if (/^#main$/i.test(line)) { section = 'main'; continue; }
        if (/^#extra$/i.test(line)) { section = 'extra'; continue; }
        if (/^!side$/i.test(line)) { section = 'side'; continue; }
        if (line.startsWith('#')) continue;
        if (!section) fail(`Line ${index + 1}: card IDs must follow #main, #extra or !side.`);
        if (!/^\d{1,10}$/.test(line) || Number(line) < 1 || Number(line) > 2147483647) {
            fail(`Line ${index + 1}: expected a positive 32-bit card ID.`);
        }
        sections[section].push(Number(line));
        if (++total > LIMITS.deckCards) fail(`A workshop deck is limited to ${LIMITS.deckCards} cards.`);
    }
    if (!total) fail('The deck contains no card IDs. Include #main and your card IDs.');
    const entries = new Map();
    for (const [name, ids] of Object.entries(sections)) {
        for (const id of ids) {
            if (!entries.has(id)) entries.set(id, { id, main: 0, extra: 0, side: 0, total: 0 });
            entries.get(id)[name]++;
            entries.get(id).total++;
        }
    }
    const normalizedYdk = ['#created by MDPro4 Deck Workshop', '#main', ...sections.main,
        '#extra', ...sections.extra, '!side', ...sections.side, ''].join('\n');
    return {
        sections, entries: [...entries.values()], normalizedYdk,
        deck: { main: sections.main.length, extra: sections.extra.length, side: sections.side.length, total, unique: entries.size },
    };
}

// Preserve offsets/newlines while hiding comments and string contents. No Lua is evaluated.
function maskLua(source, hideStrings = true) {
    return source.replace(/--\[(=*)\[[\s\S]*?\]\1\]|--[^\r\n]*|\[(=*)\[[\s\S]*?\]\2\]|"(?:\\[\s\S]|[^"\\])*"|'(?:\\[\s\S]|[^'\\])*'/g, (match) => {
        if (!hideStrings && !match.startsWith('--')) return match;
        return match.replace(/[^\r\n]/g, ' ');
    });
}

function inspectLua(source, cardId) {
    const code = maskLua(source);
    const commentsRemoved = maskLua(source, false);
    const lineAt = (offset) => source.slice(0, offset).split('\n').length;
    const functions = [];
    const effects = [];
    const references = [];
    const calls = [];
    const byVariable = new Map();
    const seenReferences = new Set();
    const addReference = (id, offset, reason) => {
        if (id < 1 || id > 2147483647 || id === cardId) return;
        const line = lineAt(offset);
        const key = `${id}:${line}`;
        if (!seenReferences.has(key) && references.length < 128) {
            seenReferences.add(key);
            references.push({ id, line, reason });
        }
    };
    for (const match of code.matchAll(/\bfunction\s+([\w.:]+)\s*\(|\b([\w.]+)\s*=\s*function\s*\(/g)) {
        if (functions.length < 96) functions.push({ name: match[1] || match[2], line: lineAt(match.index) });
    }
    const events = [];
    for (const match of code.matchAll(/\b(\w+)\s*=\s*(?:Effect\.CreateEffect\s*\(|(\w+)\s*:\s*Clone\s*\()/g)) {
        events.push({ index: match.index, match, kind: 'effect' });
    }
    for (const match of code.matchAll(/\b(\w+)\s*:\s*(Set[A-Za-z]+)\s*\(/g)) {
        events.push({ index: match.index, match, kind: 'property' });
    }
    for (const event of events.sort((a, b) => a.index - b.index)) {
        const { match } = event;
        if (event.kind === 'effect') {
            if (effects.length >= 64) continue;
            const effect = { key: `${match[1]}:${lineAt(match.index)}`, variable: match[1], line: lineAt(match.index), properties: {}, callbacks: [] };
            if (match[2]) effect.cloneOf = byVariable.get(match[2])?.key || match[2];
            effects.push(effect);
            byVariable.set(match[1], effect);
            continue;
        }
        const effect = byVariable.get(match[1]);
        if (!effect) continue;
        const start = match.index + match[0].length;
        let end = start;
        let depth = 1;
        for (; end < code.length && end - start < 1000; end++) {
            if (code[end] === '(') depth++;
            if (code[end] === ')' && --depth === 0) break;
        }
        const value = commentsRemoved.slice(start, end).trim();
        effect.properties[match[2]] = value;
        if (['SetCondition', 'SetCost', 'SetTarget', 'SetOperation', 'SetValue'].includes(match[2]) && /^[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*$/.test(value)) {
            effect.callbacks.push({ role: match[2].slice(3).toLowerCase(), name: value, line: lineAt(match.index) });
        }
    }
    for (const match of code.matchAll(/\b((?:Duel|aux|Fusion|Synchro|Xyz|Link|Ritual|Pendulum|RD|RushDuel)\.[A-Za-z_]\w*)\s*\(/g)) {
        if (calls.length < 160) calls.push({ name: match[1], line: lineAt(match.index) });
    }
    // These are literal references in specific card-ID APIs, not every number in a script.
    for (const match of code.matchAll(/\b(?:IsCode|IsOriginalCode|IsSummonCode|AddCodeList|AddListedNames)\s*\(([^)\n]{0,500})\)/g)) {
        for (const numeric of match[1].matchAll(/(?:^|,)\s*(\d{1,10})\s*(?=,|$)/g)) addReference(Number(numeric[1]), match.index, 'literal card-ID call');
    }
    for (const match of code.matchAll(/\b(?:CopyEffect|ReplaceEffect)\s*\(\s*(\d{1,10})\s*[,)]/g)) {
        addReference(Number(match[1]), match.index, 'literal effect copy');
    }
    for (const match of code.matchAll(/\blisted_names\s*=\s*\{([^}]{0,1000})\}/g)) {
        for (const numeric of match[1].matchAll(/(?:^|,)\s*(\d{1,10})\s*(?=,|$)/g)) addReference(Number(numeric[1]), match.index, 'listed_names');
    }
    for (const match of commentsRemoved.matchAll(/\bDuel\.LoadScript\s*\(\s*["']c(\d{1,10})\.lua["']/g)) {
        if (code.startsWith('Duel.LoadScript', match.index)) addReference(Number(match[1]), match.index, 'script include');
    }
    return { functions, effects, references, calls };
}

function typeLabel(type) {
    const flags = [[1, 'Monster'], [2, 'Spell'], [4, 'Trap'], [16, 'Normal'], [32, 'Effect'], [64, 'Fusion'],
        [128, 'Ritual'], [4096, 'Tuner'], [8192, 'Synchro'], [8388608, 'Xyz'], [16777216, 'Pendulum'], [67108864, 'Link']];
    return flags.filter(([flag]) => (type & flag) !== 0).map(([, label]) => label).join(' / ') || 'Unknown';
}

function createGraph(cards) {
    const nodes = new Map();
    const edges = [];
    const addNode = (node) => { if (!nodes.has(node.id)) nodes.set(node.id, node); };
    const addEdge = (source, target, kind, label, line, cardId) => edges.push({ id: `edge:${edges.length}`, source, target, kind, label, line, cardId });
    for (const card of cards) {
        const root = `card:${card.id}`;
        addNode({ id: root, kind: 'card', label: card.name, cardId: card.id, inDeck: true });
        for (const fn of card.script.functions) {
            const id = `function:${card.id}:${fn.name}`;
            addNode({ id, kind: 'function', label: fn.name, cardId: card.id, line: fn.line });
            addEdge(root, id, 'defines', 'defines', fn.line, card.id);
        }
        for (const effect of card.script.effects) {
            const id = `effect:${card.id}:${effect.key}`;
            addNode({ id, kind: 'effect', label: effect.variable, cardId: card.id, line: effect.line });
            addEdge(root, id, 'registers', effect.cloneOf ? 'clones effect' : 'creates effect', effect.line, card.id);
            for (const callback of effect.callbacks) {
                const target = `function:${card.id}:${callback.name}`;
                const fn = card.script.functions.find((entry) => entry.name === callback.name);
                addNode({ id: target, kind: 'function', label: callback.name, cardId: card.id, line: fn?.line ?? callback.line, external: !fn });
                addEdge(id, target, 'callback', callback.role, callback.line, card.id);
            }
        }
        for (const reference of card.script.references) {
            const target = `card:${reference.id}`;
            addNode({ id: target, kind: 'card', label: reference.name, cardId: reference.id, inDeck: reference.inDeck });
            addEdge(root, target, 'references', reference.reason, reference.line, card.id);
        }
        for (const reference of card.related || []) {
            const target = `card:${reference.id}`;
            addNode({ id: target, kind: 'card', label: reference.name, cardId: reference.id, inDeck: reference.inDeck });
            addEdge(root, target, 'related', reference.kind, undefined, card.id);
            edges[edges.length - 1].evidenceSource = 'RelatedCards static index';
        }
    }
    return { nodes: [...nodes.values()], edges };
}

function createReport(result) {
    const lines = ['MDPro4 Deck Workshop — card and script evidence', `Format: ${FORMATS[result.format]}`,
        `Deck: ${result.deck.main} main / ${result.deck.extra} extra / ${result.deck.side} side`,
        `CDB SHA-256: ${result.provenance.databaseSha256}`, `Snapshot manifest SHA-256: ${result.provenance.manifestSha256 || 'unavailable'}`,
        `Related index SHA-256: ${result.provenance.relatedIndexSha256 || 'unavailable'}`,
        `Related index used: ${result.provenance.relatedIndexUsed ? 'yes' : 'no'}`,
        `Related index source: ${result.provenance.relatedSource ? JSON.stringify(result.provenance.relatedSource) : 'unavailable'}`,
        STATIC_NOTICE, 'Prompt offsets are texts.str1..str16 offsets, not printed effect paragraph numbers.', '',
        ...result.warnings.map((warning) => `NOTE: ${warning}`), ''];
    for (const card of result.cards) {
        lines.push('='.repeat(78), `${card.name} [${card.id}] ×${card.count}`, `${card.typeLabel} | ${card.counts.main} main / ${card.counts.extra} extra / ${card.counts.side} side`,
            card.description, '', 'PROMPT STRINGS');
        for (const prompt of card.prompts) lines.push(`offset ${prompt.index} / description ${prompt.descriptionId}: ${prompt.text}`);
        lines.push('', `SCRIPT: ${card.script.path || 'missing'}`, `SHA-256: ${card.script.sha256 || 'unavailable'}`);
        for (const effect of card.script.effects) {
            lines.push(`Effect ${effect.key}${effect.cloneOf ? ` (clone of ${effect.cloneOf})` : ''}`);
            for (const [key, value] of Object.entries(effect.properties)) lines.push(`  ${key}: ${value}`);
        }
        for (const call of card.script.calls) lines.push(`Lua API/helper call L${call.line}: ${call.name}`);
        for (const reference of card.script.references) lines.push(`Reference L${reference.line}: ${reference.name} [${reference.id}] (${reference.reason})`);
        for (const reference of card.related || []) lines.push(`RelatedCards static ${reference.kind}: ${reference.name} [${reference.id}]${reference.inDeck ? ' (in deck)' : ''}`);
        lines.push('', card.script.source || '[No unambiguous matching script in this snapshot.]', '');
    }
    return lines.join('\n');
}

function createWorkshop(options = {}) {
    const workspaceRoot = options.workspaceRoot || path.resolve(__dirname, '../..');
    const dataRoot = path.join(workspaceRoot, 'DeckWorkshop', 'Data');
    const indexes = new Map();
    const hashes = new Map();
    const relations = new Map();

    function fileHash(file) {
        if (!fs.existsSync(file)) return null;
        const stat = fs.statSync(file);
        const cached = hashes.get(file);
        if (cached?.mtimeMs === stat.mtimeMs && cached.size === stat.size) return cached.hash;
        const hash = sha256(fs.readFileSync(file));
        hashes.set(file, { mtimeMs: stat.mtimeMs, size: stat.size, hash });
        return hash;
    }

    function formatRoot(format) {
        if (!Object.hasOwn(FORMATS, format)) fail('Choose the standard or rush format.');
        return path.join(dataRoot, format);
    }

    function relationIndex(format) {
        const file = path.join(workspaceRoot, 'DeckWorkshop', 'Relations', `${format}.json.gz`);
        if (!fs.existsSync(file)) return null;
        const stat = fs.statSync(file);
        const cached = relations.get(format);
        if (cached?.mtimeMs === stat.mtimeMs && cached.size === stat.size) return cached.data;
        const data = JSON.parse(zlib.gunzipSync(fs.readFileSync(file), { maxOutputLength: 100 * 1024 * 1024 }).toString('utf8'));
        if (data.schemaVersion !== 1 || data.format !== format || !data.cards || !Array.isArray(data.categories)) {
            fail(`The ${FORMATS[format]} relationship index has an unsupported format. Rebuild it with python DeckWorkshop/build_relations.py.`, 503);
        }
        relations.set(format, { mtimeMs: stat.mtimeMs, size: stat.size, data });
        return data;
    }

    function relatedCards(index, id, deckIds, lookup) {
        const categories = index?.cards[String(id)];
        if (!categories) return { related: [], relatedCount: 0 };
        const kinds = ['ID', 'Arch', ...index.categories.filter((kind) => kind !== 'ID' && kind !== 'Arch')];
        const selected = new Map();
        let relatedCount = 0;
        const add = (target, kind) => {
            if (target === id || !Number.isInteger(target) || selected.has(target) || selected.size >= 24) return;
            selected.set(target, { id: target, kind, name: lookup.get(target)?.name || `Card ${target}`,
                inDeck: deckIds.has(target), reason: kind, source: 'RelatedCards static index' });
        };
        // Deck members first, then direct ID/Archetype relations before broad filters.
        for (const kind of kinds) {
            const ids = categories[kind];
            if (!Array.isArray(ids)) continue;
            relatedCount += ids.length;
            for (const target of ids) if (deckIds.has(target)) add(target, kind);
        }
        for (const kind of kinds) {
            const ids = categories[kind];
            if (!Array.isArray(ids)) continue;
            for (const target of ids) {
                if (selected.size >= 24) break;
                add(target, kind);
            }
        }
        return { related: [...selected.values()], relatedCount };
    }

    function indexScripts(format) {
        const root = path.join(formatRoot(format), 'scripts');
        if (!fs.existsSync(root)) return new Map();
        const stamp = fs.statSync(root).mtimeMs;
        if (indexes.get(format)?.stamp === stamp) return indexes.get(format).index;
        const index = new Map();
        const visit = (directory, depth) => {
            if (depth > 12) return;
            for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
                if (entry.isSymbolicLink()) continue;
                const full = path.join(directory, entry.name);
                if (entry.isDirectory()) visit(full, depth + 1);
                else if (entry.isFile() && /^c\d+\.lua$/i.test(entry.name)) {
                    const id = Number(entry.name.slice(1, -4));
                    if (!index.has(id)) index.set(id, []);
                    index.get(id).push(full);
                }
            }
        };
        visit(root, 0);
        indexes.set(format, { stamp, index });
        return index;
    }

    function openDatabase(format) {
        const file = path.join(formatRoot(format), 'cards.cdb');
        if (!fs.existsSync(file)) fail(`${FORMATS[format]} data has not been imported. Run DeckWorkshop/import_data.py with explicit source paths.`, 503);
        return new Database(file, { readonly: true, fileMustExist: true });
    }

    function status() {
        const formats = Object.entries(FORMATS).map(([id, label]) => {
            const available = fs.existsSync(path.join(formatRoot(id), 'cards.cdb'));
            let cardCount = 0;
            if (available) {
                const db = openDatabase(id);
                try { cardCount = db.prepare('SELECT COUNT(*) AS count FROM texts').get().count; } finally { db.close(); }
            }
            return { id, label, available, cardCount, scriptCount: indexScripts(id).size,
                relationsAvailable: fs.existsSync(path.join(workspaceRoot, 'DeckWorkshop', 'Relations', `${id}.json.gz`)) };
        });
        const example = path.join(workspaceRoot, 'WindBot', 'Decks', 'AI_TheCrimsonKing.ydk');
        return { formats, limits: LIMITS, sample: { format: 'standard', name: 'CrimsonWorkshop', ydk: fs.existsSync(example) ? fs.readFileSync(example, 'utf8') : '#main\n62991792\n#extra\n!side\n' } };
    }

    function analyze(input) {
        const format = input?.format;
        formatRoot(format);
        const parsed = parseYdk(input?.ydk);
        const db = openDatabase(format);
        try {
            const lookup = db.prepare('SELECT texts.*, datas.alias, datas.type, datas.atk, datas.def, datas.level, datas.race, datas.attribute FROM texts LEFT JOIN datas ON datas.id = texts.id WHERE texts.id = ?');
            const nameLookup = db.prepare('SELECT name FROM texts WHERE id = ?');
            const scriptIndex = indexScripts(format);
            const databaseSha256 = fileHash(path.join(formatRoot(format), 'cards.cdb'));
            const loadedRelationshipIndex = relationIndex(format);
            let relationshipIndex = loadedRelationshipIndex;
            const warnings = [STATIC_NOTICE, 'Deck sizes and card copies are retained; no banlist or format legality is inferred.'];
            if (relationshipIndex?.source?.databaseSha256 != null
                && String(relationshipIndex.source.databaseSha256).toLowerCase() !== databaseSha256) {
                warnings.push('The RelatedCards index was built from a different card database and has been omitted. Rebuild it with python DeckWorkshop/build_relations.py.');
                relationshipIndex = null;
            }
            if (relationshipIndex) warnings.push('RelatedCards categories are static search relationships, not combo or strength recommendations. The map shows at most 24 related cards per deck card, prioritizing deck members, ID and archetype links.');
            if (format === 'rush') warnings.push('Rush data is available for research. The normal WindBot RDA scaffold has not been validated for Rush rules; executor generation is disabled for this format.');
            const deckIds = new Set(parsed.entries.map((entry) => entry.id));
            let sourceBytes = 0;
            const cards = parsed.entries.map((entry) => {
                const row = lookup.get(entry.id);
                if (!row) warnings.push(`Card ${entry.id} is absent from the ${FORMATS[format]} database.`);
                let candidates = scriptIndex.get(entry.id) || [];
                let resolvedId = entry.id;
                if (!candidates.length && row?.alias > 0 && scriptIndex.has(row.alias)) {
                    candidates = scriptIndex.get(row.alias);
                    resolvedId = row.alias;
                    warnings.push(`${row.name}: using database alias ${row.alias} for its script.`);
                }
                const script = { available: false, path: null, sha256: null, source: '', resolvedId,
                    functions: [], effects: [], references: [], calls: [] };
                if (candidates.length > 1) warnings.push(`Card ${entry.id} has ${candidates.length} matching scripts; no ambiguous script was selected.`);
                if (candidates.length === 1) {
                    const bytes = fs.statSync(candidates[0]).size;
                    if (bytes > LIMITS.scriptBytes || sourceBytes + bytes > LIMITS.sourceBytes) warnings.push(`Script for card ${entry.id} exceeds the analysis source budget and was omitted.`);
                    else {
                        const buffer = fs.readFileSync(candidates[0]);
                        sourceBytes += buffer.length;
                        script.available = true;
                        script.path = path.relative(workspaceRoot, candidates[0]).split(path.sep).join('/');
                        script.sha256 = sha256(buffer);
                        script.source = buffer.toString('utf8').replace(/^\uFEFF/, '');
                        Object.assign(script, inspectLua(script.source, resolvedId));
                        script.references = script.references.map((reference) => ({ ...reference,
                            name: nameLookup.get(reference.id)?.name || `Card ${reference.id}`, inDeck: deckIds.has(reference.id) }));
                    }
                }
                const prompts = [];
                for (let index = 0; index < 16; index++) {
                    if (row?.[`str${index + 1}`]) prompts.push({ index, text: row[`str${index + 1}`], descriptionId: entry.id * 16 + index });
                }
                return { id: entry.id, name: row?.name || `Unknown card ${entry.id}`, description: row?.desc || '',
                    found: Boolean(row), count: entry.total, counts: { main: entry.main, extra: entry.extra, side: entry.side, total: entry.total },
                    sections: ['main', 'extra', 'side'].filter((section) => entry[section]),
                    type: row?.type || 0, typeLabel: typeLabel(row?.type || 0), attack: row?.atk ?? null, defense: row?.def ?? null,
                    level: row ? (row.level & 255) : null, race: row?.race ?? null, attribute: row?.attribute ?? null, prompts, script,
                    ...relatedCards(relationshipIndex, entry.id, deckIds, nameLookup) };
            });
            const manifestPath = path.join(workspaceRoot, 'DeckWorkshop', 'data-provenance.json');
            const result = { format, deck: parsed.deck, cards, graph: createGraph(cards), warnings, normalizedYdk: parsed.normalizedYdk,
                provenance: { database: `DeckWorkshop/Data/${format}/cards.cdb`, databaseSha256,
                    manifest: 'DeckWorkshop/data-provenance.json', manifestSha256: fileHash(manifestPath),
                    relatedIndexSha256: loadedRelationshipIndex ? fileHash(path.join(workspaceRoot, 'DeckWorkshop', 'Relations', `${format}.json.gz`)) : null,
                    relatedIndexUsed: Boolean(relationshipIndex), relatedSource: loadedRelationshipIndex?.source || null } };
            result.report = createReport(result);
            return result;
        } finally { db.close(); }
    }

    function generate(input) {
        if (input?.format !== 'standard') fail('Executor generation currently supports standard decks only. Rush analysis and text export remain available.');
        const analysis = analyze(input);
        return generateFiles(input.name, analysis, workspaceRoot);
    }

    return { status, analyze, generate };
}

module.exports = { createWorkshop, parseYdk, inspectLua, maskLua, LIMITS };
