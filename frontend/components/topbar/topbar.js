import { UI_TEXT } from "../../constants/ui-text.js";

export function createTopBar({
    title,
    theme,
    onToggleSidebar,
    onToggleTheme,
    userInitials
}) {
    const header = document.createElement("header");
    header.className = "topbar";

    const left = document.createElement("div");
    left.className = "topbar__left";

    const toggle = document.createElement("button");
    toggle.type = "button";
    toggle.className = "topbar__icon-button";
    toggle.setAttribute("aria-label", UI_TEXT.topbar.toggleSidebar);
    toggle.textContent = "☰";
    toggle.addEventListener("click", onToggleSidebar);

    const context = document.createElement("div");
    context.className = "topbar__context";
    context.innerHTML = `
        <div class="topbar__title">${title}</div>
    `;

    const right = document.createElement("div");
    right.className = "topbar__right";

    const themeButton = document.createElement("button");
    themeButton.type = "button";
    themeButton.className = "topbar__theme-toggle";
    themeButton.textContent = theme === "dark" ? UI_TEXT.topbar.themeLight : UI_TEXT.topbar.themeDark;
    themeButton.addEventListener("click", onToggleTheme);

    const profile = document.createElement("button");
    profile.type = "button";
    profile.className = "topbar__profile";
    profile.setAttribute("aria-label", UI_TEXT.topbar.profileLabel);
    profile.textContent = userInitials;

    left.appendChild(toggle);
    left.appendChild(context);
    right.appendChild(themeButton);
    right.appendChild(profile);
    header.appendChild(left);
    header.appendChild(right);

    return header;
}
