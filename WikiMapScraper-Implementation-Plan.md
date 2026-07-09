# WikiMapScraper .NET Project Plan

## 1. Project Goal
Build a .NET web application where a user enters a topic (for example: pyramid, lighthouse, statue), the app finds Wikipedia pages with coordinates, stores the data in SQLite via Entity Framework Core, and shows map pins with topic-specific colors. Clicking a pin opens a popup containing:
- Wikipedia article title
- First few words of summary
- Thumbnail image

## 2. MVP Scope
- One web page with map + topic input
- One topic search imports 1 Wikipedia result initially
- Persist data in SQLite with EF Core
- Display imported pin on map
- Popup displays title, snippet, thumbnail
- Different topics render with different marker colors

## 3. Architecture (MVP)
- Frontend: ASP.NET Core Razor Pages + Leaflet.js
- Backend: ASP.NET Core endpoints (JSON)
- Data: EF Core + SQLite
- External source: Wikipedia APIs (preferred over raw HTML scraping for reliability)

## 4. Proposed Solution Structure
- WikiMapScraper.sln
- WikiMapScraper.Web/
  - Pages/
  - Controllers/ or minimal API endpoints
  - Services/
  - Data/
  - Models/
  - wwwroot/js/
  - wwwroot/css/

## 5. Implementation Phases

### Phase 0: Prerequisites (0.5h)
1. Confirm .NET SDK 8+ is installed.
2. Create project folder and initialize solution.
3. Verify app runs locally.

Suggested commands:

```powershell
dotnet --version
dotnet new sln -n WikiMapScraper
dotnet new webapp -n WikiMapScraper.Web
dotnet sln add .\WikiMapScraper.Web\
cd .\WikiMapScraper.Web\
dotnet run
```

### Phase 1: Add dependencies and baseline setup (0.5h)
1. Add EF Core and SQLite packages.
2. Add design-time tools for migrations.
3. Add HttpClient usage pattern (typed client or service).

Suggested commands:

```powershell
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Microsoft.EntityFrameworkCore.Tools
```

### Phase 2: Domain model and database schema (1h)
Create entities:
1. Topic
- Id
- Name (unique)
- ColorHex
- CreatedUtc

2. Place
- Id
- WikiPageId (unique)
- Title
- Extract
- ThumbnailUrl
- Latitude
- Longitude
- CanonicalUrl
- FetchedUtc

3. TopicPlace (join table)
- Id
- TopicId
- PlaceId
- Unique index on (TopicId, PlaceId)

Tasks:
1. Create AppDbContext.
2. Configure relationships and indexes in Fluent API.
3. Add connection string in appsettings.json.

### Phase 3: Migrations and DB initialization (0.5h)
1. Create initial migration.
2. Apply migration to local SQLite database.
3. Validate DB file creation and schema.

Suggested commands:

```powershell
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### Phase 4: Wikipedia integration service (2h)
Create a service interface and implementation:
- IWikipediaService
- WikipediaService

Workflow:
1. Search by topic (start with top 1 result).
2. Read page summary for title/extract/thumbnail/url.
3. Retrieve coordinates.
4. Return normalized DTO to app layer.

Notes:
- Prefer API-first over HTML scraping.
- Add timeout and robust null checks.
- Skip pages with missing coordinates.

### Phase 5: Import/use-case service (1.5h)
Create SearchImportService:
1. Normalize topic text.
2. Find or create Topic row.
3. Assign deterministic topic color.
4. Call WikipediaService for one result.
5. Upsert Place by WikiPageId.
6. Link TopicPlace if not already linked.
7. Return marker DTO(s) for UI.

### Phase 6: API endpoints (1h)
Implement endpoints:
1. POST /api/search
- Input: topic string
- Behavior: import from Wikipedia and persist
- Output: imported marker(s)

2. GET /api/markers
- Optional query: topic
- Output: all persisted marker DTOs with topic color

Marker DTO fields:
- latitude
- longitude
- title
- extractPreview
- thumbnailUrl
- topicName
- topicColor
- canonicalUrl

### Phase 7: Map UI with topic search (2h)
1. Add Leaflet CSS/JS.
2. Build page layout:
- Search text box
- Search button
- Map container
- Optional legend for topic colors

3. On submit:
- POST to /api/search
- Refresh markers via /api/markers

4. Render markers:
- Color marker by topicColor
- Bind popup with title, preview text, thumbnail image, link

### Phase 8: Topic color strategy (0.75h)
Implement stable color assignment:
1. Hash topic name to hue.
2. Use fixed saturation/lightness.
3. Convert HSL to hex and persist in Topic.ColorHex.
4. Ensure same topic always gets same color.

### Phase 9: Validation, logging, and UX polish (1h)
1. Validate topic input (required, min/max length).
2. Add friendly messages for:
- no results
- no coordinates found
- external API error

3. Add basic request/response logging for search operations.
4. Add loading state while fetching.

### Phase 10: Testing (2h)
Unit tests:
1. Topic color generator determinism.
2. Upsert and dedup logic.
3. Extract preview truncation.

Integration tests:
1. EF Core + SQLite migration test.
2. Endpoint test with mocked Wikipedia service.

Manual smoke tests:
1. Search pyramid -> one marker displayed.
2. Search lighthouse -> different color marker.
3. Clicking each pin shows title/snippet/thumbnail.

## 6. Acceptance Criteria
1. User can enter topic and submit.
2. At least one Wikipedia page with coordinates is imported.
3. Data persists in SQLite and survives restart.
4. Marker appears on map with topic-specific color.
5. Popup shows title, snippet, and thumbnail.
6. Subsequent topic searches appear simultaneously with different colors.

## 7. Estimated Timeline
- Phase 0-3: 2.5h
- Phase 4-6: 4.5h
- Phase 7-9: 3.75h
- Phase 10: 2h

Total: about 12.75 hours (1.5 to 2 working days)

## 8. Risks and Mitigations
1. Missing coordinates in some Wikipedia pages
- Mitigation: skip and fetch next candidate (future enhancement).

2. Inconsistent thumbnail availability
- Mitigation: make thumbnail optional in popup.

3. Duplicate imports
- Mitigation: unique indexes + upsert logic.

4. API instability/rate issues
- Mitigation: retries, timeouts, and caching (future enhancement).

## 9. Next Iterations (Post-MVP)
1. Increase results from 1 to configurable N.
2. Add filters/toggles per topic.
3. Add clustering for dense pins.
4. Add API response caching.
5. Add Wikipedia HTML scraping fallback if API data is incomplete.

## 10. Suggested Task Checklist for Execution
1. Scaffold solution and web app.
2. Add EF Core + SQLite packages.
3. Create entities and DbContext.
4. Create initial migration and update DB.
5. Implement Wikipedia service.
6. Implement import service.
7. Build API endpoints.
8. Build Leaflet UI and popup rendering.
9. Add topic color hashing and persistence.
10. Add tests and smoke test scenarios.
