using Labyrinth.Core;
using Labyrinth.Infrastructure;
using Labyrinth.ValueObjects;
using Labyrinth.Entities;

namespace Labyrinth.Application.Domain;

public class RemoteCrawler : ICrawler
{
    private readonly Guid _crawlerId;
    private readonly ILabyrinthService _service;
    private CrawlerDto _currentState;

    public RemoteCrawler(Guid crawlerId, ILabyrinthService service, CrawlerDto initialState)
    {
        _crawlerId = crawlerId;
        _service = service;
        _currentState = initialState;
    }

    public int X => _currentState.X;
    public int Y => _currentState.Y;
    public Direction Direction => _currentState.Direction.ToDomain();

    public bool CanMoveForward => _currentState.FacingTile != TileType.Wall 
                                   && _currentState.FacingTile != TileType.Outside;

    public async Task<TileType> GetFacingTileTypeAsync()
    {
        await RefreshStateAsync();
        return _currentState.FacingTile;
    }

    public async Task<bool> TryUnlockAsync(Inventory keyChain)
    {
        var facingTile = await GetFacingTileTypeAsync();
        if (facingTile != TileType.Door)
        {
            return false;
        }

        var bag = await _service.GetBagAsync(_crawlerId);
        return bag.Any(item => item.Type == ItemType.Key);
    }

    public async Task<Inventory?> TryWalkAsync(Inventory? keyChain)
    {
        if (!CanMoveForward)
        {
            return null;
        }

        var facingTile = await GetFacingTileTypeAsync();
        if (facingTile == TileType.Door)
        {
            var unlocked = await TryUnlockAsync(keyChain ?? new MyInventory());
            if (!unlocked)
            {
                return null;
            }
        }

        await RefreshStateAsync();

        var items = await _service.GetItemsAsync(_crawlerId);
        
        var inventory = new MyInventory();
        foreach (var itemDto in items)
        {
        }

        return inventory;
    }

    private async Task RefreshStateAsync()
    {
        _currentState = await _service.GetCrawlerByIdAsync(_crawlerId);
    }

    public async Task TurnAsync(Direction newDirection)
    {
        var updatedCrawler = _currentState with 
        { 
            Direction = newDirection.ToDto() 
        };
        
        _currentState = await _service.UpdateCrawlerAsync(_crawlerId, updatedCrawler);
    }
}
