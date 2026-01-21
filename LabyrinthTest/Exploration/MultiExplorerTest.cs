using Labyrinth;
using Labyrinth.Exploration;
using NUnit.Framework;
using System.Threading.Tasks;

namespace LabyrinthTest.Exploration;

public class MultiExplorerTest
{
    [Test]
    public async Task MultiExplorer_StartsMultipleCrawlers_SharedMapUpdated()
    {
        var mapStr = """
+-----+
|     |
|  x  |
|     |
+-----+
""";
        var maze = new Labyrinth.Maze(mapStr);
        var multi = new MultiExplorer(maze);

        var ok = await multi.StartAsync(3, timeoutMs: 2000);

        Assert.That(ok, Is.True);

        var got = multi.Map.TryGet(out var snap);
        Assert.That(got, Is.True);

        // At least the start cell should be known (either Start or Visited)
        Assert.That(snap.ContainsValue(Labyrinth.Exploration.CellType.Start) || snap.ContainsValue(Labyrinth.Exploration.CellType.Visited));
    }
}
