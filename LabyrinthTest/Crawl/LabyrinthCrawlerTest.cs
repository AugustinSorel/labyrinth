using Labyrinth.Crawl;
using Labyrinth.Items;
using Labyrinth.Tiles;

namespace LabyrinthTest.Crawl;

[TestFixture(Description = "Integration test for the crawler implementation in the labyrinth")]
public class LabyrinthCrawlerTest
{
    private static ICrawler

        NewCrawlerFor(string ascii_map) =>
        new Labyrinth.Labyrinth(ascii_map).NewCrawler();

    private static void AssertThat(ICrawler test, int x, int y, Direction dir, Type facingTile)
    {
        using var all = Assert.EnterMultipleScope();

        Assert.That(test.X, Is.EqualTo(x));
        Assert.That(test.Y, Is.EqualTo(y));
        Assert.That(test.Direction, Is.EqualTo(dir));
        Assert.That(test.FacingTile, Is.TypeOf(facingTile));
    }

    #region Initialization
    [Test]
    public void InitWithCenteredX() =>
        AssertThat(
            NewCrawlerFor("""
                +--+
                | x|
                +--+
                """
            ),
            x: 2, y: 1,
            Direction.North,
            typeof(Wall)
        );

    [Test]
    public void InitWithMultipleXUsesLastOne() =>
        AssertThat(
            NewCrawlerFor("""
                +--+
                | x|
                |x |
                +--+
                """
            ),
            x: 1, y: 2,
            Direction.North,
            typeof(Room)
        );

    [Test]
    public void InitWithNoXThrowsArgumentException() =>
        Assert.Throws<ArgumentException>(() =>
            new Labyrinth.Labyrinth("""
                +--+
                |  |
                +--+
                """
            )
        );
    #endregion

    #region Labyrinth borders
    [Test]
    public void FacingNorthOnUpperTileReturnsOutside() =>
         AssertThat(
            NewCrawlerFor("""
                +x+
                | |
                +-+
                """
            ),
            x: 1, y: 0,
            Direction.North,
            typeof(Outside)
        );

    [Test]
    public void FacingWestOnFarLeftTileReturnsOutside()
    {
        var test = NewCrawlerFor("""
            +-+
            x |
            +-+
            """
        );
        test.Direction.TurnLeft();
        AssertThat(test,
            x: 0, y: 1,
            Direction.West,
            typeof(Outside)
        );
    }

    [Test]
    public void FacingEastOnFarRightTileReturnsOutside()
    {
        var test = NewCrawlerFor("""
            +-+
            | x
            +-+
            """
        );
        test.Direction.TurnRight();
        AssertThat(test,
            x: 2, y: 1,
            Direction.East,
            typeof(Outside)
        );
    }

    [Test]
    public void FacingSouthOnBottomTileReturnsOutside()
    {
        var test = NewCrawlerFor("""
            +-+
            | |
            +x+
            """
        );
        test.Direction.TurnLeft();
        test.Direction.TurnLeft();
        AssertThat(test,
            x: 1, y: 2,
            Direction.South,
            typeof(Outside)
        );
    }
    #endregion

    #region Moves
    [Test]
    public void TurnLeftFacesWestTile()
    {
        var test = NewCrawlerFor("""
            +---+
            |/xk|
            +---+
            """
        );
        test.Direction.TurnLeft();
        AssertThat(test,
            x: 2, y: 1,
            Direction.West,
            typeof(Door)
        );
    }
    [Test]
    public void WalkReturnsInventoryAndChangesPositionAndFacingTile()
    {
        var test = NewCrawlerFor("""
            +/-+
            |  |
            |xk|
            +--+
            """
        );
        var inventory = test.Walk();

        Assert.That(inventory.HasItems, Is.False);
        AssertThat(test,
            x: 1, y: 1,
            Direction.North,
            typeof(Door)
        );
    }

    [Test]
    public void TurnAndWalkReturnsInventoryChangesPositionAndFacingTile()
    {
        var test = NewCrawlerFor("""
            +--+
            |x |
            +--+
            """
        );
        test.Direction.TurnRight();

        var inventory = test.Walk();

        Assert.That(inventory.HasItems, Is.False);
        AssertThat(test,
            x: 2, y: 1,
            Direction.East,
            typeof(Wall)
        );
    }

    [Test]
    public void WalkOnNonTraversableTileThrowsInvalidOperationExceptionAndDontMove()
    {
        var test = NewCrawlerFor("""
            +--+
            |/-+
            |xk|
            +--+
            """
        );
        Assert.Throws<InvalidOperationException>(() => test.Walk());
        AssertThat(test,
            x: 1, y: 2,
            Direction.North,
            typeof(Door)
        );
    }

    [Test]
    public void WalkOutsideThrowsInvalidOperationExceptionAndDontMove()
    {
        var test = NewCrawlerFor("""
            |x|
            | |
            +-+
            """
        );
        Assert.Throws<InvalidOperationException>(() => test.Walk());
        AssertThat(test,
            x: 1, y: 0,
            Direction.North,
            typeof(Outside)
        );
    }
    #endregion

    #region Items and doors
    [Test]
    public async Task WalkInARoomWithAnItem()
    {
        var test = NewCrawlerFor("""
        +---+
        |  k|
        |/ x|
        +---+
        """
        );
        var inventory = test.Walk();

        using var all = Assert.EnterMultipleScope();

        var itemTypes = await inventory.GetItemTypesAsync();

        Assert.That(inventory.HasItems, Is.True);

        Assert.That(itemTypes.First(), Is.EqualTo(typeof(Key)));
    }

    [Test]
    public async Task WalkUseAWrongKeyToOpenADoor()
    {
        var test = NewCrawlerFor("""
            +---+
            |/ k|
            |k  |
            |x /|
            +---+
            """);
        var inventory = test.Walk();
        var door = (Door)test.FacingTile;

        Assert.That(await door.OpenAsync(inventory), Is.False);
        Assert.That(door.IsLocked, Is.True);
        Assert.That(door.IsTraversable, Is.False);
        Assert.That(inventory.HasItems, Is.True);
    }

    [Test]
    public async Task WalkUseKeyToOpenADoorAndPass()
    {
        var laby = new Labyrinth.Labyrinth("""
                +--+
                |xk|
                +-/|
                """);
        var test = laby.NewCrawler();

        test.Direction.TurnRight();

        var inventory = test.Walk();

        test.Direction.TurnRight();
        await ((Door)test.FacingTile).OpenAsync(inventory);

        test.Walk();

        using var all = Assert.EnterMultipleScope();

        Assert.That(test.X, Is.EqualTo(2));
        Assert.That(test.Y, Is.EqualTo(2));
        Assert.That(test.Direction, Is.EqualTo(Direction.South));
        Assert.That(test.FacingTile, Is.TypeOf<Outside>());
    }
    #endregion
}
