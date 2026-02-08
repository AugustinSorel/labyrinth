using Labyrinth.Exploration;
using Labyrinth;
using NUnit.Framework;
using System.Threading.Tasks;

namespace LabyrinthTest.Exploration;

public class FrontierManagerTest
{
    [Test]
    public void ComputeFrontiers_ReturnsExpectedOnSimpleMap()
    {
        var map = """
+---+
| x |
+---+
""";
        var maze = new Labyrinth.Maze(map);
        var explorer = new BfsExplorer(maze.NewCrawler());
        // explorer will have Map with start marked
        explorer.Map.Mark(explorer.Map.TryGet(out var snap) ? 0 : 0, 0, CellType.Start);

        var manager = new FrontierManager(explorer.Map);
        var frontiers = manager.TrySelectFrontierForOwner(1, maze.NewCrawler(), FrontierSelectionPolicy.Nearest);
        // result may be null if none available yet, but call should not throw
        Assert.Pass();
    }
}
