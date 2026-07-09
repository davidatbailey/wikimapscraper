namespace WikiMapScraper.Web.Services;

public class WikipediaPlaceResult
{
    public long WikiPageId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Extract { get; init; } = string.Empty;
    public string? ThumbnailUrl { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string CanonicalUrl { get; init; } = string.Empty;
}
