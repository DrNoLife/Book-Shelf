namespace LnShelf.Client.Features.Profile.Components;

public partial class CoverSharingCard : IDisposable
{
    private static readonly IReadOnlyList<SegmentedOption<bool>> SharingOptions =
    [
        new(false, "Off", "Do not share your covers"),
        new(true, "On", "Share your uploaded covers with other readers"),
    ];

    [Inject]
    public required CoverSharingState State { get; set; }

    protected override async Task OnInitializedAsync()
    {
        State.Changed += OnStateChanged;
        await State.LoadAsync();
    }

    public void Dispose() => State.Changed -= OnStateChanged;

    private static string SharedLine(CoverSharingResponse sharing) => sharing.SharedCount switch
    {
        0 => "Shared since you turned it on. None of your covers is shared yet.",
        1 => "1 of your covers is shared.",
        var count => $"{count} of your covers are shared.",
    };

    private Task OnEnabledChangedAsync(bool enabled) => State.SetEnabledAsync(enabled);

    private void OnStateChanged() => InvokeAsync(StateHasChanged);
}
