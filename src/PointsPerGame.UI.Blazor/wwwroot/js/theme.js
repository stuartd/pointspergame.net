(function () {
    const storageKey = "points-per-game-theme";
    const supportedThemes = ["system", "light", "dark"];

    function getPreference() {
        try {
            const storedTheme = localStorage.getItem(storageKey);
            return supportedThemes.includes(storedTheme) ? storedTheme : "system";
        } catch {
            return "system";
        }
    }

    function applyPreference(theme) {
        if (theme === "system") {
            document.documentElement.removeAttribute("data-theme");
            return;
        }

        document.documentElement.setAttribute("data-theme", theme);
    }

    function setPreference(theme) {
        if (!supportedThemes.includes(theme)) {
            return;
        }

        try {
            if (theme === "system") {
                localStorage.removeItem(storageKey);
            } else {
                localStorage.setItem(storageKey, theme);
            }
        } catch {
            // The selected theme still applies for this page when storage is unavailable.
        }

        applyPreference(theme);
    }

    window.ppgTheme = { getPreference, setPreference };
    applyPreference(getPreference());
})();
