using Labyrinth;
using Labyrinth.Crawl;
using Labyrinth.Sys;
using Moq;
using static Labyrinth.RandExplorer;

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

    private RandExplorer NewExplorerFor(
        string labyrinth, 
        out ExplorerEventsCatcher events,
        params Actions[] actions
    ) {
        var laby = new Labyrinth.Maze(labyrinth);
        var mockRnd = new Mock<IEnumRandomizer<Actions>>();

        mockRnd.Setup(r => r.Next()).Returns(
            new Queue<Actions>(actions).Dequeue
        );
        var explorer = new RandExplorer(
            laby.NewCrawler(),
            mockRnd.Object
        );
        events = new ExplorerEventsCatcher(explorer);
        return explorer;
    }
    
    [Test]
    public void GetOutNegativeThrowsException()
    {
        var test = NewExplorerFor("""
            + +
            |x|
            +-+
            """,
            out var events
        );
        Assert.That(
            async () => await test.GetOutAsync(-3), 
            Throws.TypeOf<ArgumentOutOfRangeException>()
        );
        Assert.That(events.DirectionChangedCount, Is.EqualTo(0));
        Assert.That(events.PositionChangedCount , Is.EqualTo(0));
    }

    [Test]
    public void GetOutZeroThrowsException()
    {
        var test = NewExplorerFor("""
            + +
            |x|
            +-+
            """,
            out var events
        );
        Assert.That(
            async () => await test.GetOutAsync(0), 
            Throws.TypeOf<ArgumentOutOfRangeException>()
        );
        Assert.That(events.DirectionChangedCount, Is.EqualTo(0));
        Assert.That(events.PositionChangedCount , Is.EqualTo(0));
    }

    [Test]
    public async Task GetOutInHole()
    {
        var test = NewExplorerFor("""
            +-+
            |x/
            +-+
            |k|
            +-+
            """,
            out var events
        );
        var left = await test.GetOutAsync(10);

        Assert.That(left, Is.EqualTo(0));
        Assert.That(events.DirectionChangedCount, Is.EqualTo(10));
        Assert.That(events.PositionChangedCount , Is.EqualTo(0));
    }

    [Test]
    public async Task GetOutFacingOutsideAtStart()
    {
        var test = NewExplorerFor("""
            | x |
            |   |
            +---+
            """,
            out var events
        );
        var left = await test.GetOutAsync(10);

        Assert.That(left, Is.EqualTo(10));
        Assert.That(events.DirectionChangedCount, Is.EqualTo(0));
        Assert.That(events.PositionChangedCount , Is.EqualTo(0));
    }

    [Test]
    public async Task GetOutRotatingOnce()
    {
        var test = NewExplorerFor("""
            --+
              |
            x |
            --+
            """, 
            out var events,
            Actions.TurnLeft
        );

        var left = await test.GetOutAsync(10);

        Assert.That(left, Is.EqualTo(9));
        Assert.That(events.DirectionChangedCount, Is.EqualTo(1));
        Assert.That(events.PositionChangedCount , Is.EqualTo(0));
        Assert.That(events.LastArgs, Is.EqualTo((0, 2, Direction.West)));
    }

    [Test]
    public async Task GetOutRotatingTwice()
    {
        var test = NewExplorerFor("""
            +---+
            |   |
            | x |
            """,
            out var events,
            Actions.TurnLeft,
            Actions.TurnLeft
        );

        var left = await test.GetOutAsync(10);

        Assert.That(left, Is.EqualTo(8));
        Assert.That(events.DirectionChangedCount, Is.EqualTo(2));
        Assert.That(events.LastArgs, Is.EqualTo((2, 2, Direction.South)));
    }

    [Test]
    public async Task GetOutWalkingOnce()
    {
        var test = NewExplorerFor("""
            --+
             x|
            --+
            """,
            out var events,
            // auto turn left
            Actions.Walk
        );

        var left = await test.GetOutAsync(10);

        Assert.That(left, Is.EqualTo(8));
        Assert.That(events.PositionChangedCount , Is.EqualTo(1));
        Assert.That(events.DirectionChangedCount, Is.EqualTo(1));
        Assert.That(events.LastArgs, Is.EqualTo((0, 1, Direction.West)));
    }

    [Test]
    public async Task GetOutWalkingExactMoves()
    {
        var test = NewExplorerFor("""
            ---+
              x|
            ---+
            """,
            out var events,
            // auto turn left
            Actions.Walk,
            Actions.Walk
        );

        var left = await test.GetOutAsync(3);

        Assert.That(left, Is.EqualTo(0));
        Assert.That(events.DirectionChangedCount, Is.EqualTo(1));
        Assert.That(events.PositionChangedCount , Is.EqualTo(2));
        Assert.That(events.LastArgs, Is.EqualTo((0, 1, Direction.West)));
    }

    [Test]
    public async Task GetOutWithMultipleMoves()
    {
        var test = NewExplorerFor("""
            +---+
               k|
            + -/+
            |  x|
            | --+
            """,
            out var events,
            // auto turn left
            Actions.Walk,
            Actions.Walk,
            // auto turn left
            Actions.TurnLeft,
            Actions.TurnLeft,
            Actions.Walk,
            Actions.Walk,
            // auto turn left
            Actions.Walk
        );

        var left = await test.GetOutAsync(15);

        Assert.That(left, Is.EqualTo(5));
        Assert.That(events.DirectionChangedCount, Is.EqualTo(5));
        Assert.That(events.PositionChangedCount , Is.EqualTo(5));
        Assert.That(events.LastArgs, Is.EqualTo((0, 1, Direction.West)));
    }

    [Test]
    public async Task GetOutPassingADoor()
    {
        var test = NewExplorerFor("""
            +-/-+
            | k |
            | x |
            +---+
            """,
            out var events,
            Actions.Walk,
            Actions.Walk
        );
        var left = await test.GetOutAsync(10);

        Assert.That(left, Is.EqualTo(8));
        Assert.That(events.DirectionChangedCount, Is.EqualTo(0));
        Assert.That(events.PositionChangedCount , Is.EqualTo(2));
        Assert.That(events.LastArgs, Is.EqualTo((2, 0, Direction.North)));
    }

    [Test]
    public async Task GetOutPassingTwoDoors()
    {
        var test = NewExplorerFor("""
            +--+
            |kx|
            +/-+
            | k/
            +--+
            """,
            out var events,
            // auto turn left
            Actions.Walk, // key
            // auto turn left
            Actions.Walk, // door
            Actions.Walk,
            // auto turn left
            Actions.Walk, // key
            Actions.Walk  // door
        );
        var left = await test.GetOutAsync(10);

        Assert.That(left, Is.EqualTo(2));
        Assert.That(events.DirectionChangedCount, Is.EqualTo(3));
        Assert.That(events.PositionChangedCount , Is.EqualTo(5));
        Assert.That(events.LastArgs, Is.EqualTo((3, 3, Direction.East)));
    }

    [Test]
    public async Task GetOutPassingTwoKeysBeforeDoors()
    {
        var test = NewExplorerFor("""
            +--+
            |kx/
            | k/
            +--+
            """,
            out var events,
            Actions.Walk,// key
            Actions.Walk,
            Actions.Walk,// swap keys
            Actions.Walk // door
        );
        var left = await test.GetOutAsync(10);

        Assert.That(left, Is.EqualTo(3));
        Assert.That(events.DirectionChangedCount, Is.EqualTo(3));
        Assert.That(events.PositionChangedCount , Is.EqualTo(4));
        Assert.That(events.LastArgs, Is.EqualTo((3, 2, Direction.East)));
    }

    [Test]
    public async Task GetOutPassingMultipleKeysBeforeDoors()
    {
        var test = NewExplorerFor("""
            +---+
            |kx /
            |k|/|
            |k/ |
            +---+
            """,
            out var events,
            Actions.Walk,// key
            // auto turn left
            Actions.Walk,// key 
            Actions.Walk,// key
            // auto turn left
            Actions.Walk,// door
            Actions.Walk,
            // auto turn left
            Actions.Walk,
            Actions.Walk,// door
            // auto turn left
            Actions.TurnLeft,
            Actions.TurnLeft,
            Actions.Walk // door
        );
        var left = await test.GetOutAsync(20);

        Assert.That(left, Is.EqualTo(5));
        Assert.That(events.DirectionChangedCount, Is.EqualTo(7));
        Assert.That(events.PositionChangedCount, Is.EqualTo(8));
        Assert.That(events.LastArgs, Is.EqualTo((4, 1, Direction.East)));
    }

}
