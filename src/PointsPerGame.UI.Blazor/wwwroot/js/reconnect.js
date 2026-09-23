(function () {
    const modal = document.getElementById("components-reconnect-modal");
    let reloading = false;
    let leavingPage = false;

    function hideForNavigation() {
        leavingPage = true;
        modal.style.display = "none";
    }

    // Navigation can close the connection before the next page has loaded.
    window.addEventListener("beforeunload", hideForNavigation);
    window.addEventListener("pagehide", hideForNavigation);
    window.addEventListener("pageshow", function () {
        leavingPage = false;
        modal.style.removeProperty("display");
    });

    modal.addEventListener("components-reconnect-state-changed", function (event) {
        // A restarted server or expired session requires a fresh page and circuit.
        if (!leavingPage && !reloading && (event.detail.state === "rejected" || event.detail.state === "resume-failed")) {
            reloading = true;
            window.location.reload();
        }
    });

    Blazor.start({
        reconnectionOptions: {
            // Keep Blazor's retry backoff, but allow recovery after long outages.
            maxRetries: undefined
        }
    });
})();
