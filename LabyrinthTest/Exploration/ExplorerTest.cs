using Labyrinth;
using Labyrinth.Crawl;
using Moq;

namespace LabyrinthTest.Exploration;

public class ExplorerTest
{
    private class ExplorerEventsCatcher
    {
        public ExplorerEventsCatcher(RandExplorer explorer)
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
        var explorer = new RandExplorer(laby.NewCrawler());
        var events = new ExplorerEventsCatcher(explorer);

        // Créer une tâche qui s'exécute pour un temps limité
        var runTask = Task.Run(async () => await explorer.RunAsync());
        await Task.Delay(500); // Laisser l'explorateur tourner pendant 500ms

        // Vérifier que l'explorateur a tourné (mais pas bougé)
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
        var explorer = new RandExplorer(laby.NewCrawler());
        var events = new ExplorerEventsCatcher(explorer);

        var runTask = Task.Run(async () => await explorer.RunAsync());
        await Task.Delay(500);

        // Vérifier que l'explorateur s'est déplacé
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
        var explorer = new RandExplorer(laby.NewCrawler());
        var events = new ExplorerEventsCatcher(explorer);

        var runTask = Task.Run(async () => await explorer.RunAsync());
        await Task.Delay(1000);

        // Vérifier que les événements sont levés
        Assert.That(events.PositionChangedCount + events.DirectionChangedCount, Is.GreaterThan(0));
    }
}
