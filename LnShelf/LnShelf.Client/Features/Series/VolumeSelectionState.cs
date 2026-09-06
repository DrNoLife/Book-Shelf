namespace LnShelf.Client.Features.Series;

/// <summary>
/// What the volume section is doing rather than what it holds: which volume's menu is
/// open, whether select mode is on, and what is selected.
/// </summary>
// Page UI state, kept out of SeriesState because it has nothing to do with the series —
// and out of the tiles, because only one menu may be open at a time and a tile cannot
// know about its siblings.
public sealed class VolumeSelectionState
{
    /// <summary>The volume whose state menu is open, or null.</summary>
    public Guid? OpenMenuVolumeId { get; private set; }

    public bool IsSelectMode { get; private set; }

    public IReadOnlySet<Guid> SelectedVolumeIds => _selected;

    private readonly HashSet<Guid> _selected = [];

    public event Action? Changed;

    public void OpenMenu(VolumeResponse volume)
    {
        ArgumentNullException.ThrowIfNull(volume);

        OpenMenuVolumeId = volume.VolumeId;
        Changed?.Invoke();
    }

    public void CloseMenu()
    {
        if (OpenMenuVolumeId is null)
        {
            return;
        }

        OpenMenuVolumeId = null;
        Changed?.Invoke();
    }

    public void ToggleSelectMode()
    {
        IsSelectMode = !IsSelectMode;

        // A menu belonging to a tile that is about to become a checkbox has to go.
        OpenMenuVolumeId = null;

        if (!IsSelectMode)
        {
            _selected.Clear();
        }

        Changed?.Invoke();
    }

    public void ToggleSelection(VolumeResponse volume)
    {
        ArgumentNullException.ThrowIfNull(volume);

        if (!_selected.Remove(volume.VolumeId))
        {
            _selected.Add(volume.VolumeId);
        }

        Changed?.Invoke();
    }
}
