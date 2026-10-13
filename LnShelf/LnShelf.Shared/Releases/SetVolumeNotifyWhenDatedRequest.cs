namespace LnShelf.Shared.Releases;

/// <summary>Asks to be told, or not, when one undated volume gets a date.</summary>
public sealed record SetVolumeNotifyWhenDatedRequest
{
    public required bool NotifyWhenDated { get; init; }
}
