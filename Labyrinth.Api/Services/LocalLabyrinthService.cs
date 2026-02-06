using Labyrinth.Api.Models;
using Labyrinth.Crawl;
using Labyrinth.Items;
using Labyrinth.Tiles;
using System.Collections.Concurrent;

namespace Labyrinth.Api.Services;


public class LocalLabyrinthService : ILabyrinthService
{
    
    private readonly ConcurrentDictionary<Guid, CrawlerState> _crawlers = new();
    
    private readonly ConcurrentDictionary<Guid, Maze> _labyrinths = new();
    
    private const int MaxCrawlersPerAppKey = 3;
    
    private const string DefaultMazeAscii = @"+--+--------+
|  /        |
|  +--+--+  |
|     |k    |
+--+  |  +--+
   |k  x    |
+  +-------/|
|           |
+-----------+";

    public Task<CrawlerDto> CreateCrawlerAsync(Guid appKey, SettingsDto? settings)
    {
        var existingCount = _crawlers.Values.Count(c => c.AppKey == appKey);
        if (existingCount >= MaxCrawlersPerAppKey)
        {
            throw new InvalidOperationException($"App key {appKey} has reached the maximum of {MaxCrawlersPerAppKey} crawlers");
        }
        
        var maze = _labyrinths.GetOrAdd(appKey, _ => new Maze(DefaultMazeAscii));
        
        var crawlerId = Guid.NewGuid();
        
        var internalCrawler = maze.NewCrawler();
        
        var state = new CrawlerState
        {
            Id = crawlerId,
            AppKey = appKey,
            Crawler = internalCrawler,
            Maze = maze,
            Inventory = new MyInventory(),
            IsWalking = false
        };
        
        _crawlers[crawlerId] = state;
        
        return Task.FromResult(MapToDto(state));
    }
    
    public Task<IEnumerable<CrawlerDto>> GetCrawlersAsync(Guid appKey)
    {
        var crawlers = _crawlers.Values
            .Where(c => c.AppKey == appKey)
            .Select(MapToDto);
        
        return Task.FromResult(crawlers);
    }
    
    public Task<CrawlerDto?> GetCrawlerAsync(Guid crawlerId, Guid appKey)
    {
        if (!_crawlers.TryGetValue(crawlerId, out var state))
            return Task.FromResult<CrawlerDto?>(null);
            
        if (state.AppKey != appKey)
            return Task.FromResult<CrawlerDto?>(null);
            
        return Task.FromResult<CrawlerDto?>(MapToDto(state));
    }
    
    public async Task<CrawlerDto?> UpdateCrawlerAsync(Guid crawlerId, Guid appKey, CrawlerUpdateDto update)
    {
        if (!_crawlers.TryGetValue(crawlerId, out var state))
            return null;
            
        if (state.AppKey != appKey)
            return null;
        
        if (update.Direction.HasValue)
        {
            var targetDir = IntToDirection(update.Direction.Value);
            while (!state.Crawler.Direction.Equals(targetDir))
            {
                state.Crawler.TurnRight();
            }
        }
        
        if (update.IsWalking.HasValue)
        {
            var wasWalking = state.IsWalking;
            state.IsWalking = update.IsWalking.Value;
            
            if (!wasWalking && state.IsWalking)
            {
                await state.Crawler.TryWalkAsync(state.Inventory);
            }
        }
        
        return MapToDto(state);
    }
    
    public Task<bool> DeleteCrawlerAsync(Guid crawlerId, Guid appKey)
    {
        if (!_crawlers.TryGetValue(crawlerId, out var state))
            return Task.FromResult(false);
            
        if (state.AppKey != appKey)
            return Task.FromResult(false);
        
        var removed = _crawlers.TryRemove(crawlerId, out _);
        
        if (removed && !_crawlers.Values.Any(c => c.AppKey == appKey))
        {
            _labyrinths.TryRemove(appKey, out _);
        }
        
        return Task.FromResult(removed);
    }
    
    public Task<IEnumerable<InventoryItemDto>?> GetCrawlerBagAsync(Guid crawlerId, Guid appKey)
    {
        if (!_crawlers.TryGetValue(crawlerId, out var state))
            return Task.FromResult<IEnumerable<InventoryItemDto>?>(null);
            
        if (state.AppKey != appKey)
            return Task.FromResult<IEnumerable<InventoryItemDto>?>(null);
        
        var items = Enumerable.Range(0, state.Inventory.Count)
            .Select(_ => new InventoryItemDto("Key"));
        
        return Task.FromResult<IEnumerable<InventoryItemDto>?>(items);
    }
    
    public Task<bool> UpdateCrawlerBagAsync(Guid crawlerId, Guid appKey, IEnumerable<InventoryItemDto> items)
    {
        if (!_crawlers.TryGetValue(crawlerId, out var state))
            return Task.FromResult(false);
            
        if (state.AppKey != appKey)
            return Task.FromResult(false);
        
        state.Inventory = new MyInventory();
        foreach (var item in items.Where(i => i.Type == "Key"))
        {
            state.Inventory.AddItem(new Key());
        }
        
        return Task.FromResult(true);
    }
    
    public async Task<IEnumerable<InventoryItemDto>?> GetCrawlerLocationItemsAsync(Guid crawlerId, Guid appKey)
    {
        if (!_crawlers.TryGetValue(crawlerId, out var state))
            return null;
            
        if (state.AppKey != appKey)
            return null;
        
        var tileType = await state.Crawler.GetFacingTileTypeAsync();
        
        
        return Array.Empty<InventoryItemDto>();
    }
    
    public Task<bool> UpdateCrawlerLocationItemsAsync(Guid crawlerId, Guid appKey, IEnumerable<InventoryItemDto> items)
    {
        if (!_crawlers.TryGetValue(crawlerId, out var state))
            return Task.FromResult(false);
            
        if (state.AppKey != appKey)
            return Task.FromResult(false);
        
        
        return Task.FromResult(true);
    }
    
    
    private static CrawlerDto MapToDto(CrawlerState state)
    {
        return new CrawlerDto(
            state.Id,
            state.Crawler.X,
            state.Crawler.Y,
            DirectionToInt(state.Crawler.Direction),
            state.IsWalking,
            state.AppKey
        );
    }
    
    private static int DirectionToInt(Direction d) => d switch
    {
        var x when x.Equals(Direction.North) => 0,
        var x when x.Equals(Direction.East) => 1,
        var x when x.Equals(Direction.South) => 2,
        var x when x.Equals(Direction.West) => 3,
        _ => 0
    };
    
    private static Direction IntToDirection(int i) => i switch
    {
        0 => Direction.North,
        1 => Direction.East,
        2 => Direction.South,
        3 => Direction.West,
        _ => Direction.North
    };
    
    private class CrawlerState
    {
        public required Guid Id { get; init; }
        public required Guid AppKey { get; init; }
        public required ICrawler Crawler { get; init; }
        public required Maze Maze { get; init; }
        public required Inventory Inventory { get; set; }
        public bool IsWalking { get; set; }
    }
}
