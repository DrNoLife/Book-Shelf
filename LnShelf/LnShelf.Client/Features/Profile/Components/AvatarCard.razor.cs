using LnShelf.Shared.Auth;

namespace LnShelf.Client.Features.Profile.Components;

public partial class AvatarCard : IDisposable
{
    /// <summary>Matches the API's cap (CoverImageOptions.MaximumUploadBytes), as a cover upload does.</summary>
    private const long UploadLimitBytes = 8 * 1024 * 1024;

    private readonly CancellationTokenSource _disposal = new();
    private InputFile? _input;
    private bool _busy;
    private bool _uploading;
    private string? _message;

    [Parameter]
    public required CurrentUserResponse User { get; set; }

    [Inject]
    public required ProfileState State { get; set; }

    [Inject]
    public required IJSRuntime JavaScript { get; set; }

    private string HelpText => User switch
    {
        { HasUploadedAvatar: true } => "Showing a picture you uploaded. Only you see it; your public shelf never shows it.",
        { AvatarVersion: not null } => "Showing your Google picture. Upload one of your own to use that instead.",
        _ => "No picture yet, so your initial stands in. Upload one to use that instead.",
    };

    private async Task OpenPickerAsync()
    {
        if (_input?.Element is { } input)
        {
            await JavaScript.InvokeVoidAsync("lnshelf.openFilePicker", input);
        }
    }

    private async Task OnFilePickedAsync(InputFileChangeEventArgs args)
    {
        var file = args.File;

        // Checked before opening, as the cover upload does: OpenReadStream throws for a file
        // over its cap, which would surface as the error bar rather than a sentence.
        if (file.Size > UploadLimitBytes)
        {
            _message = $"{file.Name} is {file.Size / 1024} KB; the limit for a picture is {UploadLimitBytes / 1024} KB.";
            return;
        }

        _uploading = true;
        await RunAsync(async cancellationToken =>
        {
            await using var content = file.OpenReadStream(UploadLimitBytes, cancellationToken);
            await State.SetAvatarAsync(content, file.Name, cancellationToken);
        });
        _uploading = false;
    }

    private Task UseGooglePictureAsync() => RunAsync(State.RemoveAvatarAsync);

    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        _busy = true;
        _message = null;

        try
        {
            await action(_disposal.Token);
        }
        catch (HttpRequestException exception)
        {
            _message = exception.Message;
        }
        finally
        {
            _busy = false;
        }
    }

    public void Dispose()
    {
        _disposal.Cancel();
        _disposal.Dispose();
    }
}
