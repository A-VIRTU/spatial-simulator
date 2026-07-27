let map = null;
let entitiesStore = [];
let edgesStore = [];
let eventsStore = [];
let selectedEntityId = 'settlement_runarov';

document.addEventListener('DOMContentLoaded', async () => {
    initTabs();
    initMap();
    initSignalR();

    await loadData();

    document.getElementById('btn-reload').addEventListener('click', loadData);
    document.getElementById('btn-step-agent').addEventListener('click', executeAgentStep);
    document.getElementById('btn-search-memory').addEventListener('click', searchAgentMemories);
    document.getElementById('btn-take-matches').addEventListener('click', () => triggerAgentAction('take_item', { item: 'item_sirky' }));
    document.getElementById('btn-move-corridor').addEventListener('click', () => triggerAgentAction('move', { destination: 'room_corridor' }));

    document.getElementById('tree-search').addEventListener('input', (e) => filterTree(e.target.value));
});

// Admin Sub-tabs switcher
function initTabs() {
    const tabs = document.querySelectorAll('.tab-item');
    tabs.forEach(tab => {
        tab.addEventListener('click', () => {
            tabs.forEach(t => t.classList.remove('active'));
            document.querySelectorAll('.view-panel').forEach(p => p.classList.remove('active'));

            tab.classList.add('active');
            const panelId = tab.dataset.panel;
            const targetPanel = document.getElementById(panelId);
            if (targetPanel) targetPanel.classList.add('active');

            if (panelId === 'panel-map' && map) {
                setTimeout(() => map.invalidateSize(), 150);
            } else if (panelId === 'panel-floorplan') {
                renderFloorPlan();
            }
        });
    });
}

// Leaflet Map Initialization centered on real Runářov village (Lat: 49.5728, Lon: 16.8774)
function initMap() {
    map = L.map('map', { zoomControl: true }).setView([49.5728, 16.8774], 16);

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '© OpenStreetMap contributors'
    }).addTo(map);
}

// Load Data from API
async function loadData() {
    try {
        const resEntities = await fetch('/api/entities/settlement_runarov/subtree');
        if (resEntities.ok) {
            entitiesStore = await resEntities.json();
        }

        const resEdges = await fetch('/api/edges');
        if (resEdges.ok) {
            edgesStore = await resEdges.json();
        }

        const resEvents = await fetch('/api/events');
        if (resEvents.ok) {
            eventsStore = await resEvents.json();
        }

        renderTree();
        renderMapMarkers();
        renderTimeline();
        selectEntity(selectedEntityId);
    } catch (err) {
        console.error("Error loading data:", err);
    }
}

// Render Left Sidebar Containment Tree
function renderTree(filterQuery = '') {
    const container = document.getElementById('tree-container');
    if (!container) return;
    container.innerHTML = '';

    const root = entitiesStore.find(e => e.id === 'settlement_runarov');
    if (!root) return;

    container.appendChild(buildTreeNode(root, filterQuery.toLowerCase()));
}

function buildTreeNode(entity, filterQuery = '') {
    const children = entitiesStore.filter(e => e.parentId === entity.id);

    if (filterQuery && !entity.name.toLowerCase().includes(filterQuery) && !children.some(c => c.name.toLowerCase().includes(filterQuery))) {
        return document.createTextNode('');
    }

    const nodeEl = document.createElement('div');
    nodeEl.className = 'tree-node';

    const itemEl = document.createElement('div');
    itemEl.className = `tree-item ${selectedEntityId === entity.id ? 'selected' : ''}`;
    itemEl.innerHTML = `<span class="type-badge">${entity.type}</span> <span>${entity.name}</span>`;
    itemEl.onclick = (e) => {
        e.stopPropagation();
        selectEntity(entity.id);
    };

    nodeEl.appendChild(itemEl);

    if (children.length > 0) {
        const childrenContainer = document.createElement('div');
        childrenContainer.className = 'tree-children';
        children.forEach(child => {
            const childNode = buildTreeNode(child, filterQuery);
            if (childNode) childrenContainer.appendChild(childNode);
        });
        nodeEl.appendChild(childrenContainer);
    }

    return nodeEl;
}

function filterTree(query) {
    renderTree(query);
}

// Select Entity & Fill Main View
function selectEntity(id) {
    selectedEntityId = id;
    const entity = entitiesStore.find(e => e.id === id);
    if (!entity) return;

    // 1. Update Title Bar
    document.getElementById('ent-name').textContent = entity.name;
    document.getElementById('ent-type').textContent = entity.type;
    document.getElementById('ent-id').textContent = entity.id;

    // 2. Update Breadcrumbs
    renderBreadcrumbs(entity);

    // 3. Update Detail & Attributes Panel
    document.getElementById('ent-desc').textContent = entity.semantic?.description || 'Bez textového popisu.';

    const tagsBox = document.getElementById('ent-tags');
    tagsBox.innerHTML = (entity.semantic?.tags || []).map(t => `<span class="tag-chip">${t}</span>`).join(' ') || '<span style="color:var(--text-muted); font-size:0.8rem;">Bez tagů</span>';

    const spatialKv = document.getElementById('ent-spatial-kv');
    spatialKv.innerHTML = `
        <span class="kv-label">Rámec:</span><span class="kv-value">${entity.spatial?.frame || 'World'}</span>
        <span class="kv-label">GPS Lat:</span><span class="kv-value">${entity.spatial?.globalAnchor?.lat || 'N/A'}</span>
        <span class="kv-label">GPS Lon:</span><span class="kv-value">${entity.spatial?.globalAnchor?.lon || 'N/A'}</span>
        <span class="kv-label">Hloubka stromu:</span><span class="kv-value">${entity.depth}</span>
    `;

    const provKv = document.getElementById('ent-prov-kv');
    provKv.innerHTML = `
        <span class="kv-label">Zdroj:</span><span class="kv-value">${entity.provenance?.source || 'Katastr (RÚIAN)'}</span>
        <span class="kv-label">Confidence:</span><span class="kv-value">${entity.provenance?.confidence || 1.0}</span>
        <span class="kv-label">Stav generace:</span><span class="kv-value">${entity.generation?.state || 'Verified'}</span>
        <span class="kv-label">Metoda:</span><span class="kv-value">${entity.generation?.method || 'cadastre'}</span>
    `;

    // 4. Update Children Table
    renderChildrenTable(entity.id);

    // 5. Update Agent Panel if Agent
    renderAgentInfo();

    // 6. Auto-center map if entity has GPS anchor
    if (entity.spatial?.globalAnchor && map) {
        map.panTo([entity.spatial.globalAnchor.lat, entity.spatial.globalAnchor.lon]);
    }

    // Highlight selected item in tree
    document.querySelectorAll('.tree-item').forEach(el => el.classList.remove('selected'));
}

// Render Breadcrumb Bar
function renderBreadcrumbs(entity) {
    const container = document.getElementById('breadcrumb-container');
    if (!container) return;
    container.innerHTML = '';

    const chain = [];
    let current = entity;
    while (current) {
        chain.unshift(current);
        current = entitiesStore.find(e => e.id === current.parentId);
    }

    chain.forEach((item, idx) => {
        const span = document.createElement('span');
        span.className = 'breadcrumb-item';
        span.textContent = item.name;
        span.onclick = () => selectEntity(item.id);
        container.appendChild(span);

        if (idx < chain.length - 1) {
            const sep = document.createElement('span');
            sep.textContent = ' / ';
            sep.style.color = 'var(--text-muted)';
            container.appendChild(sep);
        }
    });
}

// Render Children Table
function renderChildrenTable(parentId) {
    const tbody = document.getElementById('children-table-body');
    const countSpan = document.getElementById('children-count');
    if (!tbody) return;

    const children = entitiesStore.filter(e => e.parentId === parentId);
    countSpan.textContent = children.length;
    tbody.innerHTML = '';

    if (children.length === 0) {
        tbody.innerHTML = '<tr><td colspan="5" style="text-align:center; color:var(--text-muted);">Žádné dětské uzly ve větvi.</td></tr>';
        return;
    }

    children.forEach(child => {
        const tr = document.createElement('tr');
        tr.innerHTML = `
            <td><strong>${child.name}</strong></td>
            <td><span class="type-badge">${child.type}</span></td>
            <td style="font-family:var(--font-mono); font-size:0.75rem;">${child.id}</td>
            <td>${child.generation?.state || 'Verified'}</td>
            <td><button class="btn btn-secondary btn-sm" onclick="selectEntity('${child.id}')">🔍 Detail</button></td>
        `;
        tbody.appendChild(tr);
    });
}

// Render Map Markers, Roads, Footpaths, and Runářovský Potok Waterways
function renderMapMarkers() {
    if (!map) return;

    map.eachLayer(layer => {
        if (layer instanceof L.Marker || layer instanceof L.CircleMarker || layer instanceof L.Polyline) {
            map.removeLayer(layer);
        }
    });

    // 1. Render Roads & Waterway Edges
    edgesStore.forEach(edge => {
        const fromE = entitiesStore.find(e => e.id === edge.fromId);
        const toE = entitiesStore.find(e => e.id === edge.toId);

        if (fromE?.spatial?.globalAnchor && toE?.spatial?.globalAnchor) {
            const p1 = [fromE.spatial.globalAnchor.lat, fromE.spatial.globalAnchor.lon];
            const p2 = [toE.spatial.globalAnchor.lat, toE.spatial.globalAnchor.lon];

            if (edge.kind === 'Waterway') {
                const line = L.polyline([p1, p2], {
                    color: '#0284c7', // Bright blue for Runářovský potok & streams
                    weight: 4,
                    opacity: 0.95,
                    lineCap: 'round'
                }).addTo(map);
                line.bindTooltip('<b>💧 Runářovský potok / Vodní tok</b>');
            } else if (edge.kind === 'Road') {
                L.polyline([p1, p2], {
                    color: '#f59e0b', // Amber/gold for roads and streets
                    weight: 3,
                    opacity: 0.8
                }).addTo(map);
            } else {
                L.polyline([p1, p2], {
                    color: '#64748b',
                    weight: 1.5,
                    opacity: 0.6
                }).addTo(map);
            }
        }
    });

    // 2. Render Buildings, POIs, Water Areas & Agents
    entitiesStore.forEach(entity => {
        // Skip waypoint nodes from circle clutter
        if (entity.semantic?.tags?.includes('road_node') || entity.semantic?.tags?.includes('water_node')) {
            return;
        }

        if (entity.spatial?.globalAnchor) {
            const anchor = entity.spatial.globalAnchor;
            let color = '#38bdf8';
            let radius = 6;

            if (entity.type === 'Building') color = '#38bdf8';
            else if (entity.type === 'Area' || entity.semantic?.tags?.includes('waterway')) { color = '#0284c7'; radius = 8; }
            else if (entity.type === 'Place') { color = '#a855f7'; radius = 7; }
            else if (entity.type === 'Agent') { color = '#f43f5e'; radius = 10; }

            const marker = L.circleMarker([anchor.lat, anchor.lon], {
                radius: radius,
                fillColor: color,
                color: '#ffffff',
                weight: 1.5,
                opacity: 1,
                fillOpacity: 0.85
            }).addTo(map);

            marker.bindTooltip(`<b>${entity.name}</b><br/>Typ: ${entity.type}`);
            marker.on('click', () => selectEntity(entity.id));
        }
    });
}

// Render SVG FloorPlan
function renderFloorPlan() {
    const svg = document.getElementById('floorplan-svg');
    if (!svg) return;
    svg.innerHTML = '';

    const houseRect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
    houseRect.setAttribute('x', '40'); houseRect.setAttribute('y', '40');
    houseRect.setAttribute('width', '720'); houseRect.setAttribute('height', '520');
    houseRect.setAttribute('rx', '12'); houseRect.setAttribute('fill', '#1e293b');
    houseRect.setAttribute('stroke', '#374151'); houseRect.setAttribute('stroke-width', '4');
    svg.appendChild(houseRect);

    const defaultRooms = [
        { name: 'Vstupní chodba s věšákem', x: 70, y: 70, w: 220, h: 220 },
        { name: 'Kuchyň s oknem do dvora', x: 310, y: 70, w: 430, h: 220 },
        { name: 'Obývací pokoj s kamny', x: 70, y: 310, w: 350, h: 230 },
        { name: 'Ložnice', x: 440, y: 310, w: 300, h: 230 }
    ];

    defaultRooms.forEach(r => {
        const group = document.createElementNS('http://www.w3.org/2000/svg', 'g');
        const rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
        rect.setAttribute('x', r.x); rect.setAttribute('y', r.y);
        rect.setAttribute('width', r.w); rect.setAttribute('height', r.h);
        rect.setAttribute('rx', '8'); rect.setAttribute('class', 'room-rect');

        const text = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        text.setAttribute('x', r.x + r.w / 2); text.setAttribute('y', r.y + r.h / 2);
        text.setAttribute('class', 'room-label');
        text.textContent = r.name;

        group.appendChild(rect);
        group.appendChild(text);
        svg.appendChild(group);
    });

    const agent = entitiesStore.find(e => e.type === 'Agent');
    if (agent) {
        const circle = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
        circle.setAttribute('cx', '525'); circle.setAttribute('cy', '180'); circle.setAttribute('r', '14');
        circle.setAttribute('fill', '#f43f5e'); circle.setAttribute('stroke', '#fff'); circle.setAttribute('stroke-width', '2.5');

        const text = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        text.setAttribute('x', '525'); text.setAttribute('y', '215');
        text.setAttribute('class', 'room-label'); text.setAttribute('font-size', '13px');
        text.textContent = agent.name;

        svg.appendChild(circle);
        svg.appendChild(text);
    }
}

// Render Agent Info
function renderAgentInfo() {
    const agent = entitiesStore.find(e => e.type === 'Agent');
    const container = document.getElementById('agent-info-box');
    if (!agent || !container) return;

    container.innerHTML = `
        <span class="kv-label">Jméno:</span><span class="kv-value">${agent.name}</span>
        <span class="kv-label">Persona:</span><span class="kv-value">${agent.agent?.personaRef || 'PMJ Jana Novotná'}</span>
        <span class="kv-label">Aktuální cíl:</span><span class="kv-value">${agent.agent?.currentGoal || 'Uvařit oběd'}</span>
        <span class="kv-label">Poloha:</span><span class="kv-value">${agent.parentId || 'room_kitchen'}</span>
    `;
}

// Stanford Agent Memory Search
async function searchAgentMemories() {
    const query = document.getElementById('memory-query-input').value;
    const container = document.getElementById('memory-results-container');
    if (!container) return;

    const filtered = eventsStore.filter(e => !query || e.text.toLowerCase().includes(query.toLowerCase()));
    container.innerHTML = '';

    if (filtered.length === 0) {
        container.innerHTML = '<p style="color:var(--text-muted); font-size:0.85rem;">Žádné paměťové záznamy neodpovídají dotazu.</p>';
        return;
    }

    filtered.forEach(evt => {
        const item = document.createElement('div');
        item.className = 'memory-item';
        item.innerHTML = `
            <span class="memory-score">Skóre: ${(evt.importance || 8.0).toFixed(1)}</span>
            <div style="font-size:0.85rem;">${evt.text}</div>
            <div style="font-size:0.7rem; color:var(--text-muted); margin-top:0.25rem;">Čas: ${new Date(evt.ts).toLocaleTimeString()}</div>
        `;
        container.appendChild(item);
    });
}

// Render Timeline Stream
function renderTimeline() {
    const container = document.getElementById('timeline-stream-container');
    if (!container) return;
    container.innerHTML = '';

    eventsStore.forEach(evt => {
        const el = document.createElement('div');
        el.className = 'timeline-event';
        el.innerHTML = `
            <div style="font-size:0.75rem; color:var(--text-muted); display:flex; justify-space-between;">
                <span>[${evt.kind}]</span><span>${new Date(evt.ts).toLocaleTimeString()}</span>
            </div>
            <div style="margin-top:0.25rem; font-size:0.85rem;">${evt.text}</div>
        `;
        container.appendChild(el);
    });
}

// Execute Agent Step
async function executeAgentStep() {
    try {
        const btn = document.getElementById('btn-step-agent');
        btn.disabled = true;
        btn.innerHTML = '<span>⏳ Zpracovávám...</span>';

        const res = await fetch('/api/agents/agent_jana_novotna/step', { method: 'POST' });
        if (res.ok) {
            const data = await res.json();
            alert(`Reakce agentky Jany Novotné:\n\n"${data.response}"`);
            await loadData();
        }
        btn.disabled = false;
        btn.innerHTML = '<span>⚡ Krok agenty Jany</span>';
    } catch (err) {
        console.error(err);
        document.getElementById('btn-step-agent').disabled = false;
    }
}

function triggerAgentAction(action, payload) {
    alert(`Akce ${action} byla odeslána.`);
}

// SignalR Real-time
function initSignalR() {
    try {
        const connection = new signalR.HubConnectionBuilder().withUrl("/hubs/simulation").withAutomaticReconnect().build();
        connection.on("EventRecorded", (evt) => {
            eventsStore.unshift(evt);
            renderTimeline();
        });
        connection.start().catch(err => console.error(err));
    } catch (e) {}
}
