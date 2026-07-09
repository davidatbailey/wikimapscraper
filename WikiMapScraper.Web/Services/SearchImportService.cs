using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using WikiMapScraper.Web.Data;
using WikiMapScraper.Web.Models;
using WikiMapScraper.Web.Utilities;

namespace WikiMapScraper.Web.Services;

public class SearchImportService : ISearchImportService
{
    private readonly AppDbContext _dbContext;
    private readonly IWikipediaService _wikipediaService;
    private readonly ILogger<SearchImportService> _logger;

    public SearchImportService(AppDbContext dbContext, IWikipediaService wikipediaService, ILogger<SearchImportService> logger)
    {
        _dbContext = dbContext;
        _wikipediaService = wikipediaService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MarkerDto>> ImportTopicAsync(string topic, int limit, int offset = 0, CancellationToken cancellationToken = default)
    {
        var normalizedTopic = NormalizeTopic(topic);
        var normalizedTopicLower = normalizedTopic.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedTopic))
        {
            return [];
        }

        var normalizedLimit = Math.Clamp(limit, 1, 10);
        var normalizedOffset = Math.Max(0, offset);

        var topicEntity = await _dbContext.Topics
            .FirstOrDefaultAsync(t => t.Name.ToLower() == normalizedTopicLower, cancellationToken);

        if (topicEntity is null)
        {
            topicEntity = new Topic
            {
                Name = normalizedTopic,
                ColorHex = TopicColorGenerator.GenerateHexColor(normalizedTopic),
                CreatedUtc = DateTime.UtcNow
            };

            _dbContext.Topics.Add(topicEntity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var wikiResults = await _wikipediaService.SearchPlacesAsync(normalizedTopic, normalizedLimit, normalizedOffset, cancellationToken);

        if (wikiResults.Count == 0)
        {
            _logger.LogInformation("No importable Wikipedia places found for topic '{Topic}'.", normalizedTopic);
            return [];
        }

        var importedPlaceIds = new List<int>();
        var seenPageIds = new HashSet<long>();

        foreach (var result in wikiResults)
        {
            if (!seenPageIds.Add(result.WikiPageId))
            {
                continue;
            }

            var place = await _dbContext.Places
                .FirstOrDefaultAsync(p => p.WikiPageId == result.WikiPageId, cancellationToken);

            if (place is not null && place.IsHidden)
            {
                continue;
            }

            if (place is null)
            {
                place = new Place
                {
                    WikiPageId = result.WikiPageId,
                    Title = result.Title,
                    Extract = result.Extract,
                    ThumbnailUrl = result.ThumbnailUrl,
                    Latitude = result.Latitude,
                    Longitude = result.Longitude,
                    CanonicalUrl = result.CanonicalUrl,
                    FetchedUtc = DateTime.UtcNow
                };

                _dbContext.Places.Add(place);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            else
            {
                place.Title = result.Title;
                place.Extract = result.Extract;
                place.ThumbnailUrl = result.ThumbnailUrl;
                place.Latitude = result.Latitude;
                place.Longitude = result.Longitude;
                place.CanonicalUrl = result.CanonicalUrl;
                place.FetchedUtc = DateTime.UtcNow;
            }

            var alreadyLinked = await _dbContext.TopicPlaces
                .AnyAsync(tp => tp.TopicId == topicEntity.Id && tp.PlaceId == place.Id, cancellationToken);

            if (!alreadyLinked)
            {
                _dbContext.TopicPlaces.Add(new TopicPlace
                {
                    TopicId = topicEntity.Id,
                    PlaceId = place.Id
                });

                importedPlaceIds.Add(place.Id);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (importedPlaceIds.Count == 0)
        {
            return [];
        }

        var importedTopicPlaces = await _dbContext.TopicPlaces
            .AsNoTracking()
            .Include(tp => tp.Topic)
            .Include(tp => tp.Place)
            .Where(tp => tp.TopicId == topicEntity.Id && importedPlaceIds.Contains(tp.PlaceId) && !tp.Place.IsHidden)
            .OrderBy(tp => tp.Place.Title)
            .ToListAsync(cancellationToken);

        return importedTopicPlaces.Select(MapTopicPlaceToMarker).ToList();
    }

    public async Task<IReadOnlyList<MarkerDto>> GetMarkersAsync(string? topic, bool includeHidden = false, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.TopicPlaces
            .AsNoTracking()
            .Include(tp => tp.Topic)
            .Include(tp => tp.Place)
            .AsQueryable();

        if (!includeHidden)
        {
            query = query.Where(tp => !tp.Place.IsHidden);
        }

        if (!string.IsNullOrWhiteSpace(topic))
        {
            var normalized = NormalizeTopic(topic);
            var normalizedLower = normalized.ToLowerInvariant();
            query = query.Where(tp => tp.Topic.Name.ToLower() == normalizedLower);
        }

        var topicPlaces = await query
            .OrderBy(tp => tp.Topic.Name)
            .ThenBy(tp => tp.Place.Title)
            .ToListAsync(cancellationToken);

        return topicPlaces.Select(MapTopicPlaceToMarker).ToList();
    }

    public async Task<bool> HidePlaceAsync(int placeId, CancellationToken cancellationToken = default)
    {
        var place = await _dbContext.Places.FirstOrDefaultAsync(p => p.Id == placeId, cancellationToken);
        if (place is null)
        {
            return false;
        }

        if (place.IsHidden)
        {
            return true;
        }

        place.IsHidden = true;
        place.HiddenUtc = DateTime.UtcNow;
        await SaveChangesWithRetryAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UnhidePlaceAsync(int placeId, CancellationToken cancellationToken = default)
    {
        var place = await _dbContext.Places.FirstOrDefaultAsync(p => p.Id == placeId, cancellationToken);
        if (place is null)
        {
            return false;
        }

        if (!place.IsHidden)
        {
            return true;
        }

        place.IsHidden = false;
        place.HiddenUtc = null;
        await SaveChangesWithRetryAsync(cancellationToken);
        return true;
    }

    private async Task SaveChangesWithRetryAsync(CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException ex) when (attempt < maxAttempts && IsTransientSqliteLock(ex))
            {
                var delay = TimeSpan.FromMilliseconds(120 * attempt);
                _logger.LogWarning(ex, "SQLite was busy while saving hide/unhide state. Retrying attempt {Attempt} in {DelayMs} ms.", attempt, delay.TotalMilliseconds);
                await Task.Delay(delay, cancellationToken);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool IsTransientSqliteLock(DbUpdateException ex)
    {
        var sqliteEx = ex.InnerException as SqliteException;
        return sqliteEx?.SqliteErrorCode is 5 or 6;
    }

    private static string NormalizeTopic(string topic)
    {
        return topic.Trim();
    }

    private static string BuildPreview(string extract, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(extract))
        {
            return "No summary available.";
        }

        if (extract.Length <= maxLength)
        {
            return extract;
        }

        var cut = extract[..maxLength];
        var lastSpace = cut.LastIndexOf(' ');
        if (lastSpace > 0)
        {
            cut = cut[..lastSpace];
        }

        return $"{cut}...";
    }

    private static MarkerDto MapTopicPlaceToMarker(TopicPlace topicPlace)
    {
        return new MarkerDto
        {
            PlaceId = topicPlace.Place.Id,
            IsHidden = topicPlace.Place.IsHidden,
            Latitude = topicPlace.Place.Latitude,
            Longitude = topicPlace.Place.Longitude,
            Title = topicPlace.Place.Title,
            ExtractPreview = BuildPreview(topicPlace.Place.Extract, 120),
            ThumbnailUrl = topicPlace.Place.ThumbnailUrl,
            TopicName = topicPlace.Topic.Name,
            TopicColor = topicPlace.Topic.ColorHex,
            CanonicalUrl = topicPlace.Place.CanonicalUrl
        };
    }
}
