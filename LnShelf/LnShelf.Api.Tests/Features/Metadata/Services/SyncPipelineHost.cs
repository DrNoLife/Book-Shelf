using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using LnShelf.Api.Tests.Features.Metadata.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The app's own composition root with only its two outer edges replaced: the network, by
/// captured responses, and Postgres, by an in-memory database. Covers land in a real folder.
/// </summary>
// Built after a sync matched every volume of a shelf and cached none of their covers, which
// every unit test passed through: each one constructs its subject with `new` and hands it
// doubles for its neighbours, so none of them could see what the pipeline as wired does to a
// volume. This drives the sync the way the app does — ISeriesSyncService resolved from the
// container, the real refresher, enrichment, writer, cover cache, fetcher, normalizer and
// file store — and the only things that are not the app's are the bytes on the wire.
//
// Answers by host: iTunes search, MangaUpdates series, MangaDex search and aggregates, and any
// image CDN. A request to anywhere else is recorded and answered 404, so a test can assert the
// pipeline did not wander off to a source it was not meant to reach (AniList, Open Library).
// MangaDex answers an empty search unless the test hands it captures: every manga series now
// gets a chapter-map check, and most of these tests are not about it. Wikipedia and Wikidata
// likewise answer "nothing known" unless given captures, though a series whose volumes carry
// no ISBN never reaches them at all.
internal sealed class SyncPipelineHost : IAsyncDisposable
{
    private readonly InMemoryDatabaseRoot _databaseRoot = new();
    private readonly string _databaseName = $"sync-pipeline-{Guid.CreateVersion7()}";
    private readonly ServiceProvider _provider;
    private readonly byte[] _cover = Png();

    public SyncPipelineHost(
        string appleBooksFixture,
        string mangaUpdatesFixture,
        MangaDexCaptures? mangaDex = null,
        WikimediaCaptures? wikimedia = null,
        IReadOnlyDictionary<string, string?>? settings = null)
    {
        CoverRoot = Path.Combine(Path.GetTempPath(), $"lnshelf-sync-pipeline-{Guid.CreateVersion7()}");
        Directory.CreateDirectory(CoverRoot);

        var appleBooks = Fixture(appleBooksFixture);
        var mangaUpdates = Fixture(mangaUpdatesFixture);
        var mangaDexAnswers = new MangaDexAnswers(
            mangaDex is null ? EmptyMangaDexSearch : Fixture(mangaDex.Search),
            mangaDex?.English is { } english ? Fixture(english) : EmptyMangaDexAggregate,
            mangaDex?.All is { } all ? Fixture(all) : EmptyMangaDexAggregate,
            mangaDex?.AggregateStatus ?? HttpStatusCode.OK);

        // The test's own settings over the app's, for a test about an option (an allowance).
        var configuration = new Dictionary<string, string?>(settings ?? new Dictionary<string, string?>())
        {
            ["Covers:FileSystem:RootPath"] = CoverRoot,
        };
        var services = CompositionRootTests.BuildServices(configuration);

        // Postgres out, the in-memory provider in. Both registrations go: AddDbContext keeps
        // its provider call as a separate configuration, and leaving it would give the
        // context two providers.
        services.RemoveAll<DbContextOptions<LnShelfDbContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<LnShelfDbContext>>();
        services.AddDbContext<LnShelfDbContext>(options =>
            options.UseInMemoryDatabase(_databaseName, _databaseRoot));

        // Every typed client's innermost handler, under the app's own politeness handlers.
        services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(
            () => new FixtureHandler(request => Answer(request, appleBooks, mangaUpdates, mangaDexAnswers, wikimedia ?? new()))));

        _provider = services.BuildServiceProvider();
    }

    public string CoverRoot { get; }

    /// <summary>Every MangaDex request, so a test can tell a search from an aggregate.</summary>
    public ConcurrentQueue<Uri> MangaDexRequests { get; } = new();

    /// <summary>Every Wikipedia and Wikidata request.</summary>
    public ConcurrentQueue<Uri> WikimediaRequests { get; } = new();

    /// <summary>Requests that went to neither a source this host answers for nor a CDN.</summary>
    public ConcurrentQueue<Uri> UnexpectedRequests { get; } = new();

    /// <summary>Every image fetched, so a test can tell a fresh fetch from a kept file.</summary>
    public ConcurrentQueue<Uri> ImageRequests { get; } = new();

    public async Task SeedAsync(Series series)
    {
        await using var scope = Scope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LnShelfDbContext>();
        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>One whole-shelf sync, resolved and run the way the nightly loop does it.</summary>
    public async Task<SyncRun> SyncAsync()
    {
        await using var scope = Scope();
        return await scope.ServiceProvider.GetRequiredService<ISeriesSyncService>()
            .RunAsync(CancellationToken.None);
    }

    /// <summary>The series as saved, read through a fresh context.</summary>
    public async Task<Series> LoadAsync(Guid seriesId)
    {
        await using var scope = Scope();
        return await scope.ServiceProvider.GetRequiredService<LnShelfDbContext>().Series
            .Include(series => series.Volumes)
            .ThenInclude(volume => volume.UserState)
            .AsNoTracking()
            .SingleAsync(series => series.Id == seriesId);
    }

    /// <summary>What the series page's disagreement list would show for the series now.</summary>
    public async Task<IReadOnlyList<SourceConflictResponse>> ConflictsAsync(Guid seriesId)
    {
        await using var scope = Scope();
        var conflicts = await scope.ServiceProvider.GetRequiredService<ISourceConflictQueryService>()
            .GetAsync(seriesId, CancellationToken.None);

        return conflicts ?? throw new InvalidOperationException($"No series {seriesId} to list conflicts for.");
    }

    public bool StoreHolds(string objectKey) =>
        File.Exists(Path.Combine(CoverRoot, objectKey.Replace('/', Path.DirectorySeparatorChar)));

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();

        if (Directory.Exists(CoverRoot))
        {
            Directory.Delete(CoverRoot, recursive: true);
        }
    }

    private AsyncServiceScope Scope()
    {
        var scope = _provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUser>().ActAs(TestUsers.Owner);
        return scope;
    }

    private HttpResponseMessage Answer(
        HttpRequestMessage request,
        string appleBooks,
        string mangaUpdates,
        MangaDexAnswers mangaDex,
        WikimediaCaptures wikimedia)
    {
        var uri = request.RequestUri!;

        if (uri.Host == "itunes.apple.com")
        {
            // The store's own content type, which is not JSON (AppleBooksClient says why).
            return Respond(new StringContent(appleBooks, Encoding.UTF8, "text/javascript"));
        }

        // Whichever series is asked about: each test seeds one, linked to the capture it passes.
        if (uri.Host == "api.mangaupdates.com"
            && uri.Segments.Length > 1
            && uri.Segments[^2] == "series/"
            && long.TryParse(uri.Segments[^1], CultureInfo.InvariantCulture, out _))
        {
            return Respond(new StringContent(mangaUpdates, Encoding.UTF8, "application/json"));
        }

        if (uri.Host == "api.mangadex.org")
        {
            MangaDexRequests.Enqueue(uri);
            return mangaDex.StatusFor(uri) is HttpStatusCode.OK
                ? Respond(new StringContent(mangaDex.For(uri), Encoding.UTF8, "application/json"))
                : new HttpResponseMessage(mangaDex.StatusFor(uri));
        }

        if (WikimediaCaptures.Answers(uri))
        {
            WikimediaRequests.Enqueue(uri);
            return Respond(new StringContent(wikimedia.For(uri), Encoding.UTF8, "application/json"));
        }

        if (uri.Host.EndsWith("mzstatic.com", StringComparison.Ordinal))
        {
            ImageRequests.Enqueue(uri);
            var content = new ByteArrayContent(_cover);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return Respond(content);
        }

        UnexpectedRequests.Enqueue(uri);
        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private const string EmptyMangaDexSearch = """{"result":"ok","response":"collection","data":[]}""";

    private const string EmptyMangaDexAggregate = """{"result":"ok","volumes":[]}""";

    private sealed record MangaDexAnswers(string Search, string English, string All, HttpStatusCode AggregateStatus)
    {
        public HttpStatusCode StatusFor(Uri uri) =>
            uri.AbsolutePath.EndsWith("/aggregate", StringComparison.Ordinal) ? AggregateStatus : HttpStatusCode.OK;

        public string For(Uri uri)
        {
            if (!uri.AbsolutePath.EndsWith("/aggregate", StringComparison.Ordinal))
            {
                return Search;
            }

            return uri.Query.Contains("translatedLanguage", StringComparison.Ordinal) ? English : All;
        }
    }

    private static HttpResponseMessage Respond(HttpContent content) =>
        new(HttpStatusCode.OK) { Content = content };

    private static string Fixture(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Features", "Metadata", "Fixtures", fileName));

    private static byte[] Png()
    {
        using var image = new Image<Rgba32>(200, 300);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    private sealed class FixtureHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(answer(request));
    }
}

/// <summary>
/// Captured MangaDex answers for one series: its title search and its two aggregates, or the
/// status the aggregates answer with instead (a 404 for a record MangaDex no longer has).
/// </summary>
internal sealed record MangaDexCaptures(
    string Search,
    string? English = null,
    string? All = null,
    HttpStatusCode AggregateStatus = HttpStatusCode.OK);
