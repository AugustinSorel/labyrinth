using Labyrinth.Crawl;

namespace Labyrinth.Api.Abstractions;

/// <summary>
/// Factory for creating crawlers.
/// Provides abstraction to switch between local and remote crawler implementations.
/// </summary>
public interface ICrawlerFactory
{
    /// <summary>
    /// Creates a new crawler instance.
    /// </summary>
    /// <param name="appKey">The application key</param>
    /// <returns>A crawler instance</returns>
    Task<ICrawler> CreateCrawlerAsync(string appKey);
    
    /// <summary>
    /// Gets all crawlers for the specified application key.
    /// </summary>
    /// <param name="appKey">The application key</param>
    /// <returns>List of crawlers</returns>
    Task<IEnumerable<ICrawler>> GetCrawlersAsync(string appKey);
}
