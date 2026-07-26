let map = null;
let entitiesStore = [];
let edgesStore = [];
let eventsStore = [];
let selectedEntityId = null;

document.addEventListener('DOMContentLoaded', async () => {
    initTabs();
    initMap();
    initSignalR();

    await loadData();

    document.getElementById('btn-reload').addEventListener('click', loadData);
    document.getElementById('btn-step-agent').addEventListener('click', executeAgentStep);
    document.getElementById('building-select').addEventListener('change', renderFloorPlan);
    document.getElementById('floor-select').addEventListener('change', renderFloorPlan);
});

// Tab Switcher
function initTabs() {
    const tabs = document.querySelectorAll('.tab-btn');
    tabs.forEach(tab => {
        tab.addEventListener('click', () => {
            tabs.forEach(t => t.classList.remove('active'));
            document.querySelectorAll('.tab-content').forEach(c => c.classList.remove('active'));

            tab.classList.add('active');
            const targetId = tab.dataset.tab;
            const targetContent = document.getElementById(targetId);
            if (targetContent) {
                targetContent.classList.add('active');
            }

            if (targetId === 'tab-map' && map) {
                setTimeout(() => {
                    map.invalidateSize();
                }, 100);
            } else if (targetId === 'tab-floorplan') {
                renderFloorPlan();
            } else if (targetId === 'tab-tree') {
                renderTree();
            } else if (targetId === 'tab-timeline') {
                renderTimeline();
            }
        });
    });
}

// Leaflet Map Initialization
function initMap() {
    // Runářov center coords: Lat 49.5427, Lon 16.8963
    map = L.map('map', { zoomControl: true }).setView([49.5427, 16.8963], 16);

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '© OpenStreetMap contributors'
    }).addTo(map);

    setTimeout(() => {
        if (map) map.invalidateSize();
    }, 300);
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

        renderMapMarkers();
        renderTree();
        renderTimeline();
        if (selectedEntityId) selectEntity(selectedEntityId);
    } catch (err) {
        console.error("Error loading data:", err);
    }
}

// Render Map Markers & Buildings
function renderMapMarkers() {
    if (!map) return;

    map.eachLayer(layer => {
        if (layer instanceof L.Marker || layer instanceof L.CircleMarker || layer instanceof L.Polyline) {
            map.removeLayer(layer);
        }
    });

    // Render 110 Buildings and Places
    entitiesStore.forEach(entity => {
        if (entity.spatial && entity.spatial.globalAnchor) {
            const anchor = entity.spatial.globalAnchor;
            const lat = anchor.lat;
            const lon = anchor.lon;

            let color = '#38bdf8'; // Default cyan
            let radius = 7;

            if (entity.type === 'Building') {
                color = entity.generation?.state === 3 ? '#38bdf8' : (entity.generation?.state === 2 ? '#4ade80' : '#fbbf24');
            } else if (entity.type === 'Place') {
                color = '#c084fc';
                radius = 9;
            } else if (entity.type === 'Agent') {
                color = '#f43f5e';
                radius = 11;
            }

            const marker = L.circleMarker([lat, lon], {
                radius: radius,
                fillColor: color,
                color: '#ffffff',
                weight: 2,
                opacity: 1,
                fillOpacity: 0.9
            }).addTo(map);

            marker.bindTooltip(`<b>${entity.name}</b><br/>Typ: ${entity.type}`);
            marker.on('click', () => selectEntity(entity.id));
        }
    });

    // Render Road Network Edges
    edgesStore.forEach(edge => {
        const fromEntity = entitiesStore.find(e => e.id === edge.fromId);
        const toEntity = entitiesStore.find(e => e.id === edge.toId);

        if (fromEntity?.spatial?.globalAnchor && toEntity?.spatial?.globalAnchor) {
            const p1 = [fromEntity.spatial.globalAnchor.lat, fromEntity.spatial.globalAnchor.lon];
            const p2 = [toEntity.spatial.globalAnchor.lat, toEntity.spatial.globalAnchor.lon];

            L.polyline([p1, p2], {
                color: edge.kind === 'Road' ? '#38bdf8' : '#94a3b8',
                weight: edge.kind === 'Road' ? 3 : 2,
                dashArray: edge.kind === 'Path' ? '4, 4' : null,
                opacity: 0.7
            }).addTo(map);
        }
    });
}

// Render SVG FloorPlan for selected building
function renderFloorPlan() {
    const svg = document.getElementById('floorplan-svg');
    if (!svg) return;
    svg.innerHTML = '';

    // Draw house container border
    const houseRect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
    houseRect.setAttribute('x', '40');
    houseRect.setAttribute('y', '40');
    houseRect.setAttribute('width', '720');
    houseRect.setAttribute('height', '520');
    houseRect.setAttribute('rx', '12');
    houseRect.setAttribute('fill', '#1e293b');
    houseRect.setAttribute('stroke', '#374151');
    houseRect.setAttribute('stroke-width', '4');
    svg.appendChild(houseRect);

    // Get rooms generated for Čp. 23 přízemí
    const floorRooms = entitiesStore.filter(e => e.parentId === 'floor_building_cp_23_1' && e.type === 'Room');
    const defaultRooms = [
        { name: 'Vstupní chodba s věšákem', x: 70, y: 70, w: 220, h: 220 },
        { name: 'Kuchyň s oknem do dvora', x: 310, y: 70, w: 430, h: 220 },
        { name: 'Obývací pokoj', x: 70, y: 310, w: 350, h: 230 },
        { name: 'Ložnice', x: 440, y: 310, w: 300, h: 230 }
    ];

    const roomsToRender = floorRooms.length > 0 ? floorRooms.map((r, i) => ({
        ...defaultRooms[i % defaultRooms.length],
        id: r.id,
        name: r.name,
        entity: r
    })) : defaultRooms;

    roomsToRender.forEach(r => {
        const group = document.createElementNS('http://www.w3.org/2000/svg', 'g');

        const rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
        rect.setAttribute('x', r.x);
        rect.setAttribute('y', r.y);
        rect.setAttribute('width', r.w);
        rect.setAttribute('height', r.h);
        rect.setAttribute('rx', '8');
        rect.setAttribute('class', 'room-rect');
        rect.onclick = () => { if (r.id) selectEntity(r.id); };

        const text = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        text.setAttribute('x', r.x + r.w / 2);
        text.setAttribute('y', r.y + r.h / 2);
        text.setAttribute('class', 'room-label');
        text.textContent = r.name;

        group.appendChild(rect);
        group.appendChild(text);
        svg.appendChild(group);
    });

    // Render Agent Jana inside Kitchen
    const agent = entitiesStore.find(e => e.type === 'Agent');
    if (agent) {
        const agentDot = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
        agentDot.setAttribute('cx', '525');
        agentDot.setAttribute('cy', '180');
        agentDot.setAttribute('r', '14');
        agentDot.setAttribute('class', 'agent-dot');
        agentDot.onclick = () => selectEntity(agent.id);

        const agentText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        agentText.setAttribute('x', '525');
        agentText.setAttribute('y', '215');
        agentText.setAttribute('class', 'room-label');
        agentText.setAttribute('font-size', '13px');
        agentText.textContent = agent.name;

        svg.appendChild(agentDot);
        svg.appendChild(agentText);
    }
}

// Render Containment Tree Explorer
function renderTree() {
    const container = document.getElementById('tree-container');
    if (!container) return;
    container.innerHTML = '';

    const root = entitiesStore.find(e => e.id === 'settlement_runarov');
    if (!root) return;

    container.appendChild(buildTreeNode(root));
}

function buildTreeNode(entity) {
    const nodeEl = document.createElement('div');
    nodeEl.className = 'tree-node';

    const contentEl = document.createElement('div');
    contentEl.className = `tree-node-content ${selectedEntityId === entity.id ? 'selected' : ''}`;
    contentEl.innerHTML = `<span class="node-type">${entity.type}</span> <strong>${entity.name}</strong>`;
    contentEl.onclick = (e) => {
        e.stopPropagation();
        selectEntity(entity.id);
    };

    nodeEl.appendChild(contentEl);

    const children = entitiesStore.filter(e => e.parentId === entity.id);
    if (children.length > 0) {
        const childrenContainer = document.createElement('div');
        childrenContainer.className = 'tree-children';
        children.forEach(child => {
            childrenContainer.appendChild(buildTreeNode(child));
        });
        nodeEl.appendChild(childrenContainer);
    }

    return nodeEl;
}

// Render GTU Timeline
function renderTimeline() {
    const container = document.getElementById('events-container');
    if (!container) return;
    container.innerHTML = '';

    if (eventsStore.length === 0) {
        container.innerHTML = '<p style="color:var(--text-secondary); text-align:center;">Zatím nebyly zaznamenány žádné události.</p>';
        return;
    }

    eventsStore.forEach(evt => {
        const card = document.createElement('div');
        card.className = 'event-card';
        const ts = new Date(evt.ts).toLocaleTimeString();
        card.innerHTML = `
            <div class="event-meta">
                <span>[${evt.kind}]</span>
                <span>${ts}</span>
            </div>
            <div class="event-text">${evt.text}</div>
        `;
        container.appendChild(card);
    });
}

// Select Entity & Fill Inspector Panel
function selectEntity(id) {
    selectedEntityId = id;
    const entity = entitiesStore.find(e => e.id === id);
    if (!entity) return;

    document.getElementById('insp-type').textContent = entity.type;

    const body = document.getElementById('insp-body');
    const tags = (entity.semantic?.tags || []).map(t => `<span class="tag-chip">${t}</span>`).join(' ');

    let extraHtml = '';
    if (entity.agent) {
        extraHtml = `
            <div class="info-section">
                <h4>🤖 Stav agenty</h4>
                <p><strong>Persona:</strong> ${entity.agent.personaRef}</p>
                <p><strong>Aktuální cíl:</strong> ${entity.agent.currentGoal || 'Nemá cíl'}</p>
            </div>
        `;
    }

    body.innerHTML = `
        <div class="info-section">
            <h3 style="font-family:var(--font-heading); font-size:1.2rem; margin-bottom:0.5rem;">${entity.name}</h3>
            <p style="color:var(--text-secondary); font-size:0.85rem;">${entity.semantic?.description || 'Bez popisu.'}</p>
        </div>

        <div class="info-section">
            <h4>📌 Atributy & Stav</h4>
            <div class="info-grid">
                <span class="info-label">ID:</span><span class="info-value">${entity.id}</span>
                <span class="info-label">Stav generace:</span><span class="info-value">${entity.generation?.state || 'N/A'}</span>
                <span class="info-label">Zdroj:</span><span class="info-value">${entity.provenance?.source || 'N/A'} (Jistota: ${entity.provenance?.confidence || 1.0})</span>
            </div>
        </div>

        <div class="info-section">
            <h4>🏷️ Tagy</h4>
            <div class="tag-cloud">${tags || 'Žádné tagy'}</div>
        </div>

        ${extraHtml}
    `;

    document.querySelectorAll('.tree-node-content').forEach(el => el.classList.remove('selected'));
}

// Execute Agent Step Action
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

// SignalR Real-time Updates
function initSignalR() {
    try {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/simulation")
            .withAutomaticReconnect()
            .build();

        connection.on("EventRecorded", (evt) => {
            eventsStore.unshift(evt);
            renderTimeline();
        });

        connection.start().catch(err => console.error("SignalR connection error:", err));
    } catch (e) {
        console.log("SignalR client not initialized.");
    }
}
