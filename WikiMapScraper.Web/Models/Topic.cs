namespace WikiMapScraper.Web.Models;

public class Topic
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#2A7F62";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public List<TopicPlace> TopicPlaces { get; set; } = new();
}
