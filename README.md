# 🗺️ WikiMapScraper

WikiMapScraper is an ASP.NET Core Razor Pages app that searches Wikipedia for coordinate-enabled places by topic, stores results in SQLite, and plots them on an interactive Leaflet map.

## Purpose

WikiMapScraper solves a practical problem — how to locate types of things around the world by topic:

1. Take a human topic like "pyramid" or "lighthouse".
2. Find related Wikipedia pages that can be shown on a map.
3. Save useful results locally so the app can reuse them.
4. Let users progressively load more results and control visibility.

Online maps show well-known places, but they are not searchable by topic and tend to contain a lot of noise like business names. Wikipedia has rich place data with coordinates, but it is a live external service that can fail or rate-limit requests — both of which the app needs to handle gracefully. Storing results locally makes the data faster to access and gives consistent behavior on repeat visits.

https://github.com/user-attachments/assets/5bb6d97a-df8b-45da-8368-3f9b94f2d4c8

## Tech Stack

- .NET 10 (`net10.0`)
- ASP.NET Core Razor Pages + minimal APIs
- Entity Framework Core 10 + SQLite
- Leaflet + OpenStreetMap tiles
- xUnit tests

## Prerequisites

- .NET 10 SDK installed
- Optional for migration commands: `dotnet-ef` tool

Install EF CLI tool (if needed):

```bash
dotnet tool install --global dotnet-ef
```

## Getting Started

From the repository root (`wikimapscraper`):

1. Restore dependencies:

```bash
dotnet restore
```

2. Create/update the SQLite database:

```bash
dotnet ef database update --project WikiMapScraper.Web
```

3. Run the web app:

```bash
dotnet run --project WikiMapScraper.Web
```

4. Open the URL shown in the terminal (typically `https://localhost:5150`).

## Running Tests

Run all tests:

```bash
dotnet test
```

## Detailed Guide

`API_GUIDE.md` explains:

- How form input becomes API requests (`topic`, `limit`, `offset`)
- How Wikipedia results are filtered to coordinate-enabled pages
- How deduplication and topic-place linking work in SQLite
- How marker rendering and topic-level `Find More` pagination work
- How hide/unhide visibility rules affect results
- How Entity Framework Core is used in this app
- How Wikipedia retry/caching handles HTTP 429 and repeated requests

## Configuration

The main settings are in `WikiMapScraper.Web/appsettings.json`.

Default connection string:

```json
"ConnectionStrings": {
  "DefaultConnection": "Data Source=wikimap.db"
}
```

This creates `wikimap.db` in the app's working directory.

## API Endpoints

### POST `/api/search`

Imports places for a topic.

Request body:

```json
{
  "topic": "pyramid",
  "limit": 1
}
```

Notes:

- `topic` is required.
- `limit` is clamped to `1..10`.

### GET `/api/markers`

Returns all imported markers.

### GET `/api/markers?topic=pyramid`

Returns markers filtered by topic.

## Behavior Notes

- The UI submits search requests with `limit: 5` and paginates with offset for `Find More`.
- Topic names are used as entered (trimmed), and each topic gets a deterministic color.
- Re-importing the same topic/place pair does not create duplicate records.
- Popup thumbnails are displayed at a larger size to improve readability.

## Troubleshooting

- If you see database table errors, run the migration command again:

```bash
dotnet ef database update --project WikiMapScraper.Web
```

- If HTTPS dev certificate issues occur:

```bash
dotnet dev-certs https --trust
```

### Pls get in touch with any questions or requests 😊🌈