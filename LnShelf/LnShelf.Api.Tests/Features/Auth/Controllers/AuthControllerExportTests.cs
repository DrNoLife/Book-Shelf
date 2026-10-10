using LnShelf.Api.Features.Auth.Controllers;
using LnShelf.Shared.Auth;
using LnShelf.Shared.Preferences;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LnShelf.Api.Tests.Features.Auth.Controllers;

/// <summary>
/// The export is a download, not a page: what the browser saves, under what name, and that
/// nothing along the way may keep a copy of personal data.
/// </summary>
public sealed class AuthControllerExportTests
{
    [Fact]
    public async Task ExportAccountAsync_ASignedInUser_IsADatedJsonAttachmentNobodyMayCache()
    {
        var controller = CreateController(Export());

        var result = await controller.ExportAccountAsync(CancellationToken.None);

        var file = result.ShouldBeOfType<FileContentResult>();
        file.ContentType.ShouldBe("application/json");
        file.FileDownloadName.ShouldBe("lnshelf-export-2026-10-10.json");
        controller.Response.Headers.CacheControl.ToString().ShouldBe("no-store");
    }

    // Names, not numbers: the contract enums are strings on the wire, and a person reading the
    // file has no table to look an integer up in.
    [Fact]
    public async Task ExportAccountAsync_TheFile_SpellsEnumsAsNames()
    {
        var result = await CreateController(Export()).ExportAccountAsync(CancellationToken.None);

        var json = Encoding.UTF8.GetString(result.ShouldBeOfType<FileContentResult>().FileContents);

        json.ShouldContain("\"gridDensity\": \"Compact\"");
        json.ShouldContain("\"email\": \"owner@example.com\"");
    }

    [Fact]
    public async Task ExportAccountAsync_TheUsersRowIsGone_IsNotFound()
    {
        var result = await CreateController(export: null).ExportAccountAsync(CancellationToken.None);

        result.ShouldBeOfType<NotFoundResult>();
    }

    // Only the export is exercised, so the collaborators the other actions need stay null.
    private static AuthController CreateController(AccountExportResponse? export)
    {
        var query = Substitute.For<IAccountExportQueryService>();
        query.ExportAsync(Arg.Any<CancellationToken>()).Returns(export);

        return new AuthController(null!, null!, null!, query)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    private static AccountExportResponse Export() => new()
    {
        ExportedAt = new DateTimeOffset(2026, 10, 10, 8, 30, 0, TimeSpan.Zero),
        Account = new AccountExportAccount
        {
            Id = TestUsers.Owner,
            Issuer = "https://accounts.google.com",
            Subject = "sub-owner",
            Email = "owner@example.com",
            CreatedAt = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
            LastSeenAt = new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero),
        },
        Sharing = new AccountExportSharing { IsPublic = false },
        Preferences = new PreferencesResponse
        {
            GridDensity = GridDensity.Compact,
            LibrarySort = LibrarySort.MostBehind,
            LibraryView = LibraryView.Covers,
            VolumeView = VolumeView.Tiles,
            ShowVolumeTitles = true,
            DefaultLibraryFilter = LibraryFilter.Behind,
        },
        Series = [],
        ImportBatches = [],
        SyncRuns = [],
    };
}
