/**
 * Animation Control Module
 * Pauses CSS animations when page is not visible to improve performance
 */

(function () {
    'use strict';

    // Track visibility state
    let isPageVisible = true;

    /**
     * Initialize the animation control system
     */
    function init() {
        // Listen for visibility changes
        document.addEventListener('visibilitychange', handleVisibilityChange);

        // Initial state
        handleVisibilityChange();

        console.log('[AnimationControl] Initialized');
    }

    /**
     * Handle page visibility changes
     */
    function handleVisibilityChange() {
        isPageVisible = !document.hidden;

        if (isPageVisible) {
            resumeAnimations();
        } else {
            pauseAnimations();
        }
    }

    /**
     * Pause all animations by adding class to body
     */
    function pauseAnimations() {
        document.body.classList.add('animation-paused');
        console.log('[AnimationControl] Animations paused');
    }

    /**
     * Resume all animations by removing class from body
     */
    function resumeAnimations() {
        document.body.classList.remove('animation-paused');
        console.log('[AnimationControl] Animations resumed');
    }

    /**
     * Manually pause animations (callable from Blazor)
     */
    window.AnimationControl = {
        pause: pauseAnimations,
        resume: resumeAnimations,
        isVisible: function() { return isPageVisible; }
    };

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
