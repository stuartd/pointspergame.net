const measuredWidths = new Map();
const activeSizers = new WeakMap();
document.fonts.addEventListener("loadingdone", () => measuredWidths.clear());

export function sizeTeamColumn(table, teamNames) {
    if (!table?.isConnected || teamNames.length === 0) {
        return;
    }

    activeSizers.get(table)?.();

    const container = table.closest(".table-scroll");
    const shell = table.closest(".site-shell");
    const cell = table.querySelector("tbody .team-column");
    if (!container || !shell || !cell) {
        return;
    }

    const namesKey = JSON.stringify(teamNames);
    const canvas = document.createElement("canvas");
    const context = canvas.getContext("2d");
    if (!context) {
        return;
    }

    let disposed = false;

    function updateWidth() {
        if (disposed || !table.isConnected) {
            return;
        }

        const style = getComputedStyle(cell);
        const font = style.font;
        const markerMargin = parseFloat(getComputedStyle(document.documentElement).fontSize) * 0.15;
        const padding = parseFloat(style.paddingLeft) + parseFloat(style.paddingRight);
        const key = `${font}:${padding}:${markerMargin}:${namesKey}`;
        let width = measuredWidths.get(key);
        if (width === undefined) {
            context.font = font;
            const textWidth = Math.max(...teamNames.map(name => context.measureText(name).width));

            // Leave room for deduction references in this league or group.
            context.font = `${parseFloat(style.fontSize) * 0.75}px ${style.fontFamily}`;
            const markerWidth = context.measureText(String(teamNames.length)).width;
            width = Math.ceil(textWidth + markerWidth + markerMargin + padding);
            measuredWidths.set(key, width);
        }

        table.style.setProperty("--measured-team-column-width", `${width}px`);
    }

    const resizeObserver = new ResizeObserver(updateWidth);
    resizeObserver.observe(container);

    const removalObserver = new MutationObserver(() => {
        if (!table.isConnected) {
            dispose();
        }
    });
    removalObserver.observe(shell.parentNode, { childList: true, subtree: true });

    function dispose() {
        if (disposed) {
            return;
        }

        disposed = true;
        resizeObserver.disconnect();
        removalObserver.disconnect();
        document.fonts.removeEventListener("loadingdone", updateWidth);
        activeSizers.delete(table);
    }

    activeSizers.set(table, dispose);
    document.fonts.ready.then(updateWidth);
    document.fonts.addEventListener("loadingdone", updateWidth);
    updateWidth();
}
