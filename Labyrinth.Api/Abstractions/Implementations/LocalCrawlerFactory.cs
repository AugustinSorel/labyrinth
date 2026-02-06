using Labyrinth.Api.Abstractions;
using Labyrinth.Crawl;
using System.Collections.Concurrent;

namespace Labyrinth.Api.Abstractions.Implementations;

/// <summary>
/// Factory for creating local crawlers using in-memory maze logic.
/// </summary>
public class LocalCrawlerFactory : ICrawlerFactory
{
    private readonly ConcurrentDictionary<string, Maze> _mazes = new();
    private readonly ConcurrentDictionary<string, List<ICrawler>> _crawlers = new();
    
    private const string DefaultMazeAscii = @"+--+--------+
|  /        |
|  +--+--+  |
|     |k    |
+--+  |  +--+
   |k  x    |
+  +-------/|
|           |
+-----------+";
    
    public Task<ICrawler> CreateCrawlerAsync(string appKey)
    {
        var maze = _mazes.GetOrAdd(appKey, _ => new Maze(DefaultMazeAscii));
        var crawler = maze.NewCrawler();
        
        var crawlerList = _crawlers.GetOrAdd(appKey, _ => new List<ICrawler>());
        crawlerList.Add(crawler);
        
        return Task.FromResult(crawler);
    }
    
    public Task<IEnumerable<ICrawler>> GetCrawlersAsync(string appKey)
    {
        if (_crawlers.TryGetValue(appKey, out var crawlers))
        {
            return Task.FromResult<IEnumerable<ICrawler>>(crawlers);
        }
        
        return Task.FromResult(Enumerable.Empty<ICrawler>());
    }
}
