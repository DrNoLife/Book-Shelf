// The only JavaScript in the client, and only for the things Blazor cannot do from
// C#: read a rendered layout, move focus between sibling elements, hand a
// dropped file to a file input, copy a link to the clipboard, ask whether the
// phone layout is showing, remember a per-browser display flag or the shelf's filters, and
// follow a finger dragging a bottom sheet down.
window.lnshelf = {
    // One entry per bottom sheet that listens for drags, so disposing it can remove exactly
    // the listeners it added.
    sheetDragDisposers: new WeakMap(),

    // Drag a bottom sheet down to close it. Pointer events cover mouse and touch alike; the
    // sheet follows the pointer with a transform (no layout work) and the scrim fades with it.
    // A drag starts only when the sheet is scrolled to its top and the pointer moves down, so
    // scrolling the content is never taken over. Letting go past a third of the sheet's
    // height, or with a quick downward flick, calls CloseFromDragAsync on the .NET owner,
    // which closes through the same path as Done, Escape and the scrim.
    registerSheetDrag: function (sheet, scrim, dotNetReference) {
        if (!sheet || window.lnshelf.sheetDragDisposers.has(sheet)) return;

        const dragThreshold = 6;       // px of downward travel before a press becomes a drag
        const closeFraction = 0.3;     // of the sheet's height
        const flickVelocity = 0.5;     // px per ms
        const flickMinimumDistance = 30;
        const flickWindow = 100;       // ms; older movement does not make a flick
        const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');

        let pointerId = null;
        let startY = 0;
        let offset = 0;
        let dragging = false;
        let lastY = 0;
        let lastTime = 0;
        let velocity = 0;
        let closing = false;

        const apply = distance => {
            offset = Math.max(0, distance);   // never above its resting place
            sheet.style.transform = offset === 0 ? '' : 'translateY(' + offset + 'px)';
            if (scrim) scrim.style.opacity = offset === 0 ? '' : String(Math.max(0, 1 - offset / sheet.offsetHeight));
        };

        const reset = () => {
            pointerId = null;
            dragging = false;
            sheet.classList.remove('sheet--dragging');
        };

        const snapBack = () => {
            if (!reducedMotion.matches && offset > 0) {
                const clear = () => {
                    sheet.style.transition = '';
                    if (scrim) scrim.style.transition = '';
                };
                sheet.style.transition = 'transform 200ms ease-out';
                if (scrim) scrim.style.transition = 'opacity 200ms ease-out';
                sheet.addEventListener('transitionend', clear, { once: true });
            }
            apply(0);
        };

        const stopClick = event => {
            event.stopPropagation();
            event.preventDefault();
        };

        const onPointerDown = event => {
            if (closing) return;

            // A second finger, or any pointer other than the one being followed, cancels
            // the drag rather than being mistaken for it.
            if (pointerId !== null) {
                if (dragging) snapBack();
                reset();
                return;
            }

            if (!event.isPrimary || (event.pointerType === 'mouse' && event.button !== 0)) return;
            if (sheet.scrollTop > 0) return;
            if (event.target.closest && event.target.closest('input, textarea, select')) return;

            pointerId = event.pointerId;
            startY = lastY = event.clientY;
            lastTime = event.timeStamp;
            velocity = 0;
        };

        const onPointerMove = event => {
            if (event.pointerId !== pointerId) return;

            const distance = event.clientY - startY;
            if (!dragging) {
                if (distance < dragThreshold) {
                    // Upward, or content scrolled meanwhile: this gesture belongs to the content.
                    if (distance < -dragThreshold || sheet.scrollTop > 0) reset();
                    return;
                }
                dragging = true;
                sheet.classList.add('sheet--dragging');
            }

            const elapsed = event.timeStamp - lastTime;
            if (elapsed > 0) velocity = (event.clientY - lastY) / elapsed;
            lastY = event.clientY;
            lastTime = event.timeStamp;
            apply(distance - dragThreshold);
        };

        const onPointerUp = event => {
            if (event.pointerId !== pointerId) return;

            // A finger that paused sends no pointermove, so the last speed may be stale: only a
            // movement within the final 100 ms counts as a flick.
            if (event.timeStamp - lastTime > flickWindow) velocity = 0;

            const wasDragging = dragging;
            reset();
            if (!wasDragging) return;

            // The release of a drag must not also count as a tap on whatever was under it.
            window.addEventListener('click', stopClick, { capture: true, once: true });
            setTimeout(() => window.removeEventListener('click', stopClick, true), 0);

            const farEnough = offset > sheet.offsetHeight * closeFraction;
            const flicked = velocity > flickVelocity && offset > flickMinimumDistance;
            if (farEnough || flicked) {
                closing = true;
                dotNetReference.invokeMethodAsync('CloseFromDragAsync');
            } else {
                snapBack();
            }
        };

        const onPointerCancel = event => {
            if (event.pointerId !== pointerId) return;
            if (dragging) snapBack();
            reset();
        };

        // The browser decides at the first touchmove whether the gesture is a native pan, and
        // later moves cannot be cancelled. So a downward move from a sheet resting at its top
        // is cancelled from the very first one, before the drag has formally started: a
        // downward move cannot scroll content that is already at the top, and upward moves
        // are left alone so the content still scrolls.
        let touchStartY = 0;
        const onTouchStart = event => {
            if (event.touches.length === 1) touchStartY = event.touches[0].clientY;
        };
        const onTouchMove = event => {
            if (!event.cancelable || event.touches.length !== 1) return;
            const movedDown = event.touches[0].clientY > touchStartY;
            if (dragging || (pointerId !== null && sheet.scrollTop <= 0 && movedDown)) event.preventDefault();
        };

        sheet.addEventListener('pointerdown', onPointerDown);
        sheet.addEventListener('touchstart', onTouchStart, { passive: true });
        sheet.addEventListener('touchmove', onTouchMove, { passive: false });
        window.addEventListener('pointermove', onPointerMove);
        window.addEventListener('pointerup', onPointerUp);
        window.addEventListener('pointercancel', onPointerCancel);

        window.lnshelf.sheetDragDisposers.set(sheet, () => {
            sheet.removeEventListener('pointerdown', onPointerDown);
            sheet.removeEventListener('touchstart', onTouchStart);
            sheet.removeEventListener('touchmove', onTouchMove);
            window.removeEventListener('pointermove', onPointerMove);
            window.removeEventListener('pointerup', onPointerUp);
            window.removeEventListener('pointercancel', onPointerCancel);
        });
    },

    unregisterSheetDrag: function (sheet) {
        if (!sheet) return;
        const dispose = window.lnshelf.sheetDragDisposers.get(sheet);
        if (!dispose) return;
        dispose();
        window.lnshelf.sheetDragDisposers.delete(sheet);
    },

    // The volume grid is auto-fill, so its column count is a layout fact rather than
    // a constant. Counting the tiles that share the first row's offsetTop is the
    // cheapest honest way to ask the browser what it actually did.
    gridColumnCount: function (grid) {
        if (!grid) return 1;
        const tiles = grid.querySelectorAll('[data-volume]');
        if (tiles.length === 0) return 1;

        const firstTop = tiles[0].getBoundingClientRect().top;
        let columns = 0;
        for (const tile of tiles) {
            if (Math.abs(tile.getBoundingClientRect().top - firstTop) > 1) break;
            columns++;
        }
        return Math.max(1, columns);
    },

    focusVolumeTile: function (grid, volumeNumber) {
        if (!grid) return;
        const tile = grid.querySelector('[data-volume="' + volumeNumber + '"]');
        if (tile) tile.focus();
    },

    // Where a tile actually is, plus the viewport it has to fit inside. The placement
    // arithmetic lives in C# (PopoverPlacer) so it can be tested without a browser;
    // this only reports the measurement.
    tileRect: function (grid, volumeNumber) {
        if (!grid) return null;
        const tile = grid.querySelector('[data-volume="' + volumeNumber + '"]');
        if (!tile) return null;

        const rect = tile.getBoundingClientRect();
        return {
            left: rect.left,
            top: rect.top,
            right: rect.right,
            bottom: rect.bottom,
            viewportWidth: window.innerWidth,
            viewportHeight: window.innerHeight
        };
    },

    focusFirstItem: function (container) {
        if (!container) return;
        const items = container.querySelectorAll('button:not(:disabled)');
        if (items.length > 0) items[0].focus();
    },

    // The other end of a dialog, for the focus guard before its first control: Shift+Tab
    // from the top wraps to the bottom instead of leaving the dialog.
    focusLastItem: function (container) {
        if (!container) return;
        const items = container.querySelectorAll('button:not(:disabled), a[href]');
        if (items.length > 0) items[items.length - 1].focus();
    },

    // The phone breakpoint, asked from code for the one choice CSS cannot make: a volume's
    // details open as a bottom sheet on a phone and as a popover beside the tile elsewhere.
    // The same width app.css documents under "the phone breakpoint".
    isPhoneLayout: function () {
        return window.matchMedia('(max-width: 720px)').matches;
    },

    // Whether the stylesheet is showing an element at all. The search page's preview panel is
    // hidden by a container query whenever it would otherwise drop under the results, and a
    // row then opens its preview as a sheet instead: the layout decides, the code only asks.
    isDisplayed: function (element) {
        return !!element && element.getClientRects().length > 0;
    },

    // Focus back on the control marked aria-current inside a container: the search row whose
    // preview sheet just closed, so a keyboard carries on from where it opened the sheet.
    focusCurrent: function (container) {
        if (!container) return;
        const current = container.querySelector('[aria-current="true"]');
        if (current) current.focus();
    },

    // Puts a value back into an input Blazor will not rewrite: a refused date leaves the
    // rendered value attribute unchanged, so the DOM keeps what the reader picked. Setting it
    // here, rather than rebuilding the element, keeps focus where it is.
    setInputValue: function (input, value) {
        if (input) input.value = value;
    },

    // Arrow keys inside a menu wrap, which is what a menu is expected to do and what
    // keeps the last row reachable without counting rows in C#.
    moveMenuFocus: function (container, step) {
        if (!container) return;

        // A date input uses the arrows itself, to step the month, day or year under the
        // cursor; moving focus away would make the read date impossible to edit by keyboard.
        if (document.activeElement && document.activeElement.tagName === 'INPUT') return;

        const items = Array.from(container.querySelectorAll('button:not(:disabled)'));
        if (items.length === 0) return;

        const current = items.indexOf(document.activeElement);
        const next = (current + step + items.length) % items.length;
        items[next].focus();
    },

    hasFocusWithin: function (container) {
        return !!container && container.contains(document.activeElement);
    },

    // A dropped file cannot be read from Blazor's DragEventArgs, so it is handed to
    // the tile's own file input and the input's change event is raised. Drop and
    // picker then travel exactly the same code path.
    handOffDroppedFile: function (input, dataTransfer) {
        if (!input || !dataTransfer || dataTransfer.files.length === 0) return false;

        input.files = dataTransfer.files;
        input.dispatchEvent(new Event('change', { bubbles: true }));
        return true;
    },

    // Registers the native drop listeners for one tile. Blazor's own drop event does
    // not carry the DataTransfer object, so the listener has to be a native one.
    registerCoverDrop: function (tile, input) {
        if (!tile || !input || tile.dataset.dropRegistered === 'true') return;
        tile.dataset.dropRegistered = 'true';

        const isImageDrag = event =>
            Array.from(event.dataTransfer?.items ?? []).some(item => item.type.startsWith('image/'));

        tile.addEventListener('dragover', event => {
            if (!isImageDrag(event)) return;
            event.preventDefault();
            tile.classList.add('tile-wrap--dropping');
        });

        tile.addEventListener('dragleave', () => tile.classList.remove('tile-wrap--dropping'));

        tile.addEventListener('drop', event => {
            tile.classList.remove('tile-wrap--dropping');
            if (!isImageDrag(event)) return;
            event.preventDefault();
            window.lnshelf.handOffDroppedFile(input, event.dataTransfer);
        });
    },

    // Cleared first: an input still holding the last file fires no change event when the
    // same file is picked again, so a retry after a failed upload, or after "Remove cover",
    // silently did nothing.
    openFilePicker: function (input) {
        if (!input) return;
        input.value = '';
        input.click();
    },

    // The browser's own confirm, for the one action that cannot be undone. A modal built
    // in Blazor would look more at home, but it would also be dismissable by clicking
    // past it — and this is the only place in the app where the wrong answer costs a
    // series' read history, ownership and prices with no way back.
    confirm: function (message) {
        return window.confirm(message);
    },

    // A display choice that belongs to this browser rather than to the account — whether
    // the shelf's colour key is open. localStorage, which C# cannot reach; wrapped because a
    // browser in a strict privacy mode throws on it, and a key that will not stay open is
    // no reason to break the page.
    readFlag: function (name) {
        try { return window.localStorage.getItem(name) === 'true'; } catch { return false; }
    },

    writeFlag: function (name, value) {
        try { window.localStorage.setItem(name, value ? 'true' : 'false'); } catch { /* not remembered */ }
    },

    // The same, for a small value with some shape: the shelf's remembered filters. A value
    // that is missing or will not parse reads as null, as if nothing had been remembered;
    // writing null forgets it.
    readJson: function (name) {
        try {
            const text = window.localStorage.getItem(name);
            return text === null ? null : JSON.parse(text);
        } catch {
            return null;
        }
    },

    writeJson: function (name, value) {
        try {
            if (value === null || value === undefined) {
                window.localStorage.removeItem(name);
            } else {
                window.localStorage.setItem(name, JSON.stringify(value));
            }
        } catch { /* not remembered */ }
    },

    // The shared-shelf link's "Copy link". The clipboard needs a secure context (the
    // tunnel's https, or localhost); on plain LAN http this rejects, and the card tells
    // the user to copy the link from its field instead.
    copyText: function (text) {
        return navigator.clipboard.writeText(text);
    }
};
