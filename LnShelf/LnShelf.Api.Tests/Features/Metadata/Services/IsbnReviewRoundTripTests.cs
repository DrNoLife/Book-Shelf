using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The review file and the command that applies it — the human-in-the-loop half of
/// ISBN discovery. What is written must come back readable, and what comes back must
/// only ever add.
/// </summary>
public sealed class IsbnReviewRoundTripTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), $"lnshelf-isbn-review-{Guid.CreateVersion7()}");

    private static readonly Guid SeriesId = Guid.CreateVersion7();

    [Fact]
    public async Task WriteThenRead_ARowWithAnIsbn_ComesBackAsAnInstruction()
    {
        var path = Path.Combine(_directory, "isbn-review.csv");
        var reviewFile = CreateReviewFile();

        await reviewFile.WriteAsync(path, [Row(2, "9780316363921")], CancellationToken.None);
        var instructions = await reviewFile.ReadAsync(path, CancellationToken.None);

        var instruction = instructions.ShouldHaveSingleItem();
        instruction.SeriesId.ShouldBe(SeriesId);
        instruction.VolumeNumber.ShouldBe(2);
        instruction.Isbn.ShouldBe("9780316363921");
    }

    [Fact]
    public async Task WriteThenRead_ARowThatFoundNothing_IsNotAnInstruction()
    {
        var path = Path.Combine(_directory, "isbn-review.csv");
        var reviewFile = CreateReviewFile();

        await reviewFile.WriteAsync(
            path, [Row(2, "9780316363921"), Row(3, isbn: null)], CancellationToken.None);

        // The failure row stays in the file so it does not read as a complete list, and
        // is skipped rather than taken as "clear this volume's ISBN".
        (await reviewFile.ReadAsync(path, CancellationToken.None)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task WriteThenRead_ATitleWithACommaAndQuotes_SurvivesTheRoundTrip()
    {
        var path = Path.Combine(_directory, "isbn-review.csv");
        var reviewFile = CreateReviewFile();

        var row = Row(3, "9780316363983") with
        {
            SeriesTitle = "Overlord, Vol. 3: \"The Bloody Valkyrie\"",
            OpenLibraryTitle = "Overlord, Volume 3: The Bloody Valkyrie",
        };

        await reviewFile.WriteAsync(path, [row], CancellationToken.None);

        (await reviewFile.ReadAsync(path, CancellationToken.None))
            .ShouldHaveSingleItem().VolumeNumber.ShouldBe(3);
    }

    [Fact]
    public async Task ApplyAsync_ReviewedIsbns_LandOnTheirVolumes()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, volumeCount: 3);

        var report = await CreateApplyService(dbContext).ApplyAsync(
            [Instruction(1, "9780316272247"), Instruction(2, "9780316363921")], CancellationToken.None);

        report.Written.ShouldBe(2);
        report.AlreadyRecorded.ShouldBe(0);

        var volumes = await dbContext.Volumes.OrderBy(volume => volume.Number).ToListAsync();
        volumes[0].Isbn.ShouldBe("9780316272247");
        volumes[1].Isbn.ShouldBe("9780316363921");
        volumes[2].Isbn.ShouldBeNull();
    }

    [Fact]
    public async Task ApplyAsync_Twice_WritesNothingTheSecondTime()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, volumeCount: 2);

        var instructions = new[] { Instruction(1, "9780316272247") };

        await CreateApplyService(dbContext).ApplyAsync(instructions, CancellationToken.None);
        var second = await CreateApplyService(dbContext).ApplyAsync(instructions, CancellationToken.None);

        second.Written.ShouldBe(0);
        second.AlreadyRecorded.ShouldBe(1);
    }

    [Fact]
    public async Task ApplyAsync_AVolumeThatAlreadyHasADifferentIsbn_KeepsIt()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, volumeCount: 1);

        var volume = await dbContext.Volumes.SingleAsync();
        volume.Isbn = "9781975301170";
        await dbContext.SaveChangesAsync();

        await CreateApplyService(dbContext).ApplyAsync(
            [Instruction(1, "9780316272247")], CancellationToken.None);

        // Additive only. A value already on a volume was imported or already reviewed,
        // and this command has no grounds to overrule either.
        (await dbContext.Volumes.SingleAsync()).Isbn.ShouldBe("9781975301170");
    }

    [Fact]
    public async Task ApplyAsync_ARowNamingAVolumeTheShelfDoesNotTrack_IsCountedNotThrown()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, volumeCount: 2);

        var report = await CreateApplyService(dbContext).ApplyAsync(
            [Instruction(9, "9780316398879")], CancellationToken.None);

        report.Written.ShouldBe(0);
        report.WithoutVolume.ShouldBe(1);
    }

    private static IsbnDiscoveryRow Row(int volumeNumber, string? isbn) => new()
    {
        SeriesId = SeriesId,
        SeriesTitle = "Overlord",
        VolumeNumber = volumeNumber,
        Isbn = isbn,
        OpenLibraryTitle = isbn is null ? null : "Overlord, Volume 2: The Dark Warrior",
        Publisher = isbn is null ? null : "Yen On",
        Outcome = isbn is null ? IsbnDiscoveryOutcome.NoSearchResults : IsbnDiscoveryOutcome.ConfirmedEnglish,
        Note = "Written by a test.",
    };

    private static IsbnReviewInstruction Instruction(int volumeNumber, string isbn) => new()
    {
        SeriesId = SeriesId,
        VolumeNumber = volumeNumber,
        Isbn = isbn,
    };

    private static IsbnReviewFile CreateReviewFile() => new(NullLogger<IsbnReviewFile>.Instance);

    private static IsbnApplyService CreateApplyService(LnShelfDbContext dbContext) =>
        new(dbContext, NullLogger<IsbnApplyService>.Instance);

    private static async Task SeedAsync(LnShelfDbContext dbContext, int volumeCount)
    {
        var series = new Series
        {
            Id = SeriesId,
            AniListId = 85976,
            TitleEnglish = "Overlord",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
        };

        foreach (var number in Enumerable.Range(1, volumeCount))
        {
            series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = number });
        }

        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync();
    }

    private static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"isbn-apply-{Guid.CreateVersion7()}")
            .Options, TestUsers.Current);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
