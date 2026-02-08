using Labyrinth;
using Labyrinth.Crawl;
using Moq;

namespace LabyrinthTest.Exploration;

public class ExplorerTest
{
    private class ExplorerEventsCatcher
    {
        public ExplorerEventsCatcher(BfsExplorer explorer)
        {
            explorer.PositionChanged  += (s, e) => CatchEvent(ref _positionChangedCount , e);
            explorer.DirectionChanged += (s, e) => CatchEvent(ref _directionChangedCount, e);
        }
        public int PositionChangedCount => _positionChangedCount;
        public int DirectionChangedCount => _directionChangedCount;

        public (int X, int Y, Direction Dir)? LastArgs { get; private set; } = null;

        private void CatchEvent(ref int counter, CrawlingEventArgs e)
        {
            counter++;
            LastArgs = (e.X, e.Y, e.Direction);
        }
        private int _directionChangedCount = 0, _positionChangedCount = 0;
    }

    [Test]
    public async Task SimpleExplorationRotatesOnWall()
    {
        var laby = new Labyrinth.Maze("""
            +-+
            |x|
            +-+
            """);
        var explorer = new BfsExplorer(laby.NewCrawler());
        var events = new ExplorerEventsCatcher(explorer);

        // Cr�er une t�che qui s'ex�cute pour un temps limit�
        var runTask = Task.Run(async () => await explorer.RunAsync());
        await Task.Delay(500); // Laisser l'explorateur tourner pendant 500ms

        // V�rifier que l'explorateur a tourn� (mais pas boug�)
        Assert.That(events.DirectionChangedCount, Is.GreaterThan(0));
        Assert.That(events.PositionChangedCount, Is.EqualTo(0));
    }

    [Test]
    public async Task SimpleExplorationMovesWhenPossible()
    {
        var laby = new Labyrinth.Maze("""
            +---+
            |x  |
            +---+
            """);
        var explorer = new BfsExplorer(laby.NewCrawler());
        var events = new ExplorerEventsCatcher(explorer);

        var runTask = Task.Run(async () => await explorer.RunAsync());
        await Task.Delay(500);

        // V�rifier que l'explorateur s'est d�plac�
        Assert.That(events.PositionChangedCount, Is.GreaterThan(0));
    }

    [Test]
    public async Task ExplorationEventsAreFired()
    {
        var laby = new Labyrinth.Maze("""
            +-----+
            |x    |
            +-----+
            """);
        var explorer = new BfsExplorer(laby.NewCrawler());
        var events = new ExplorerEventsCatcher(explorer);

        var runTask = Task.Run(async () => await explorer.RunAsync());
        await Task.Delay(1000);

        // V�rifier que les �v�nements sont lev�s
        Assert.That(events.PositionChangedCount + events.DirectionChangedCount, Is.GreaterThan(0));
    }
}
