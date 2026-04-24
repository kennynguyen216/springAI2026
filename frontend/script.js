let threadId = null;
let viewDate = new Date();
let storedEvents = [];

async function send() {
    const input = document.getElementById('query');
    const val = input.value.trim();
    if(!val) return;

    renderBubble('user', val);
    input.value = '';
    
    showThinking();

    try {
        const response = await fetch('/chat', {
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
    div.innerHTML = (role === 'alfred') ? marked.parse(text) : text;
    chat.appendChild(div);
    chat.scrollTop = chat.scrollHeight;
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

async function loadEvents() {
    const res = await fetch('/events');
    storedEvents = await res.json();
    renderCalendarGrid();
}

function renderCalendarGrid() {
    const grid = document.getElementById('calendar-grid');
    const label = document.getElementById('month-label');
    const year = viewDate.getFullYear(), month = viewDate.getMonth();
    label.innerText = new Intl.DateTimeFormat('en-US', { month: 'long', year: 'numeric' }).format(viewDate);

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
            tag.style = "background: var(--lsu-purple); color: white; font-size: 0.7rem; padding: 4px; margin-top: 4px; border-radius: 4px; border-left: 3px solid var(--lsu-gold);";
            tag.innerText = e.title;
            cell.appendChild(tag);
        });
        grid.appendChild(cell);
    }
}
