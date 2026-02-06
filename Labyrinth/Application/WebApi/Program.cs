using Labyrinth.Infrastructure;
using Labyrinth.Application.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var appKey = builder.Configuration["Labyrinth:AppKey"] 
    ?? throw new InvalidOperationException("Labyrinth:AppKey configuration is required");

builder.Services.AddHttpClient<ILabyrinthService, LabyrinthService>(client =>
{
    client.BaseAddress = new Uri("https://labyrinth.syllab.com");
})
.ConfigureHttpClient((serviceProvider, client) => { })
.AddTypedClient((client, serviceProvider) => new LabyrinthService(client, appKey));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/crawlers", async (ILabyrinthService labyrinthService) =>
{
    var crawlers = await labyrinthService.GetCrawlersAsync();
    return Results.Ok(crawlers);
})
.WithName("GetCrawlers")
.WithOpenApi();

app.MapPost("/crawlers", async (SettingsDto? settings, ILabyrinthService labyrinthService) =>
{
    var crawler = await labyrinthService.CreateCrawlerAsync(settings);
    return Results.Created($"/crawlers/{crawler.Id}", crawler);
})
.WithName("CreateCrawler")
.WithOpenApi();

app.MapGet("/crawlers/{id:guid}", async (Guid id, ILabyrinthService labyrinthService) =>
{
    try
    {
        var crawler = await labyrinthService.GetCrawlerByIdAsync(id);
        return Results.Ok(crawler);
    }
    catch (InvalidOperationException)
    {
        return Results.NotFound();
    }
})
.WithName("GetCrawlerById")
.WithOpenApi();

app.MapPatch("/crawlers/{id:guid}", async (Guid id, CrawlerDto crawler, ILabyrinthService labyrinthService) =>
{
    try
    {
        var updated = await labyrinthService.UpdateCrawlerAsync(id, crawler);
        return Results.Ok(updated);
    }
    catch (InvalidOperationException)
    {
        return Results.NotFound();
    }
})
.WithName("UpdateCrawler")
.WithOpenApi();

app.MapDelete("/crawlers/{id:guid}", async (Guid id, ILabyrinthService labyrinthService) =>
{
    try
    {
        await labyrinthService.DeleteCrawlerAsync(id);
        return Results.NoContent();
    }
    catch (InvalidOperationException)
    {
        return Results.NotFound();
    }
})
.WithName("DeleteCrawler")
.WithOpenApi();

app.MapGet("/crawlers/{id:guid}/bag", async (Guid id, ILabyrinthService labyrinthService) =>
{
    try
    {
        var bag = await labyrinthService.GetBagAsync(id);
        return Results.Ok(bag);
    }
    catch (InvalidOperationException)
    {
        return Results.NotFound();
    }
})
.WithName("GetBag")
.WithOpenApi();

app.MapPut("/crawlers/{id:guid}/bag", async (Guid id, List<InventoryItemDto> items, ILabyrinthService labyrinthService) =>
{
    try
    {
        var updatedBag = await labyrinthService.UpdateBagAsync(id, items);
        return Results.Ok(updatedBag);
    }
    catch (InvalidOperationException)
    {
        return Results.NotFound();
    }
})
.WithName("UpdateBag")
.WithOpenApi();

app.MapGet("/crawlers/{id:guid}/items", async (Guid id, ILabyrinthService labyrinthService) =>
{
    try
    {
        var items = await labyrinthService.GetItemsAsync(id);
        return Results.Ok(items);
    }
    catch (InvalidOperationException)
    {
        return Results.NotFound();
    }
})
.WithName("GetItems")
.WithOpenApi();

app.MapPut("/crawlers/{id:guid}/items", async (Guid id, List<InventoryItemDto> items, ILabyrinthService labyrinthService) =>
{
    try
    {
        var updatedItems = await labyrinthService.UpdateItemsAsync(id, items);
        return Results.Ok(updatedItems);
    }
    catch (InvalidOperationException)
    {
        return Results.NotFound();
    }
})
.WithName("UpdateItems")
.WithOpenApi();

app.MapGet("/groups", async (ILabyrinthService labyrinthService) =>
{
    var groups = await labyrinthService.GetGroupsAsync();
    return Results.Ok(groups);
})
.WithName("GetGroups")
.WithOpenApi();
app.Run();
