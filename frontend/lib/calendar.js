export function createCalendarState() {
    const today = new Date();

    return {
        viewDate: new Date(today.getFullYear(), today.getMonth(), 1).toISOString(),
        events: [],
        selectedEventId: null,
        isModalOpen: false,
        draftEvent: null,
        error: ""
    };
}

export function normalizeCalendarEvent(event) {
    return {
        id: event.id,
        title: event.title,
        eventDate: event.eventDate,
        description: event.description ?? ""
    };
}

export function getMonthMatrix(viewDateIso) {
    const viewDate = new Date(viewDateIso);
    const year = viewDate.getFullYear();
    const month = viewDate.getMonth();
    const firstWeekday = new Date(year, month, 1).getDay();
    const daysInMonth = new Date(year, month + 1, 0).getDate();
    const cells = [];

    for (let index = 0; index < firstWeekday; index += 1) {
        cells.push(null);
    }

    for (let day = 1; day <= daysInMonth; day += 1) {
        cells.push(new Date(year, month, day));
    }

    while (cells.length % 7 !== 0) {
        cells.push(null);
    }

    return cells;
}

export function formatMonthLabel(viewDateIso) {
    return new Intl.DateTimeFormat(undefined, {
        month: "long",
        year: "numeric"
    }).format(new Date(viewDateIso));
}

export function getEventsForDate(events, date) {
    return events.filter((event) => isSameDay(new Date(event.eventDate), date));
}

export function shiftCalendarMonth(viewDateIso, delta) {
    const next = new Date(viewDateIso);
    next.setMonth(next.getMonth() + delta);
    next.setDate(1);
    return next.toISOString();
}

export function createDraftEvent(selectedDate = null, sourceEvent = null) {
    if (sourceEvent) {
        return {
            id: sourceEvent.id,
            title: sourceEvent.title,
            date: sourceEvent.eventDate.split("T")[0],
            description: sourceEvent.description ?? ""
        };
    }

    return {
        id: null,
        title: "",
        date: selectedDate ? toDateInputValue(selectedDate) : "",
        description: ""
    };
}

function isSameDay(left, right) {
    return left.getFullYear() === right.getFullYear()
        && left.getMonth() === right.getMonth()
        && left.getDate() === right.getDate();
}

function toDateInputValue(date) {
    const year = date.getFullYear();
    const month = `${date.getMonth() + 1}`.padStart(2, "0");
    const day = `${date.getDate()}`.padStart(2, "0");

    return `${year}-${month}-${day}`;
}
