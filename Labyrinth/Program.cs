using Labyrinth;
using Labyrinth.Crawl;
using Labyrinth.Exploration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

char OwnerChar(int ownerId) => (ownerId <= 9 && ownerId >= 1) ? (char)('0' + ownerId) : 'X';

string RenderMap(ExplorationMap map, Dictionary<int, (int X, int Y)> explorerPositions)
{
    if (!map.TryGet(out var snapshot)) return "";

    int minX = int.MaxValue, maxX = int.MinValue;
    int minY = int.MaxValue, maxY = int.MinValue;

    foreach (var kvp in snapshot)
    {
        if (kvp.Key.X < minX) minX = kvp.Key.X;
        if (kvp.Key.X > maxX) maxX = kvp.Key.X;
        if (kvp.Key.Y < minY) minY = kvp.Key.Y;
        if (kvp.Key.Y > maxY) maxY = kvp.Key.Y;
    }

    if (minX > maxX || minY > maxY) return "";

    var sb = new StringBuilder();

    for (int y = minY; y <= maxY; y++)
    {
        for (int x = minX; x <= maxX; x++)
        {
            bool isExplorer = false;
            foreach (var exp in explorerPositions)
            {
                if (exp.Value.X == x && exp.Value.Y == y)
                {
                    sb.Append(OwnerChar(exp.Key));
                    isExplorer = true;
                    break;
                }
            }

            if (!isExplorer)
            {
                var cellType = snapshot.TryGetValue((x, y), out var ct) ? ct : CellType.Unknown;
                sb.Append(cellType switch
                {
                    CellType.Visited => '.',
                    CellType.Start => 'S',
                    CellType.Wall => '#',
                    CellType.Door => '/',
                    CellType.Outside => 'O',
                    CellType.Empty => ' ',
                    _ => '?'
                });
            }
        }
        sb.AppendLine();
    }

    return sb.ToString();
}

var appKeyFromArgs = args.Length > 0 ? args[0] : null;

var map = new ExplorationMap();

const int ExplorerCount = 3;
var explorers = new List<BfsExplorer>();
var tasks = new List<Task>();
var explorerPositions = new Dictionary<int, (int X, int Y)>();

var useRemote = (Environment.GetEnvironmentVariable("LAB_USE_REMOTE") ?? "false").ToLowerInvariant() == "true";
var appKey = appKeyFromArgs ?? Environment.GetEnvironmentVariable("LAB_APP_KEY") ?? "D98E5988-58E3-4BCE-B050-46E1903E6777";
var baseUrl = Environment.GetEnvironmentVariable("LAB_BASE_URL") ?? "https://labyrinth.syllab.com";

if (!useRemote)
{
    Console.WriteLine("Set LAB_USE_REMOTE=true and LAB_APP_KEY to run.");
    return;
}

if (string.IsNullOrWhiteSpace(appKey))
{
    Console.WriteLine("LAB_APP_KEY is not set.");
    return;
}

Console.WriteLine("Creating crawlers...");

List<ApiCrawler> remoteCrawlersToDispose = new();
List<ICrawler> crawlers = new();

for (int i = 0; i < ExplorerCount; i++)
{
    var api = await Labyrinth.Crawl.ApiCrawler.CreateAsync(baseUrl, appKey!, null);
    crawlers.Add(api);
    remoteCrawlersToDispose.Add(api);
}

Console.WriteLine($"Starting exploration with {ExplorerCount} explorers...");

using var cts = new CancellationTokenSource();
var token = cts.Token;

bool exitFound = false;
int exitOwnerId = 0;

map.ExitFound += (s, e) =>
{
    exitFound = true;
    exitOwnerId = e.OwnerId;
    cts.Cancel();
};

var stopwatch = Stopwatch.StartNew();

for (int i = 0; i < ExplorerCount; i++)
{
    var ownerId = i + 1;
    var crawler = crawlers[i];
    var explorer = new BfsExplorer(crawler, map, ownerId);
    explorers.Add(explorer);

    explorerPositions[ownerId] = (crawler.X, crawler.Y);

    explorer.PositionChanged += (s, e) =>
    {
        explorerPositions[ownerId] = (e.X, e.Y);
    };

    tasks.Add(Task.Run(async () => await explorer.RunAsync(token)));
}

int timeoutMs = 300000;
var all = Task.WhenAll(tasks);
await Task.WhenAny(all, Task.Delay(timeoutMs, token));

stopwatch.Stop();

Console.Clear();
Console.WriteLine("═══════════════════════════════════════════");
Console.WriteLine("           EXPLORATION RESULTS             ");
Console.WriteLine("═══════════════════════════════════════════");
Console.WriteLine();

Console.WriteLine(RenderMap(map, explorerPositions));

Console.WriteLine("═══════════════════════════════════════════");

if (exitFound)
{
    Console.WriteLine($"  ✓ EXIT FOUND by explorer {exitOwnerId}!");
}
else
{
    Console.WriteLine("  ✗ Exploration finished (no exit found)");
}

Console.WriteLine($"  ⏱ Time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
Console.WriteLine($"  👥 Explorers: {ExplorerCount}");

if (map.TryGet(out var finalSnapshot))
{
    var visited = finalSnapshot.Count(kvp => kvp.Value == CellType.Visited || kvp.Value == CellType.Start);
    var walls = finalSnapshot.Count(kvp => kvp.Value == CellType.Wall);
    var doors = finalSnapshot.Count(kvp => kvp.Value == CellType.Door);
    Console.WriteLine($"  📊 Cells visited: {visited} | Walls: {walls} | Doors remaining: {doors}");
}

Console.WriteLine("═══════════════════════════════════════════");

foreach (var rc in remoteCrawlersToDispose)
{
    try { await rc.DisposeAsync(); } catch { }
}

Console.WriteLine("\nPress any key to exit...");
try { if (!Console.IsInputRedirected) Console.ReadKey(); } catch { }