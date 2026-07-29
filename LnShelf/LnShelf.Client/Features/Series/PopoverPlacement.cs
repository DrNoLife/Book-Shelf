namespace LnShelf.Client.Features.Series;

/// <summary>
/// A tile's position in the viewport, measured in the browser.
/// </summary>
public sealed record TileRect
{
    public required double Left { get; init; }
    public required double Top { get; init; }
    public required double Right { get; init; }
    public required double Bottom { get; init; }
    public required double ViewportWidth { get; init; }
    public required double ViewportHeight { get; init; }
}

/// <summary>Where the menu gets drawn, in viewport coordinates.</summary>
public sealed record PopoverPlacement(double Left, double Top, bool FlippedHorizontally, bool FlippedVertically);

/// <summary>
/// Chooses the menu's corner. Pure arithmetic, kept out of the component so the
/// edge-flipping rules can be asserted without a browser.
/// </summary>
public static class PopoverPlacer
{
    /// <summary>Breathing room between tile and menu, and between menu and viewport edge.</summary>
    private const double Gap = 6;

    public static PopoverPlacement For(TileRect tile, double menuWidth, double menuHeight)
    {
        ArgumentNullException.ThrowIfNull(tile);

        // Default: aligned to the tile's left edge, hanging below it. A tile near the
        // right or bottom edge flips to the opposite anchor rather than being clamped,
        // because clamping would leave the menu covering the tile it belongs to.
        var flippedHorizontally = tile.Left + menuWidth + Gap > tile.ViewportWidth;
        var left = flippedHorizontally ? tile.Right - menuWidth : tile.Left;

        var flippedVertically = tile.Bottom + Gap + menuHeight + Gap > tile.ViewportHeight;
        var top = flippedVertically ? tile.Top - menuHeight - Gap : tile.Bottom + Gap;

        // A menu taller or wider than the viewport has nowhere to flip to; pinning it
        // to the near edge at least keeps its first rows reachable.
        return new PopoverPlacement(
            Math.Max(Gap, left),
            Math.Max(Gap, top),
            flippedHorizontally,
            flippedVertically);
    }
}
