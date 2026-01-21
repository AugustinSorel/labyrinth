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

void DrawAt(int x, int y, char c)
{
    lock (consoleLock)
    {
        try
        {
            Console.SetCursorPosition(x, y);
            Console.Write(c);
            Console.SetCursorPosition(0, 0);
        }
        catch
        {
            // ignore out of range cursor positions
        }
    }
}

// Build a labyrinth
var labyrinth = new Labyrinth.Maze("""
+--+--------+
|  /        |
|  +--+--+  |
|     |k    |
+--+  |  +--+
   |k  x    |
+  +-------/|
|           |
+-----------+
""");

var map = new ExplorationMap();

const int ExplorerCount = 3;
var crawlers = Enumerable.Range(0, ExplorerCount).Select(_ => labyrinth.NewCrawler()).ToList();
var explorers = new List<RandExplorer>();
var tasks = new List<Task>();

using var cts = new CancellationTokenSource();
var token = cts.Token;

// Subscribe to exit found on the shared map
map.ExitFound += (s, e) =>
{
    lock (consoleLock)
    {
        DrawAt(e.ExitX, e.ExitY, 'E');
        Console.SetCursorPosition(0, labyrinth.ToString().Split('\n').Length + 2);
        Console.WriteLine($"Exit found by explorer {e.OwnerId} at ({e.ExitX},{e.ExitY})");
    }
    cts.Cancel();
};

// Prepare console
Console.Clear();
Console.WriteLine(labyrinth);

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
int timeoutMs = 10000; // 10s
var all = Task.WhenAll(tasks);
var completed = await Task.WhenAny(all, Task.Delay(timeoutMs, token));

if (completed == all && !token.IsCancellationRequested)
{
    Console.SetCursorPosition(0, labyrinth.ToString().Split('\n').Length + 1);
    Console.WriteLine("Exploration complete.");
}
else if (token.IsCancellationRequested)
{
    Console.SetCursorPosition(0, labyrinth.ToString().Split('\n').Length + 1);
    Console.WriteLine("Exploration stopped: exit found.");
}
else
{
    Console.SetCursorPosition(0, labyrinth.ToString().Split('\n').Length + 1);
    Console.WriteLine("Timeout reached.");
}

// Print final map snapshot
if (map.TryGet(out var snapshot))
{
    Console.WriteLine("\nDiscovered map snapshot:\n");
    Console.WriteLine(map.ToString());
}

Console.WriteLine("Press any key to exit...");
Console.ReadKey();
