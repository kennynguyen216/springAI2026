/**
 * Creates the contextual action menu shown for a sidebar conversation.
 * The menu supports pinning, inline renaming, project assignment, and
 * destructive deletion confirmation without affecting the rest of the sidebar.
 */
export function createSidebarContextMenu({
    isPinned,
    projectName,
    isDeleteConfirming,
    isProjectPickerOpen,
    projectDraft,
    projectOptions,
    onTogglePin,
    onStartRename,
    onOpenProjectPicker,
    onProjectDraftChange,
    onSaveProject,
    onRequestDelete,
    onClose
}) {
    const wrapper = document.createElement("div");
    wrapper.className = "sidebar-context-menu";

    if (isProjectPickerOpen) {
        wrapper.appendChild(createProjectPicker({
            projectDraft,
            projectOptions,
            onProjectDraftChange,
            onSaveProject,
            onClose
        }));

        return wrapper;
    }

    wrapper.appendChild(createAction({
        icon: createIcon("pin"),
        label: isPinned ? "Unpin" : "Pin",
        onClick: onTogglePin
    }));

    wrapper.appendChild(createAction({
        icon: createIcon("rename"),
        label: "Rename",
        onClick: onStartRename
    }));

    wrapper.appendChild(createAction({
        icon: createIcon("project"),
        label: projectName ? `Add to Project · ${projectName}` : "Add to Project",
        onClick: onOpenProjectPicker
    }));

    wrapper.appendChild(createAction({
        icon: createIcon("delete"),
        label: isDeleteConfirming ? "Delete · Click again to confirm" : "Delete",
        onClick: onRequestDelete,
        destructive: true
    }));

    return wrapper;
}

function createAction({ icon, label, onClick, destructive = false }) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = `sidebar-context-menu__action ${
        destructive ? "sidebar-context-menu__action--destructive" : ""
    }`;
    button.addEventListener("click", onClick);

    const iconSlot = document.createElement("span");
    iconSlot.className = "sidebar-context-menu__icon";
    iconSlot.appendChild(icon);

    const labelSlot = document.createElement("span");
    labelSlot.className = "sidebar-context-menu__label";
    labelSlot.textContent = label;

    button.appendChild(iconSlot);
    button.appendChild(labelSlot);
    return button;
}

function createProjectPicker({
    projectDraft,
    projectOptions,
    onProjectDraftChange,
    onSaveProject,
    onClose
}) {
    const panel = document.createElement("div");
    panel.className = "sidebar-context-menu__project";

    const title = document.createElement("div");
    title.className = "sidebar-context-menu__project-title";
    title.textContent = "Assign project";

    const input = document.createElement("input");
    input.className = "sidebar-context-menu__input";
    input.placeholder = "Project name";
    input.value = projectDraft ?? "";
    input.addEventListener("input", (event) => {
        onProjectDraftChange(event.target.value);
    });
    input.addEventListener("keydown", (event) => {
        if (event.key === "Enter") {
            event.preventDefault();
            onSaveProject();
        }
    });

    const options = document.createElement("div");
    options.className = "sidebar-context-menu__project-options";

    const clearButton = document.createElement("button");
    clearButton.type = "button";
    clearButton.className = "sidebar-context-menu__chip";
    clearButton.textContent = "No project";
    clearButton.addEventListener("click", () => {
        onProjectDraftChange("");
        onSaveProject();
    });
    options.appendChild(clearButton);

    projectOptions.forEach((project) => {
        const chip = document.createElement("button");
        chip.type = "button";
        chip.className = "sidebar-context-menu__chip";
        chip.textContent = project;
        chip.addEventListener("click", () => {
            onProjectDraftChange(project);
            onSaveProject(project);
        });
        options.appendChild(chip);
    });

    const actions = document.createElement("div");
    actions.className = "sidebar-context-menu__project-actions";

    const cancelButton = document.createElement("button");
    cancelButton.type = "button";
    cancelButton.className = "sidebar-context-menu__secondary";
    cancelButton.textContent = "Back";
    cancelButton.addEventListener("click", onClose);

    const saveButton = document.createElement("button");
    saveButton.type = "button";
    saveButton.className = "sidebar-context-menu__primary";
    saveButton.textContent = "Save";
    saveButton.addEventListener("click", () => onSaveProject());

    actions.appendChild(cancelButton);
    actions.appendChild(saveButton);

    panel.appendChild(title);
    panel.appendChild(input);
    panel.appendChild(options);
    panel.appendChild(actions);

    requestAnimationFrame(() => input.focus());
    return panel;
}

function createIcon(kind) {
    const icon = document.createElementNS("http://www.w3.org/2000/svg", "svg");
    icon.setAttribute("viewBox", "0 0 24 24");
    icon.setAttribute("fill", "none");
    icon.setAttribute("stroke", "currentColor");
    icon.setAttribute("stroke-width", "1.8");
    icon.setAttribute("stroke-linecap", "round");
    icon.setAttribute("stroke-linejoin", "round");

    const path = document.createElementNS("http://www.w3.org/2000/svg", "path");

    switch (kind) {
        case "pin":
            path.setAttribute("d", "M8 4h8l-2.5 5v4l2 2H8.5l2-2V9L8 4Zm4 11v5");
            break;
        case "rename":
            path.setAttribute("d", "M4 20h4l10-10-4-4L4 16v4Zm8-12 4 4");
            break;
        case "project":
            path.setAttribute("d", "M4 7h6l2 2h8v9a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V7Z");
            break;
        default:
            path.setAttribute("d", "M6 7h12M9 7V5h6v2m-8 4v7m4-7v7m4-7v7M5 7l1 13h12l1-13");
            break;
    }

    icon.appendChild(path);
    return icon;
}
