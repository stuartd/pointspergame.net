(function () {
    const modal = document.getElementById("components-reconnect-modal");
    let reloading = false;

    modal.addEventListener("components-reconnect-state-changed", function (event) {
        // A restarted server or expired session requires a fresh page and circuit.
        if (!reloading && (event.detail.state === "rejected" || event.detail.state === "resume-failed")) {
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
