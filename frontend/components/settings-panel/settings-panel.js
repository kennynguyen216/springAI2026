export function createSettingsPanel({
    isOpen,
    theme,
    title,
    description,
    themeLabel,
    historyLabel,
    closeLabel,
    historyCountLabel,
    onClose,
    onClearHistory
}) {
    const wrapper = document.createElement("div");
    wrapper.className = `settings-panel ${isOpen ? "settings-panel--open" : ""}`;

    const overlay = document.createElement("button");
    overlay.type = "button";
    overlay.className = "settings-panel__overlay";
    overlay.addEventListener("click", onClose);

    const panel = document.createElement("aside");
    panel.className = "settings-panel__card";

    const header = document.createElement("div");
    header.className = "settings-panel__header";
    header.innerHTML = `
        <div>
            <h2 class="settings-panel__title">${title}</h2>
            <p class="settings-panel__description">${description}</p>
        </div>
    `;

    const closeButton = document.createElement("button");
    closeButton.type = "button";
    closeButton.className = "settings-panel__close";
    closeButton.textContent = closeLabel;
    closeButton.addEventListener("click", onClose);

    header.appendChild(closeButton);

    const body = document.createElement("div");
    body.className = "settings-panel__body";

    const themeRow = document.createElement("div");
    themeRow.className = "settings-panel__row";
    themeRow.innerHTML = `
        <span>${themeLabel}</span>
        <strong>${theme}</strong>
    `;

    const historyRow = document.createElement("div");
    historyRow.className = "settings-panel__row settings-panel__row--stacked";
    historyRow.innerHTML = `
        <div>
            <span>${historyLabel}</span>
            <small>${historyCountLabel}</small>
        </div>
    `;

    const clearButton = document.createElement("button");
    clearButton.type = "button";
    clearButton.className = "settings-panel__danger";
    clearButton.textContent = historyLabel;
    clearButton.addEventListener("click", onClearHistory);

    historyRow.appendChild(clearButton);

    body.appendChild(themeRow);
    body.appendChild(historyRow);

    panel.appendChild(header);
    panel.appendChild(body);
    wrapper.appendChild(overlay);
    wrapper.appendChild(panel);

    return wrapper;
}
