using Labyrinth.Crawl;
using Labyrinth.Items;
using Labyrinth.Tiles;

namespace LabyrinthTest.Crawl;

[TestFixture(Description = "Integration test for the crawler implementation in the labyrinth")]
public class LabyrinthCrawlerTest
{
    private static ICrawler NewCrawlerFor(string ascii_map) =>
            new Labyrinth.Labyrinth(ascii_map).NewCrawler();

    private static async Task AssertThatAsync(ICrawler test, int x, int y, Direction dir, Type facingTileType)
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
        await AssertThatAsync(
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
    public async Task InitWithMultipleXUsesLastOne() =>
        await AssertThatAsync(
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
    public async Task FacingNorthOnUpperTileReturnsOutside() =>
         await AssertThatAsync(
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
    public async Task FacingWestOnFarLeftTileReturnsOutside()
    {
        var test = NewCrawlerFor("""
            +-+
            x |
            +-+
            """
        );
        test.Direction.TurnLeft();
        await AssertThatAsync(test,
            x: 0, y: 1,
            Direction.West,
            typeof(Outside)
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
        await AssertThatAsync(test,
            x: 2, y: 1,
            Direction.East,
            typeof(Outside)
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
        await AssertThatAsync(test,
            x: 1, y: 2,
            Direction.South,
            typeof(Outside)
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
        await AssertThatAsync(test,
            x: 2, y: 1,
            Direction.West,
            typeof(Door)
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

        var inventory = await test.TryWalkAsync(new MyInventory());

        Assert.That(inventory, Is.Not.Null);
        Assert.That(inventory!.HasItems, Is.False);

        await AssertThatAsync(test,
            x: 1, y: 1,
            Direction.North,
            typeof(Door)
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

        var inventory = await test.TryWalkAsync(new MyInventory());

        Assert.That(inventory, Is.Not.Null);
        Assert.That(inventory!.HasItems, Is.False);

        Assert.That(inventory.HasItems, Is.False);
        await AssertThatAsync(test,
            x: 2, y: 1,
            Direction.East,
            typeof(Wall)
        );
    }

    [Test]
    public async Task WalkOnNonTraversableTileThrowsInvalidOperationExceptionAndDontMove()
    {
        var test = NewCrawlerFor("""
            +--+
            |/-+
            |xk|
            +--+
            """
        );
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await test.TryWalkAsync(new MyInventory());
        });

        await AssertThatAsync(test,
            x: 1, y: 2,
            Direction.North,
            typeof(Door)
        );
    }

    [Test]
    public async Task WalkOutsideThrowsInvalidOperationExceptionAndDontMove()
    {
        var test = NewCrawlerFor("""
            |x|
            | |
            +-+
            """
        );
        // Action: Attempt to walk. 
        // Logic change: Instead of throwing, it now returns null (failure).
        var result = await test.TryWalkAsync(new MyInventory());

        // Assert: Operation failed (null)
        Assert.That(result, Is.Null);

        // Assert: Position is unchanged
        await AssertThatAsync(test,
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
        var inventory = await test.TryWalkAsync(new MyInventory());

        Assert.That(inventory!.HasItems, Is.True);

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


        var bag = new MyInventory();

        var roomInventory = await test.TryWalkAsync(bag);

        Assert.That(roomInventory, Is.Not.Null, "Should successfully walk into the first room.");

        await bag.TryMoveItemFromAsync(roomInventory!);
        Assert.That(bag.HasItems, Is.True, "Client should now have the key.");

        var result = await test.TryWalkAsync(bag);

        Assert.That(result, Is.Null, "Should not be able to pass: The key was wrong.");
        Assert.That(bag.HasItems, Is.True);
        Assert.That(await test.GetFacingTileTypeAsync(), Is.EqualTo(typeof(Door)));

    }

    [Test]
    public async Task WalkUseKeyToOpenADoorAndPass()
    {
        // Arrange
        var laby = new Labyrinth.Labyrinth("""
                +--+
                |xk|
                +-/|
                """);
        var test = laby.NewCrawler();
        var bag = new MyInventory(); // The client needs a bag to hold the key

        test.Direction.TurnRight();

        var roomLoot = await test.TryWalkAsync(bag);

        await bag.TryMoveItemFromAsync(roomLoot!);

        test.Direction.TurnRight();

        var result = await test.TryWalkAsync(bag);

        using var all = Assert.EnterMultipleScope();

        Assert.That(result, Is.Not.Null, "The crawler should have passed through the door.");
        Assert.That(test.X, Is.EqualTo(2));
        Assert.That(test.Y, Is.EqualTo(2));
        Assert.That(test.Direction, Is.EqualTo(Direction.South));

        Assert.That(await test.GetFacingTileTypeAsync(), Is.EqualTo(typeof(Outside)));
    }
    #endregion
}
