let threadId = null;
let viewDate = new Date();
let storedEvents = [];
const backendBaseUrlPromise = resolveBackendBaseUrl();

async function resolveBackendBaseUrl() {
    const explicit = window.localStorage.getItem('carts-backend-url');
    if (explicit) return explicit.replace(/\/$/, '');

    const originBase = `${window.location.protocol}//${window.location.hostname}`;
    const sameOriginPort = window.location.port;
    const candidatePorts = ['5188', '5189', '5190', '5191', '5192'];

    if (candidatePorts.includes(sameOriginPort)) {
        return '';
    }

    for (const port of candidatePorts) {
        const baseUrl = `${originBase}:${port}`;
        try {
            const response = await fetch(`${baseUrl}/health`);
            if (response.ok) {
                return baseUrl;
            }
        } catch {
        }
    }

    return `${originBase}:5188`;
}

async function send() {
    const input = document.getElementById('query');
    const val = input.value.trim();
    if(!val) return;

    const userBubble = renderBubble('user', val);
    input.value = '';

    showThinking();

    try {
        const backendBaseUrl = await backendBaseUrlPromise;
        const response = await fetch(`${backendBaseUrl}/chat`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ Message: val, ThreadId: threadId })
        });
        const data = await response.json();

        if (!response.ok) {
            const message = data?.detail || data?.title || "The backend returned an error while processing that request.";
            throw new Error(message);
        }

        threadId = data.threadId;

        removeThinking();
        renderBubble('alfred', data.response);
        userBubble.scrollIntoView({ behavior: 'smooth', block: 'start' });
    } catch (e) {
        removeThinking();
        const message = e?.message || "Unable to reach the C# backend.";
        renderBubble('alfred', `**System Error**: ${message}`);
    }
}

function showThinking() {
    const chat = document.getElementById('chat-container');
    const div = document.createElement('div');
    div.id = 'typing-indicator';
    div.className = 'bubble alfred thinking-bubble';
    div.innerHTML = '<div class="dot"></div><div class="dot"></div><div class="dot"></div>';
    chat.appendChild(div);
    chat.scrollTop = chat.scrollHeight;
}

function removeThinking() {
    const indicator = document.getElementById('typing-indicator');
    if (indicator) indicator.remove();
}

function renderBubble(role, text) {
    const chat = document.getElementById('chat-container');
    const div = document.createElement('div');
    div.className = `bubble ${role}`;
    const clean = text.replace(/^```[\w]*\n?/gm, '').replace(/```$/gm, '');
    div.innerHTML = (role === 'alfred') ? marked.parse(clean) : text;
    chat.appendChild(div);
    chat.scrollTop = chat.scrollHeight;
    return div;
}

function nav(view) {
    const chat = document.getElementById('chat-view'), cal = document.getElementById('calendar-view');
    const nc = document.getElementById('nav-chat'), ncal = document.getElementById('nav-cal');
    if (view === 'calendar') {
        chat.style.display = 'none'; cal.style.display = 'flex';
        nc.classList.add('active'); ncal.classList.add('active');
        loadEvents(); 
    } else {
        chat.style.display = 'flex'; cal.style.display = 'none';
        ncal.classList.remove('active'); nc.classList.add('active');
    }
}

let activeEventId = null;

function openEventModal(e) {
    activeEventId = e.id;
    document.getElementById('modal-title').innerText = e.title;
    document.getElementById('modal-date').innerText = new Date(e.eventDate).toLocaleDateString('en-US', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' });
    const desc = e.description || '';
    document.getElementById('modal-desc').innerText = desc;
    document.getElementById('modal-desc-label').style.display = desc ? 'block' : 'none';
    document.getElementById('modal-desc').style.display = desc ? 'block' : 'none';
    document.getElementById('event-modal').classList.add('active');
}

function closeEventModal() {
    document.getElementById('event-modal').classList.remove('active');
    activeEventId = null;
}

async function deleteEventFromModal() {
    if (!activeEventId) return;
    await fetch(`/events/${activeEventId}`, { method: 'DELETE' });
    closeEventModal();
    await loadEvents();
}

function openAddModal() {
    document.getElementById('add-title').value = '';
    document.getElementById('add-date').value = '';
    document.getElementById('add-desc').value = '';
    document.getElementById('add-modal').classList.add('active');
}

function closeAddModal() {
    document.getElementById('add-modal').classList.remove('active');
}

async function submitAddEvent() {
    const title = document.getElementById('add-title').value.trim();
    const date = document.getElementById('add-date').value;
    if (!title || !date) return alert('Title and date are required.');
    await fetch('/events', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ title, date, description: document.getElementById('add-desc').value })
    });
    closeAddModal();
    await loadEvents();
}

function openEditForm() {
    const title = document.getElementById('modal-title').innerText;
    const desc = document.getElementById('modal-desc').innerText;
    const rawEvent = storedEvents.find(e => e.id === activeEventId);
    document.getElementById('edit-title').value = title;
    document.getElementById('edit-date').value = rawEvent ? rawEvent.eventDate.split('T')[0] : '';
    document.getElementById('edit-desc').value = desc;
    document.getElementById('modal-view').style.display = 'none';
    document.getElementById('modal-edit').style.display = 'block';
}

function closeEditForm() {
    document.getElementById('modal-view').style.display = 'block';
    document.getElementById('modal-edit').style.display = 'none';
}

async function saveEventEdit() {
    if (!activeEventId) return;
    const body = {
        title: document.getElementById('edit-title').value,
        date: document.getElementById('edit-date').value,
        description: document.getElementById('edit-desc').value
    };
    await fetch(`/events/${activeEventId}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body)
    });
    closeEventModal();
    await loadEvents();
}

function changeMonth(delta) {
    viewDate.setMonth(viewDate.getMonth() + delta);
    renderCalendarGrid();
}

function changeYear(year) {
    viewDate.setFullYear(parseInt(year));
    renderCalendarGrid();
}

function populateYearDropdown() {
    const select = document.getElementById('year-select');
    const current = new Date().getFullYear();
    select.innerHTML = '';
    for (let y = current - 2; y <= current + 5; y++) {
        const opt = document.createElement('option');
        opt.value = y;
        opt.innerText = y;
        if (y === viewDate.getFullYear()) opt.selected = true;
        select.appendChild(opt);
    }
}

async function loadEvents() {
    const backendBaseUrl = await backendBaseUrlPromise;
    const res = await fetch(`${backendBaseUrl}/events`);
    storedEvents = await res.json();
    populateYearDropdown();
    renderCalendarGrid();
}

function renderCalendarGrid() {
    const grid = document.getElementById('calendar-grid');
    const label = document.getElementById('month-label');
    const year = viewDate.getFullYear(), month = viewDate.getMonth();
    label.innerText = new Intl.DateTimeFormat('en-US', { month: 'long' }).format(viewDate);
    const sel = document.getElementById('year-select');
    if (sel) sel.value = viewDate.getFullYear();

    const labels = Array.from(grid.querySelectorAll('.cal-day-label'));
    grid.innerHTML = '';
    labels.forEach(l => grid.appendChild(l));

    const firstDay = new Date(year, month, 1).getDay();
    const daysInMonth = new Date(year, month + 1, 0).getDate();

    for (let i = 0; i < firstDay; i++) {
        const empty = document.createElement('div');
        empty.className = 'cal-cell'; empty.style.background = '#f9f9f9';
        grid.appendChild(empty);
    }

    for (let day = 1; day <= daysInMonth; day++) {
        const cell = document.createElement('div');
        cell.className = 'cal-cell';
        cell.innerHTML = `<div>${day}</div>`;
        const dayEvents = storedEvents.filter(e => {
            const d = new Date(e.eventDate);
            return d.getDate() === day && d.getMonth() === month && d.getFullYear() === year;
        });
        dayEvents.forEach(e => {
            const tag = document.createElement('div');
            tag.style = "background: var(--lsu-purple); color: white; font-size: 0.7rem; padding: 4px; margin-top: 4px; border-radius: 4px; border-left: 3px solid var(--lsu-gold); cursor: pointer;";
            tag.innerText = e.title;
            tag.onclick = () => openEventModal(e);
            cell.appendChild(tag);
        });
        grid.appendChild(cell);
    }
}
