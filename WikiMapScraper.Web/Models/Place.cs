namespace WikiMapScraper.Web.Models;

public class Place
{
    public int Id { get; set; }
    public long WikiPageId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Extract { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string CanonicalUrl { get; set; } = string.Empty;
    public DateTime FetchedUtc { get; set; } = DateTime.UtcNow;
    public bool IsHidden { get; set; }
    public DateTime? HiddenUtc { get; set; }

    public List<TopicPlace> TopicPlaces { get; set; } = new();
}
