# WikiMapScraper

WikiMapScraper is an ASP.NET Core Razor Pages app that searches Wikipedia for coordinate-enabled places by topic, stores results in SQLite, and plots them on an interactive Leaflet map.

## What It Does

- Searches Wikipedia for places by topic (for example: pyramid, lighthouse, statue).
- Imports matching place data (title, summary, coordinates, thumbnail, URL).
- Persists topics and places in a SQLite database.
- Shows markers on a map with per-topic color coding.
- Exposes lightweight JSON endpoints for search/import and marker retrieval.
- Includes a detailed markdown architecture guide that explains the full search pipeline.

## Tech Stack

- .NET 9 (`net9.0`)
- ASP.NET Core Razor Pages + minimal APIs
- Entity Framework Core 9 + SQLite
- Leaflet + OpenStreetMap tiles
- xUnit tests

## Repository Layout

- `WikiMapScraper.sln` - Solution file
- `WikiMapScraper.Web/` - Web app
- `WikiMapScraper.Web.Tests/` - Unit tests

## Prerequisites

- .NET 9 SDK installed
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

4. Open the URL shown in the terminal (typically `https://localhost:xxxx`).

## Running Tests

Run all tests:

```bash
dotnet test
```

## Detailed Architecture Guide

See the dedicated markdown guide:

- `API_GUIDE.md`

This document explains, in detail:

- How form input becomes API requests (`topic`, `limit`, `offset`)
- How Wikipedia results are filtered to coordinate-enabled pages
- How deduplication and topic-place linking work in SQLite
- How marker rendering and topic-level `Find More` pagination work
- How hide/unhide visibility rules affect results
- How Entity Framework Core is used in this app
- How Wikipedia retry/caching handles HTTP 429 and repeated requests

## Configuration

Main settings are in `WikiMapScraper.Web/appsettings.json`.

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

## Current Behavior Notes

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
