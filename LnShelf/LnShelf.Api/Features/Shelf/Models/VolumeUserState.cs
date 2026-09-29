namespace LnShelf.Api.Features.Shelf.Models;

/// <summary>
/// Per-volume user state on the two independent axes, read and ownership.
/// A volume with no row here is Unread and NotOwned — absence is the default,
/// so nothing has to be written for volumes the user has not touched.
/// </summary>
public sealed class VolumeUserState
{
    /// <summary>Primary key and foreign key — one row per volume.</summary>
    public Guid VolumeId { get; set; }

    public ReadStatus ReadStatus { get; set; }
    public DateOnly? ReadAt { get; set; }

    public bool Owned { get; set; }

    /// <summary>Null when the volume is not owned.</summary>
    public OwnedFormat? OwnedFormat { get; set; }

    /// <summary>numeric(<see cref="PurchasePricePrecision"/>, <see cref="PurchasePriceScale"/>).</summary>
    public const int PurchasePricePrecision = 10;

    public const int PurchasePriceScale = 2;

    /// <summary>The largest price the column can hold: eight digits before the point, two after.</summary>
    // Spelled out beside the precision it follows from rather than computed, because it is
    // what the write guard compares against and a reader should see the number. A price
    // above it used to reach Postgres as a numeric overflow and come back as a 500.
    public const decimal MaximumPurchasePrice = 99_999_999.99m;

    public DateOnly? PurchasedAt { get; set; }
    public decimal? PurchasePrice { get; set; }

    /// <summary>A user-supplied cover always wins over synced art (§4.3).</summary>
    public string? CoverOverrideObjectKey { get; set; }
}
