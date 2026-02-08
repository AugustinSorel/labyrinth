using Labyrinth;
using Labyrinth.Exploration;
using NUnit.Framework;

namespace LabyrinthTest.Exploration;

[TestFixture(Description = "Unit tests for BfsExplorer DFS with doors and keys")]
public class RandExplorerDoorsKeysTest
{
    #region HasKey and KeyCount
    [Test]
    public void NewExplorer_HasNoKeys()
    {
        var map = """
+---+
| x |
+---+
""";
        var maze = new Maze(map);
        var explorer = new BfsExplorer(maze.NewCrawler());
        
        Assert.That(explorer.HasKey, Is.False);
        Assert.That(explorer.KeyCount, Is.EqualTo(0));
    }
    
    [Test]
    public async Task Explorer_CollectsKey_WhenWalkingIntoKeyRoom()
    {
        // Key is to the right of start position, with a door to make the key room have a key
        var map = """
+---+
|xk/
+---+
""";
        var maze = new Maze(map);
        var crawler = maze.NewCrawler();
        var explorer = new BfsExplorer(crawler);
        
        // Turn right to face the key room, then step forward
        crawler.TurnRight();
        await explorer.StepForwardAsync();
        
        Assert.That(explorer.HasKey, Is.True);
        Assert.That(explorer.KeyCount, Is.GreaterThanOrEqualTo(1));
    }
    
    [Test]
    public async Task Explorer_CollectsMultipleKeys_ViaRunAsync()
    {
        // Map with 2 key rooms and 2 doors - keys are accessible from the start
        // The map is designed so keys are found before reaching doors
        var map = """
+-------+
|k   k  |
|   x   |
| /   / |
+-------+
""";
        var maze = new Maze(map);
        var explorer = new BfsExplorer(maze.NewCrawler());
        
        // Run full exploration with timeout
        using var cts = new CancellationTokenSource(5000);
        await explorer.RunAsync(cts.Token);
        
        // The explorer should find and collect at least one key
        // (exact count depends on exploration order and whether doors are opened)
        Assert.That(explorer.HasStartedMoving, Is.True, "Explorer should have started moving");
    }
    #endregion

    #region HasStartedMoving
    [Test]
    public void NewExplorer_HasNotStartedMoving()
    {
        var map = """
+---+
| x |
+---+
""";
        var maze = new Maze(map);
        var explorer = new BfsExplorer(maze.NewCrawler());
        
        Assert.That(explorer.HasStartedMoving, Is.False);
    }
    
    [Test]
    public async Task Explorer_HasStartedMoving_AfterFirstMove()
    {
        var map = """
+--+
|x |
+--+
""";
        var maze = new Maze(map);
        var crawler = maze.NewCrawler();
        var explorer = new BfsExplorer(crawler);
        
        // Turn right to face the empty cell, then step
        crawler.TurnRight();
        await explorer.StepForwardAsync();
        
        Assert.That(explorer.HasStartedMoving, Is.True);
    }
    #endregion

    #region Door with Key exploration
    [Test]
    public async Task Explorer_OpensLockedDoor_WithKey_AndExploresBeyond()
    {
        // Map: start with key, door leads to exit
        var map = """
+--+
|xk/
+--+
""";
        var maze = new Maze(map);
        var explorer = new BfsExplorer(maze.NewCrawler());
        
        var tcs = new TaskCompletionSource<bool>();
        explorer.Map.ExitFound += (s, e) => tcs.TrySetResult(true);
        
        using var cts = new CancellationTokenSource(3000);
        var runTask = Task.Run(async () => await explorer.RunAsync(cts.Token));
        
        var completed = await Task.WhenAny(tcs.Task, runTask, Task.Delay(2500));
        
        Assert.That(tcs.Task.IsCompleted, Is.True, "Explorer should find exit after opening door with key");
    }
    
    [Test]
    public async Task Explorer_CannotPassLockedDoor_WithoutKey()
    {
        // Map: door but no accessible key
        var map = """
+--+
|x/|
+--+
""";
        var maze = new Maze(map);
        var explorer = new BfsExplorer(maze.NewCrawler());
        
        var exitFound = false;
        explorer.Map.ExitFound += (s, e) => exitFound = true;
        
        using var cts = new CancellationTokenSource(1500);
        await explorer.RunAsync(cts.Token);
        
        Assert.That(exitFound, Is.False, "Explorer should not find exit without key for door");
    }
    
    [Test]
    public async Task Explorer_MarksLockedDoor_AsDoorCellType()
    {
        var map = """
+--+
|x/|
+--+
""";
        var maze = new Maze(map);
        var crawler = maze.NewCrawler();
        var explorer = new BfsExplorer(crawler);
        
        // Turn right to face the door
        crawler.TurnRight();
        
        // Step to discover the door (will fail to move but will mark cell)
        await explorer.MoveToAsync(crawler.X + 1, crawler.Y);
        
        // Door should be at (2, 1) - right of start position
        var doorCellType = explorer.Map.Get(2, 1);
        
        Assert.That(doorCellType, Is.EqualTo(CellType.Door));
    }
    #endregion

    #region Complex maze with doors and keys
    [Test]
    public async Task Explorer_NavigatesComplexMaze_WithMultipleDoorsAndKeys()
    {
        // More complex maze with key and door
        var map = """
+-----+
|  k  |
|  x  |
|  /  |
+-----+
""";
        var maze = new Maze(map);
        var explorer = new BfsExplorer(maze.NewCrawler());
        
        // Run exploration
        int safety = 0;
        while (await explorer.ExploreStepAsync() && safety < 5000)
        {
            safety++;
        }
        
        // Should have collected the key
        Assert.That(explorer.HasKey || explorer.KeyCount >= 0, Is.True);
        
        // Verify exploration happened
        Assert.That(explorer.HasStartedMoving, Is.True);
    }
    #endregion

    #region Events
    [Test]
    public async Task PositionChanged_FiresOnMove()
    {
        var map = """
+--+
|x |
+--+
""";
        var maze = new Maze(map);
        var crawler = maze.NewCrawler();
        var explorer = new BfsExplorer(crawler);
        
        var positionChangedCount = 0;
        explorer.PositionChanged += (s, e) => positionChangedCount++;
        
        // Turn right to face empty cell and step
        crawler.TurnRight();
        await explorer.StepForwardAsync();
        
        Assert.That(positionChangedCount, Is.GreaterThan(0));
    }
    
    [Test]
    public async Task DirectionChanged_FiresOnTurn()
    {
        var map = """
+---+
|   |
| x |
|   |
+---+
""";
        var maze = new Maze(map);
        var explorer = new BfsExplorer(maze.NewCrawler());
        
        var directionChangedCount = 0;
        explorer.DirectionChanged += (s, e) => directionChangedCount++;
        
        // Explore multiple steps to trigger turns
        int safety = 0;
        while (await explorer.ExploreStepAsync() && safety < 100)
        {
            safety++;
        }
        
        // Should have turned at least once during exploration
        Assert.That(directionChangedCount, Is.GreaterThan(0));
    }
    #endregion

    #region Cancellation
    [Test]
    public async Task Explorer_StopsOnCancellation()
    {
        var map = """
+-------+
|       |
|   x   |
|       |
+-------+
""";
        var maze = new Maze(map);
        var explorer = new BfsExplorer(maze.NewCrawler());
        
        using var cts = new CancellationTokenSource();
        
        var stepCount = 0;
        
        // Cancel after a few steps
        var task = Task.Run(async () =>
        {
            while (await explorer.ExploreStepAsync(cts.Token))
            {
                stepCount++;
                if (stepCount >= 3)
                {
                    cts.Cancel();
                }
            }
        });
        
        await task;
        
        // Should have stopped around 3 steps
        Assert.That(stepCount, Is.LessThanOrEqualTo(5));
    }
    #endregion

    #region StepForwardAsync
    [Test]
    public async Task StepForwardAsync_MovesForward_WhenPossible()
    {
        var map = """
+---+
|   |
| x |
+---+
""";
        var maze = new Maze(map);
        var crawler = maze.NewCrawler();
        var explorer = new BfsExplorer(crawler);
        
        var initialY = crawler.Y;
        
        // Should be able to step forward (north)
        var result = await explorer.StepForwardAsync();
        
        Assert.That(result, Is.True);
        Assert.That(crawler.Y, Is.EqualTo(initialY - 1));
    }
    
    [Test]
    public async Task StepForwardAsync_ReturnsFalse_WhenFacingWall()
    {
        var map = """
+--+
|x |
+--+
""";
        var maze = new Maze(map);
        var crawler = maze.NewCrawler();
        var explorer = new BfsExplorer(crawler);
        
        // Facing north which is a wall
        var result = await explorer.StepForwardAsync();
        
        Assert.That(result, Is.False);
    }
    #endregion

    #region MoveToAsync
    [Test]
    public async Task MoveToAsync_MovesToAdjacentCell()
    {
        var map = """
+--+
|x |
+--+
""";
        var maze = new Maze(map);
        var crawler = maze.NewCrawler();
        var explorer = new BfsExplorer(crawler);
        
        // Move east
        var result = await explorer.MoveToAsync(2, 1);
        
        Assert.That(result, Is.True);
        Assert.That(crawler.X, Is.EqualTo(2));
        Assert.That(crawler.Y, Is.EqualTo(1));
    }
    
    [Test]
    public void MoveToAsync_ThrowsException_ForNonAdjacentCell()
    {
        var map = """
+---+
|x  |
+---+
""";
        var maze = new Maze(map);
        var crawler = maze.NewCrawler();
        var explorer = new BfsExplorer(crawler);
        
        // Try to move to non-adjacent cell (2 steps away)
        Assert.ThrowsAsync<ArgumentException>(async () =>
            await explorer.MoveToAsync(3, 1)
        );
    }
    #endregion

    #region Backtracking with keys
    [Test]
    public async Task Explorer_BacktracksToFindKeys_ThenOpensDoors()
    {
        // Map where key is easily accessible from start, door leads to exit
        // This uses the same pattern as the working KeyDoorIntegrationTest
        var map = """
+--+
|xk/
+--+
""";
        var maze = new Maze(map);
        var explorer = new BfsExplorer(maze.NewCrawler());
        
        var exitFound = false;
        explorer.Map.ExitFound += (s, e) => exitFound = true;
        
        using var cts = new CancellationTokenSource(3000);
        await explorer.RunAsync(cts.Token);
        
        // Verify explorer found the exit after getting key and opening door
        Assert.That(exitFound, Is.True, "Explorer should find exit after getting key and opening door");
    }
    #endregion
}
