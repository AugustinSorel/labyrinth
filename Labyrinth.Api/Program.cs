using Labyrinth.Api.Models;
using Labyrinth.Api.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ILabyrinthService, LocalLabyrinthService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null; 
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();


app.MapGet("/crawlers", async (
    [FromServices] ILabyrinthService service,
    [FromQuery(Name = "appKey")] Guid? appKey) =>
{
    if (!appKey.HasValue)
        return Results.Problem("A valid app key is required", statusCode: 401);
    
    var crawlers = await service.GetCrawlersAsync(appKey.Value);
    return Results.Ok(crawlers);
})
.WithName("GetCrawlers")
.WithTags("Crawlers")
.WithSummary("Retrieves all crawlers associated with the specified application key.")
.Produces<IEnumerable<CrawlerDto>>(200)
.Produces<ProblemDetails>(401);

app.MapPost("/crawlers", async (
    [FromServices] ILabyrinthService service,
    [FromQuery(Name = "appKey")] Guid? appKey,
    [FromBody] SettingsDto? settings) =>
{
    if (!appKey.HasValue)
        return Results.Problem("A valid app key is required", statusCode: 401);
    
    try
    {
        var crawler = await service.CreateCrawlerAsync(appKey.Value, settings);
        return Results.Created($"/crawlers/{crawler.Id}", crawler);
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("maximum"))
    {
        return Results.Problem(ex.Message, statusCode: 403);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
})
.WithName("CreateCrawler")
.WithTags("Crawlers")
.WithSummary("Creates a new labyrinth crawler instance from a maximum of 3 associated with the specified application key.")
.Produces<CrawlerDto>(201)
.Produces<ProblemDetails>(400)
.Produces<ProblemDetails>(401)
.Produces<ProblemDetails>(403);

app.MapGet("/crawlers/{id}", async (
    [FromServices] ILabyrinthService service,
    [FromRoute] Guid id,
    [FromQuery(Name = "appKey")] Guid? appKey) =>
{
    if (!appKey.HasValue)
        return Results.Problem("A valid app key is required", statusCode: 401);
    
    var crawler = await service.GetCrawlerAsync(id, appKey.Value);
    
    if (crawler == null)
        return Results.Problem("This app key cannot access to this crawler", statusCode: 403);
    
    return Results.Ok(crawler);
})
.WithName("GetCrawler")
.WithTags("Crawlers")
.WithSummary("Retrieves information about a specific crawler identified by its unique identifier.")
.Produces<CrawlerDto>(200)
.Produces<ProblemDetails>(401)
.Produces<ProblemDetails>(403);

app.MapPatch("/crawlers/{id}", async (
    [FromServices] ILabyrinthService service,
    [FromRoute] Guid id,
    [FromQuery(Name = "appKey")] Guid? appKey,
    [FromBody] CrawlerUpdateDto update) =>
{
    if (!appKey.HasValue)
        return Results.Problem("A valid app key is required", statusCode: 401);
    
    var crawler = await service.UpdateCrawlerAsync(id, appKey.Value, update);
    
    if (crawler == null)
        return Results.Problem("This app key cannot access to this crawler", statusCode: 403);
    
    return Results.Ok(crawler);
})
.WithName("UpdateCrawler")
.WithTags("Crawlers")
.WithSummary("Updates the direction and/or walking state of the specified crawler. The only way to MOVE THE CRAWLER.")
.Produces<CrawlerDto>(200)
.Produces<ProblemDetails>(401)
.Produces<ProblemDetails>(403);

app.MapDelete("/crawlers/{id}", async (
    [FromServices] ILabyrinthService service,
    [FromRoute] Guid id,
    [FromQuery(Name = "appKey")] Guid? appKey) =>
{
    if (!appKey.HasValue)
        return Results.Problem("A valid app key is required", statusCode: 401);
    
    var deleted = await service.DeleteCrawlerAsync(id, appKey.Value);
    
    if (!deleted)
        return Results.Problem("This app key cannot access to this crawler", statusCode: 403);
    
    return Results.NoContent();
})
.WithName("DeleteCrawler")
.WithTags("Crawlers")
.WithSummary("Deletes the specified crawler if the provided application key has access.")
.Produces(204)
.Produces<ProblemDetails>(401)
.Produces<ProblemDetails>(403);

app.MapGet("/crawlers/{id}/bag", async (
    [FromServices] ILabyrinthService service,
    [FromRoute] Guid id,
    [FromQuery(Name = "appKey")] Guid? appKey) =>
{
    if (!appKey.HasValue)
        return Results.Problem("A valid app key is required", statusCode: 401);
    
    var items = await service.GetCrawlerBagAsync(id, appKey.Value);
    
    if (items == null)
        return Results.Problem("This app key cannot access to this crawler", statusCode: 403);
    
    return Results.Ok(items);
})
.WithName("GetCrawlerBag")
.WithTags("Crawlers")
.WithSummary("Gets the list of items currently held in the specified crawler's inventory.")
.Produces<IEnumerable<InventoryItemDto>>(200)
.Produces<ProblemDetails>(401)
.Produces<ProblemDetails>(403);

app.MapPut("/crawlers/{id}/bag", async (
    [FromServices] ILabyrinthService service,
    [FromRoute] Guid id,
    [FromQuery(Name = "appKey")] Guid? appKey,
    [FromBody] IEnumerable<InventoryItemDto> items) =>
{
    if (!appKey.HasValue)
        return Results.Problem("A valid app key is required", statusCode: 401);
    
    var success = await service.UpdateCrawlerBagAsync(id, appKey.Value, items);
    
    if (!success)
        return Results.Problem("This app key cannot access to this crawler", statusCode: 403);
    
    return Results.NoContent();
})
.WithName("UpdateCrawlerBag")
.WithTags("Crawlers")
.WithSummary("Updates the inventory bag for the specified crawler. Frequently used to put one or more item in a room.")
.Produces(204)
.Produces<ProblemDetails>(401)
.Produces<ProblemDetails>(403);

app.MapGet("/crawlers/{id}/items", async (
    [FromServices] ILabyrinthService service,
    [FromRoute] Guid id,
    [FromQuery(Name = "appKey")] Guid? appKey) =>
{
    if (!appKey.HasValue)
        return Results.Problem("A valid app key is required", statusCode: 401);
    
    var items = await service.GetCrawlerLocationItemsAsync(id, appKey.Value);
    
    if (items == null)
        return Results.Problem("This app key cannot access to this crawler", statusCode: 403);
    
    return Results.Ok(items);
})
.WithName("GetCrawlerLocationItems")
.WithTags("Crawlers")
.WithSummary("Retrieves the list of inventory items currently associated with the current crawler location (i.e. tile).")
.Produces<IEnumerable<InventoryItemDto>>(200)
.Produces<ProblemDetails>(401)
.Produces<ProblemDetails>(403);

app.MapPut("/crawlers/{id}/items", async (
    [FromServices] ILabyrinthService service,
    [FromRoute] Guid id,
    [FromQuery(Name = "appKey")] Guid? appKey,
    [FromBody] IEnumerable<InventoryItemDto> items) =>
{
    if (!appKey.HasValue)
        return Results.Problem("A valid app key is required", statusCode: 401);
    
    var success = await service.UpdateCrawlerLocationItemsAsync(id, appKey.Value, items);
    
    if (!success)
        return Results.Problem("This app key cannot access to this crawler", statusCode: 403);
    
    return Results.NoContent();
})
.WithName("UpdateCrawlerLocationItems")
.WithTags("Crawlers")
.WithSummary("Updates the items placed in the tile of the specified crawler. Frequently used to TAKE one or more ITEM in a room and move them in the crawler's bag (i.e. inventory).")
.Produces(204)
.Produces<ProblemDetails>(401)
.Produces<ProblemDetails>(403);


app.MapGet("/Groups", () => Results.Ok(Array.Empty<object>()))
.WithName("GetGroups")
.WithTags("Groups")
.WithSummary("Retrieves all available registered player groups.")
.Produces<IEnumerable<object>>(200);

app.Run();
