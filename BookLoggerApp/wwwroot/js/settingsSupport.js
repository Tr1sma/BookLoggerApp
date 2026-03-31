window.settingsSupport = window.settingsSupport || {
    loadBuyMeACoffeeButton: function (containerId, fallbackId) {
        const container = document.getElementById(containerId);
        const fallback = document.getElementById(fallbackId);

        if (!container || container.dataset.bmcInitialized === "true") {
            return;
        }

        container.dataset.bmcInitialized = "true";

        const hasRenderedWidget = function () {
            return Array.from(container.childNodes).some(function (node) {
                return node.nodeType === Node.ELEMENT_NODE && node.tagName !== "SCRIPT";
            });
        };

        const hideFallbackIfReady = function () {
            if (fallback && hasRenderedWidget()) {
                fallback.style.display = "none";
                return true;
            }

            return false;
        };

        const observer = new MutationObserver(function () {
            if (hideFallbackIfReady()) {
                observer.disconnect();
            }
        });

        observer.observe(container, { childList: true, subtree: true });

        const script = document.createElement("script");
        script.type = "text/javascript";
        script.src = "https://cdnjs.buymeacoffee.com/1.0.0/button.prod.min.js";
        script.async = true;
        script.setAttribute("data-name", "bmc-button");
        script.setAttribute("data-slug", "tristanatzk");
        script.setAttribute("data-color", "#803900");
        script.setAttribute("data-emoji", "");
        script.setAttribute("data-font", "Cookie");
        script.setAttribute("data-text", "Buy me a coffee");
        script.setAttribute("data-outline-color", "#ffffff");
        script.setAttribute("data-font-color", "#ffffff");
        script.setAttribute("data-coffee-color", "#FFDD00");
        script.onerror = function () {
            observer.disconnect();
        };

        container.appendChild(script);

        window.setTimeout(function () {
            hideFallbackIfReady();
            observer.disconnect();
        }, 4000);
    }
};
