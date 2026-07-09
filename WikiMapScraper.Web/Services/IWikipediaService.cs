namespace WikiMapScraper.Web.Services;

public interface IWikipediaService
{
    Task<IReadOnlyList<WikipediaPlaceResult>> SearchPlacesAsync(string topic, int limit, int offset = 0, CancellationToken cancellationToken = default);
}
