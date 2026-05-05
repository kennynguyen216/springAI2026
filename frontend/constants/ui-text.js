export const UI_TEXT = {
    app: {
        name: "Alfred",
        tagline: "School and work assistant"
    },
    sidebar: {
        chat: "Chat",
        calendar: "Calendar",
        newChat: "New chat",
        historyTitle: "Chat history",
        scanInbox: "Scan inbox",
        settings: "Settings",
        emptyHistory: "No conversations yet"
    },
    topbar: {
        messageCountLabel: "messages",
        userInitials: "AH",
        profileLabel: "Profile",
        themeDark: "Dark mode",
        themeLight: "Light mode",
        toggleSidebar: "Toggle sidebar",
        calendarTitle: "Calendar"
    },
    emptyState: {
        title: "Alfred",
        description: "A polished productivity assistant for emails, planning, notes, research, and focused school or work support.",
        promptCards: [
            {
                title: "Draft a professor email",
                prompt: "Can you draft a polite email to my professor asking for an extension on my assignment?"
            },
            {
                title: "Summarize meeting notes",
                prompt: "Please summarize my meeting notes into action items and deadlines."
            },
            {
                title: "Plan my workday",
                prompt: "Help me plan my workday around three meetings and a project deadline."
            },
            {
                title: "Research kickoff",
                prompt: "Create a research outline for a professional presentation on AI agent guardrails."
            }
        ]
    },
    input: {
        placeholder: "Ask Alfred",
        sendLabel: "Send",
        hint: ""
    },
    calendar: {
        addEvent: "Add event",
        saveEvent: "Save event",
        deleteEvent: "Delete event",
        cancel: "Cancel",
        today: "Today",
        titleLabel: "Title",
        dateLabel: "Date",
        detailsLabel: "Details",
        emptyDay: "No events scheduled.",
        newEventTitle: "New event",
        editEventTitle: "Event details",
        invalidEvent: "Title and date are required."
    },
    settings: {
        title: "Settings",
        description: "Manage Alfred's appearance and local conversation history.",
        themeLabel: "Current theme",
        clearHistoryLabel: "Clear local chat history",
        closeLabel: "Close",
        historyCountSuffix: "saved conversations"
    },
    status: {
        alfredThinking: "Alfred is working on it",
        systemError: "System Error: Unable to reach the C# backend.",
        scanningInbox: "Scanning your inbox for relevant deadlines and events...",
        scanInboxError: "Unable to scan the inbox right now.",
        calendarError: "Unable to load calendar events right now.",
        blockedLabel: "Blocked response",
        blockedDescription: "Alfred can’t answer that because it falls outside its school and work scope."
    },
    messages: {
        assistantLabel: "Alfred",
        userLabel: "You",
        copyCode: "Copy",
        copiedCode: "Copied"
    }
};
