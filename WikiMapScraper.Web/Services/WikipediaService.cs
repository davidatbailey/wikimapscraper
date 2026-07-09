using System.Net.Http.Json;
using System.Net;
using Microsoft.Extensions.Caching.Memory;

namespace WikiMapScraper.Web.Services;

public class WikipediaService : IWikipediaService
{
	private static readonly TimeSpan SearchCacheTtl = TimeSpan.FromMinutes(30);
	private static readonly TimeSpan ResourceCacheTtl = TimeSpan.FromHours(6);
	private const int MaxRateLimitRetryAttempts = 3;

	private readonly HttpClient _httpClient;
	private readonly IMemoryCache _cache;
	private readonly ILogger<WikipediaService> _logger;

	public WikipediaService(HttpClient httpClient, IMemoryCache cache, ILogger<WikipediaService> logger)
	{
		_httpClient = httpClient;
		_cache = cache;
		_logger = logger;
	}

	public async Task<IReadOnlyList<WikipediaPlaceResult>> SearchPlacesAsync(string topic, int limit, int offset = 0, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(topic))
		{
			return [];
		}

		var normalizedLimit = Math.Clamp(limit, 1, 10);
		var normalizedOffset = Math.Max(0, offset);
		var cacheKey = $"wiki-search:{topic.Trim().ToLowerInvariant()}:{normalizedLimit}:{normalizedOffset}";

		if (_cache.TryGetValue(cacheKey, out IReadOnlyList<WikipediaPlaceResult>? cachedResults))
		{
			return cachedResults ?? [];
		}

		var results = new List<WikipediaPlaceResult>();
		var searchOffset = normalizedOffset;
		var maxAttempts = 5;

		for (var attempt = 0; attempt < maxAttempts && results.Count < normalizedLimit; attempt++)
		{
			var searchUrl = BuildSearchUrl(topic.Trim(), normalizedLimit, searchOffset);
			var searchData = await GetFromJsonWithRateLimitRetryAsync<WikipediaSearchResponse>(searchUrl, cancellationToken);

			var hits = searchData?.Query?.Search;
			if (hits is null || hits.Count == 0)
			{
				break;
			}

			foreach (var hit in hits)
			{
				if (results.Count >= normalizedLimit)
				{
					break;
				}

				var pageTitle = hit.Title;
				var summaryUrl = $"https://en.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(pageTitle)}";
				WikipediaSummaryResponse? summary;
				try
				{
					summary = await GetFromJsonWithRateLimitRetryAsync<WikipediaSummaryResponse>(summaryUrl, cancellationToken);
				}
				catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
				{
					_logger.LogWarning("Wikipedia rate limit reached while fetching summary for '{Title}'.", pageTitle);
					break;
				}

				if (summary is null)
				{
					continue;
				}

				var coordinates = summary.Coordinates;
				if (coordinates is null)
				{
					_logger.LogInformation("Skipping Wikipedia page '{Title}' because no coordinates were returned.", pageTitle);
					continue;
				}

				var pageId = summary.PageId != 0 ? summary.PageId : hit.PageId;

				results.Add(new WikipediaPlaceResult
				{
					WikiPageId = pageId,
					Title = summary.Title ?? pageTitle,
					Extract = summary.Extract ?? string.Empty,
					ThumbnailUrl = summary.Thumbnail?.Source,
					Latitude = coordinates.Lat,
					Longitude = coordinates.Lon,
					CanonicalUrl = summary.ContentUrls?.Desktop?.Page ?? $"https://en.wikipedia.org/wiki/{Uri.EscapeDataString(pageTitle)}"
				});
			}

			searchOffset += hits.Count;
		}

		var finalResults = results.AsReadOnly();
		_cache.Set(cacheKey, finalResults, new MemoryCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = SearchCacheTtl
		});

		return finalResults;
	}

	private async Task<T?> GetFromJsonWithRateLimitRetryAsync<T>(string url, CancellationToken cancellationToken)
	{
		if (_cache.TryGetValue(url, out T? cached))
		{
			return cached;
		}

		for (var attempt = 1; attempt <= MaxRateLimitRetryAttempts; attempt++)
		{
			try
			{
				var value = await _httpClient.GetFromJsonAsync<T>(url, cancellationToken);
				if (value is not null)
				{
					_cache.Set(url, value, new MemoryCacheEntryOptions
					{
						AbsoluteExpirationRelativeToNow = ResourceCacheTtl
					});
				}

				return value;
			}
			catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests && attempt < MaxRateLimitRetryAttempts)
			{
				var delay = TimeSpan.FromMilliseconds(300 * attempt);
				_logger.LogWarning(ex, "Wikipedia rate limit hit on attempt {Attempt} for {Url}. Retrying in {DelayMs} ms.", attempt, url, delay.TotalMilliseconds);
				await Task.Delay(delay, cancellationToken);
			}
		}

		return default;
	}

	private static string BuildSearchUrl(string topic, int limit, int offset)
	{
		return "https://en.wikipedia.org/w/api.php"
			+ "?action=query"
			+ "&list=search"
			+ "&format=json"
			+ "&origin=*"
			+ $"&srlimit={limit * 4}"
			+ $"&sroffset={offset}"
			+ $"&srsearch={Uri.EscapeDataString(topic)}";
	}

	private sealed class WikipediaSearchResponse
	{
		public WikipediaQuery? Query { get; set; }
	}

	private sealed class WikipediaQuery
	{
		public List<WikipediaSearchHit> Search { get; set; } = [];
	}

	private sealed class WikipediaSearchHit
	{
		public long PageId { get; set; }
		public string Title { get; set; } = string.Empty;
	}

	private sealed class WikipediaSummaryResponse
	{
		public long PageId { get; set; }
		public string? Title { get; set; }
		public string? Extract { get; set; }
		public WikipediaCoordinates? Coordinates { get; set; }
		public WikipediaThumbnail? Thumbnail { get; set; }
		public WikipediaContentUrls? ContentUrls { get; set; }
	}

	private sealed class WikipediaCoordinates
	{
		public double Lat { get; set; }
		public double Lon { get; set; }
	}

	private sealed class WikipediaThumbnail
	{
		public string? Source { get; set; }
	}

	private sealed class WikipediaContentUrls
	{
		public WikipediaDesktopLink? Desktop { get; set; }
	}

	private sealed class WikipediaDesktopLink
	{
		public string? Page { get; set; }
	}
}
