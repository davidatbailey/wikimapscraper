namespace WikiMapScraper.Web.Models;

public class TopicPlace
{
    public int Id { get; set; }

    public int TopicId { get; set; }
    public Topic Topic { get; set; } = null!;

    public int PlaceId { get; set; }
    public Place Place { get; set; } = null!;
}
