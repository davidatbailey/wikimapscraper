using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WikiMapScraper.Web.Data;
using WikiMapScraper.Web.Services;

namespace WikiMapScraper.Web.Tests;

public class SearchImportServiceTests
{
    [Fact]
    public async Task ImportTopicAsync_FirstImport_CreatesTopicPlaceAndMarker()
    {
        await using var dbContext = CreateDbContext();
        var wikipediaService = new FakeWikipediaService(
        [
            new WikipediaPlaceResult
            {
                WikiPageId = 1001,
                Title = "Great Pyramid of Giza",
                Extract = "The Great Pyramid of Giza is the oldest and largest of the pyramids in the Giza pyramid complex.",
                ThumbnailUrl = "https://img.example/pyramid.jpg",
                Latitude = 29.9792,
                Longitude = 31.1342,
                CanonicalUrl = "https://en.wikipedia.org/wiki/Great_Pyramid_of_Giza"
            }
        ]);

        var sut = new SearchImportService(dbContext, wikipediaService, NullLogger<SearchImportService>.Instance);

        var markers = await sut.ImportTopicAsync("pyramid", 1);

        Assert.Single(markers);
        Assert.Equal("pyramid", markers[0].TopicName);
        Assert.Equal(1, await dbContext.Topics.CountAsync());
        Assert.Equal(1, await dbContext.Places.CountAsync());
        Assert.Equal(1, await dbContext.TopicPlaces.CountAsync());
    }

    [Fact]
    public async Task ImportTopicAsync_ReimportSameTopic_DoesNotDuplicateRows()
    {
        await using var dbContext = CreateDbContext();
        var wikipediaService = new FakeWikipediaService(
        [
            new WikipediaPlaceResult
            {
                WikiPageId = 2002,
                Title = "Lighthouse of Alexandria",
                Extract = "The Lighthouse of Alexandria was one of the Seven Wonders of the Ancient World.",
                ThumbnailUrl = "https://img.example/lighthouse.jpg",
                Latitude = 31.2135,
                Longitude = 29.8853,
                CanonicalUrl = "https://en.wikipedia.org/wiki/Lighthouse_of_Alexandria"
            }
        ]);

        var sut = new SearchImportService(dbContext, wikipediaService, NullLogger<SearchImportService>.Instance);

        await sut.ImportTopicAsync("lighthouse", 1);
        await sut.ImportTopicAsync("lighthouse", 1);

        Assert.Equal(1, await dbContext.Topics.CountAsync());
        Assert.Equal(1, await dbContext.Places.CountAsync());
        Assert.Equal(1, await dbContext.TopicPlaces.CountAsync());
    }

    [Fact]
    public async Task ImportTopicAsync_WithOffset_ImportsAdditionalUniqueRows()
    {
        await using var dbContext = CreateDbContext();
        var wikipediaService = new FakeWikipediaService(
        [
            new WikipediaPlaceResult
            {
                WikiPageId = 4101,
                Title = "Giza Necropolis",
                Extract = "A large archaeological site on the Giza Plateau.",
                Latitude = 29.9773,
                Longitude = 31.1325,
                CanonicalUrl = "https://en.wikipedia.org/wiki/Giza_Necropolis"
            },
            new WikipediaPlaceResult
            {
                WikiPageId = 4102,
                Title = "Pyramid of Djoser",
                Extract = "An archaeological remain in the Saqqara necropolis.",
                Latitude = 29.8712,
                Longitude = 31.2165,
                CanonicalUrl = "https://en.wikipedia.org/wiki/Pyramid_of_Djoser"
            }
        ]);

        var sut = new SearchImportService(dbContext, wikipediaService, NullLogger<SearchImportService>.Instance);

        var firstBatch = await sut.ImportTopicAsync("pyramid", 1, 0);
        var secondBatch = await sut.ImportTopicAsync("pyramid", 1, 1);

        Assert.Single(firstBatch);
        Assert.Single(secondBatch);
        Assert.Equal(2, await dbContext.Places.CountAsync());
        Assert.Equal(2, await dbContext.TopicPlaces.CountAsync());
    }

    [Fact]
    public async Task GetMarkersAsync_LongExtract_ReturnsTruncatedPreview()
    {
        await using var dbContext = CreateDbContext();
        var longExtract = string.Join(' ', Enumerable.Repeat("pyramid", 40));

        var wikipediaService = new FakeWikipediaService(
        [
            new WikipediaPlaceResult
            {
                WikiPageId = 3003,
                Title = "Step pyramid",
                Extract = longExtract,
                ThumbnailUrl = null,
                Latitude = 29.8712,
                Longitude = 31.2165,
                CanonicalUrl = "https://en.wikipedia.org/wiki/Step_pyramid"
            }
        ]);

        var sut = new SearchImportService(dbContext, wikipediaService, NullLogger<SearchImportService>.Instance);

        await sut.ImportTopicAsync("pyramid", 1);
        var markers = await sut.GetMarkersAsync("pyramid");

        var marker = Assert.Single(markers);
        Assert.EndsWith("...", marker.ExtractPreview);
        Assert.True(marker.ExtractPreview.Length <= 123);
    }

    [Fact]
    public async Task HidePlaceAsync_HiddenPlaceIsExcludedFromMarkers()
    {
        await using var dbContext = CreateDbContext();
        var wikipediaService = new FakeWikipediaService(
        [
            new WikipediaPlaceResult
            {
                WikiPageId = 5001,
                Title = "Temple of Artemis",
                Extract = "A Greek temple dedicated to an ancient local form of the goddess Artemis.",
                Latitude = 37.9497,
                Longitude = 27.3639,
                CanonicalUrl = "https://en.wikipedia.org/wiki/Temple_of_Artemis"
            }
        ]);

        var sut = new SearchImportService(dbContext, wikipediaService, NullLogger<SearchImportService>.Instance);

        var imported = await sut.ImportTopicAsync("temple", 1);
        var placeId = Assert.Single(imported).PlaceId;

        var hidden = await sut.HidePlaceAsync(placeId);
        var markers = await sut.GetMarkersAsync("temple");

        Assert.True(hidden);
        Assert.Empty(markers);
        Assert.True(await dbContext.Places.AnyAsync(p => p.Id == placeId && p.IsHidden));
    }

    [Fact]
    public async Task GetMarkersAsync_IncludeHiddenTrue_ReturnsHiddenRows()
    {
        await using var dbContext = CreateDbContext();
        var wikipediaService = new FakeWikipediaService(
        [
            new WikipediaPlaceResult
            {
                WikiPageId = 6001,
                Title = "Colossus of Rhodes",
                Extract = "A statue of the Greek sun-god Helios.",
                Latitude = 36.4511,
                Longitude = 28.2278,
                CanonicalUrl = "https://en.wikipedia.org/wiki/Colossus_of_Rhodes"
            }
        ]);

        var sut = new SearchImportService(dbContext, wikipediaService, NullLogger<SearchImportService>.Instance);

        var imported = await sut.ImportTopicAsync("statue", 1);
        var placeId = Assert.Single(imported).PlaceId;
        await sut.HidePlaceAsync(placeId);

        var hiddenExcluded = await sut.GetMarkersAsync("statue");
        var hiddenIncluded = await sut.GetMarkersAsync("statue", includeHidden: true);

        Assert.Empty(hiddenExcluded);
        var hiddenMarker = Assert.Single(hiddenIncluded);
        Assert.True(hiddenMarker.IsHidden);
    }

    [Fact]
    public async Task UnhidePlaceAsync_UnhidesPlaceAndReturnsVisibleMarkers()
    {
        await using var dbContext = CreateDbContext();
        var wikipediaService = new FakeWikipediaService(
        [
            new WikipediaPlaceResult
            {
                WikiPageId = 7001,
                Title = "Mausoleum at Halicarnassus",
                Extract = "A tomb built between 353 and 350 BC.",
                Latitude = 37.0381,
                Longitude = 27.4241,
                CanonicalUrl = "https://en.wikipedia.org/wiki/Mausoleum_at_Halicarnassus"
            }
        ]);

        var sut = new SearchImportService(dbContext, wikipediaService, NullLogger<SearchImportService>.Instance);

        var imported = await sut.ImportTopicAsync("mausoleum", 1);
        var placeId = Assert.Single(imported).PlaceId;

        await sut.HidePlaceAsync(placeId);
        var unhidden = await sut.UnhidePlaceAsync(placeId);
        var markers = await sut.GetMarkersAsync("mausoleum");

        Assert.True(unhidden);
        Assert.Single(markers);
        Assert.False(markers[0].IsHidden);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private sealed class FakeWikipediaService : IWikipediaService
    {
        private readonly IReadOnlyList<WikipediaPlaceResult> _results;

        public FakeWikipediaService(IReadOnlyList<WikipediaPlaceResult> results)
        {
            _results = results;
        }

        public Task<IReadOnlyList<WikipediaPlaceResult>> SearchPlacesAsync(string topic, int limit, int offset = 0, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((IReadOnlyList<WikipediaPlaceResult>)_results.Skip(offset).Take(limit).ToList());
        }
    }
}
