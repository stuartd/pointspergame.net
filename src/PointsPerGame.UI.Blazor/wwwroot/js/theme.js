(function () {
    const storageKey = "points-per-game-theme";
    const supportedModes = ["light", "dark"];
    const requestedMode = new URLSearchParams(window.location.search).get("mode");
    let mode = requestedMode;

    if (requestedMode === "system") {
        try {
            localStorage.removeItem(storageKey);
        } catch {
            // The system theme still applies when storage is unavailable.
        }

        document.documentElement.removeAttribute("data-theme");
        return;
    }

    if (supportedModes.includes(requestedMode)) {
        try {
            localStorage.setItem(storageKey, requestedMode);
        } catch {
            // The requested theme still applies for this page when storage is unavailable.
        }
    } else {
        try {
            mode = localStorage.getItem(storageKey);
        } catch {
            mode = null;
        }
    }

    if (supportedModes.includes(mode)) {
        document.documentElement.setAttribute("data-theme", mode);
    }
})();
