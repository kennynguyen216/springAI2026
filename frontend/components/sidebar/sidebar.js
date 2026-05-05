import { UI_TEXT } from "../../constants/ui-text.js";
import { createSidebarContextMenu } from "../sidebar-context-menu/sidebar-context-menu.js";

export function createSidebar({
    title,
    subtitle,
    isOpen,
    activeView,
    activeConversationId,
    historySections,
    projectOptions,
    contextMenuState,
    renameState,
    onShowChat,
    onShowCalendar,
    onConversationSelected,
    onStartNewChat,
    onToggleSidebar,
    onOpenSettings,
    onScanInbox,
    onOpenConversationMenu,
    onCloseConversationMenu,
    onToggleConversationPin,
    onStartConversationRename,
    onRenameDraftChange,
    onCommitConversationRename,
    onCancelConversationRename,
    onOpenProjectPicker,
    onProjectDraftChange,
    onSaveProjectAssignment,
    onRequestConversationDelete
}) {
    const aside = document.createElement("aside");
    aside.className = `sidebar ${isOpen ? "sidebar--open" : "sidebar--closed"}`;

    const overlay = document.createElement("button");
    overlay.type = "button";
    overlay.className = `sidebar__overlay ${isOpen ? "sidebar__overlay--visible" : ""}`;
    overlay.addEventListener("click", onToggleSidebar);

    const panel = document.createElement("div");
    panel.className = "sidebar__panel";

    const header = document.createElement("div");
    header.className = "sidebar__header";

    const toggleButton = document.createElement("button");
    toggleButton.type = "button";
    toggleButton.className = "sidebar__toggle";
    toggleButton.setAttribute("aria-label", UI_TEXT.topbar.toggleSidebar);
    toggleButton.textContent = "☰";
    toggleButton.addEventListener("click", onToggleSidebar);

    const brand = document.createElement("div");
    brand.className = "sidebar__brand";
    brand.innerHTML = `
        <div class="sidebar__brand-mark">A</div>
        <div class="sidebar__brand-copy">
            <div class="sidebar__title">${title}</div>
            <div class="sidebar__subtitle">${subtitle}</div>
        </div>
    `;

    header.appendChild(toggleButton);
    header.appendChild(brand);

    const newChatButton = document.createElement("button");
    newChatButton.type = "button";
    newChatButton.className = "sidebar__new-chat";
    newChatButton.textContent = UI_TEXT.sidebar.newChat;
    newChatButton.addEventListener("click", onStartNewChat);

    const viewSwitch = document.createElement("div");
    viewSwitch.className = "sidebar__view-switch";
    viewSwitch.appendChild(createViewButton(UI_TEXT.sidebar.chat, activeView === "chat", onShowChat));
    viewSwitch.appendChild(createViewButton(UI_TEXT.sidebar.calendar, activeView === "calendar", onShowCalendar));

    const quickActions = document.createElement("div");
    quickActions.className = "sidebar__quick-actions";

    const scanInboxButton = document.createElement("button");
    scanInboxButton.type = "button";
    scanInboxButton.className = "sidebar__quick-button";
    scanInboxButton.textContent = UI_TEXT.sidebar.scanInbox;
    scanInboxButton.addEventListener("click", onScanInbox);
    quickActions.appendChild(scanInboxButton);

    const historySection = document.createElement("section");
    historySection.className = "sidebar__section";
    historySection.appendChild(createSectionHeader(UI_TEXT.sidebar.historyTitle));

    const historyScroll = document.createElement("div");
    historyScroll.className = "sidebar__history-scroll";

    if (historySections.length === 0) {
        historyScroll.appendChild(createEmptyLabel(UI_TEXT.sidebar.emptyHistory));
    } else {
        for (const group of historySections) {
            const wrapper = document.createElement("div");
            wrapper.className = "sidebar__group";

            const label = document.createElement("div");
            label.className = "sidebar__group-label";
            label.textContent = group.label;
            wrapper.appendChild(label);

            const list = document.createElement("div");
            list.className = "sidebar__history-list";

            group.items.forEach((conversation) => {
                const item = createConversationItem({
                    conversation,
                    isActive: conversation.id === activeConversationId,
                    isMenuOpen: contextMenuState.conversationId === conversation.id,
                    isRenameOpen: renameState.conversationId === conversation.id,
                    contextMenuState,
                    renameState,
                    projectOptions,
                    onConversationSelected,
                    onOpenConversationMenu,
                    onToggleConversationPin,
                    onStartConversationRename,
                    onRenameDraftChange,
                    onCommitConversationRename,
                    onCancelConversationRename,
                    onOpenProjectPicker,
                    onProjectDraftChange,
                    onSaveProjectAssignment,
                    onRequestConversationDelete,
                    onCloseConversationMenu
                });

                list.appendChild(item);
            });

            wrapper.appendChild(list);
            historyScroll.appendChild(wrapper);
        }
    }

    historySection.appendChild(historyScroll);

    const footer = document.createElement("div");
    footer.className = "sidebar__footer";

    const settingsButton = document.createElement("button");
    settingsButton.type = "button";
    settingsButton.className = "sidebar__settings";
    settingsButton.textContent = UI_TEXT.sidebar.settings;
    settingsButton.addEventListener("click", onOpenSettings);

    footer.appendChild(settingsButton);

    if (contextMenuState.conversationId) {
        const dismissLayer = document.createElement("button");
        dismissLayer.type = "button";
        dismissLayer.className = "sidebar__menu-dismiss";
        dismissLayer.addEventListener("click", onCloseConversationMenu);
        aside.appendChild(dismissLayer);
    }

    panel.appendChild(header);
    panel.appendChild(newChatButton);
    panel.appendChild(viewSwitch);
    panel.appendChild(quickActions);
    panel.appendChild(historySection);
    panel.appendChild(footer);

    aside.appendChild(overlay);
    aside.appendChild(panel);
    return aside;
}

function createConversationItem({
    conversation,
    isActive,
    isMenuOpen,
    isRenameOpen,
    contextMenuState,
    renameState,
    projectOptions,
    onConversationSelected,
    onOpenConversationMenu,
    onToggleConversationPin,
    onStartConversationRename,
    onRenameDraftChange,
    onCommitConversationRename,
    onCancelConversationRename,
    onOpenProjectPicker,
    onProjectDraftChange,
    onSaveProjectAssignment,
    onRequestConversationDelete,
    onCloseConversationMenu
}) {
    const row = document.createElement("div");
    row.className = "sidebar__history-row";
    row.addEventListener("contextmenu", (event) => {
        event.preventDefault();
        onOpenConversationMenu(conversation.id);
    });

    const button = document.createElement("button");
    button.type = "button";
    button.className = `sidebar__history-item ${isActive ? "sidebar__history-item--active" : ""}`;
    button.addEventListener("click", () => onConversationSelected(conversation.id));

    const label = document.createElement("div");
    label.className = "sidebar__history-title";

    if (isRenameOpen) {
        const input = document.createElement("input");
        input.className = "sidebar__rename-input";
        input.value = renameState.draftTitle;
        input.addEventListener("input", (event) => onRenameDraftChange(event.target.value));
        input.addEventListener("blur", () => onCommitConversationRename(conversation.id));
        input.addEventListener("keydown", (event) => {
            if (event.key === "Enter") {
                event.preventDefault();
                onCommitConversationRename(conversation.id);
            }

            if (event.key === "Escape") {
                event.preventDefault();
                onCancelConversationRename();
            }
        });

        label.appendChild(input);
        requestAnimationFrame(() => {
            input.focus();
            input.select();
        });
    } else {
        label.textContent = conversation.title;
    }

    button.appendChild(label);

    if (conversation.isPinned) {
        const pin = document.createElement("span");
        pin.className = "sidebar__pin-indicator";
        pin.textContent = "📌";
        button.appendChild(pin);
    }

    const menuButton = document.createElement("button");
    menuButton.type = "button";
    menuButton.className = `sidebar__menu-button ${isMenuOpen ? "sidebar__menu-button--visible" : ""}`;
    menuButton.textContent = "⋯";
    menuButton.addEventListener("click", (event) => {
        event.stopPropagation();
        onOpenConversationMenu(conversation.id);
    });

    row.appendChild(button);
    row.appendChild(menuButton);

    if (isMenuOpen) {
        const menu = createSidebarContextMenu({
            isPinned: conversation.isPinned,
            projectName: conversation.projectName,
            isDeleteConfirming: contextMenuState.pendingDeleteConversationId === conversation.id,
            isProjectPickerOpen: contextMenuState.mode === "project",
            projectDraft: contextMenuState.projectDraft,
            projectOptions,
            onTogglePin: () => onToggleConversationPin(conversation.id),
            onStartRename: () => onStartConversationRename(conversation.id),
            onOpenProjectPicker: () => onOpenProjectPicker(conversation.id),
            onProjectDraftChange,
            onSaveProject: (explicitProjectName) =>
                onSaveProjectAssignment(conversation.id, explicitProjectName),
            onRequestDelete: () => onRequestConversationDelete(conversation.id),
            onClose: onCloseConversationMenu
        });

        row.appendChild(menu);
    }

    return row;
}

function createViewButton(text, isActive, onClick) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = `sidebar__view-button ${isActive ? "sidebar__view-button--active" : ""}`;
    button.textContent = text;
    button.addEventListener("click", onClick);
    return button;
}

function createSectionHeader(text) {
    const header = document.createElement("div");
    header.className = "sidebar__section-header";
    header.textContent = text;
    return header;
}

function createEmptyLabel(text) {
    const empty = document.createElement("div");
    empty.className = "sidebar__empty";
    empty.textContent = text;
    return empty;
}
