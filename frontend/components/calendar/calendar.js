import { UI_TEXT } from "../../constants/ui-text.js";
import {
    formatMonthLabel,
    getEventsForDate,
    getMonthMatrix
} from "../../lib/calendar.js";

export function createCalendar({
    state,
    onPreviousMonth,
    onNextMonth,
    onGoToToday,
    onOpenNewEvent,
    onOpenEvent,
    onCloseModal,
    onDraftChange,
    onSaveEvent,
    onDeleteEvent
}) {
    const section = document.createElement("section");
    section.className = "calendar-view";

    const header = document.createElement("div");
    header.className = "calendar-view__header";
    header.innerHTML = `
        <div class="calendar-view__title-group">
            <h1 class="calendar-view__title">${formatMonthLabel(state.viewDate)}</h1>
        </div>
    `;

    const controls = document.createElement("div");
    controls.className = "calendar-view__controls";

    controls.appendChild(createButton("←", "calendar-view__nav", onPreviousMonth));
    controls.appendChild(createButton(UI_TEXT.calendar.today, "calendar-view__today", onGoToToday));
    controls.appendChild(createButton("→", "calendar-view__nav", onNextMonth));
    controls.appendChild(createButton(UI_TEXT.calendar.addEvent, "calendar-view__add", onOpenNewEvent));

    header.appendChild(controls);

    const weekdays = document.createElement("div");
    weekdays.className = "calendar-view__weekdays";
    ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"].forEach((day) => {
        const label = document.createElement("div");
        label.className = "calendar-view__weekday";
        label.textContent = day;
        weekdays.appendChild(label);
    });

    const grid = document.createElement("div");
    grid.className = "calendar-view__grid";

    getMonthMatrix(state.viewDate).forEach((date) => {
        const cell = document.createElement("button");
        cell.type = "button";
        cell.className = `calendar-view__cell ${date ? "" : "calendar-view__cell--empty"}`;
        cell.disabled = !date;

        if (!date) {
            grid.appendChild(cell);
            return;
        }

        const dayNumber = document.createElement("div");
        dayNumber.className = "calendar-view__day";
        dayNumber.textContent = `${date.getDate()}`;
        cell.appendChild(dayNumber);

        const dayEvents = getEventsForDate(state.events, date);
        dayEvents.slice(0, 3).forEach((event) => {
            const chip = document.createElement("div");
            chip.className = "calendar-view__event-chip";
            chip.textContent = event.title;
            cell.appendChild(chip);
        });

        if (dayEvents.length > 3) {
            const more = document.createElement("div");
            more.className = "calendar-view__more";
            more.textContent = `+${dayEvents.length - 3} more`;
            cell.appendChild(more);
        }

        cell.addEventListener("click", () => {
            if (dayEvents.length > 0) {
                onOpenEvent(dayEvents[0].id);
            } else {
                onOpenNewEvent(date);
            }
        });

        grid.appendChild(cell);
    });

    section.appendChild(header);
    section.appendChild(weekdays);
    section.appendChild(grid);

    if (state.error) {
        const error = document.createElement("div");
        error.className = "calendar-view__error";
        error.textContent = state.error;
        section.appendChild(error);
    }

    if (state.isModalOpen && state.draftEvent) {
        section.appendChild(createModal({
            draftEvent: state.draftEvent,
            existingEvent: state.events.find((event) => event.id === state.draftEvent.id) ?? null,
            onCloseModal,
            onDraftChange,
            onSaveEvent,
            onDeleteEvent
        }));
    }

    return section;
}

function createModal({
    draftEvent,
    existingEvent,
    onCloseModal,
    onDraftChange,
    onSaveEvent,
    onDeleteEvent
}) {
    const overlay = document.createElement("div");
    overlay.className = "calendar-modal";
    overlay.addEventListener("click", onCloseModal);

    const card = document.createElement("div");
    card.className = "calendar-modal__card";
    card.addEventListener("click", (event) => event.stopPropagation());

    const title = document.createElement("h2");
    title.className = "calendar-modal__title";
    title.textContent = existingEvent ? UI_TEXT.calendar.editEventTitle : UI_TEXT.calendar.newEventTitle;

    const form = document.createElement("form");
    form.className = "calendar-modal__form";
    form.addEventListener("submit", (event) => {
        event.preventDefault();
        onSaveEvent();
    });

    form.appendChild(createField(UI_TEXT.calendar.titleLabel, "text", draftEvent.title, (value) => {
        onDraftChange("title", value);
    }));
    form.appendChild(createField(UI_TEXT.calendar.dateLabel, "date", draftEvent.date, (value) => {
        onDraftChange("date", value);
    }));
    form.appendChild(createField(UI_TEXT.calendar.detailsLabel, "text", draftEvent.description, (value) => {
        onDraftChange("description", value);
    }));

    const actions = document.createElement("div");
    actions.className = "calendar-modal__actions";

    if (existingEvent) {
        const deleteButton = document.createElement("button");
        deleteButton.type = "button";
        deleteButton.className = "calendar-modal__delete";
        deleteButton.textContent = UI_TEXT.calendar.deleteEvent;
        deleteButton.addEventListener("click", () => onDeleteEvent(existingEvent.id));
        actions.appendChild(deleteButton);
    }

    const cancelButton = document.createElement("button");
    cancelButton.type = "button";
    cancelButton.className = "calendar-modal__cancel";
    cancelButton.textContent = UI_TEXT.calendar.cancel;
    cancelButton.addEventListener("click", onCloseModal);

    const saveButton = document.createElement("button");
    saveButton.type = "submit";
    saveButton.className = "calendar-modal__save";
    saveButton.textContent = UI_TEXT.calendar.saveEvent;

    actions.appendChild(cancelButton);
    actions.appendChild(saveButton);

    card.appendChild(title);
    card.appendChild(form);
    card.appendChild(actions);
    overlay.appendChild(card);

    return overlay;
}

function createField(labelText, type, value, onChange) {
    const wrapper = document.createElement("label");
    wrapper.className = "calendar-modal__field";

    const label = document.createElement("span");
    label.className = "calendar-modal__label";
    label.textContent = labelText;

    const input = document.createElement("input");
    input.className = "calendar-modal__input";
    input.type = type;
    input.value = value ?? "";
    input.addEventListener("input", (event) => onChange(event.target.value));

    wrapper.appendChild(label);
    wrapper.appendChild(input);
    return wrapper;
}

function createButton(text, className, onClick) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = className;
    button.textContent = text;
    button.addEventListener("click", onClick);
    return button;
}
