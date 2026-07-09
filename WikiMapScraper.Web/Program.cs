using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Net;
using WikiMapScraper.Web.Data;
using WikiMapScraper.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddMemoryCache();
var databaseDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(databaseDirectory);
var databasePath = Path.Combine(databaseDirectory, "wikimap.db");
var connectionString = new SqliteConnectionStringBuilder
{
    DataSource = databasePath
}.ToString();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddHttpClient<IWikipediaService, WikipediaService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("WikiMapScraper/1.0");
});
builder.Services.AddScoped<ISearchImportService, SearchImportService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapPost("/api/search", async (SearchRequest request, ISearchImportService searchService, CancellationToken cancellationToken) =>
{
    var topic = request.Topic?.Trim() ?? string.Empty;
    if (string.IsNullOrWhiteSpace(topic))
    {
        return Results.BadRequest(new { error = "Topic is required." });
    }

    var resultLimit = request.Limit is > 0 and <= 10 ? request.Limit.Value : 5;
    var offset = request.Offset is >= 0 ? request.Offset.Value : 0;
    try
    {
        var importedMarkers = await searchService.ImportTopicAsync(topic, resultLimit, offset, cancellationToken);
        return Results.Ok(importedMarkers);
    }
    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
    {
        return Results.Json(new { error = "Wikipedia rate limit reached. Please wait a moment and try again." }, statusCode: StatusCodes.Status429TooManyRequests);
    }
    catch (HttpRequestException)
    {
        return Results.Json(new { error = "Wikipedia request failed. Please try again shortly." }, statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapGet("/api/markers", async (string? topic, ISearchImportService searchService, CancellationToken cancellationToken, bool includeHidden = false) =>
{
    var markers = await searchService.GetMarkersAsync(topic, includeHidden, cancellationToken);
    return Results.Ok(markers);
});

app.MapPost("/api/places/{placeId:int}/hide", async (int placeId, ISearchImportService searchService, CancellationToken cancellationToken) =>
{
    try
    {
        var hidden = await searchService.HidePlaceAsync(placeId, cancellationToken);
        return hidden ? Results.NoContent() : Results.NotFound();
    }
    catch (DbUpdateException)
    {
        return Results.Json(new { error = "Could not update visibility right now. Please try again." }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/api/places/{placeId:int}/unhide", async (int placeId, ISearchImportService searchService, CancellationToken cancellationToken) =>
{
    try
    {
        var unhidden = await searchService.UnhidePlaceAsync(placeId, cancellationToken);
        return unhidden ? Results.NoContent() : Results.NotFound();
    }
    catch (DbUpdateException)
    {
        return Results.Json(new { error = "Could not update visibility right now. Please try again." }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
}

app.Run();

internal sealed record SearchRequest(string? Topic, int? Limit, int? Offset);
