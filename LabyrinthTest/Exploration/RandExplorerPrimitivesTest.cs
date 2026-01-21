using Labyrinth;
using Labyrinth.Crawl;
using Labyrinth.Sys;
using NUnit.Framework;

namespace LabyrinthTest.Exploration;

public class RandExplorerPrimitivesTest
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
    public async Task StepForward_WhenBlocked_ReturnsFalse_AndNoPositionEvent()
    {
        var laby = new Labyrinth.Maze("""
            +-+
            |x|
            +-+
            """);
        var crawler = laby.NewCrawler();
        var explorer = new RandExplorer(crawler);
        var events = new ExplorerEventsCatcher(explorer);

        var result = await explorer.StepForwardAsync();

        Assert.That(result, Is.False);
        Assert.That(events.PositionChangedCount, Is.EqualTo(0));
    }

    [Test]
    public async Task StepForward_WhenOpen_ReturnsTrue_AndPositionEvent()
    {
        var laby = new Labyrinth.Maze("""
            +---+
            |x  |
            +---+
            """);
        var crawler = laby.NewCrawler();
        var explorer = new RandExplorer(crawler);
        var events = new ExplorerEventsCatcher(explorer);

        // Turn the crawler to face the open corridor on the right
        crawler.TurnRight();

        var result = await explorer.StepForwardAsync();

        Assert.That(result, Is.True);
        Assert.That(events.PositionChangedCount, Is.GreaterThan(0));
    }

    [Test]
    public void MoveToAsync_NonAdjacent_ThrowsArgumentException()
    {
        var laby = new Labyrinth.Maze("""
            +-----+
            |x    |
            +-----+
            """);
        var crawler = laby.NewCrawler();
        var explorer = new RandExplorer(crawler);

        Assert.ThrowsAsync<ArgumentException>(async () => await explorer.MoveToAsync(crawler.X + 2, crawler.Y));
    }

    [Test]
    public async Task MoveToAsync_Adjacent_TurnsAndMoves_RaisesEvents()
    {
        var laby = new Labyrinth.Maze("""
            +---+
            | x |
            |   |
            +---+
            """);
        var crawler = laby.NewCrawler();
        var explorer = new RandExplorer(crawler);
        var events = new ExplorerEventsCatcher(explorer);

        var targetX = crawler.X - 1; // move left (west)
        var targetY = crawler.Y;

        var moved = await explorer.MoveToAsync(targetX, targetY);

        Assert.That(moved, Is.True);
        Assert.That(events.PositionChangedCount, Is.EqualTo(1));
        Assert.That(events.DirectionChangedCount, Is.GreaterThan(0));
        Assert.That(events.LastArgs?.X, Is.EqualTo(targetX));
        Assert.That(events.LastArgs?.Y, Is.EqualTo(targetY));
    }
}
