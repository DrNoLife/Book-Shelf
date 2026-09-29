namespace LnShelf.Api.Tests.Features.Import.Services;

/// <summary>
/// Collapsing per-volume books into per-volume state, including the case the real
/// export actually contains: one volume listed twice, under two titles.
/// </summary>
public sealed class DesiredVolumeStateMergerTests
{
    [Fact]
    public void Merge_TwoGroupsForOneSeries_UnionsTheirVolumes()
    {
        // The real Overlord case: English rows 2-9 and 15, Japanese rows 1 and
        // 10-14, both resolving to one AniList id.
        var english = CreateGroup("Overlord", [2, 3, 4, 5, 6, 7, 8, 9, 15]);
        var japanese = CreateGroup("オーバーロード", [1, 10, 11, 12, 13, 14]);

        var merged = DesiredVolumeStateMerger.Merge([english, japanese]);

        merged.Keys.Order().ShouldBe(Enumerable.Range(1, 15));
    }

    [Fact]
    public void Merge_TheSameVolumeInTwoGroups_BecomesOneState()
    {
        // Slime volume 15 is in the export twice: "…, Volume 15" and
        // "転生したらスライムだった件 15".
        var english = CreateGroup("Slime", [15]);
        var japanese = CreateGroup("転生したらスライムだった件", [15]);

        var merged = DesiredVolumeStateMerger.Merge([english, japanese]);

        merged.Count.ShouldBe(1);
        merged[15].ReadStatus.ShouldBe(ReadStatus.Read);
    }

    [Fact]
    public void Combine_ReadAndUnread_KeepsRead()
    {
        var read = CreateState(1, ReadStatus.Read, owned: true);
        var unread = CreateState(1, ReadStatus.Unread, owned: false);

        DesiredVolumeStateMerger.Combine(read, unread).ReadStatus.ShouldBe(ReadStatus.Read);
        DesiredVolumeStateMerger.Combine(unread, read).ReadStatus.ShouldBe(ReadStatus.Read);
    }

    [Fact]
    public void Combine_OwnedAndNotOwned_KeepsOwned()
    {
        var owned = CreateState(1, ReadStatus.Unread, owned: true);
        var notOwned = CreateState(1, ReadStatus.Unread, owned: false);

        DesiredVolumeStateMerger.Combine(owned, notOwned).Owned.ShouldBeTrue();
        DesiredVolumeStateMerger.Combine(notOwned, owned).Owned.ShouldBeTrue();
    }

    [Fact]
    public void Combine_DigitalAndPhysical_KeepsPhysical()
    {
        // Owning the same volume in both means owning a book; the file is the copy
        // that is not on a shelf.
        var digital = CreateState(1, ReadStatus.Read, owned: true, OwnedFormat.Digital);
        var physical = CreateState(1, ReadStatus.Read, owned: true, OwnedFormat.Physical);

        DesiredVolumeStateMerger.Combine(digital, physical).OwnedFormat.ShouldBe(OwnedFormat.Physical);
        DesiredVolumeStateMerger.Combine(physical, digital).OwnedFormat.ShouldBe(OwnedFormat.Physical);
    }

    [Fact]
    public void Combine_TwoReadDates_KeepsTheLater()
    {
        var earlier = CreateState(1, ReadStatus.Read, owned: true, readAt: new DateOnly(2021, 4, 21));
        var later = CreateState(1, ReadStatus.Read, owned: true, readAt: new DateOnly(2022, 9, 27));

        DesiredVolumeStateMerger.Combine(earlier, later).ReadAt.ShouldBe(new DateOnly(2022, 9, 27));
        DesiredVolumeStateMerger.Combine(later, earlier).ReadAt.ShouldBe(new DateOnly(2022, 9, 27));
    }

    [Fact]
    public void Combine_NotOwnedAtAll_HasNoOwnershipFormat() =>
        DesiredVolumeStateMerger.Combine(
                CreateState(1, ReadStatus.Read, owned: false, OwnedFormat.Digital),
                CreateState(1, ReadStatus.Read, owned: false, OwnedFormat.Physical))
            .OwnedFormat.ShouldBeNull();

    [Fact]
    public void Merge_ARowWithAnIsbn_CarriesItThrough()
    {
        // The identifier the export gave: parsed since the first import, and dropped
        // on the floor until now (the enrichment verdict, §2).
        var group = CreateGroup("Overlord", [3], isbn: "9780316363983");

        DesiredVolumeStateMerger.Merge([group])[3].Isbn.ShouldBe("9780316363983");
    }

    [Theory]
    [InlineData("978-1-9753-0117-0", "9781975301170")]
    [InlineData(" 9781975301170 ", "9781975301170")]
    [InlineData("", null)]
    [InlineData(null, null)]

    // StoryGraph's "ISBN/UID" column is also where it puts its own ids for books that
    // have no ISBN. Storing one would buy a lookup that can only 404.
    [InlineData("f5a1c0de-0000-4000-8000-000000000000", null)]
    public void Merge_AnIsbnTheExportWroteLoosely_IsNormalisedOrRefused(string? raw, string? expected)
    {
        var group = CreateGroup("Overlord", [3], isbn: raw);

        DesiredVolumeStateMerger.Merge([group])[3].Isbn.ShouldBe(expected);
    }

    [Fact]
    public void Merge_OneVolumeInTwoEditions_KeepsTheEnglishIsbn()
    {
        // The real Slime shape: one volume listed twice, once per edition. Japanese
        // registration group 4 against Yen On's 978-1.
        var english = CreateGroup("Slime", [1], isbn: "9781975301118");
        var japanese = CreateGroup("転生したらスライムだった件", [1], isbn: "9784896374599");

        DesiredVolumeStateMerger.Merge([english, japanese])[1].Isbn.ShouldBe("9781975301118");
        DesiredVolumeStateMerger.Merge([japanese, english])[1].Isbn.ShouldBe("9781975301118");
    }

    [Fact]
    public void Merge_OneVolumeWhereOnlyOneEditionHasAnIsbn_KeepsTheOneThereIs()
    {
        // Also real: the Japanese Slime volume 15 row has a blank ISBN cell.
        var english = CreateGroup("Slime", [15], isbn: "9781646599387");
        var japanese = CreateGroup("転生したらスライムだった件", [15]);

        DesiredVolumeStateMerger.Merge([english, japanese])[15].Isbn.ShouldBe("9781646599387");
        DesiredVolumeStateMerger.Merge([japanese, english])[15].Isbn.ShouldBe("9781646599387");
    }

    [Fact]
    public void Merge_OneVolumeInTwoJapaneseEditions_KeepsOneOfThem()
    {
        // There is no English edition to prefer, so the rule has nothing to say and
        // must not drop the identifier the shelf does have.
        var first = CreateGroup("転生したらスライムだった件", [1], isbn: "9784896374599");
        var second = CreateGroup("Tensei shitara Slime Datta Ken", [1], isbn: "9784896375954");

        DesiredVolumeStateMerger.Merge([first, second])[1].Isbn.ShouldNotBeNull();
    }

    [Fact]
    public void Merge_AReadRowAndALaterReadingRow_KeepsTheReadRowsDate()
    {
        // A re-read in progress: the Reading row's date is newer, but it is not the date
        // this volume was finished.
        var read = CreateGroup("Overlord", [1]);
        read.Rows.Single().ReadAt = new DateOnly(2021, 11, 23);
        var reading = CreateGroup("Overlord", [1]);
        reading.Rows.Single().ReadStatus = ReadStatus.Reading;
        reading.Rows.Single().ReadAt = new DateOnly(2024, 2, 1);

        var merged = DesiredVolumeStateMerger.Merge([read, reading])[1];

        merged.ReadStatus.ShouldBe(ReadStatus.Read);
        merged.ReadAt.ShouldBe(new DateOnly(2021, 11, 23));
        merged.ReadingEvidenceAt.ShouldBe(new DateOnly(2021, 11, 23));
    }

    [Fact]
    public void Merge_ABookWithNoVolumeNumber_BecomesVolumeOne()
    {
        var group = CreateGroup("Vesten mod vesten", []);
        group.Rows.Add(new ImportRow
        {
            Id = Guid.CreateVersion7(),
            LineNumber = 10,
            RawTitle = "Vesten mod vesten",
            VolumeNumber = null,
            ReadStatus = ReadStatus.Reading,
            OwnedInExport = false,
        });

        var merged = DesiredVolumeStateMerger.Merge([group]);

        // Dropping it would silently lose a book; volume 1 is the honest reading of
        // "a series in one book".
        merged.Keys.ShouldBe([1]);
        merged[1].ReadStatus.ShouldBe(ReadStatus.Reading);
    }

    [Fact]
    public void Merge_GroupWithReadImpliesOwnedOff_DoesNotOwnReadVolumes()
    {
        var group = CreateGroup("Borrowed Series", [1, 2], treatReadAsOwned: false);

        var merged = DesiredVolumeStateMerger.Merge([group]);

        merged.Values.ShouldAllBe(state => !state.Owned);
        merged.Values.ShouldAllBe(state => state.ReadStatus == ReadStatus.Read);
    }

    [Fact]
    public void Merge_GroupWithReadImpliesOwnedOn_OwnsReadVolumes()
    {
        var group = CreateGroup("Bought Series", [1, 2]);

        DesiredVolumeStateMerger.Merge([group]).Values.ShouldAllBe(state => state.Owned);
    }

    private static ImportGroup CreateGroup(
        string seriesTitle,
        int[] volumeNumbers,
        bool treatReadAsOwned = true,
        string? isbn = null)
    {
        var group = new ImportGroup
        {
            Id = Guid.CreateVersion7(),
            SeriesTitle = seriesTitle,
            SeriesKey = TitleNormalizer.Normalize(seriesTitle),
            TreatReadAsOwned = treatReadAsOwned,
        };

        foreach (var volumeNumber in volumeNumbers)
        {
            group.Rows.Add(new ImportRow
            {
                Id = Guid.CreateVersion7(),
                LineNumber = volumeNumber,
                RawTitle = $"{seriesTitle}, Vol. {volumeNumber}",
                VolumeNumber = volumeNumber,
                ReadStatus = ReadStatus.Read,
                OwnedInExport = false,
                OwnedFormat = OwnedFormat.Digital,
                Isbn = isbn,
            });
        }

        return group;
    }

    private static DesiredVolumeState CreateState(
        int volumeNumber,
        ReadStatus readStatus,
        bool owned,
        OwnedFormat? ownedFormat = null,
        DateOnly? readAt = null) => new()
        {
            VolumeNumber = volumeNumber,
            ReadStatus = readStatus,
            Owned = owned,
            OwnedFormat = ownedFormat,
            ReadAt = readAt,
        };
}
