using Labyrinth.Crawl;
using Labyrinth.Items;
using Labyrinth.Tiles;

namespace LabyrinthTest.Crawl;

[TestFixture(Description = "Integration test for the crawler implementation in the labyrinth")]
public class LabyrinthCrawlerTest
{
    private static ICrawler

        NewCrawlerFor(string ascii_map) =>
        new Labyrinth.Maze(ascii_map).NewCrawler();

    private static async Task AssertThat(ICrawler test, int x, int y, Direction dir, TileType facingTileType)
    {
        using var all = Assert.EnterMultipleScope();

        Assert.That(test.X, Is.EqualTo(x));
        Assert.That(test.Y, Is.EqualTo(y));
        Assert.That(test.Direction, Is.EqualTo(dir));
        Assert.That(await test.GetFacingTileTypeAsync(), Is.EqualTo(facingTileType));
    }

    #region Initialization
    [Test]
    public async Task InitWithCenteredX() =>
        await AssertThat(
            NewCrawlerFor("""
                +--+
                | x|
                +--+
                """
            ),
            x: 2, y: 1,
            Direction.North,
            TileType.Wall
        );

    [Test]
    public async Task InitWithMultipleXUsesLastOne() =>
        await AssertThat(
            NewCrawlerFor("""
                +--+
                | x|
                |x |
                +--+
                """
            ),
            x: 1, y: 2,
            Direction.North,
            TileType.Empty
        );

    [Test]
    public void InitWithNoXThrowsArgumentException() =>
        Assert.Throws<ArgumentException>(() =>
            new Labyrinth.Maze("""
                +--+
                |  |
                +--+
                """
            )
        );
    #endregion

    #region Labyrinth borders
    [Test]
    public async Task FacingNorthOnUpperTileReturnsOutside() =>
         await AssertThat(
            NewCrawlerFor("""
                +x+
                | |
                +-+
                """
            ),
            x: 1, y: 0,
            Direction.North,
            TileType.Outside
        );

    [Test]
    public async Task FacingWestOnFarLeftTileReturnsOutside()
    {
        var test = NewCrawlerFor("""
            +-+
            x |
            +-+
            """
        );
        test.Direction.TurnLeft();
        await AssertThat(test,
            x: 0, y: 1,
            Direction.West,
            TileType.Outside
        );
    }

    [Test]
    public async Task FacingEastOnFarRightTileReturnsOutside()
    {
        var test = NewCrawlerFor("""
            +-+
            | x
            +-+
            """
        );
        test.Direction.TurnRight();
        await AssertThat(test,
            x: 2, y: 1,
            Direction.East,
            TileType.Outside
        );
    }

    [Test]
    public async Task FacingSouthOnBottomTileReturnsOutside()
    {
        var test = NewCrawlerFor("""
            +-+
            | |
            +x+
            """
        );
        test.Direction.TurnLeft();
        test.Direction.TurnLeft();
        await AssertThat(test,
            x: 1, y: 2,
            Direction.South,
            TileType.Outside
        );
    }
    #endregion

    #region Moves
    [Test]
    public async Task TurnLeftFacesWestTile()
    {
        var test = NewCrawlerFor("""
            +---+
            |/xk|
            +---+
            """
        );
        test.Direction.TurnLeft();
        await AssertThat(test,
            x: 2, y: 1,
            Direction.West,
            TileType.Door
        );
    }
    [Test]
    public async Task WalkReturnsInventoryAndChangesPositionAndFacingTile()
    {
        var test = NewCrawlerFor("""
            +/-+
            |  |
            |xk|
            +--+
            """
        );
        var inventory = await test.TryWalkAsync(null);

        Assert.That(inventory, Is.Not.Null);
        Assert.That(inventory!.HasItems, Is.False);
        await AssertThat(test,
            x: 1, y: 1,
            Direction.North,
            TileType.Door
        );
    }

    [Test]
    public async Task TurnAndWalkReturnsInventoryChangesPositionAndFacingTile()
    {
        var test = NewCrawlerFor("""
            +--+
            |x |
            +--+
            """
        );
        test.Direction.TurnRight();

        var inventory = await test.TryWalkAsync(null);

        Assert.That(inventory, Is.Not.Null);
        Assert.That(inventory!.HasItems, Is.False);
        await AssertThat(test,
            x: 2, y: 1,
            Direction.East,
            TileType.Wall
        );
    }

    [Test]
    public async Task WalkOnNonTraversableTileReturnsNullAndDontMove()
    {
        var test = NewCrawlerFor("""
            +--+
            |/-+
            |xk|
            +--+
            """
        );
        var inventory = await test.TryWalkAsync(null);
        Assert.That(inventory, Is.Null);
        await AssertThat(test,
            x: 1, y: 2,
            Direction.North,
            TileType.Door
        );
    }

    [Test]
    public async Task WalkOutsideReturnsNullAndDontMove()
    {
        var test = NewCrawlerFor("""
            |x|
            | |
            +-+
            """
        );
        var inventory = await test.TryWalkAsync(null);
        Assert.That(inventory, Is.Null);
        await AssertThat(test,
            x: 1, y: 0,
            Direction.North,
            TileType.Outside
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
        var inventory = await test.TryWalkAsync(null);

        using var all = Assert.EnterMultipleScope();

        Assert.That(inventory, Is.Not.Null);
        Assert.That(inventory!.HasItems, Is.True);
        Assert.That((await inventory.ItemTypes()).First(), Is.EqualTo(typeof(Key)));
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
        var inventory = await test.TryWalkAsync(null);
        var facingTileType = await test.GetFacingTileTypeAsync();

        Assert.That(inventory, Is.Not.Null);
        Assert.That(facingTileType, Is.EqualTo(TileType.Door));
        
        // Try to walk through door with wrong key
        var walkResult = await test.TryWalkAsync(inventory);
        Assert.That(walkResult, Is.Null); // Should fail because wrong key
        
        Assert.That(inventory!.HasItems, Is.True);
    }

    [Test]
    public async Task WalkUseKeyToOpenADoorAndPass()
    {
        var laby = new Labyrinth.Maze("""
                +--+
                |xk|
                +-/|
                """);
        var test = laby.NewCrawler();

        test.Direction.TurnRight();

        var inventory = await test.TryWalkAsync(null);

        test.Direction.TurnRight();
        ((Door)test.FacingTile).Open(inventory);
        
        // Try to walk through door with key
        var walkResult = await test.TryWalkAsync(inventory);

        using var all = Assert.EnterMultipleScope();

        Assert.That(walkResult, Is.Not.Null);
        Assert.That(test.X, Is.EqualTo(2));
        Assert.That(test.Y, Is.EqualTo(2));
        Assert.That(test.Direction, Is.EqualTo(Direction.South));
        Assert.That(await test.GetFacingTileTypeAsync(), Is.EqualTo(TileType.Outside));
    }
    #endregion
}
