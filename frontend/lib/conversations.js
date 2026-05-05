import { UI_TEXT } from "../constants/ui-text.js";

export function createConversation(seedText) {
    const timestamp = new Date().toISOString();

    return {
        id: crypto.randomUUID(),
        threadId: null,
        title: formatConversationTitle([{ role: "user", content: seedText }]),
        createdAt: timestamp,
        updatedAt: timestamp,
        isPinned: false,
        projectName: null,
        messages: []
    };
}

export function createMessage(role, content, overrides = {}) {
    return {
        id: crypto.randomUUID(),
        role,
        content,
        timestamp: new Date().toISOString(),
        isBlocked: false,
        isError: false,
        isStatus: false,
        ...overrides
    };
}

export function normalizeConversation(conversation) {
    return {
        ...conversation,
        isPinned: conversation?.isPinned ?? false,
        projectName: conversation?.projectName ?? null,
        messages: Array.isArray(conversation?.messages) ? conversation.messages : []
    };
}

export function formatConversationTitle(messages) {
    const firstUserMessage = messages.find((message) => message.role === "user")?.content ?? "New conversation";
    return firstUserMessage.length > 42
        ? `${firstUserMessage.slice(0, 39)}...`
        : firstUserMessage;
}

export function getRelativeDateLabel(timestamp) {
    const now = new Date();
    const target = new Date(timestamp);
    const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    const compareDate = new Date(target.getFullYear(), target.getMonth(), target.getDate());
    const delta = Math.round((today - compareDate) / 86400000);

    if (delta === 0) {
        return "Today";
    }

    if (delta === 1) {
        return "Yesterday";
    }

    if (delta < 7) {
        return "This week";
    }

    return target.toLocaleDateString(undefined, {
        month: "short",
        day: "numeric",
        year: "numeric"
    });
}

export function detectBlockedResponse(text) {
    return text === "I’m here to help with school- or work-related questions and tasks, but I can’t help with unrelated topics."
        || text === "I can only help with school or work-related productivity tasks like emails, assignments, scheduling, documents, notes, and academic or professional research."
        || text.includes(UI_TEXT.status.blockedDescription);
}

export function buildSidebarHistorySections(conversations) {
    const normalized = conversations
        .map(normalizeConversation)
        .sort(sortConversations);

    const pinnedItems = normalized.filter((conversation) => conversation.isPinned);
    const projectGroups = normalized
        .filter((conversation) => !conversation.isPinned && conversation.projectName)
        .reduce((map, conversation) => {
            if (!map.has(conversation.projectName)) {
                map.set(conversation.projectName, []);
            }

            map.get(conversation.projectName).push(conversation);
            return map;
        }, new Map());

    const dateGroups = normalized
        .filter((conversation) => !conversation.isPinned && !conversation.projectName)
        .reduce((map, conversation) => {
            const label = getRelativeDateLabel(conversation.updatedAt);
            if (!map.has(label)) {
                map.set(label, []);
            }

            map.get(label).push(conversation);
            return map;
        }, new Map());

    const sections = [];

    if (pinnedItems.length > 0) {
        sections.push({
            id: "pinned",
            label: "Pinned",
            items: pinnedItems
        });
    }

    const projectSections = Array.from(projectGroups.entries())
        .sort(([left], [right]) => left.localeCompare(right))
        .map(([label, items]) => ({
            id: `project:${label}`,
            label,
            items
        }));

    sections.push(...projectSections);

    const dateSections = Array.from(dateGroups.entries()).map(([label, items]) => ({
        id: `date:${label}`,
        label,
        items
    }));

    sections.push(...dateSections);
    return sections;
}

export function getProjectNames(conversations) {
    return Array.from(new Set(
        conversations
            .map((conversation) => normalizeConversation(conversation).projectName)
            .filter(Boolean)))
        .sort((left, right) => left.localeCompare(right));
}

function sortConversations(left, right) {
    const leftPinned = left.isPinned ? 1 : 0;
    const rightPinned = right.isPinned ? 1 : 0;

    if (leftPinned !== rightPinned) {
        return rightPinned - leftPinned;
    }

    return new Date(right.updatedAt).getTime() - new Date(left.updatedAt).getTime();
}
