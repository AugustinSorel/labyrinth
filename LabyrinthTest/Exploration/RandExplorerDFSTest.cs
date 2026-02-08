using Labyrinth;
using Labyrinth.Crawl;
using Labyrinth.Exploration;
using NUnit.Framework;
using System.Linq;
using System.Threading.Tasks;

namespace LabyrinthTest.Exploration;

public class RandExplorerDFSTest
{
    private int CountTraversableChars(string ascii)
    {
        var s = ascii.Replace("\r", "");
        int count = 0;
        foreach (var ch in s)
        {
            if (ch == ' ' || ch == 'x' || ch == 'k' || ch == '/') count++;
        }
        return count;
    }

    [Test]
    public async Task DFS_VisitsAllReachableCells_OnSmallRoom()
    {
        var map = """
+-----+
|     |
|  x  |
|     |
+-----+
""";
        var laby = new Labyrinth.Maze(map);
        var crawler = laby.NewCrawler();
        var explorer = new BfsExplorer(crawler);

        // mark start cell
        explorer.Map.Mark(crawler.X, crawler.Y, CellType.Start);

        int safety = 0;
        while (await explorer.ExploreStepAsync())
        {
            safety++;
            if (safety > 10000) Assert.Fail("Explorer exceeded safety iterations");
        }

        var expected = CountTraversableChars(map);
        Assert.That(expected, Is.GreaterThan(0));

        var ok = explorer.Map.TryGet(out var snapshot);
        Assert.That(ok, Is.True);

        var visited = snapshot.Values.Count(v => v == CellType.Visited || v == CellType.Start);

        Assert.That(visited, Is.EqualTo(expected), "DFS should visit all reachable traversable cells");
    }

    [Test]
    public async Task DFS_PerformsBacktracking_MoreMovesThanUniqueCells()
    {
        var map = """
+-----+
|     |
|  x  |
|     |
+-----+
""";
        var laby = new Labyrinth.Maze(map);
        var crawler = laby.NewCrawler();
        var explorer = new BfsExplorer(crawler);

        explorer.Map.Mark(crawler.X, crawler.Y, CellType.Start);

        int moveEvents = 0;
        explorer.PositionChanged += (s, e) => moveEvents++;

        int safety = 0;
        while (await explorer.ExploreStepAsync())
        {
            safety++;
            if (safety > 10000) Assert.Fail("Explorer exceeded safety iterations");
        }

        var expected = CountTraversableChars(map);
        var ok = explorer.Map.TryGet(out var snapshot);
        Assert.That(ok, Is.True);

        var visited = snapshot.Values.Count(v => v == CellType.Visited || v == CellType.Start);

        // Ensure we've visited all cells
        Assert.That(visited, Is.EqualTo(expected));

        // And that we performed more move events than unique visited cells (because of backtracking)
        Assert.That(moveEvents, Is.GreaterThan(visited));
    }
}
