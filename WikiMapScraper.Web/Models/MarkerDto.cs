namespace WikiMapScraper.Web.Models;

public class MarkerDto
{
    public int PlaceId { get; init; }
    public bool IsHidden { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string Title { get; init; } = string.Empty;
    public string ExtractPreview { get; init; } = string.Empty;
    public string? ThumbnailUrl { get; init; }
    public string TopicName { get; init; } = string.Empty;
    public string TopicColor { get; init; } = "#2A7F62";
    public string CanonicalUrl { get; init; } = string.Empty;
}
