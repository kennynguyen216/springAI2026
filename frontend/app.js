import { UI_TEXT } from "./constants/ui-text.js";
import { createSidebar } from "./components/sidebar/sidebar.js";
import { createTopBar } from "./components/topbar/topbar.js";
import { createEmptyState } from "./components/empty-state/empty-state.js";
import { createChatThread } from "./components/chat-thread/chat-thread.js";
import { createChatInput } from "./components/chat-input/chat-input.js";
import { createSettingsPanel } from "./components/settings-panel/settings-panel.js";
import { createCalendar } from "./components/calendar/calendar.js";
import {
    buildSidebarHistorySections,
    createConversation,
    createMessage,
    detectBlockedResponse,
    formatConversationTitle,
    getProjectNames
} from "./lib/conversations.js";
import {
    getInitialState,
    persistState,
    persistTheme
} from "./lib/storage.js";
import {
    createCalendarState,
    createDraftEvent,
    formatMonthLabel,
    normalizeCalendarEvent,
    shiftCalendarMonth
} from "./lib/calendar.js";

const root = document.getElementById("app");
const state = getInitialState();

const appState = {
    ...state,
    calendar: createCalendarState(),
    isLoading: false,
    isSettingsOpen: false,
    isSidebarOpen: window.innerWidth > 900,
    streamToken: 0,
    contextMenuState: createContextMenuState(),
    renameState: createRenameState(),
    chatViewport: {
        autoScrollEnabled: true,
        scrollTop: 0
    }
};

const callbacks = {
    onPromptSelected: (prompt) => {
        sendMessage(prompt);
    },
    onShowChat: () => {
        appState.activeView = "chat";
        appState.isSidebarOpen = window.innerWidth > 900;
        closeConversationUiState();
        saveAndRender();
    },
    onShowCalendar: () => {
        appState.activeView = "calendar";
        appState.isSidebarOpen = window.innerWidth > 900;
        closeConversationUiState();
        saveAndRender();
    },
    onConversationSelected: (conversationId) => {
        appState.activeView = "chat";
        appState.activeConversationId = conversationId;
        appState.isSidebarOpen = window.innerWidth > 900;
        appState.chatViewport.autoScrollEnabled = true;
        closeConversationUiState();
        saveAndRender();
    },
    onStartNewChat: () => {
        appState.activeView = "chat";
        appState.activeConversationId = null;
        appState.chatViewport.autoScrollEnabled = true;
        closeConversationUiState();
        saveAndRender();
    },
    onToggleSidebar: () => {
        appState.isSidebarOpen = !appState.isSidebarOpen;
        render();
    },
    onToggleTheme: () => {
        appState.theme = appState.theme === "dark" ? "light" : "dark";
        applyTheme();
        saveAndRender();
    },
    onOpenSettings: () => {
        appState.isSettingsOpen = true;
        render();
    },
    onCloseSettings: () => {
        appState.isSettingsOpen = false;
        render();
    },
    onClearHistory: () => {
        appState.conversations = [];
        appState.activeConversationId = null;
        appState.isSettingsOpen = false;
        closeConversationUiState();
        saveAndRender();
    },
    onScanInbox: async () => {
        await scanInbox();
    },
    onSendMessage: (message) => {
        sendMessage(message);
    },
    onOpenConversationMenu: (conversationId) => {
        const conversation = findConversation(conversationId);
        appState.contextMenuState = {
            conversationId,
            mode: "actions",
            projectDraft: conversation?.projectName ?? "",
            pendingDeleteConversationId: null
        };
        render();
    },
    onCloseConversationMenu: () => {
        appState.contextMenuState = createContextMenuState();
        render();
    },
    onToggleConversationPin: (conversationId) => {
        const conversation = findConversation(conversationId);
        if (!conversation) {
            return;
        }

        conversation.isPinned = !conversation.isPinned;
        closeConversationUiState();
        saveAndRender();
    },
    onStartConversationRename: (conversationId) => {
        const conversation = findConversation(conversationId);
        if (!conversation) {
            return;
        }

        appState.renameState = {
            conversationId,
            draftTitle: conversation.title
        };
        appState.contextMenuState = createContextMenuState();
        render();
    },
    onRenameDraftChange: (value) => {
        appState.renameState.draftTitle = value;
    },
    onCommitConversationRename: (conversationId) => {
        const conversation = findConversation(conversationId);
        if (!conversation) {
            appState.renameState = createRenameState();
            render();
            return;
        }

        const nextTitle = appState.renameState.draftTitle.trim();
        if (nextTitle) {
            conversation.title = nextTitle;
        }

        appState.renameState = createRenameState();
        saveAndRender();
    },
    onCancelConversationRename: () => {
        appState.renameState = createRenameState();
        render();
    },
    onOpenProjectPicker: (conversationId) => {
        const conversation = findConversation(conversationId);
        appState.contextMenuState = {
            conversationId,
            mode: "project",
            projectDraft: conversation?.projectName ?? "",
            pendingDeleteConversationId: null
        };
        render();
    },
    onProjectDraftChange: (value) => {
        appState.contextMenuState.projectDraft = value;
    },
    onSaveProjectAssignment: (conversationId, explicitProjectName = null) => {
        const conversation = findConversation(conversationId);
        if (!conversation) {
            return;
        }

        const nextProject = (explicitProjectName ?? appState.contextMenuState.projectDraft).trim();
        conversation.projectName = nextProject || null;
        closeConversationUiState();
        saveAndRender();
    },
    onRequestConversationDelete: (conversationId) => {
        if (appState.contextMenuState.pendingDeleteConversationId === conversationId) {
            deleteConversation(conversationId);
            return;
        }

        appState.contextMenuState.pendingDeleteConversationId = conversationId;
        render();
    },
    onChatScrollChange: ({ autoScrollEnabled, scrollTop }) => {
        const stateChanged = appState.chatViewport.autoScrollEnabled !== autoScrollEnabled;
        appState.chatViewport.scrollTop = scrollTop;
        appState.chatViewport.autoScrollEnabled = autoScrollEnabled;

        if (stateChanged) {
            render();
        }
    },
    onJumpToBottom: () => {
        appState.chatViewport.autoScrollEnabled = true;
        render();
    },
    onPreviousMonth: () => {
        appState.calendar.viewDate = shiftCalendarMonth(appState.calendar.viewDate, -1);
        render();
    },
    onNextMonth: () => {
        appState.calendar.viewDate = shiftCalendarMonth(appState.calendar.viewDate, 1);
        render();
    },
    onGoToToday: () => {
        appState.calendar.viewDate = createCalendarState().viewDate;
        render();
    },
    onOpenNewEvent: (date = null) => {
        appState.calendar.isModalOpen = true;
        appState.calendar.draftEvent = createDraftEvent(date);
        render();
    },
    onOpenEvent: (eventId) => {
        const sourceEvent = appState.calendar.events.find((event) => event.id === eventId) ?? null;
        appState.calendar.isModalOpen = true;
        appState.calendar.draftEvent = createDraftEvent(null, sourceEvent);
        render();
    },
    onCloseCalendarModal: () => {
        appState.calendar.isModalOpen = false;
        appState.calendar.draftEvent = null;
        render();
    },
    onCalendarDraftChange: (field, value) => {
        if (!appState.calendar.draftEvent) {
            return;
        }

        appState.calendar.draftEvent[field] = value;
        render();
    }
};

window.addEventListener("resize", () => {
    if (window.innerWidth > 900) {
        appState.isSidebarOpen = true;
    }

    render();
});

applyTheme();
render();
loadEvents();

async function sendMessage(rawMessage) {
    const message = rawMessage.trim();
    if (!message || appState.isLoading) {
        return;
    }

    let conversation = getActiveConversation();
    if (!conversation) {
        conversation = createConversation(message);
        appState.conversations.unshift(conversation);
        appState.activeConversationId = conversation.id;
    }

    const userMessage = createMessage("user", message);
    const assistantMessage = createMessage("assistant", "", {
        isStreaming: true,
        hasStreamedText: false
    });

    conversation.messages.push(userMessage);
    conversation.messages.push(assistantMessage);
    conversation.updatedAt = userMessage.timestamp;
    conversation.title = formatConversationTitle(conversation.messages);

    appState.isLoading = true;
    appState.chatViewport.autoScrollEnabled = true;
    appState.isSidebarOpen = window.innerWidth > 900;
    appState.streamToken += 1;
    closeConversationUiState();
    saveAndRender();

    try {
        const response = await fetch("/chat/stream", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                Message: message,
                ThreadId: conversation.threadId
            })
        });

        if (!response.ok) {
            throw new Error(UI_TEXT.status.systemError);
        }

        await consumeChatStream(response, conversation.id, assistantMessage.id, appState.streamToken);
    } catch {
        removeMessage(conversation.id, assistantMessage.id);
        conversation.messages.push(createMessage("assistant", UI_TEXT.status.systemError, {
            isError: true
        }));
        saveAndRender();
    } finally {
        appState.isLoading = false;
        saveAndRender();
    }
}

async function scanInbox() {
    if (appState.isLoading) {
        return;
    }

    let conversation = getActiveConversation();
    if (!conversation) {
        conversation = createConversation(UI_TEXT.sidebar.scanInbox);
        appState.conversations.unshift(conversation);
        appState.activeConversationId = conversation.id;
    }

    const statusMessage = createMessage("assistant", UI_TEXT.status.scanningInbox, {
        isStatus: true
    });

    conversation.messages.push(statusMessage);
    appState.isLoading = true;
    closeConversationUiState();
    saveAndRender();

    try {
        const response = await fetch("/scan-inbox", { method: "POST" });
        const payload = await response.json();
        statusMessage.content = payload.message;
        statusMessage.isStatus = false;
        await loadEvents();
    } catch {
        statusMessage.content = UI_TEXT.status.scanInboxError;
        statusMessage.isError = true;
        statusMessage.isStatus = false;
    } finally {
        appState.isLoading = false;
        conversation.updatedAt = new Date().toISOString();
        saveAndRender();
    }
}

function render() {
    const activeConversation = getActiveConversation();
    const historySections = buildSidebarHistorySections(appState.conversations);
    const projectOptions = getProjectNames(appState.conversations);

    root.innerHTML = "";
    root.className = "alfred-app-shell";

    const layout = document.createElement("div");
    layout.className = `app-layout ${appState.isSidebarOpen ? "app-layout--sidebar-open" : "app-layout--sidebar-closed"}`;

    const sidebar = createSidebar({
        title: UI_TEXT.app.name,
        subtitle: UI_TEXT.app.tagline,
        isOpen: appState.isSidebarOpen,
        activeView: appState.activeView,
        activeConversationId: appState.activeConversationId,
        historySections,
        projectOptions,
        contextMenuState: appState.contextMenuState,
        renameState: appState.renameState,
        onShowChat: callbacks.onShowChat,
        onShowCalendar: callbacks.onShowCalendar,
        onConversationSelected: callbacks.onConversationSelected,
        onStartNewChat: callbacks.onStartNewChat,
        onToggleSidebar: callbacks.onToggleSidebar,
        onOpenSettings: callbacks.onOpenSettings,
        onScanInbox: callbacks.onScanInbox,
        onOpenConversationMenu: callbacks.onOpenConversationMenu,
        onCloseConversationMenu: callbacks.onCloseConversationMenu,
        onToggleConversationPin: callbacks.onToggleConversationPin,
        onStartConversationRename: callbacks.onStartConversationRename,
        onRenameDraftChange: callbacks.onRenameDraftChange,
        onCommitConversationRename: callbacks.onCommitConversationRename,
        onCancelConversationRename: callbacks.onCancelConversationRename,
        onOpenProjectPicker: callbacks.onOpenProjectPicker,
        onProjectDraftChange: callbacks.onProjectDraftChange,
        onSaveProjectAssignment: callbacks.onSaveProjectAssignment,
        onRequestConversationDelete: callbacks.onRequestConversationDelete
    });

    const shell = document.createElement("div");
    shell.className = "workspace-shell";

    const topBar = createTopBar({
        title: appState.activeView === "calendar"
            ? `${UI_TEXT.topbar.calendarTitle} · ${formatMonthLabel(appState.calendar.viewDate)}`
            : activeConversation?.title ?? UI_TEXT.emptyState.title,
        theme: appState.theme,
        isSidebarOpen: appState.isSidebarOpen,
        onToggleSidebar: callbacks.onToggleSidebar,
        onToggleTheme: callbacks.onToggleTheme,
        userInitials: UI_TEXT.topbar.userInitials
    });

    const content = document.createElement("main");
    content.className = "workspace-main";

    if (appState.activeView === "calendar") {
        content.appendChild(createCalendar({
            state: appState.calendar,
            onPreviousMonth: callbacks.onPreviousMonth,
            onNextMonth: callbacks.onNextMonth,
            onGoToToday: callbacks.onGoToToday,
            onOpenNewEvent: callbacks.onOpenNewEvent,
            onOpenEvent: callbacks.onOpenEvent,
            onCloseModal: callbacks.onCloseCalendarModal,
            onDraftChange: callbacks.onCalendarDraftChange,
            onSaveEvent: saveCalendarEvent,
            onDeleteEvent: deleteCalendarEvent
        }));
    } else if (activeConversation) {
        content.appendChild(createChatThread({
            messages: activeConversation.messages,
            autoScrollEnabled: appState.chatViewport.autoScrollEnabled,
            savedScrollTop: appState.chatViewport.scrollTop,
            showJumpToBottom: appState.isLoading && !appState.chatViewport.autoScrollEnabled,
            onScrollStateChange: callbacks.onChatScrollChange,
            onJumpToBottom: callbacks.onJumpToBottom
        }));
    } else {
        content.classList.add("workspace-main--landing");

        const landingShell = document.createElement("div");
        landingShell.className = "landing-shell";

        landingShell.appendChild(createEmptyState({
            title: UI_TEXT.emptyState.title,
            description: UI_TEXT.emptyState.description,
            promptCards: UI_TEXT.emptyState.promptCards,
            onPromptSelected: callbacks.onPromptSelected
        }));

        landingShell.appendChild(createChatInput({
            placeholder: UI_TEXT.input.placeholder,
            disabled: appState.isLoading,
            loading: appState.isLoading,
            sendLabel: UI_TEXT.input.sendLabel,
            hint: UI_TEXT.input.hint,
            onSend: callbacks.onSendMessage
        }));

        content.appendChild(landingShell);
    }

    shell.appendChild(topBar);
    shell.appendChild(content);

    if (appState.activeView === "chat" && activeConversation) {
        shell.appendChild(createChatInput({
            placeholder: UI_TEXT.input.placeholder,
            disabled: appState.isLoading,
            loading: appState.isLoading,
            sendLabel: UI_TEXT.input.sendLabel,
            hint: UI_TEXT.input.hint,
            onSend: callbacks.onSendMessage
        }));
    }

    const settingsPanel = createSettingsPanel({
        isOpen: appState.isSettingsOpen,
        theme: appState.theme,
        title: UI_TEXT.settings.title,
        description: UI_TEXT.settings.description,
        themeLabel: UI_TEXT.settings.themeLabel,
        historyLabel: UI_TEXT.settings.clearHistoryLabel,
        closeLabel: UI_TEXT.settings.closeLabel,
        historyCountLabel: `${appState.conversations.length} ${UI_TEXT.settings.historyCountSuffix}`,
        onClose: callbacks.onCloseSettings,
        onClearHistory: callbacks.onClearHistory
    });

    layout.appendChild(sidebar);
    layout.appendChild(shell);
    root.appendChild(layout);
    root.appendChild(settingsPanel);
}

function saveAndRender() {
    persistTheme(appState.theme);
    persistState({
        theme: appState.theme,
        conversations: appState.conversations,
        activeConversationId: appState.activeConversationId,
        activeView: appState.activeView
    });
    render();
}

function applyTheme() {
    document.documentElement.dataset.theme = appState.theme;

    const darkTheme = document.getElementById("hljs-dark-theme");
    const lightTheme = document.getElementById("hljs-light-theme");

    if (darkTheme && lightTheme) {
        darkTheme.disabled = appState.theme !== "dark";
        lightTheme.disabled = appState.theme === "dark";
    }
}

function getActiveConversation() {
    return appState.conversations.find((conversation) => conversation.id === appState.activeConversationId) ?? null;
}

async function loadEvents() {
    try {
        const response = await fetch("/events");
        if (!response.ok) {
            throw new Error(UI_TEXT.status.calendarError);
        }

        const payload = await response.json();
        appState.calendar.events = payload.map(normalizeCalendarEvent);
        appState.calendar.error = "";
        render();
    } catch {
        appState.calendar.error = UI_TEXT.status.calendarError;
        render();
    }
}

async function saveCalendarEvent() {
    const draftEvent = appState.calendar.draftEvent;
    if (!draftEvent?.title?.trim() || !draftEvent?.date) {
        window.alert(UI_TEXT.calendar.invalidEvent);
        return;
    }

    const payload = {
        title: draftEvent.title.trim(),
        date: draftEvent.date,
        description: draftEvent.description ?? ""
    };

    const url = draftEvent.id ? `/events/${draftEvent.id}` : "/events";
    const method = draftEvent.id ? "PUT" : "POST";

    await fetch(url, {
        method,
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify(payload)
    });

    appState.calendar.isModalOpen = false;
    appState.calendar.draftEvent = null;
    await loadEvents();
}

async function deleteCalendarEvent(eventId) {
    await fetch(`/events/${eventId}`, {
        method: "DELETE"
    });

    appState.calendar.isModalOpen = false;
    appState.calendar.draftEvent = null;
    await loadEvents();
}

async function consumeChatStream(response, conversationId, messageId, token) {
    if (!response.body) {
        throw new Error(UI_TEXT.status.systemError);
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = "";

    while (true) {
        const { done, value } = await reader.read();

        if (done) {
            break;
        }

        if (token !== appState.streamToken) {
            await reader.cancel();
            return;
        }

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split("\n");
        buffer = lines.pop() ?? "";

        for (const line of lines) {
            if (!line.trim()) {
                continue;
            }

            handleStreamEvent(JSON.parse(line), conversationId, messageId);
        }
    }

    if (buffer.trim()) {
        handleStreamEvent(JSON.parse(buffer), conversationId, messageId);
    }
}

function handleStreamEvent(event, conversationId, messageId) {
    const conversation = findConversation(conversationId);
    const message = conversation?.messages.find((item) => item.id === messageId);

    if (!conversation || !message) {
        return;
    }

    switch (event.type) {
        case "meta":
            if (event.threadId) {
                conversation.threadId = event.threadId;
            }
            break;
        case "chunk":
            message.content += event.text ?? "";
            message.hasStreamedText = true;
            render();
            break;
        case "replace":
            message.content = event.text ?? "";
            message.isBlocked = detectBlockedResponse(message.content);
            message.hasStreamedText = true;
            render();
            break;
        case "done":
            message.isStreaming = false;
            conversation.updatedAt = new Date().toISOString();
            conversation.title = formatConversationTitle(conversation.messages);
            saveAndRender();
            break;
        case "error":
            message.isStreaming = false;
            message.isError = true;
            message.content = event.text || UI_TEXT.status.systemError;
            saveAndRender();
            break;
        default:
            break;
    }
}

function findConversation(conversationId) {
    return appState.conversations.find((conversation) => conversation.id === conversationId) ?? null;
}

function removeMessage(conversationId, messageId) {
    const conversation = findConversation(conversationId);
    if (!conversation) {
        return;
    }

    conversation.messages = conversation.messages.filter((message) => message.id !== messageId);
}

function deleteConversation(conversationId) {
    appState.conversations = appState.conversations.filter((conversation) => conversation.id !== conversationId);

    if (appState.activeConversationId === conversationId) {
        appState.activeConversationId = appState.conversations[0]?.id ?? null;
    }

    closeConversationUiState();
    saveAndRender();
}

function closeConversationUiState() {
    appState.contextMenuState = createContextMenuState();
    appState.renameState = createRenameState();
}

function createContextMenuState() {
    return {
        conversationId: null,
        mode: "actions",
        projectDraft: "",
        pendingDeleteConversationId: null
    };
}

function createRenameState() {
    return {
        conversationId: null,
        draftTitle: ""
    };
}
