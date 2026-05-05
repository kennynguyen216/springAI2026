export function createEmptyState({ title, description, promptCards, onPromptSelected }) {
    const section = document.createElement("section");
    section.className = "empty-state";

    const hero = document.createElement("div");
    hero.className = "empty-state__hero";
    hero.innerHTML = `
        <h1 class="empty-state__title">${title}</h1>
        <p class="empty-state__description">${description}</p>
    `;

    const grid = document.createElement("div");
    grid.className = "empty-state__grid";

    promptCards.forEach((card) => {
        const button = document.createElement("button");
        button.type = "button";
        button.className = "empty-state__card";
        button.innerHTML = `
            <div class="empty-state__card-title">${card.title}</div>
            <div class="empty-state__card-body">${card.prompt}</div>
        `;
        button.addEventListener("click", () => onPromptSelected(card.prompt));
        grid.appendChild(button);
    });

    section.appendChild(hero);
    section.appendChild(grid);
    return section;
}
