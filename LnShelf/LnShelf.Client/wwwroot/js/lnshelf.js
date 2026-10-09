// The only JavaScript in the client, and only for the things Blazor cannot do from
// C#: read a rendered layout, move focus between sibling elements, and hand a
// dropped file to a file input.
window.lnshelf = {
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

    // Arrow keys inside a menu wrap, which is what a menu is expected to do and what
    // keeps the last row reachable without counting rows in C#.
    moveMenuFocus: function (container, step) {
        if (!container) return;
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
    }
};
