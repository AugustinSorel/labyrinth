using Labyrinth.Api.Models;
using Labyrinth.Tiles;

namespace Labyrinth.Api.Services;


public interface ILabyrinthService
{
    
    Task<CrawlerDto> CreateCrawlerAsync(Guid appKey, SettingsDto? settings);
    
   
    Task<IEnumerable<CrawlerDto>> GetCrawlersAsync(Guid appKey);
    
    
    Task<CrawlerDto?> GetCrawlerAsync(Guid crawlerId, Guid appKey);
    
    
    Task<CrawlerDto?> UpdateCrawlerAsync(Guid crawlerId, Guid appKey, CrawlerUpdateDto update);
    
    
    Task<bool> DeleteCrawlerAsync(Guid crawlerId, Guid appKey);
    
    
    Task<IEnumerable<InventoryItemDto>?> GetCrawlerBagAsync(Guid crawlerId, Guid appKey);
    
    
    Task<bool> UpdateCrawlerBagAsync(Guid crawlerId, Guid appKey, IEnumerable<InventoryItemDto> items);
    
   
    Task<IEnumerable<InventoryItemDto>?> GetCrawlerLocationItemsAsync(Guid crawlerId, Guid appKey);
    
    
    Task<bool> UpdateCrawlerLocationItemsAsync(Guid crawlerId, Guid appKey, IEnumerable<InventoryItemDto> items);
}
