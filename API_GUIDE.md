# How WikiMapScraper works

This guide is written for someone who may be new to web APIs, Entity Framework Core, databases, and mapping libraries.

## 1. What it does

- Searches Wikipedia for places by topic (for example: pyramid, lighthouse, statue).
- Imports matching place data (title, summary, coordinates, thumbnail, URL).
- Persists topics and places in a SQLite database.
- Shows markers on a map with per-topic color coding.
- Exposes lightweight JSON endpoints for search/import and marker retrieval.
- Includes a detailed markdown architecture guide that explains the full search pipeline.

## 2. User flow

When the user searches:

1. Browser sends `POST /api/search` with `topic`, `limit`, and `offset`.
2. API validates inputs and calls `SearchImportService.ImportTopicAsync`.
3. Service asks `WikipediaService` for candidates and page summaries.
4. Results without coordinates are skipped.
5. Valid places are inserted or updated in SQLite via EF Core.
6. Topic-place links are inserted only when missing.
7. Browser calls marker endpoints and redraws list + map markers.

This pipeline means the app can absorb unreliable external data and still maintain clean internal records. The same place can appear under multiple topics without being stored more than once.

## 3. Why the frontend uses Leaflet

The map UI is built in `WikiMapScraper.Web/Pages/Index.cshtml` with Leaflet.

Leaflet is lightweight and easy to integrate in server-rendered pages, works seamlessly with OpenStreetMap tile servers, and has mature APIs for markers, popups, and layers. It gives enough control for custom marker color, popup content, and focus behavior without the overhead of a full frontend framework.

## 4. API endpoints and their job boundaries

Endpoints are defined in `WikiMapScraper.Web/Program.cs`.

### `POST /api/search`

Imports a page-sized batch of places for a topic. It trims and validates the topic, clamps the limit to safe bounds (`1..10`, default `5`), and uses `offset` to support per-topic pagination. External failures are converted to user-friendly responses so the UI always has something sensible to show.

### `GET /api/markers`

Returns marker data objects (DTOs) for the map and topic list — each one is a small, flat object containing only the fields the UI needs, like title, coordinates, and color. Accepts an optional topic filter and an optional `includeHidden=true` flag for visibility management. Keeping this endpoint separate from import means map redraws are cheap — they never need to contact Wikipedia.

### `POST /api/places/{id}/hide` and `POST /api/places/{id}/unhide`

Toggle a place's visibility without deleting its record. Both endpoints update `IsHidden` and `HiddenUtc`, use the transient lock retry logic in the service layer, and return a retry-friendly error if the database cannot commit right now. Treating visibility as a soft toggle rather than a delete keeps the action fully reversible.

## 5. Entity Framework Core explained

EF Core acts as a translator between C# objects and a relational database. You work with plain objects (`Topic`, `Place`, `TopicPlace`), and EF Core tracks what has changed,generates the SQL to and writes only the differences back to the database.

### 5.1 Core entities

- `Topic`: a search term, with a deterministic color assigned per name.
- `Place`: the canonical Wikipedia place record — coordinates, title, extract, thumbnail, URL.
- `TopicPlace`: a join entity connecting topics and places in a many-to-many relationship.

Sharing one `Place` row across many topics avoids storing duplicate data and means a field update (for example a refreshed extract) only needs to happen in one place.

### 5.2 DbContext and migrations

`AppDbContext` is EF's connection point to the database — it is where queries are sent and where changes get saved. At startup, `Program.cs` calls `dbContext.Database.Migrate()`, which compares the migration history against the database and applies anything missing automatically. This removes a manual setup step and prevents the database getting out of sync with the code as the schema evolves.

### 5.3 Common query patterns in this project

- `AsNoTracking()` on read-only marker queries — EF skips change tracking overhead for rows it will never write back.
- `Include(...)` eager-loads related entities in one query rather than issuing extra queries per row.
- `AnyAsync(...)` before inserting into the join table checks whether that relationship already exists, preventing duplicate `TopicPlace` rows.
- `FirstOrDefaultAsync(...)` for existing-row lookups by id or page id.

These patterns keep read-heavy paths fast and write paths safe when records might already exist.

### 5.4 Deduplication strategy

The import path deduplicates at three layers:

1. In-memory per batch (`HashSet<long> seenPageIds`, a collection that automatically rejects duplicate values) — catches duplicates within a single Wikipedia response.
2. Persistent place identity (`WikiPageId`) — checks the database before inserting a new place.
3. Topic-place relationship existence check — skips the join row if it already exists.

External APIs are not always consistent, and users can re-search the same topic. Deduplicating at each layer independently protects data integrity under both conditions.

### 5.5 Visibility model (soft hide)

Hide/unhide updates fields rather than deleting rows:

- Hide: `IsHidden = true`, set `HiddenUtc`.
- Unhide: `IsHidden = false`, clear `HiddenUtc`.

The place record and its topic associations are always preserved. The user can undo a hide at any time, and no data is lost.

## 6. Why transient lock handling exists (SQLite)

SQLite is a file-based database. It is reliable for this kind of app, but because it serializes writes to a single file, concurrent writes can temporarily block each other. When the user toggles visibility for several places in quick succession, a "database is busy/locked" error can surface.

`SaveChangesWithRetryAsync` handles this by retrying a few times with a short increasing backoff, but only for known transient lock error codes. Without it, users would see random 500 errors from entirely normal actions; with it, brief contention is silently absorbed.

## 7. Wikipedia integration: retries and caching

### 7.1 Two-step enrichment

Fetching useful data from Wikipedia requires two calls per place:

1. Search endpoint (`w/api.php`) for candidate page titles.
2. Summary endpoint (`rest_v1/page/summary/{title}`) per candidate for coordinates and content.

The search response alone does not reliably include coordinates, so the summary call is what makes the marker actually plottable on the map.

### 7.2 Handling HTTP 429 (Too Many Requests)

`GetFromJsonWithRateLimitRetryAsync<T>()` retries up to a bounded attempt count. On a 429 response it waits and retries with an increasing delay, logging each attempt for diagnostics. Rate limiting is entirely normal with public APIs, and controlled retry turns those transient failures into eventual successes rather than user-visible errors.

### 7.3 Caching

`IMemoryCache` is used at two horizons:

- Search-list results: shorter lifetime (~30 minutes).
- Per-resource summaries: longer lifetime (~6 hours).

Caching reduces the volume of outgoing requests, which directly lowers the chance of hitting Wikipedia's rate limit again, and makes repeat searches for recent topics instant.

## 8. How Find More works

The 'Find More' belongs to each individual topic header. The UI stores `topicOffsets`, a JavaScript `Map` (a key-value lookup — not the geographical map — where each topic name is paired with a number representing how many results have already been loaded for it). Clicking Find More sends `POST /api/search` with that topic's name and its current offset, then increments only that topic's offset on success.

This means users can load more results for one topic without disrupting any other topic's pagination state.

## 9. Notable coding techniques

These are the implementation patterns that make the app reliable and predictable as data grows.

### 9.1 DTO boundary (`MarkerDto`)

The API returns `MarkerDto` objects to the browser instead of returning EF entities directly. In practice, that means the UI gets only what it needs (title, coordinates, topic color, and related display fields), while persistence details stay private to the server layer. This keeps the API contract stable even when the underlying entity model evolves. The cost is explicit mapping code, but the boundary is worth it because it prevents accidental coupling between database structure and frontend behavior.

### 9.2 Layered deduplication

Deduplication is intentionally done in three places because each layer protects against a different kind of duplicate:

1. In-memory batch dedupe with `HashSet<long> seenPageIds` catches duplicates inside one Wikipedia response.
2. Persistent identity dedupe via `WikiPageId` prevents duplicate `Place` rows in the database.
3. Relationship dedupe checks for existing `TopicPlace` links before insert.

This is deliberately redundant. A single dedupe check is usually not enough when data can repeat across API responses, repeated user searches, and many-to-many relationships.

### 9.3 Soft visibility model (`IsHidden`, `HiddenUtc`)

Hide/unhide updates state fields on existing records rather than deleting rows. That gives reversible user actions, keeps topic associations intact, and preserves audit-friendly metadata in `HiddenUtc`. The operational implication is that read paths must consistently filter hidden rows unless `includeHidden=true` is explicitly requested.

### 9.4 `AsNoTracking()` on read paths

Display-focused queries use `AsNoTracking()` so EF Core does not allocate change-tracking state for entities that will never be updated in that request. For marker-heavy read paths, this reduces memory and tracking overhead and improves throughput. The tradeoff is simple: those result objects are read-only in spirit, because EF is not tracking them for later update.

### 9.5 Retry and cache together

The integration layer combines retry and caching because they solve different time horizons of the same reliability problem. Retry in `GetFromJsonWithRateLimitRetryAsync<T>()` handles short-lived failures like HTTP 429. `IMemoryCache` reduces repeated upstream calls for recently requested searches and summaries. Together, they increase request success rate now and lower failure probability later, with the expected tradeoff that cached responses can be slightly stale during TTL windows.

### 9.6 Per-topic pagination state (`topicOffsets`)

The frontend stores pagination offsets per topic in a JavaScript `Map` named `topicOffsets`. So when a user clicks Find More for one topic, only that topic advances. Other topics keep their own offsets untouched. This adds a small amount of client-side state management, but it makes multi-topic behavior consistent and easy for users to reason about.
