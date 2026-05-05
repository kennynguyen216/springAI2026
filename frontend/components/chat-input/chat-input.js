export function createChatInput({
    placeholder,
    disabled,
    loading,
    sendLabel,
    hint,
    onSend,
    variant = "default"
}) {
    const form = document.createElement("form");
    form.className = `chat-input chat-input--${variant} ${disabled ? "chat-input--disabled" : ""}`;

    const bar = document.createElement("div");
    bar.className = "chat-input__bar";

    const textarea = document.createElement("textarea");
    textarea.className = "chat-input__field";
    textarea.placeholder = placeholder;
    textarea.rows = 1;
    textarea.disabled = disabled;

    textarea.addEventListener("input", () => autoSizeTextArea(textarea));
    textarea.addEventListener("keydown", (event) => {
        if (event.key === "Enter" && !event.shiftKey) {
            event.preventDefault();
            submit();
        }
    });

    const sendButton = document.createElement("button");
    sendButton.type = "submit";
    sendButton.className = "chat-input__send";
    sendButton.textContent = loading ? "..." : sendLabel;
    sendButton.disabled = disabled;

    form.addEventListener("submit", (event) => {
        event.preventDefault();
        submit();
    });

    bar.appendChild(textarea);
    bar.appendChild(sendButton);
    if (hint) {
        const hintLabel = document.createElement("div");
        hintLabel.className = "chat-input__hint";
        hintLabel.textContent = hint;
        form.appendChild(hintLabel);
    }

    form.appendChild(bar);

    requestAnimationFrame(() => {
        if (!disabled) {
            textarea.focus();
        }
    });

    return form;

    function submit() {
        if (disabled) {
            return;
        }

        const value = textarea.value.trim();
        if (!value) {
            return;
        }

        onSend(value);
        textarea.value = "";
        autoSizeTextArea(textarea);
    }
}

function autoSizeTextArea(textarea) {
    textarea.style.height = "auto";
    textarea.style.height = `${Math.min(textarea.scrollHeight, 220)}px`;
}
