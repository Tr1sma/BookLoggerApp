window.bookshelfDragDrop = {
    _dotNetRef: null,
    _container: null,
    _longPressTimer: null,
    _longPressDelay: 400,
    _dragActive: false,
    _ghost: null,
    _draggedSlot: null,
    _startX: 0,
    _startY: 0,
    _pointerId: null,
    _moveThreshold: 10,
    _longPressTriggered: false,
    _dropTarget: null,
    _dropIndicator: null,
    _autoScrollRaf: null,
    _autoScrollEdge: 60,
    _autoScrollSpeed: 8,
    _boundTouchMovePrevent: null,
    _rafId: null,
    _lastMoveX: 0,
    _lastMoveY: 0,
    _dropTargetCache: null,
    _dropTargetCacheDirty: false,
    _activeDropHighlight: null,

    // ─── Public API ───────────────────────────────────────

    init: function (dotNetRef) {
        this.dispose();
        this._dotNetRef = dotNetRef;
        this._container = document.querySelector('.shelves-container');
        if (!this._container) return;

        this._boundPointerDown = this._onPointerDown.bind(this);
        this._boundPointerMove = this._onPointerMove.bind(this);
        this._boundPointerUp = this._onPointerUp.bind(this);
        this._boundPointerCancel = this._onPointerCancel.bind(this);
        this._boundContextMenu = this._onContextMenu.bind(this);

        this._container.addEventListener('pointerdown', this._boundPointerDown);
        document.addEventListener('pointermove', this._boundPointerMove);
        document.addEventListener('pointerup', this._boundPointerUp);
        document.addEventListener('pointercancel', this._boundPointerCancel);
        document.addEventListener('contextmenu', this._boundContextMenu);
    },

    dispose: function () {
        this._cancelLongPress();
        this._cleanup();

        if (this._container) {
            this._container.removeEventListener('pointerdown', this._boundPointerDown);
        }
        document.removeEventListener('pointermove', this._boundPointerMove);
        document.removeEventListener('pointerup', this._boundPointerUp);
        document.removeEventListener('pointercancel', this._boundPointerCancel);
        document.removeEventListener('contextmenu', this._boundContextMenu);

        this._dotNetRef = null;
        this._container = null;
    },

    // ─── Event Handlers ───────────────────────────────────

    _onPointerDown: function (e) {
        // Only primary button (left click / single touch)
        if (e.button !== 0) return;

        // Ignore interactive elements
        var tag = e.target.tagName.toLowerCase();
        if (['button', 'input', 'select', 'textarea', 'a'].indexOf(tag) !== -1 ||
            e.target.closest('button, input, select, textarea, a, .btn-icon, .shelf-reorder-controls, .shelf-actions')) {
            return;
        }

        // Find the shelf-slot ancestor
        var slot = e.target.closest('.shelf-slot');
        if (!slot) return;

        // Check if this slot is inside an auto-sort shelf
        var section = slot.closest('.shelf-section');
        if (section && section.dataset.autoSort === 'true') return;

        this._startX = e.clientX;
        this._startY = e.clientY;
        this._pointerId = e.pointerId;
        this._draggedSlot = slot;
        this._longPressTriggered = false;

        // Visual feedback: slight scale on press
        slot.classList.add('long-press-active');

        // Start long-press timer
        var self = this;
        this._longPressTimer = setTimeout(function () {
            self._longPressTriggered = true;
            self._activateDrag(slot, self._startX, self._startY, self._pointerId);
        }, this._longPressDelay);
    },

    _onPointerMove: function (e) {
        if (!this._draggedSlot) return;

        // Before drag activation: check if user moved too far (= scrolling)
        if (!this._dragActive) {
            var dx = e.clientX - this._startX;
            var dy = e.clientY - this._startY;
            if (Math.sqrt(dx * dx + dy * dy) > this._moveThreshold) {
                this._cancelLongPress();
                if (this._draggedSlot) {
                    this._draggedSlot.classList.remove('long-press-active');
                }
                this._draggedSlot = null;
            }
            return;
        }

        // Active drag: store latest coordinates, schedule rAF
        e.preventDefault();
        this._lastMoveX = e.clientX;
        this._lastMoveY = e.clientY;

        if (!this._rafId) {
            var self = this;
            this._rafId = requestAnimationFrame(function () {
                self._rafId = null;
                if (!self._dragActive) return;
                self._moveGhost(self._lastMoveX, self._lastMoveY);
                self._findDropTargetFromCache(self._lastMoveX, self._lastMoveY);
                self._handleAutoScroll(self._lastMoveY);
            });
        }
    },

    _onPointerUp: function (e) {
        this._cancelLongPress();

        if (this._draggedSlot) {
            this._draggedSlot.classList.remove('long-press-active');
        }

        if (this._dragActive) {
            this._completeDrop();
        }

        // Reset state
        this._draggedSlot = null;
        this._pointerId = null;
    },

    _onPointerCancel: function (e) {
        // Browser took over the gesture (e.g. scrolling).
        // If drag was active, this should not happen (touchmove prevention should block it),
        // but as a safety net, complete the drop with whatever target we have.
        this._cancelLongPress();

        if (this._draggedSlot) {
            this._draggedSlot.classList.remove('long-press-active');
        }

        if (this._dragActive) {
            this._completeDrop();
        }

        this._draggedSlot = null;
        this._pointerId = null;
    },

    _onContextMenu: function (e) {
        // Suppress context menu during long-press / drag
        if (this._longPressTriggered || this._dragActive) {
            e.preventDefault();
        }
    },

    // ─── Touch scroll prevention ──────────────────────────
    // The browser decides at touchstart time (based on CSS touch-action)
    // whether to handle scrolling. Since we activate drag 400ms later,
    // touch-action: none set at that point is too late. Instead, we use
    // a non-passive touchmove listener that calls preventDefault() to
    // stop the browser from scrolling (and firing pointercancel).

    _preventTouchMove: function (e) {
        e.preventDefault();
    },

    _startTouchMovePrevention: function () {
        if (!this._boundTouchMovePrevent) {
            this._boundTouchMovePrevent = this._preventTouchMove.bind(this);
        }
        document.addEventListener('touchmove', this._boundTouchMovePrevent, { passive: false });
    },

    _stopTouchMovePrevention: function () {
        if (this._boundTouchMovePrevent) {
            document.removeEventListener('touchmove', this._boundTouchMovePrevent);
        }
    },

    // ─── Drag Activation ──────────────────────────────────

    _activateDrag: function (slot, x, y, pointerId) {
        this._dragActive = true;

        // CRITICAL: Prevent browser from stealing the touch gesture for scrolling.
        // This must happen before the user starts moving, so the browser's
        // touchmove handler sees our preventDefault() call.
        this._startTouchMovePrevention();

        // Capture pointer for reliable tracking even outside container
        try {
            this._container.setPointerCapture(pointerId);
        } catch (_) { /* ignore if already released */ }

        // Create ghost
        this._createGhost(slot, x, y);

        // Cache drop target positions for fast hit-testing during drag
        this._buildDropTargetCache();
        this._dropTargetCacheDirty = false;

        // Mark original as dragging
        slot.classList.remove('long-press-active');
        slot.classList.add('dragging');

        // Prevent text selection during drag
        document.body.classList.add('drag-active');

        // Haptic feedback
        if (navigator.vibrate) {
            navigator.vibrate(30);
        }
    },

    _createGhost: function (slot, x, y) {
        var rect = slot.getBoundingClientRect();
        var ghost = slot.cloneNode(true);
        ghost.className = 'drag-ghost';
        ghost.style.width = rect.width + 'px';
        ghost.style.height = rect.height + 'px';
        ghost.style.left = '0px';
        ghost.style.top = '0px';
        ghost.style.transform = 'translate(' + (x - rect.width / 2) + 'px, ' + (y - rect.height / 2) + 'px) scale(1.05) rotate(2deg)';
        document.body.appendChild(ghost);
        this._ghost = ghost;
        this._ghostOffsetX = rect.width / 2;
        this._ghostOffsetY = rect.height / 2;
    },

    _moveGhost: function (x, y) {
        if (!this._ghost) return;
        this._ghost.style.transform = 'translate(' + (x - this._ghostOffsetX) + 'px, ' + (y - this._ghostOffsetY) + 'px) scale(1.05) rotate(2deg)';
    },

    // ─── Drop Target Cache & Detection ───────────────────

    _buildDropTargetCache: function () {
        var slots = this._container.querySelectorAll('.shelf-slot');
        var sections = this._container.querySelectorAll('.shelf-section');
        var cache = { slots: [], sections: [] };

        for (var i = 0; i < slots.length; i++) {
            var slot = slots[i];
            if (slot === this._draggedSlot) continue;
            if (this._isInAutoSortShelf(slot)) continue;
            var rect = slot.getBoundingClientRect();
            cache.slots.push({
                element: slot,
                left: rect.left,
                right: rect.right,
                top: rect.top,
                bottom: rect.bottom,
                centerX: rect.left + rect.width / 2
            });
        }

        for (var j = 0; j < sections.length; j++) {
            var section = sections[j];
            if (section.dataset.autoSort === 'true') continue;
            var sRect = section.getBoundingClientRect();
            cache.sections.push({
                element: section,
                left: sRect.left,
                right: sRect.right,
                top: sRect.top,
                bottom: sRect.bottom
            });
        }

        this._dropTargetCache = cache;
    },

    _findDropTargetFromCache: function (x, y) {
        // Rebuild cache if invalidated (e.g. by auto-scroll)
        if (this._dropTargetCacheDirty) {
            this._buildDropTargetCache();
            this._dropTargetCacheDirty = false;
        }

        var cache = this._dropTargetCache;
        if (!cache) return;

        // Clear previous highlight (tracked single element, no querySelectorAll)
        this._clearDropHighlightsFast();

        var targetSlot = null;
        var insertBefore = false;

        // Simple AABB hit-test against cached rects
        for (var i = 0; i < cache.slots.length; i++) {
            var s = cache.slots[i];
            if (x >= s.left && x <= s.right && y >= s.top && y <= s.bottom) {
                targetSlot = s;
                insertBefore = x < s.centerX;
                break;
            }
        }

        if (targetSlot) {
            this._showDropIndicatorFast(targetSlot.element, insertBefore);
            this._dropTarget = {
                type: 'slot',
                element: targetSlot.element,
                insertBefore: insertBefore
            };
            return;
        }

        // Check section hit (dropping on empty shelf area)
        var targetSection = null;
        for (var j = 0; j < cache.sections.length; j++) {
            var sec = cache.sections[j];
            if (x >= sec.left && x <= sec.right && y >= sec.top && y <= sec.bottom) {
                targetSection = sec;
                break;
            }
        }

        if (targetSection) {
            targetSection.element.classList.add('drop-target-active');
            this._activeDropHighlight = targetSection.element;
            this._dropTarget = {
                type: 'section',
                element: targetSection.element
            };
        } else {
            this._dropTarget = null;
        }
    },

    _showDropIndicatorFast: function (targetSlot, before) {
        var parent = targetSlot.parentElement;
        if (!parent) return;

        // Create indicator once, reuse it across frames
        if (!this._dropIndicator) {
            this._dropIndicator = document.createElement('div');
            this._dropIndicator.className = 'drop-indicator';
        }

        parent.style.position = 'relative';

        // Use cached rects from the cache when possible
        var slotRect = targetSlot.getBoundingClientRect();
        var parentRect = parent.getBoundingClientRect();

        if (before) {
            this._dropIndicator.style.left = (slotRect.left - parentRect.left - 2) + 'px';
        } else {
            this._dropIndicator.style.left = (slotRect.right - parentRect.left - 1) + 'px';
        }

        // Only re-append if moved to a different parent
        if (this._dropIndicator.parentNode !== parent) {
            if (this._dropIndicator.parentNode) {
                this._dropIndicator.parentNode.removeChild(this._dropIndicator);
            }
            parent.appendChild(this._dropIndicator);
        }
    },

    _clearDropHighlightsFast: function () {
        // Remove indicator from DOM (but keep the element for reuse)
        if (this._dropIndicator && this._dropIndicator.parentNode) {
            this._dropIndicator.parentNode.removeChild(this._dropIndicator);
        }
        // Clear tracked highlight (single element, no querySelectorAll)
        if (this._activeDropHighlight) {
            this._activeDropHighlight.classList.remove('drop-target-active');
            this._activeDropHighlight = null;
        }
    },

    _removeDropIndicator: function () {
        if (this._dropIndicator && this._dropIndicator.parentNode) {
            this._dropIndicator.parentNode.removeChild(this._dropIndicator);
        }
        this._dropIndicator = null;
    },

    _clearDropHighlights: function () {
        this._removeDropIndicator();
        if (this._activeDropHighlight) {
            this._activeDropHighlight.classList.remove('drop-target-active');
            this._activeDropHighlight = null;
        }
        // Fallback: catch any stale highlights
        var actives = document.querySelectorAll('.drop-target-active');
        for (var i = 0; i < actives.length; i++) {
            actives[i].classList.remove('drop-target-active');
        }
    },

    _isInAutoSortShelf: function (element) {
        var section = element.closest('.shelf-section');
        return section && section.dataset.autoSort === 'true';
    },

    // ─── Auto-Scroll ──────────────────────────────────────

    _handleAutoScroll: function (y) {
        var viewH = window.innerHeight;
        var shouldScroll = false;
        var direction = 0;

        if (y < this._autoScrollEdge) {
            shouldScroll = true;
            direction = -1;
        } else if (y > viewH - this._autoScrollEdge) {
            shouldScroll = true;
            direction = 1;
        }

        if (shouldScroll && !this._autoScrollRaf) {
            var self = this;
            var scroll = function () {
                window.scrollBy(0, self._autoScrollSpeed * direction);
                // Cached rects are stale after scrolling
                self._dropTargetCacheDirty = true;
                if (self._dragActive) {
                    self._autoScrollRaf = requestAnimationFrame(scroll);
                }
            };
            this._autoScrollRaf = requestAnimationFrame(scroll);
        } else if (!shouldScroll) {
            this._stopAutoScroll();
        }
    },

    _stopAutoScroll: function () {
        if (this._autoScrollRaf) {
            cancelAnimationFrame(this._autoScrollRaf);
            this._autoScrollRaf = null;
        }
    },

    // ─── Drop Completion ──────────────────────────────────

    _completeDrop: function () {
        if (!this._dotNetRef || !this._draggedSlot) {
            this._cleanup();
            return;
        }

        var sourceSlot = this._draggedSlot;
        var sourceItemId = sourceSlot.dataset.itemId;
        var sourceItemType = sourceSlot.dataset.itemType;
        var sourceSection = sourceSlot.closest('.shelf-section');
        var sourceShelfId = sourceSection ? sourceSection.dataset.shelfId : null;

        if (!sourceItemId || !sourceShelfId) {
            this._cleanup();
            return;
        }

        var target = this._dropTarget;
        var dotNetRef = this._dotNetRef;

        if (target && target.type === 'slot') {
            var targetSlot = target.element;
            var targetItemId = targetSlot.dataset.itemId;
            var targetSection = targetSlot.closest('.shelf-section');
            var targetShelfId = targetSection ? targetSection.dataset.shelfId : null;

            if (!targetItemId || !targetShelfId) {
                this._cleanup();
                return;
            }

            if (sourceShelfId === targetShelfId) {
                // Same shelf: reorder
                dotNetRef.invokeMethodAsync('OnReorderItem',
                    sourceShelfId, sourceItemId, sourceItemType, targetItemId)
                    .catch(function (err) { console.error('OnReorderItem failed:', err); });
            } else {
                // Different shelf: move between shelves
                var targetItems = targetSection.querySelectorAll('.shelf-slot');
                var position = -1;
                for (var i = 0; i < targetItems.length; i++) {
                    if (targetItems[i] === targetSlot) {
                        position = target.insertBefore ? i : i + 1;
                        break;
                    }
                }
                dotNetRef.invokeMethodAsync('OnMoveItemToShelf',
                    sourceShelfId, targetShelfId, sourceItemId, sourceItemType, position)
                    .catch(function (err) { console.error('OnMoveItemToShelf failed:', err); });
            }
        } else if (target && target.type === 'section') {
            var sectionShelfId = target.element.dataset.shelfId;
            if (sectionShelfId && sectionShelfId !== sourceShelfId) {
                // Append to end of target shelf
                dotNetRef.invokeMethodAsync('OnMoveItemToShelf',
                    sourceShelfId, sectionShelfId, sourceItemId, sourceItemType, -1)
                    .catch(function (err) { console.error('OnMoveItemToShelf failed:', err); });
            }
        }

        this._cleanup();
    },

    // ─── Cleanup ──────────────────────────────────────────

    _cancelLongPress: function () {
        if (this._longPressTimer) {
            clearTimeout(this._longPressTimer);
            this._longPressTimer = null;
        }
    },

    _cleanup: function () {
        // Cancel pending animation frame
        if (this._rafId) {
            cancelAnimationFrame(this._rafId);
            this._rafId = null;
        }

        // Stop preventing touch scrolling
        this._stopTouchMovePrevention();

        // Remove ghost
        if (this._ghost && this._ghost.parentNode) {
            this._ghost.parentNode.removeChild(this._ghost);
        }
        this._ghost = null;

        // Remove dragging class
        if (this._draggedSlot) {
            this._draggedSlot.classList.remove('dragging');
            this._draggedSlot.classList.remove('long-press-active');
        }

        // Release pointer capture
        if (this._pointerId != null && this._container) {
            try {
                this._container.releasePointerCapture(this._pointerId);
            } catch (_) { /* ignore */ }
        }

        // Clear highlights (full cleanup with fallback querySelectorAll)
        this._clearDropHighlights();

        // Stop auto-scroll
        this._stopAutoScroll();

        // Clear drop target cache
        this._dropTargetCache = null;

        // Reset body state
        document.body.classList.remove('drag-active');

        this._dragActive = false;
        this._dropTarget = null;
        this._longPressTriggered = false;
    }
};
