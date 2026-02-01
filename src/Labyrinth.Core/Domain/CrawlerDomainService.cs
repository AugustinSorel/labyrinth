using Labyrinth.Crawl;
using Labyrinth.Dtos;
using Labyrinth.Items;

namespace Labyrinth.Core.Domain;

public class CrawlerDomainService
{
    private readonly ILabyrinthService _labyrinthService;

    public CrawlerDomainService(ILabyrinthService labyrinthService)
    {
        _labyrinthService = labyrinthService;
    }

    public async Task<ICrawler> CreateCrawlerAsync(SettingsDto? settings = null)
    {
        var crawlerDto = await _labyrinthService.CreateCrawlerAsync(settings);
        return new RemoteCrawler(crawlerDto.Id, _labyrinthService, crawlerDto);
    }

    public async Task<ICrawler> GetCrawlerAsync(Guid crawlerId)
    {
        var crawlerDto = await _labyrinthService.GetCrawlerByIdAsync(crawlerId);
        return new RemoteCrawler(crawlerDto.Id, _labyrinthService, crawlerDto);
    }

    public async Task<IEnumerable<ICrawler>> GetAllCrawlersAsync()
    {
        var crawlerDtos = await _labyrinthService.GetCrawlersAsync();
        return crawlerDtos.Select(dto => 
            new RemoteCrawler(dto.Id, _labyrinthService, dto) as ICrawler
        );
    }

    public async Task DeleteCrawlerAsync(Guid crawlerId)
    {
        await _labyrinthService.DeleteCrawlerAsync(crawlerId);
    }

    public async Task<Inventory> GetBagAsync(Guid crawlerId)
    {
        var itemDtos = await _labyrinthService.GetBagAsync(crawlerId);
        return ConvertToInventory(itemDtos);
    }

    public async Task UpdateBagAsync(Guid crawlerId, Inventory inventory)
    {
        var itemDtos = await ConvertFromInventoryAsync(inventory);
        await _labyrinthService.UpdateBagAsync(crawlerId, itemDtos);
    }

    public async Task<Inventory> GetItemsAsync(Guid crawlerId)
    {
        var itemDtos = await _labyrinthService.GetItemsAsync(crawlerId);
        return ConvertToInventory(itemDtos);
    }

    public Task<ExplorationResult> ExploreAsync(
        ICrawler crawler, 
        IExplorationStrategy strategy, 
        Inventory keyChain)
    {
        var result = new ExplorationResult();
        
        return Task.FromResult(result);
    }

    private Inventory ConvertToInventory(IEnumerable<InventoryItemDto> itemDtos)
    {
        var inventory = new MyInventory();
        
        foreach (var itemDto in itemDtos)
        {
            var item = itemDto.ToDomain();
        }

        return inventory;
    }

    private Task<IEnumerable<InventoryItemDto>> ConvertFromInventoryAsync(Inventory inventory)
    {
        return inventory.ToDtosAsync();
    }
}

public interface IExplorationStrategy
{
    Task ExecuteAsync(ICrawler crawler, Inventory keyChain);
}

public class ExplorationResult
{
    public bool Success { get; set; }
    public int StepsTaken { get; set; }
    public List<string> Log { get; set; } = new();
}
