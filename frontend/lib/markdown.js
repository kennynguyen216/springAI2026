import { UI_TEXT } from "../constants/ui-text.js";

const markedInstance = window.marked;
const hljsInstance = window.hljs;

markedInstance.setOptions({
    gfm: true,
    breaks: true
});

export function renderMarkdown(markdown) {
    return markedInstance.parse(markdown ?? "");
}

export function enhanceRenderedMarkdown(container) {
    const codeBlocks = container.querySelectorAll("pre code");
    codeBlocks.forEach((codeBlock) => {
        hljsInstance.highlightElement(codeBlock);

        const pre = codeBlock.closest("pre");
        if (!pre || pre.dataset.enhanced === "true") {
            return;
        }

        pre.dataset.enhanced = "true";

        const wrapper = document.createElement("div");
        wrapper.className = "code-block";

        const header = document.createElement("div");
        header.className = "code-block__header";

        const languageLabel = document.createElement("span");
        languageLabel.className = "code-block__language";
        languageLabel.textContent = detectLanguage(codeBlock.className);

        const copyButton = document.createElement("button");
        copyButton.type = "button";
        copyButton.className = "code-block__copy";
        copyButton.textContent = UI_TEXT.messages.copyCode;
        copyButton.addEventListener("click", async () => {
            await navigator.clipboard.writeText(codeBlock.textContent ?? "");
            copyButton.textContent = UI_TEXT.messages.copiedCode;

            window.setTimeout(() => {
                copyButton.textContent = UI_TEXT.messages.copyCode;
            }, 1600);
        });

        header.appendChild(languageLabel);
        header.appendChild(copyButton);
        wrapper.appendChild(header);
        pre.replaceWith(wrapper);
        wrapper.appendChild(pre);
    });
}

function detectLanguage(className) {
    const match = className.match(/language-([\w-]+)/);
    return match?.[1] ?? "code";
}
