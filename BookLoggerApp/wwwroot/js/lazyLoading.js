window.lazyLoading = {
    observer: null,

    init: function () {
        const options = {
            root: null, // viewport
            rootMargin: '100px', // load images 100px before they come into view
            threshold: 0.1
        };

        this.observer = new IntersectionObserver((entries, observer) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    const element = entry.target;
                    // Trigger the DotNet method attached to this element
                    if (element._dotNetHelper) {
                        element._dotNetHelper.invokeMethodAsync('LoadImage');
                        // Stop observing once triggered
                        observer.unobserve(element);
                        element._dotNetHelper = null; // Cleanup
                    }
                }
            });
        }, options);
    },

    observe: function (element, dotNetHelper) {
        if (!this.observer) {
            this.init();
        }
        if (element) {
            element._dotNetHelper = dotNetHelper;
            this.observer.observe(element);
        }
    },

    unobserve: function (element) {
        if (element && this.observer) {
            this.observer.unobserve(element);
            if (element._dotNetHelper) {
                element._dotNetHelper = null;
            }
        }
    }
};
