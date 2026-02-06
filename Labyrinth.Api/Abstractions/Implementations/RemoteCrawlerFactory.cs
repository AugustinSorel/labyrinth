using Labyrinth.Api.Abstractions;
using Labyrinth.Crawl;

namespace Labyrinth.Api.Abstractions.Implementations;

/// <summary>
/// Factory for creating remote crawlers via HTTP API.
/// </summary>
public class RemoteCrawlerFactory : ICrawlerFactory
{
    private readonly string _baseUrl;
    
    public RemoteCrawlerFactory(string baseUrl)
    {
        _baseUrl = baseUrl;
    }
    
    public async Task<ICrawler> CreateCrawlerAsync(string appKey)
    {
        return await ApiCrawler.CreateAsync(_baseUrl, appKey, null);
    }
    
    public async Task<IEnumerable<ICrawler>> GetCrawlersAsync(string appKey)
    {
        // Not implemented in current ApiCrawler
        // This would require API endpoint to list all crawlers
        await Task.CompletedTask;
        return Enumerable.Empty<ICrawler>();
    }
}
