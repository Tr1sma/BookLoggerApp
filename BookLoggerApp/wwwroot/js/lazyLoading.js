window.lazyLoading = {
    observer: null,
    _queue: [],
    _rafScheduled: false,

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
            this._queue.push(element);
            this._scheduleFlush();
        }
    },

    _scheduleFlush: function () {
        if (this._rafScheduled) return;
        this._rafScheduled = true;
        requestAnimationFrame(() => {
            this._flush();
        });
    },

    _flush: function () {
        const queue = this._queue;
        for (let i = 0; i < queue.length; i++) {
            this.observer.observe(queue[i]);
        }
        this._queue = [];
        this._rafScheduled = false;
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
