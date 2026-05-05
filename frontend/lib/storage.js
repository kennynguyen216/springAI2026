import { normalizeConversation } from "./conversations.js";

const STORAGE_KEYS = {
    theme: "alfred-theme",
    state: "alfred-ui-state"
};

export function getInitialState() {
    const storedState = safeJsonParse(window.localStorage.getItem(STORAGE_KEYS.state));
    const storedTheme = window.localStorage.getItem(STORAGE_KEYS.theme);

    return {
        theme: storedTheme || "dark",
        conversations: (storedState?.conversations ?? []).map(normalizeConversation),
        activeConversationId: storedState?.activeConversationId ?? null,
        activeView: storedState?.activeView ?? "chat"
    };
}

export function persistState(state) {
    window.localStorage.setItem(STORAGE_KEYS.state, JSON.stringify(state));
}

export function persistTheme(theme) {
    window.localStorage.setItem(STORAGE_KEYS.theme, theme);
}

function safeJsonParse(value) {
    if (!value) {
        return null;
    }

    try {
        return JSON.parse(value);
    } catch {
        return null;
    }
}
