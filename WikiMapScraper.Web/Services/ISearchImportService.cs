using WikiMapScraper.Web.Models;

namespace WikiMapScraper.Web.Services;

public interface ISearchImportService
{
    Task<IReadOnlyList<MarkerDto>> ImportTopicAsync(string topic, int limit, int offset = 0, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MarkerDto>> GetMarkersAsync(string? topic, bool includeHidden = false, CancellationToken cancellationToken = default);
    Task<bool> HidePlaceAsync(int placeId, CancellationToken cancellationToken = default);
    Task<bool> UnhidePlaceAsync(int placeId, CancellationToken cancellationToken = default);
}
