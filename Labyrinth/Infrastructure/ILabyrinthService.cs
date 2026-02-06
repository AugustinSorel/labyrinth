namespace Labyrinth.Infrastructure;

public interface ILabyrinthService
{
    Task<IEnumerable<CrawlerDto>> GetCrawlersAsync();
    Task<CrawlerDto> CreateCrawlerAsync(SettingsDto? settings = null);
    Task<CrawlerDto> GetCrawlerByIdAsync(Guid id);
    Task<CrawlerDto> UpdateCrawlerAsync(Guid id, CrawlerDto crawler);
    Task DeleteCrawlerAsync(Guid id);
    Task<IEnumerable<InventoryItemDto>> GetBagAsync(Guid id);
    Task<IEnumerable<InventoryItemDto>> UpdateBagAsync(Guid id, IEnumerable<InventoryItemDto> items);
    Task<IEnumerable<InventoryItemDto>> GetItemsAsync(Guid id);
    Task<IEnumerable<InventoryItemDto>> UpdateItemsAsync(Guid id, IEnumerable<InventoryItemDto> items);
    Task<IEnumerable<GroupInfoDto>> GetGroupsAsync();
}
