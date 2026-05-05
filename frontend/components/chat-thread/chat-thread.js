import { UI_TEXT } from "../../constants/ui-text.js";
import { enhanceRenderedMarkdown, renderMarkdown } from "../../lib/markdown.js";

/**
 * Renders the chat thread and owns viewport behaviors such as smart auto-scroll.
 */
export function createChatThread({
    messages,
    autoScrollEnabled,
    savedScrollTop,
    showJumpToBottom,
    onScrollStateChange,
    onJumpToBottom
}) {
    const section = document.createElement("section");
    section.className = "chat-thread";

    const list = document.createElement("div");
    list.className = "chat-thread__list";

    messages.forEach((message) => {
        list.appendChild(createMessageBubble(message));
    });

    section.appendChild(list);
    section.addEventListener("scroll", () => {
        const nearBottom = section.scrollHeight - (section.scrollTop + section.clientHeight) < 56;
        onScrollStateChange({
            autoScrollEnabled: nearBottom,
            scrollTop: section.scrollTop
        });
    });

    if (showJumpToBottom) {
        const jumpButton = document.createElement("button");
        jumpButton.type = "button";
        jumpButton.className = "chat-thread__jump";
        jumpButton.textContent = "Jump to bottom";
        jumpButton.addEventListener("click", onJumpToBottom);
        section.appendChild(jumpButton);
    }

    requestAnimationFrame(() => {
        if (autoScrollEnabled) {
            section.scrollTop = section.scrollHeight;
        } else {
            section.scrollTop = savedScrollTop ?? section.scrollTop;
        }
    });

    return section;
}

function createMessageBubble(message) {
    const article = document.createElement("article");
    article.className = `message-bubble message-bubble--${message.role}`;

    if (message.isBlocked) {
        article.classList.add("message-bubble--blocked");
    }

    if (message.isError) {
        article.classList.add("message-bubble--error");
    }

    if (message.isStatus) {
        article.classList.add("message-bubble--status");
    }

    const meta = document.createElement("div");
    meta.className = "message-bubble__meta";
    meta.textContent = message.role === "assistant" ? UI_TEXT.messages.assistantLabel : UI_TEXT.messages.userLabel;

    const body = document.createElement("div");
    body.className = "message-bubble__body";

    if (message.role === "assistant" && message.isStreaming && !message.hasStreamedText) {
        body.appendChild(createTypingIndicator());
    } else if (message.role === "assistant" && message.isStreaming) {
        body.appendChild(createStreamingText(message.content));
    } else if (message.role === "assistant") {
        body.innerHTML = renderMarkdown(message.content);
        enhanceRenderedMarkdown(body);
    } else {
        body.textContent = message.content;
    }

    article.appendChild(meta);
    article.appendChild(body);

    if (message.isBlocked) {
        const notice = document.createElement("div");
        notice.className = "message-bubble__blocked-note";
        notice.innerHTML = `
            <strong>${UI_TEXT.status.blockedLabel}</strong>
            <span>${UI_TEXT.status.blockedDescription}</span>
        `;
        article.appendChild(notice);
    }

    return article;
}

function createTypingIndicator() {
    const wrapper = document.createElement("div");
    wrapper.className = "typing-indicator typing-indicator--inline";
    wrapper.innerHTML = `
        <div class="typing-indicator__dots">
            <span></span>
            <span></span>
            <span></span>
        </div>
    `;
    return wrapper;
}

function createStreamingText(text) {
    const wrapper = document.createElement("div");
    wrapper.className = "message-bubble__streaming";
    wrapper.textContent = text;

    const cursor = document.createElement("span");
    cursor.className = "message-bubble__cursor";
    cursor.textContent = " ";
    wrapper.appendChild(cursor);

    return wrapper;
}
