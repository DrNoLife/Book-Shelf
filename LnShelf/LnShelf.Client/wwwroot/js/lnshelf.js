// The only JavaScript in the client, and only for the two things Blazor cannot do
// from C#: read a rendered grid's column count, and move focus to a sibling element.
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
    }
};
