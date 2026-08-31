(function () {
    const mode = new URLSearchParams(window.location.search).get("mode");
    if (mode === "light" || mode === "dark") {
        document.documentElement.setAttribute("data-theme", mode);
    }
})();
