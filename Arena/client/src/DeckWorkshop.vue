<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { requestWorkshop as api } from './workshop-api.mjs';
import './deck-workshop.css';

const status = ref(null);
const statusError = ref('');
const format = ref('standard');
const deckName = ref('MyDeck');
const ydk = ref('');
const loadedName = ref('');
const analyzing = ref(false);
const generating = ref(false);
const error = ref('');
const result = ref(null);
const analyzedInput = ref(null);
const selectedId = ref(null);
const search = ref('');
const section = ref('all');
const graphMode = ref('effects');
const inspectorTab = ref('card');
const selectedLine = ref(0);
const selectedNodeId = ref('');
const relationFocusId = ref(null);
const inputExpanded = ref(true);
const dragOver = ref(false);
const fileInput = ref(null);
const scriptView = ref(null);
const graphElement = ref(null);
const graphPanelWidth = ref(780);
const scale = ref(1);
const offset = ref({ x: 0, y: 0 });
const exports = ref(null);
const exportIndex = ref(0);
const exportDialog = ref(null);
let lastExportTrigger;
let panStart;
let requestSequence = 0;
let unmounted = false;
let graphResizeObserver;

const formats = computed(() => status.value?.formats || [
    { id: 'standard', label: 'Standard' }, { id: 'rush', label: 'Rush Duel' },
]);
const activeFormat = computed(() => formats.value.find((item) => item.id === format.value));
const cards = computed(() => result.value?.cards || []);
const selectedCard = computed(() => cards.value.find((card) => card.id === selectedId.value) || null);
const externalReference = computed(() => !selectedCard.value && result.value?.graph?.nodes.find((node) => node.id === selectedNodeId.value));
const externalReferenceSources = computed(() => (result.value?.graph?.edges || []).filter((edge) => edge.target === selectedNodeId.value && ['related', 'references'].includes(edge.kind)).slice(0, 12));
const filteredCards = computed(() => {
    const query = search.value.trim().toLocaleLowerCase();
    return cards.value.filter((card) => (section.value === 'all' || card.sections.includes(section.value))
        && (!query || `${card.name} ${card.id}`.toLocaleLowerCase().includes(query)));
});
const scriptCoverage = computed(() => cards.value.filter((card) => card.script?.available).length);
const effectCount = computed(() => cards.value.reduce((total, card) => total + (card.script?.effects?.length || 0), 0));
const sourceLines = computed(() => selectedCard.value?.script?.source?.split(/\r?\n/) || []);
const currentExport = computed(() => exports.value?.files?.[exportIndex.value]);
const stale = computed(() => analyzedInput.value && (analyzedInput.value.ydk !== ydk.value || analyzedInput.value.format !== format.value));

function shorten(text, limit = 29) {
    const value = String(text || '');
    return value.length > limit ? `${value.slice(0, limit - 1)}…` : value;
}

const graph = computed(() => {
    const width = Math.max(620, Math.min(1000, graphPanelWidth.value));
    const nodeWidth = Math.min(224, Math.floor((width - 100) / 3));
    if (!result.value?.graph) return { nodes: [], edges: [], height: 520, width, nodeWidth, omitted: 0 };
    const raw = result.value.graph;
    let nodes;
    let edges;
    if (graphMode.value !== 'effects') {
        edges = raw.edges.filter((edge) => graphMode.value === 'related'
            ? edge.kind === 'related' && edge.cardId === relationFocusId.value
            : edge.kind === 'references');
        const connectedIds = new Set(edges.flatMap((edge) => [edge.source, edge.target]));
        nodes = raw.nodes.filter((node) => node.kind === 'card' && (connectedIds.has(node.id) || node.cardId === selectedId.value));
    } else {
        nodes = raw.nodes.filter((node) => node.cardId === selectedId.value && ['card', 'function', 'effect'].includes(node.kind));
        const ids = new Set(nodes.map((node) => node.id));
        edges = raw.edges.filter((edge) => ids.has(edge.source) && ids.has(edge.target));
        const callbackFunctions = new Set(edges.filter((edge) => edge.kind === 'callback').map((edge) => edge.target));
        edges = edges.filter((edge) => edge.kind !== 'defines' || !callbackFunctions.has(edge.target));
    }
    const omitted = Math.max(0, nodes.length - 100);
    nodes = nodes.slice(0, 100);
    const ids = new Set(nodes.map((node) => node.id));
    edges = edges.filter((edge) => ids.has(edge.source) && ids.has(edge.target));
    let height = 520;
    if (graphMode.value !== 'effects') {
        const sourceIds = new Set(edges.map((edge) => edge.source));
        const sources = nodes.filter((node) => sourceIds.has(node.id));
        const targets = nodes.filter((node) => !sourceIds.has(node.id));
        if (!sources.length && targets.length) sources.push(targets.shift());
        height = Math.max(520, Math.max(sources.length, targets.length) * 78 + 100);
        nodes = [
            ...sources.map((node, index) => ({ ...node, x: nodeWidth / 2 + 20, y: 80 + index * 78 })),
            ...targets.map((node, index) => ({ ...node, x: width - nodeWidth / 2 - 20, y: 80 + index * 78 })),
        ];
    } else {
        const columns = ['card', 'effect', 'function'];
        const maxColumn = Math.max(1, ...columns.map((kind) => nodes.filter((node) => node.kind === kind).length));
        height = Math.max(520, maxColumn * 78 + 100);
        const callbackOrder = [...new Set(edges.filter((edge) => edge.kind === 'callback').map((edge) => edge.target))];
        const functionOrder = new Map(callbackOrder.map((id, index) => [id, index]));
        const left = nodeWidth / 2 + 20;
        nodes = columns.flatMap((kind, column) => {
            const group = nodes.filter((node) => node.kind === kind);
            if (kind === 'function') group.sort((a, b) => (functionOrder.get(a.id) ?? 1000) - (functionOrder.get(b.id) ?? 1000));
            return group.map((node, index) => ({ ...node, x: left + column * ((width - left * 2) / 2),
                y: 80 + index * 78 }));
        });
        const functions = new Map(nodes.filter((node) => node.kind === 'function').map((node) => [node.id, node]));
        let previousY = 0;
        for (const node of nodes.filter((item) => item.kind === 'effect')) {
            const targets = edges.filter((edge) => edge.source === node.id && edge.kind === 'callback').map((edge) => functions.get(edge.target)).filter(Boolean);
            const preferredY = targets.length ? targets.reduce((sum, target) => sum + target.y, 0) / targets.length : previousY + 78;
            node.y = Math.max(80, previousY + 78, preferredY);
            previousY = node.y;
        }
        height = Math.max(height, previousY + 80);
    }
    const byId = new Map(nodes.map((node) => [node.id, node]));
    edges = edges.map((edge) => ({ ...edge, start: byId.get(edge.source), end: byId.get(edge.target) }));
    return { nodes, edges, height, width, nodeWidth, omitted };
});

function edgePath(edge) {
    const half = graph.value.nodeWidth / 2;
    if (graphMode.value !== 'effects' && edge.start.x === edge.end.x) {
        const x = edge.start.x + half;
        return `M${x},${edge.start.y} C${x + 75},${edge.start.y} ${x + 75},${edge.end.y} ${x},${edge.end.y}`;
    }
    const forward = edge.end.x >= edge.start.x;
    const x1 = edge.start.x + (forward ? half : -half);
    const x2 = edge.end.x + (forward ? -half : half);
    if (edge.kind === 'defines') {
        const channelY = graph.value.height - 30;
        return `M${edge.start.x},${edge.start.y + 27} V${channelY} H${x2 - 12} V${edge.end.y} H${x2}`;
    }
    const mid = (x1 + x2) / 2;
    return `M${x1},${edge.start.y} C${mid},${edge.start.y} ${mid},${edge.end.y} ${x2},${edge.end.y}`;
}

function resetGraph() {
    scale.value = 1; offset.value = { x: 0, y: 0 };
    if (graphElement.value?.parentElement) graphElement.value.parentElement.scrollTo({ top: 0, left: 0 });
}
function zoom(amount) { scale.value = Math.max(0.4, Math.min(2.2, scale.value + amount)); }
function panBegin(event) {
    if (event.button !== 0 || event.target.closest('[data-graph-node]')) return;
    panStart = { x: event.clientX, y: event.clientY, offset: { ...offset.value } };
    event.currentTarget.setPointerCapture(event.pointerId);
}
function panMove(event) {
    if (!panStart || !graphElement.value) return;
    const ratio = graph.value.width / graphElement.value.getBoundingClientRect().width;
    offset.value = { x: panStart.offset.x + (event.clientX - panStart.x) * ratio,
        y: panStart.offset.y + (event.clientY - panStart.y) * ratio };
}
function panEnd() { panStart = null; }

function selectCard(card) {
    selectedId.value = card.id;
    relationFocusId.value = card.id;
    selectedLine.value = 0;
    selectedNodeId.value = `card:${card.id}`;
    inspectorTab.value = 'card';
    resetGraph();
}

async function showLine(line) {
    selectedLine.value = Number(line) || 0;
    inspectorTab.value = 'script';
    await nextTick();
    const target = scriptView.value?.querySelector(`[data-line="${selectedLine.value}"]`);
    if (target && scriptView.value) scriptView.value.scrollTop = Math.max(0, target.offsetTop - 85);
}

function selectNode(node) {
    const card = cards.value.find((item) => item.id === node.cardId);
    if (card && card.id !== selectedId.value) selectCard(card);
    if (!card) { selectedId.value = node.cardId; selectedLine.value = 0; }
    selectedNodeId.value = node.id;
    if (node.line) showLine(node.line);
    else inspectorTab.value = 'card';
}

function inspectRelated(card) {
    graphMode.value = 'related';
    const node = result.value?.graph?.nodes.find((item) => item.id === `card:${card.id}`);
    if (node) selectNode(node);
}

function inspectReferenceSource(edge) {
    const card = cards.value.find((item) => item.id === edge.cardId);
    if (card) selectCard(card);
    if (edge.line) showLine(edge.line);
}

async function analyze() {
    const sequence = ++requestSequence;
    const input = { format: format.value, ydk: ydk.value };
    analyzing.value = true;
    error.value = '';
    try {
        const value = await api('/api/workshop/analyze', input);
        if (unmounted || sequence !== requestSequence) return;
        result.value = value;
        analyzedInput.value = input;
        selectedId.value = value.cards[0]?.id || null;
        relationFocusId.value = selectedId.value;
        selectedNodeId.value = selectedId.value ? `card:${selectedId.value}` : '';
        selectedLine.value = 0;
        inspectorTab.value = 'card';
        inputExpanded.value = false;
        search.value = '';
        section.value = 'all';
        resetGraph();
    } catch (failure) {
        if (!unmounted && sequence === requestSequence) error.value = failure.message;
    } finally {
        if (!unmounted && sequence === requestSequence) analyzing.value = false;
    }
}

async function readDeck(file) {
    if (!file) return;
    error.value = '';
    if (!/\.ydk$/i.test(file.name)) { error.value = 'Choose a .ydk deck file.'; return; }
    const limit = status.value?.limits?.deckBytes || 32768;
    if (file.size > limit) { error.value = `Deck files must be at most ${Math.floor(limit / 1024)} KB.`; return; }
    try {
        ydk.value = await file.text();
        loadedName.value = file.name;
        deckName.value = file.name.replace(/\.ydk$/i, '').replace(/^AI_/, '');
    } catch { error.value = 'This deck file could not be read.'; }
}

function onDrop(event) { dragOver.value = false; readDeck(event.dataTransfer.files[0]); }
function onFile(event) { readDeck(event.target.files[0]); event.target.value = ''; }
async function loadSample() {
    if (!status.value?.sample) return;
    format.value = status.value.sample.format;
    ydk.value = status.value.sample.ydk;
    deckName.value = status.value.sample.name;
    loadedName.value = 'RDA reference deck';
    await analyze();
}

function download(name, content, mime = 'text/plain;charset=utf-8') {
    const url = URL.createObjectURL(new Blob([content], { type: mime }));
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = name.split(/[\\/]/).pop().replace(/[<>:"/\\|?*\u0000-\u001f]/g, '_');
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}

function downloadReport() {
    if (result.value) download(`${deckName.value || 'deck'}-${result.value.format}-analysis.txt`, result.value.report);
}

async function generate() {
    if (!analyzedInput.value || stale.value) return;
    generating.value = true;
    error.value = '';
    lastExportTrigger = document.activeElement;
    try {
        exports.value = await api('/api/workshop/generate', { ...analyzedInput.value, name: deckName.value });
        exportIndex.value = 0;
        await nextTick();
        exportDialog.value?.showModal();
    } catch (failure) { error.value = failure.message; }
    finally { generating.value = false; }
}

function closeExport() { exportDialog.value?.close(); lastExportTrigger?.focus(); }
watch(graphMode, resetGraph);
watch(graphElement, (element) => {
    graphResizeObserver?.disconnect();
    if (!element?.parentElement) return;
    const panel = element.parentElement;
    const updateWidth = () => {
        if (panel.clientWidth > 0) graphPanelWidth.value = panel.clientWidth;
    };
    updateWidth();
    graphResizeObserver = new ResizeObserver(updateWidth);
    graphResizeObserver.observe(panel);
}, { flush: 'post' });

onMounted(async () => {
    try { status.value = await api('/api/workshop/status'); }
    catch (failure) { statusError.value = failure.message; }
});
onBeforeUnmount(() => { unmounted = true; requestSequence++; graphResizeObserver?.disconnect(); });
</script>

<template>
    <main class="deck-workshop">
        <section class="dw-heading">
            <div>
                <div class="dw-eyebrow"><span class="dw-signal"></span> WINDBOT / AUTHORING LAB</div>
                <h1>Deck <em>Workshop.</em></h1>
                <p>Turn a deck into a map of its cards, effects, and code. Build your next executor from evidence.</p>
            </div>
            <div class="dw-heading-aside">
                <span class="dw-version">YDK → LUA → C#</span>
                <button class="dw-button dw-button-subtle" :disabled="!status?.sample || analyzing" @click="loadSample">
                    <span aria-hidden="true">↗</span> Explore the RDA deck
                </button>
            </div>
        </section>

        <div v-if="statusError" class="dw-notice dw-error" role="alert">Workshop data could not be loaded: {{ statusError }}</div>
        <div v-if="error" class="dw-notice dw-error" role="alert">{{ error }}</div>

        <section class="dw-input-panel" :class="{ compact: !inputExpanded && result }">
            <div class="dw-panel-label">
                <div><span class="dw-step">01</span><h2>Bring your deck</h2><span v-if="loadedName" class="dw-loaded-name">{{ loadedName }}</span></div>
                <button v-if="result" class="dw-text-button" :aria-expanded="inputExpanded" @click="inputExpanded = !inputExpanded">{{ inputExpanded ? 'Collapse input −' : 'Edit deck +' }}</button>
            </div>
            <div v-show="inputExpanded || !result" class="dw-input-grid">
                <div class="dw-drop-zone" :class="{ dragging: dragOver }" @dragover.prevent="dragOver = true" @dragleave.prevent="dragOver = false" @drop.prevent="onDrop">
                    <input ref="fileInput" class="dw-visually-hidden" type="file" accept=".ydk" aria-label="Import YDK deck" @change="onFile">
                    <svg viewBox="0 0 48 48" aria-hidden="true"><rect x="9" y="8" width="23" height="31" rx="3"/><path d="M17 5h19a3 3 0 0 1 3 3v26M15 24h11m-5-5 5 5-5 5"/></svg>
                    <strong>{{ loadedName || 'Drop a .ydk file here' }}</strong>
                    <span>Your main, extra, and side deck in one place.</span>
                    <button class="dw-button dw-button-subtle" @click="fileInput?.click()">Choose deck file</button>
                </div>
                <label class="dw-ydk-field"><span>OR PASTE YOUR YDK</span><textarea v-model="ydk" @input="loadedName = ''" spellcheck="false" placeholder="#main&#10;19434243&#10;#extra&#10;70902743&#10;!side" aria-label="YDK deck contents"></textarea></label>
                <div class="dw-deck-options">
                    <label class="dw-field"><span>CARD COLLECTION</span><select v-model="format"><option v-for="collection in formats" :key="collection.id" :value="collection.id">{{ collection.label }}</option></select></label>
                    <label class="dw-field"><span>EXECUTOR NAME</span><input v-model="deckName" maxlength="80" placeholder="MyDeck" autocomplete="off"></label>
                    <p class="dw-data-note"><span class="dw-signal" :class="{ unavailable: !activeFormat?.available }"></span>{{ activeFormat?.available ? `${Number(activeFormat.cardCount).toLocaleString()} cards in local snapshot` : 'Local snapshot unavailable' }}</p>
                    <button class="dw-button dw-button-primary dw-analyze" :disabled="analyzing || !ydk.trim() || !activeFormat?.available" @click="analyze">{{ analyzing ? 'Reading cards & scripts…' : 'Analyze deck' }} <span aria-hidden="true">→</span></button>
                </div>
            </div>
            <div v-if="stale" class="dw-stale" role="status">The deck input has changed. <button class="dw-text-button" :disabled="analyzing" @click="analyze">Analyze again</button> to update this workspace.</div>
        </section>

        <template v-if="result">
            <section class="dw-summary" aria-label="Deck summary">
                <div><strong>{{ result.deck.main }}</strong><span>MAIN DECK</span></div>
                <div><strong>{{ result.deck.extra }}</strong><span>EXTRA DECK</span></div>
                <div><strong>{{ result.deck.side }}</strong><span>SIDE DECK</span></div>
                <div class="dw-summary-coverage"><strong>{{ scriptCoverage }}<small>/ {{ cards.length }}</small></strong><span>CARDS WITH LUA</span></div>
                <div><strong>{{ effectCount }}</strong><span>EFFECT DECLARATIONS</span></div>
                <div class="dw-export-actions"><button class="dw-button dw-button-subtle" :disabled="stale" @click="downloadReport">↓ Text report</button><button class="dw-button dw-button-primary" :disabled="generating || stale || result.format === 'rush'" :title="result.format === 'rush' ? 'Rush card analysis is available; this WindBot scaffold targets Standard duels.' : 'Generate an editable C# starter'" @click="generate">{{ generating ? 'Generating…' : 'Generate starter' }} <span aria-hidden="true">↗</span></button></div>
            </section>
            <div v-if="result.format === 'rush'" class="dw-notice">Rush collection · Card text and Lua analysis are available. The RDA starter targets the normal WindBot runtime; Rush bot generation is not enabled.</div>
            <details v-if="result.warnings?.length" class="dw-warnings"><summary>{{ result.warnings.length }} analysis {{ result.warnings.length === 1 ? 'note' : 'notes' }} <span>Review coverage and input limits</span></summary><ul><li v-for="warning in result.warnings" :key="warning">{{ warning }}</li></ul></details>

            <section class="dw-workbench">
                <aside class="dw-roster">
                    <div class="dw-roster-title"><h2>Deck library</h2><span>{{ cards.length }}</span></div>
                    <label class="dw-search"><span aria-hidden="true">⌕</span><input v-model="search" placeholder="Find a card or ID" aria-label="Search deck cards"></label>
                    <div class="dw-section-filter" aria-label="Filter deck section"><button v-for="part in ['all', 'main', 'extra', 'side']" :key="part" :class="{ active: section === part }" :aria-pressed="section === part" @click="section = part">{{ part }}</button></div>
                    <div class="dw-card-list">
                        <button v-for="card in filteredCards" :key="card.id" class="dw-card-row" :class="{ selected: selectedId === card.id }" :aria-pressed="selectedId === card.id" @click="selectCard(card)">
                            <span class="dw-card-glyph" :class="{ extra: card.sections.includes('extra'), spell: card.typeLabel?.includes('Spell'), trap: card.typeLabel?.includes('Trap') }"><svg viewBox="0 0 22 28" aria-hidden="true"><rect x="2" y="2" width="18" height="24" rx="2"/><path d="m11 7 5 7-5 7-5-7Z"/></svg></span>
                            <span class="dw-card-row-text"><strong>{{ card.name }}</strong><small>{{ card.id }} <span v-if="!card.script?.available" class="dw-missing">NO LUA</span></small></span><span class="dw-copy-count">×{{ card.count }}</span>
                        </button>
                        <p v-if="!filteredCards.length" class="dw-empty-list">No cards match this filter.</p>
                    </div>
                    <div class="dw-roster-footer"><span class="dw-signal"></span>{{ result.format === 'rush' ? 'RUSH' : 'STANDARD' }} SNAPSHOT · READ ONLY</div>
                </aside>

                <article class="dw-map-panel">
                    <header class="dw-map-heading"><div><span class="dw-step">02</span><h2>Explore the connections</h2></div><span class="dw-static-badge">STATIC ANALYSIS</span></header>
                    <div class="dw-map-toolbar"><div class="dw-mode-switch"><button :class="{ active: graphMode === 'effects' }" :aria-pressed="graphMode === 'effects'" @click="graphMode = 'effects'">Effect map</button><button :class="{ active: graphMode === 'deck' }" :aria-pressed="graphMode === 'deck'" @click="graphMode = 'deck'">Lua references</button><button :class="{ active: graphMode === 'related' }" :aria-pressed="graphMode === 'related'" @click="graphMode = 'related'">Related cards</button></div><div class="dw-zoom"><button aria-label="Zoom out" @click="zoom(-0.2)">−</button><button aria-label="Fit graph to panel width" title="Fit to panel width" @click="resetGraph">{{ scale === 1 ? 'Fit' : `${Math.round(scale * 100)}%` }}</button><button aria-label="Zoom in" @click="zoom(0.2)">+</button></div></div>
                    <div class="dw-map-caption"><strong>{{ graphMode === 'effects' ? selectedCard?.name : graphMode === 'related' ? `Related to ${cards.find(card => card.id === relationFocusId)?.name || 'selected card'}` : 'Cards named in Lua' }}</strong><span>{{ graphMode === 'effects' ? 'Card → effect declarations → callback functions' : graphMode === 'related' ? 'RelatedCards category links; unranked research leads. Scroll the map to inspect all targets.' : 'Literal Lua references; source cards on the left, additional targets on the right. Scroll to inspect all cards.' }}</span></div>
                    <div class="dw-graph-scroll">
                        <svg ref="graphElement" class="dw-graph" :viewBox="`0 0 ${graph.width} ${graph.height}`" :style="{ width: `${graph.width}px`, height: `${graph.height}px` }" role="group" aria-label="Interactive card script graph. Select a node to inspect its source." @pointerdown="panBegin" @pointermove="panMove" @pointerup="panEnd" @pointercancel="panEnd">
                            <defs><pattern id="dw-grid" width="25" height="25" patternUnits="userSpaceOnUse"><circle cx="1" cy="1" r="1" class="dw-grid-dot"/></pattern><filter id="dw-node-glow" x="-30%" y="-50%" width="160%" height="200%"><feGaussianBlur stdDeviation="5"/></filter></defs>
                            <rect width="100%" height="100%" fill="url(#dw-grid)"/>
                            <g :transform="`translate(${offset.x} ${offset.y}) translate(${graph.width / 2} ${graph.height / 2}) scale(${scale}) translate(${-graph.width / 2} ${-graph.height / 2})`">
                                <g v-if="graphMode !== 'effects'" class="dw-graph-lane-labels"><text x="20" y="26">SOURCE CARDS</text><text :x="graph.width - graph.nodeWidth - 20" y="26">{{ graphMode === 'related' ? 'RELATED CARDS' : 'ADDITIONAL TARGETS' }}</text></g>
                                <g class="dw-graph-edges"><path v-for="edge in graph.edges" :key="edge.id" :d="edgePath(edge)" :class="[`dw-edge-${edge.kind}`, { highlighted: edge.kind === 'defines' ? edge.target === selectedNodeId : edge.source === selectedNodeId || edge.target === selectedNodeId }]"><title>{{ edge.label || edge.kind }}{{ edge.line ? ` · line ${edge.line}` : '' }}</title></path></g>
                                <g v-for="node in graph.nodes" :key="node.id" data-graph-node="true" class="dw-graph-node" :class="[`dw-node-${node.kind}`, { selected: selectedNodeId === node.id, external: !cards.some(card => card.id === node.cardId) }]" :transform="`translate(${node.x} ${node.y})`" role="button" tabindex="0" :aria-label="`${node.label}${node.line ? `, source line ${node.line}` : ''}`" @click.stop="selectNode(node)" @keydown.enter.prevent="selectNode(node)" @keydown.space.prevent="selectNode(node)">
                                    <title>{{ node.label }}{{ node.line ? ` · line ${node.line}` : '' }}</title><rect class="dw-node-halo" :x="-graph.nodeWidth / 2 - 2" y="-29" :width="graph.nodeWidth + 4" height="58" rx="9"/><rect class="dw-node-box" :x="-graph.nodeWidth / 2" y="-27" :width="graph.nodeWidth" height="54" rx="8"/><circle class="dw-node-port" :cx="-graph.nodeWidth / 2" cy="0" r="4"/><circle class="dw-node-port" :cx="graph.nodeWidth / 2" cy="0" r="4"/><text class="dw-node-kind" :x="-graph.nodeWidth / 2 + 14" y="-9">{{ node.kind.toUpperCase() }}{{ node.line ? ` / L${node.line}` : '' }}</text><text class="dw-node-label" :x="-graph.nodeWidth / 2 + 14" y="12">{{ shorten(node.label, Math.floor((graph.nodeWidth - 28) / 6.6)) }}</text>
                                </g>
                            </g>
                        </svg>
                        <div v-if="!graph.nodes.length" class="dw-graph-empty"><strong>No static connections found.</strong><span>Select another card or inspect the source directly.</span></div>
                    </div>
                    <div class="dw-map-legend"><span><i class="dw-legend-card"></i>Card</span><span><i class="dw-legend-effect"></i>Effect</span><span><i class="dw-legend-function"></i>Function</span><span v-if="graphMode === 'related'"><i class="dw-legend-related"></i>Category link</span><small>Drag to pan · select a node for source</small></div>
                    <p class="dw-map-limit">{{ graph.omitted ? `${graph.omitted} nodes are outside this view. ` : '' }}{{ graphMode === 'related' ? 'Category links are static index matches, not recommendations or verified combos. Up to 24 related cards are shown per source card.' : 'Connections show declarations and literal references. They do not prove combos, effect order, or legal plays.' }}</p>
                </article>

                <aside class="dw-inspector">
                    <header class="dw-inspector-heading"><span class="dw-step">03</span><h2>Inspect the evidence</h2></header>
                    <div class="dw-inspector-tabs"><button :class="{ active: inspectorTab === 'card' }" :aria-pressed="inspectorTab === 'card'" @click="inspectorTab = 'card'">Card & effects</button><button :class="{ active: inspectorTab === 'script' }" :aria-pressed="inspectorTab === 'script'" @click="inspectorTab = 'script'">Lua source <span v-if="selectedLine">L{{ selectedLine }}</span></button></div>
                    <template v-if="selectedCard">
                        <div v-show="inspectorTab === 'card'" class="dw-card-detail">
                            <span class="dw-detail-type">{{ selectedCard.typeLabel || 'Unknown card type' }}</span><h3>{{ selectedCard.name }}</h3><div class="dw-detail-id"><code>{{ selectedCard.id }}</code><span>×{{ selectedCard.count }} in deck</span></div>
                            <div v-if="selectedCard.type & 1" class="dw-card-stats"><div><span>LEVEL / RANK</span><strong>{{ selectedCard.level }}</strong></div><div><span>ATK</span><strong>{{ selectedCard.attack }}</strong></div><div><span>DEF</span><strong>{{ selectedCard.defense }}</strong></div></div>
                            <p class="dw-card-description">{{ selectedCard.description || 'No card text is available in this snapshot.' }}</p>
                            <section class="dw-detail-block"><h4>Script evidence <span>{{ selectedCard.script?.functions?.length || 0 }} functions</span></h4><button v-if="selectedCard.script?.available" class="dw-script-link" @click="showLine(1)"><span>↗ {{ selectedCard.script.path }}</span><small>Open source</small></button><p v-else class="dw-muted">No matching Lua script in this collection. Some normal monsters do not require one.</p><p v-if="selectedCard.script?.sha256" class="dw-source-hash" :title="selectedCard.script.sha256">SHA-256 {{ selectedCard.script.sha256.slice(0, 16) }}…</p></section>
                            <section v-if="selectedCard.script?.effects?.length" class="dw-detail-block"><h4>Declared effects <span>{{ selectedCard.script.effects.length }}</span></h4><div v-for="effect in selectedCard.script.effects" :key="`${effect.key}:${effect.line}`" class="dw-effect-detail"><button class="dw-effect-title" @click="showLine(effect.line)"><strong>{{ effect.key }}</strong><span>line {{ effect.line }} ↗</span></button><code v-for="(value, key) in effect.properties" :key="key" class="dw-effect-property"><span>{{ key.replace(/^Set/, '') }}</span>{{ value }}</code><div class="dw-callbacks"><button v-for="callback in effect.callbacks" :key="`${callback.role}:${callback.name}:${callback.line}`" @click="showLine(callback.line)"><span>{{ callback.role }}</span><code>{{ callback.name }}</code></button></div></div></section>
                            <section v-if="selectedCard.related?.length" class="dw-detail-block"><h4>Related cards <span>{{ selectedCard.related.length }} shown</span></h4><p class="dw-muted">Static category matches from RelatedCards. Research leads, not ranked deck changes.</p><button v-for="related in selectedCard.related" :key="`${related.kind}:${related.id}`" class="dw-related-card" @click="inspectRelated(related)"><span><strong>{{ related.name }}</strong><small>{{ related.reason || related.kind }}</small></span><em>{{ related.inDeck ? 'IN DECK' : related.kind }}</em></button><p v-if="selectedCard.relatedCount > selectedCard.related.length" class="dw-muted">{{ selectedCard.relatedCount }} raw index links; this view is bounded to the selected related cards.</p></section>
                            <section v-if="selectedCard.prompts?.length" class="dw-detail-block"><h4>Prompt strings <span>{{ selectedCard.prompts.length }}</span></h4><p class="dw-muted">String offsets identify prompts, not effect paragraphs.</p><div v-for="prompt in selectedCard.prompts" :key="prompt.index" class="dw-prompt"><code>str{{ prompt.index + 1 }} · {{ prompt.descriptionId }}</code><p>{{ prompt.text }}</p></div></section>
                        </div>
                        <div v-show="inspectorTab === 'script'" class="dw-source-detail"><div class="dw-source-heading"><code>{{ selectedCard.script?.path || 'No Lua source' }}</code><button v-if="selectedCard.script?.available" class="dw-text-button" @click="download(`c${selectedCard.id}.lua`, selectedCard.script.source)">↓ Save</button></div><div ref="scriptView" class="dw-source-code" tabindex="0" aria-label="Lua source with line numbers"><div v-for="(line, index) in sourceLines" :key="index" class="dw-source-line" :class="{ selected: selectedLine === index + 1 }" :data-line="index + 1"><span>{{ index + 1 }}</span><code>{{ line || ' ' }}</code></div><p v-if="!sourceLines.length" class="dw-muted">This card has no source script in the selected snapshot.</p></div></div>
                    </template>
                    <div v-else-if="externalReference" class="dw-card-detail"><span class="dw-detail-type">REFERENCED CARD · OUTSIDE THIS DECK</span><h3>{{ externalReference.label }}</h3><div class="dw-detail-id"><code>{{ externalReference.cardId }}</code></div><p class="dw-muted">This card appears in a script or category link. Include it in a deck input to inspect its text and Lua. Its presence here does not establish a useful combo or a legal target.</p><section class="dw-detail-block"><h4>Reference evidence</h4><button v-for="edge in externalReferenceSources" :key="edge.id" class="dw-related-card" @click="inspectReferenceSource(edge)"><span><strong>{{ cards.find(card => card.id === edge.cardId)?.name || edge.cardId }}</strong><small>{{ edge.kind === 'related' ? `RelatedCards category: ${edge.label}` : `Lua reference · line ${edge.line || 'unknown'}` }}</small></span><em>↗</em></button></section></div>
                </aside>
            </section>
            <section class="dw-next-step"><span class="dw-step">NEXT</span><div><h3>From a scaffold to a tested bot.</h3><p>Implement card transitions and prompt choices, compile the generated executor, then test against the same card database and scripts. Use Arena to compare frozen builds.</p></div><span class="dw-next-tag">RDA STRUCTURE / C# 6</span></section>
        </template>

        <section v-else class="dw-empty-workbench">
            <div class="dw-empty-map" aria-hidden="true"><svg viewBox="0 0 640 180"><path d="M130 90C230 90 210 45 300 45M130 90C230 90 210 135 300 135M380 45C450 45 430 90 510 90M380 135C450 135 430 90 510 90"/><rect x="30" y="65" width="130" height="50" rx="8"/><rect x="270" y="20" width="130" height="50" rx="8"/><rect x="270" y="110" width="130" height="50" rx="8"/><rect x="490" y="65" width="130" height="50" rx="8"/><text x="95" y="95">YOUR DECK</text><text x="335" y="50">CARD TEXT</text><text x="335" y="140">LUA EFFECTS</text><text x="555" y="95">EXECUTOR</text></svg></div>
            <h2>See what your deck is made of.</h2><p>Import a deck to connect card text with its actual scripts, inspect callback lines, and export a starting point for your own bot.</p>
            <div class="dw-empty-features"><span>01 / Explore cards</span><span>02 / Trace script evidence</span><span>03 / Generate an RDA starter</span></div>
        </section>

        <dialog ref="exportDialog" class="dw-export-dialog" @click="($event.target === exportDialog) && closeExport()" @cancel.prevent="closeExport">
            <div v-if="exports" class="dw-export-sheet"><header><div><span class="dw-eyebrow">EXECUTOR STARTER</span><h2>{{ exports.className || exports.name }}</h2></div><button class="dw-button dw-button-subtle" aria-label="Close generated files" @click="closeExport">✕</button></header><p class="dw-export-intro">Your deck constants and RDA planning structure are ready to edit. Card models and prompt handling still need implementation and duel validation.</p><ul v-if="exports.warnings?.length" class="dw-export-warnings"><li v-for="warning in exports.warnings" :key="warning">{{ warning }}</li></ul><div class="dw-export-content"><nav aria-label="Generated files"><button v-for="(file, index) in exports.files" :key="file.name" :class="{ active: exportIndex === index }" @click="exportIndex = index"><span>{{ file.name }}</span><small>{{ file.content.length.toLocaleString() }} characters</small></button></nav><div v-if="currentExport" class="dw-export-preview"><div><code>{{ currentExport.name }}</code><button class="dw-button dw-button-primary" @click="download(currentExport.name, currentExport.content)">↓ Download file</button></div><pre tabindex="0">{{ currentExport.content }}</pre></div></div></div>
        </dialog>
    </main>
</template>
