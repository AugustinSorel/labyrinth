using Labyrinth;
using Labyrinth.Crawl;
using Labyrinth.Exploration;
using Labyrinth.Sys;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

object consoleLock = new();

char OwnerChar(int ownerId) => (ownerId <= 9 && ownerId >= 1) ? (char)('0' + ownerId) : 'X';

void SafeSetCursor(int left, int top)
{
    if (left < 0 || top < 0) return;
    try
    {
        if (left < Console.BufferWidth && top < Console.BufferHeight)
            Console.SetCursorPosition(left, top);
    }
    catch
    {
        // ignore failures when console not available or too small
    }
}

void DrawAt(int x, int y, char c)
{
    lock (consoleLock)
    {
        try
        {
            SafeSetCursor(x, y);
            Console.Write(c);
            SafeSetCursor(0, 0);
        }
        catch
        {
            // ignore out of range cursor positions
        }
    }
}

string GenerateSimpleMaze()
{
    // Simple concentric corridors maze with two doors and two key rooms
    return
    @"  +--+--------+
    |  /        |
    |  +--+--+  |
    |     |k    |
    +--+  |  +--+
       |k  x    |
    +  +-------/|
    |           |
    +-----------+";
}

// Support passing app key as command line argument for parallel runs
var appKeyFromArgs = args.Length > 0 ? args[0] : null;

// Ask the user whether to generate a maze or use the built-in one
Console.Write("Request server to generate a new labyrinth? (y = request server, enter = send provided map to server): ");
var answer = Console.ReadLine();
var ascii = (answer?.Trim().ToLowerInvariant() == "y") ? GenerateSimpleMaze() : @"+--+--------+
|  /        |
|  +--+--+  |
|     |k    |
+--+  |  +--+
   |k  x    |
+  +-------/|
|           |
+-----------+";

// Build a local labyrinth preview (for display only)
var labyrinth = new Labyrinth.Maze(ascii);

var map = new ExplorationMap();

const int ExplorerCount = 3;
var explorers = new List<RandExplorer>();
var tasks = new List<Task>();

// Remote only: decide remote parameters using environment variables or command line
var useRemote = (Environment.GetEnvironmentVariable("LAB_USE_REMOTE") ?? "false").ToLowerInvariant() == "true";
var appKey = appKeyFromArgs ?? Environment.GetEnvironmentVariable("LAB_APP_KEY") ?? "D98E5988-58E3-4BCE-B050-46E1903E6777";
var baseUrl = Environment.GetEnvironmentVariable("LAB_BASE_URL") ?? "https://labyrinth.syllab.com";

if (!useRemote)
{
    Console.WriteLine("This program requires remote crawlers. Set environment variable LAB_USE_REMOTE=true and provide LAB_APP_KEY.");
    return;
}

if (string.IsNullOrWhiteSpace(appKey))
{
    Console.WriteLine("LAB_USE_REMOTE=true but LAB_APP_KEY is not set. Please set LAB_APP_KEY and retry.");
    return;
}

Console.WriteLine($"Using API Key: {appKey[..8]}...");

List<ApiCrawler> remoteCrawlersToDispose = new();
List<ICrawler> crawlers = new();

// create remote crawlers
for (int i = 0; i < ExplorerCount; i++)
{
    var api = await Labyrinth.Crawl.ApiCrawler.CreateAsync(baseUrl, appKey!, null);
    crawlers.Add(api);
    remoteCrawlersToDispose.Add(api);
}

// Prepare console
try
{
    Console.Clear();
}
catch
{
    // Ignore Console.Clear() failures in non-interactive mode
}
Console.WriteLine(labyrinth);

using var cts = new CancellationTokenSource();
var token = cts.Token;

// Track if exit was found
bool exitFound = false;
int exitExplorerOwner = 0;
int exitX = 0, exitY = 0;

// Subscribe to exit found on the shared map
map.ExitFound += (s, e) =>
{
    exitFound = true;
    exitExplorerOwner = e.OwnerId;
    exitX = e.ExitX;
    exitY = e.ExitY;
    lock (consoleLock)
    {
        DrawAt(e.ExitX, e.ExitY, 'E');
        SafeSetCursor(0, labyrinth.ToString().Split('\n').Length + 2);
        Console.WriteLine($"Exit found by explorer {e.OwnerId} at ({e.ExitX},{e.ExitY})");
    }
    cts.Cancel();
};

// Keep track of previous positions per owner to erase
var prevPos = new Dictionary<int, (int X, int Y)>();

for (int i = 0; i < ExplorerCount; i++)
{
    var ownerId = i + 1;
    var crawler = crawlers[i];
    var explorer = new RandExplorer(crawler, map, ownerId);
    explorers.Add(explorer);

    // initial draw
    prevPos[ownerId] = (crawler.X, crawler.Y);
    DrawAt(crawler.X, crawler.Y, OwnerChar(ownerId));

    explorer.PositionChanged += (s, e) =>
    {
        // erase previous
        var prev = prevPos[ownerId];
        DrawAt(prev.X, prev.Y, ' ');

        // draw new
        DrawAt(e.X, e.Y, OwnerChar(ownerId));
        prevPos[ownerId] = (e.X, e.Y);

        // tiny delay for visibility
        Thread.Sleep(30);
    };

    explorer.DirectionChanged += (s, e) =>
    {
        // optional: update orientation marker; keep as owner char for clarity
    };

    // Start explorer run in background, pass cancellation token
    tasks.Add(Task.Run(async () => await explorer.RunAsync(token)));
}

// Wait for all explorers to complete or timeout
int timeoutMs = 300000; // 300s (5 minutes)
var all = Task.WhenAll(tasks);
var completed = await Task.WhenAny(all, Task.Delay(timeoutMs, token));

if (completed == all && !token.IsCancellationRequested)
{
    SafeSetCursor(0, labyrinth.ToString().Split('\n').Length + 1);
    Console.WriteLine("Exploration complete.");
}
else if (token.IsCancellationRequested)
{
    SafeSetCursor(0, labyrinth.ToString().Split('\n').Length + 1);
    Console.WriteLine("Exploration stopped: exit found.");
}
else
{
    SafeSetCursor(0, labyrinth.ToString().Split('\n').Length + 1);
    Console.WriteLine("Timeout reached.");
}

// Print final map snapshot
if (map.TryGet(out var snapshot))
{
    Console.WriteLine("\nDiscovered map snapshot:\n");
    Console.WriteLine(map.ToString());
}

// Always cleanup remote crawlers at the end
Console.WriteLine("\nSuppression des crawlers...");
foreach (var rc in remoteCrawlersToDispose)
{
    try { await rc.DisposeAsync(); } catch { }
}
Console.WriteLine("Crawlers supprimés.");

Console.WriteLine("Press any key to exit...");
try
{
    if (!Console.IsInputRedirected)
    {
        Console.ReadKey();
    }
}
catch
{
    // ignore when console input not available
}