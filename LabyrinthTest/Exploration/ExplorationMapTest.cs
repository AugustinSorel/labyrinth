using Labyrinth.Exploration;
using NUnit.Framework;

namespace LabyrinthTest.Exploration;

[TestFixture(Description = "Unit tests for ExplorationMap merge logic and additional thread safety")]
public class ExplorationMapTest
{
    #region Basic Operations
    [Test]
    public void NewMap_ReturnsUnknownForUnmarkedCells()
    {
        var map = new ExplorationMap();
        
        var cellType = map.Get(5, 5);
        
        Assert.That(cellType, Is.EqualTo(CellType.Unknown));
    }
    
    [Test]
    public void Mark_SetsCorrectCellType()
    {
        var map = new ExplorationMap();
        
        map.Mark(0, 0, CellType.Wall);
        
        Assert.That(map.Get(0, 0), Is.EqualTo(CellType.Wall));
    }
    
    [Test]
    public void Constructor_WithStartPosition_MarksStartCell()
    {
        var map = new ExplorationMap(3, 4);
        
        Assert.That(map.Get(3, 4), Is.EqualTo(CellType.Start));
    }
    #endregion

    #region Merge Logic
    [Test]
    public void Merge_StartOverridesAnything()
    {
        var map = new ExplorationMap();
        
        map.Mark(0, 0, CellType.Wall);
        map.Mark(0, 0, CellType.Start);
        
        Assert.That(map.Get(0, 0), Is.EqualTo(CellType.Start));
    }
    
    [Test]
    public void Merge_VisitedOverridesAnything()
    {
        var map = new ExplorationMap();
        
        map.Mark(0, 0, CellType.Empty);
        map.Mark(0, 0, CellType.Visited);
        
        Assert.That(map.Get(0, 0), Is.EqualTo(CellType.Visited));
    }
    
    [Test]
    public void Merge_UnknownReplacedByAnyKnownType()
    {
        var map = new ExplorationMap();
        
        // Cell starts as Unknown
        Assert.That(map.Get(0, 0), Is.EqualTo(CellType.Unknown));
        
        // Mark it as Empty
        map.Mark(0, 0, CellType.Empty);
        
        Assert.That(map.Get(0, 0), Is.EqualTo(CellType.Empty));
    }
    
    [Test]
    public void Merge_WallIsNotOverwritten_ByEmpty()
    {
        var map = new ExplorationMap();
        
        map.Mark(0, 0, CellType.Wall);
        map.Mark(0, 0, CellType.Empty);
        
        Assert.That(map.Get(0, 0), Is.EqualTo(CellType.Wall));
    }
    
    [Test]
    public void Merge_OutsideIsNotOverwritten_ByEmpty()
    {
        var map = new ExplorationMap();
        
        map.Mark(0, 0, CellType.Outside);
        map.Mark(0, 0, CellType.Empty);
        
        Assert.That(map.Get(0, 0), Is.EqualTo(CellType.Outside));
    }
    
    [Test]
    public void Merge_DoorTakesPriority_OverEmpty()
    {
        var map = new ExplorationMap();
        
        map.Mark(0, 0, CellType.Empty);
        map.Mark(0, 0, CellType.Door);
        
        Assert.That(map.Get(0, 0), Is.EqualTo(CellType.Door));
    }
    
    [Test]
    public void Merge_EmptyDoesNotOverwrite_Door()
    {
        var map = new ExplorationMap();
        
        map.Mark(0, 0, CellType.Door);
        map.Mark(0, 0, CellType.Empty);
        
        Assert.That(map.Get(0, 0), Is.EqualTo(CellType.Door));
    }
    
    [Test]
    public void Merge_StartOverridesVisited()
    {
        var map = new ExplorationMap();
        
        map.Mark(0, 0, CellType.Visited);
        map.Mark(0, 0, CellType.Start);
        
        Assert.That(map.Get(0, 0), Is.EqualTo(CellType.Start));
    }
    
    [Test]
    public void Merge_VisitedOverridesStart()
    {
        var map = new ExplorationMap();
        
        map.Mark(0, 0, CellType.Start);
        map.Mark(0, 0, CellType.Visited);
        
        Assert.That(map.Get(0, 0), Is.EqualTo(CellType.Visited));
    }
    
    [Test]
    public void Merge_WallBecomesVisited()
    {
        var map = new ExplorationMap();
        
        map.Mark(0, 0, CellType.Wall);
        map.Mark(0, 0, CellType.Visited);
        
        Assert.That(map.Get(0, 0), Is.EqualTo(CellType.Visited));
    }
    #endregion

    #region Claims
    [Test]
    public void TryClaim_SucceedsForUnclaimedCell()
    {
        var map = new ExplorationMap();
        
        var result = map.TryClaim(0, 0, ownerId: 1);
        
        Assert.That(result, Is.True);
        Assert.That(map.IsClaimed(0, 0), Is.True);
        Assert.That(map.GetClaimOwner(0, 0), Is.EqualTo(1));
    }
    
    [Test]
    public void TryClaim_FailsForAlreadyClaimedCell()
    {
        var map = new ExplorationMap();
        
        map.TryClaim(0, 0, ownerId: 1);
        var result = map.TryClaim(0, 0, ownerId: 2);
        
        Assert.That(result, Is.False);
        Assert.That(map.GetClaimOwner(0, 0), Is.EqualTo(1));
    }
    
    [Test]
    public void TryClaim_ThrowsForInvalidOwnerId()
    {
        var map = new ExplorationMap();
        
        Assert.Throws<ArgumentOutOfRangeException>(() => map.TryClaim(0, 0, ownerId: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.TryClaim(0, 0, ownerId: -1));
    }
    
    [Test]
    public void TryRelease_SucceedsWithCorrectOwner()
    {
        var map = new ExplorationMap();
        
        map.TryClaim(0, 0, ownerId: 1);
        var result = map.TryRelease(0, 0, ownerId: 1);
        
        Assert.That(result, Is.True);
        Assert.That(map.IsClaimed(0, 0), Is.False);
    }
    
    [Test]
    public void TryRelease_FailsWithWrongOwner()
    {
        var map = new ExplorationMap();
        
        map.TryClaim(0, 0, ownerId: 1);
        var result = map.TryRelease(0, 0, ownerId: 2);
        
        Assert.That(result, Is.False);
        Assert.That(map.IsClaimed(0, 0), Is.True);
        Assert.That(map.GetClaimOwner(0, 0), Is.EqualTo(1));
    }
    
    [Test]
    public void TryRelease_FailsForUnclaimedCell()
    {
        var map = new ExplorationMap();
        
        var result = map.TryRelease(0, 0, ownerId: 1);
        
        Assert.That(result, Is.False);
    }
    
    [Test]
    public void GetClaimOwner_ReturnsZero_ForUnclaimedCell()
    {
        var map = new ExplorationMap();
        
        var owner = map.GetClaimOwner(5, 5);
        
        Assert.That(owner, Is.EqualTo(0));
    }
    
    [Test]
    public void IsClaimed_ReturnsFalse_ForUnclaimedCell()
    {
        var map = new ExplorationMap();
        
        Assert.That(map.IsClaimed(5, 5), Is.False);
    }
    
    [Test]
    public void Mark_WithVisited_ReleasesClaim()
    {
        var map = new ExplorationMap();
        
        map.TryClaim(0, 0, ownerId: 1);
        Assert.That(map.IsClaimed(0, 0), Is.True);
        
        map.Mark(0, 0, CellType.Visited);
        
        Assert.That(map.IsClaimed(0, 0), Is.False);
    }
    
    [Test]
    public void Mark_WithStart_ReleasesClaim()
    {
        var map = new ExplorationMap();
        
        map.TryClaim(0, 0, ownerId: 1);
        Assert.That(map.IsClaimed(0, 0), Is.True);
        
        map.Mark(0, 0, CellType.Start);
        
        Assert.That(map.IsClaimed(0, 0), Is.False);
    }
    #endregion

    #region TryGet Snapshot
    [Test]
    public void TryGet_ReturnsTrueWithCells_WhenMapHasCells()
    {
        var map = new ExplorationMap();
        map.Mark(0, 0, CellType.Wall);
        map.Mark(1, 1, CellType.Empty);
        
        var result = map.TryGet(out var snapshot);
        
        Assert.That(result, Is.True);
        Assert.That(snapshot.Count, Is.EqualTo(2));
        Assert.That(snapshot[(0, 0)], Is.EqualTo(CellType.Wall));
        Assert.That(snapshot[(1, 1)], Is.EqualTo(CellType.Empty));
    }
    
    [Test]
    public void TryGet_ReturnsFalse_WhenMapIsEmpty()
    {
        var map = new ExplorationMap();
        
        var result = map.TryGet(out var snapshot);
        
        Assert.That(result, Is.False);
        Assert.That(snapshot.Count, Is.EqualTo(0));
    }
    
    [Test]
    public void TryGet_ReturnsSnapshot_NotLiveReference()
    {
        var map = new ExplorationMap();
        map.Mark(0, 0, CellType.Wall);
        
        map.TryGet(out var snapshot);
        
        // Modify map after getting snapshot
        map.Mark(1, 1, CellType.Empty);
        
        // Snapshot should not include new cell
        Assert.That(snapshot.Count, Is.EqualTo(1));
        Assert.That(snapshot.ContainsKey((1, 1)), Is.False);
    }
    #endregion

    #region ExitFound Event
    [Test]
    public void NotifyExitFound_RaisesEvent()
    {
        var map = new ExplorationMap();
        ExitFoundEventArgs? receivedArgs = null;
        
        map.ExitFound += (s, e) => receivedArgs = e;
        
        map.NotifyExitFound(crawlerX: 1, crawlerY: 2, exitX: 3, exitY: 4, ownerId: 5);
        
        Assert.That(receivedArgs, Is.Not.Null);
        Assert.That(receivedArgs!.CrawlerX, Is.EqualTo(1));
        Assert.That(receivedArgs.CrawlerY, Is.EqualTo(2));
        Assert.That(receivedArgs.ExitX, Is.EqualTo(3));
        Assert.That(receivedArgs.ExitY, Is.EqualTo(4));
        Assert.That(receivedArgs.OwnerId, Is.EqualTo(5));
    }
    
    [Test]
    public void NotifyExitFound_DoesNotThrow_WhenNoSubscribers()
    {
        var map = new ExplorationMap();
        
        Assert.DoesNotThrow(() =>
            map.NotifyExitFound(0, 0, 1, 1, 1)
        );
    }
    #endregion

    #region ToString
    [Test]
    public void ToString_ReturnsEmptyString_ForEmptyMap()
    {
        var map = new ExplorationMap();
        
        var result = map.ToString();
        
        Assert.That(result, Is.EqualTo(string.Empty));
    }
    
    [Test]
    public void ToString_RendersCorrectCharacters()
    {
        var map = new ExplorationMap();
        map.Mark(0, 0, CellType.Wall);
        map.Mark(1, 0, CellType.Door);
        map.Mark(2, 0, CellType.Empty);
        map.Mark(3, 0, CellType.Start);
        map.Mark(4, 0, CellType.Visited);
        map.Mark(5, 0, CellType.Outside);
        
        var result = map.ToString();
        
        Assert.That(result, Does.Contain("#"));  // Wall
        Assert.That(result, Does.Contain("/"));  // Door
        Assert.That(result, Does.Contain(" "));  // Empty
        Assert.That(result, Does.Contain("S"));  // Start
        Assert.That(result, Does.Contain("v"));  // Visited
        Assert.That(result, Does.Contain("O"));  // Outside
    }
    #endregion

    #region Thread Safety - Claims
    [Test]
    public void ConcurrentTryClaim_OnlyOneSucceeds()
    {
        var map = new ExplorationMap();
        var successCount = 0;
        
        Parallel.For(1, 101, ownerId =>
        {
            if (map.TryClaim(0, 0, ownerId))
            {
                Interlocked.Increment(ref successCount);
            }
        });
        
        Assert.That(successCount, Is.EqualTo(1));
        Assert.That(map.IsClaimed(0, 0), Is.True);
    }
    
    [Test]
    public void ConcurrentTryRelease_OnlyOneSucceeds()
    {
        var map = new ExplorationMap();
        map.TryClaim(0, 0, ownerId: 50); // Use owner in the middle
        
        var releaseCount = 0;
        
        Parallel.For(1, 101, ownerId =>
        {
            if (map.TryRelease(0, 0, ownerId))
            {
                Interlocked.Increment(ref releaseCount);
            }
        });
        
        Assert.That(releaseCount, Is.EqualTo(1));
        Assert.That(map.IsClaimed(0, 0), Is.False);
    }
    
    [Test]
    public void ConcurrentClaimAndRelease_MaintainsConsistency()
    {
        var map = new ExplorationMap();
        var claimCount = 0;
        var releaseCount = 0;
        
        Parallel.For(0, 1000, i =>
        {
            var ownerId = (i % 10) + 1;
            
            if (i % 2 == 0)
            {
                if (map.TryClaim(0, 0, ownerId))
                {
                    Interlocked.Increment(ref claimCount);
                }
            }
            else
            {
                if (map.TryRelease(0, 0, ownerId))
                {
                    Interlocked.Increment(ref releaseCount);
                }
            }
        });
        
        // Release count should not exceed claim count
        Assert.That(releaseCount, Is.LessThanOrEqualTo(claimCount));
    }
    #endregion

    #region Thread Safety - Mark
    [Test]
    public void ConcurrentMarks_AllSucceed()
    {
        var map = new ExplorationMap();
        var cellCount = 100;
        
        Parallel.For(0, cellCount, i =>
        {
            map.Mark(i, 0, CellType.Visited);
        });
        
        map.TryGet(out var snapshot);
        
        Assert.That(snapshot.Count, Is.EqualTo(cellCount));
    }
    
    [Test]
    public void ConcurrentMarkAndGet_NoExceptions()
    {
        var map = new ExplorationMap();
        var exceptions = new List<Exception>();
        
        Parallel.For(0, 1000, i =>
        {
            try
            {
                if (i % 2 == 0)
                {
                    map.Mark(i % 10, i % 10, CellType.Visited);
                }
                else
                {
                    _ = map.Get(i % 10, i % 10);
                }
            }
            catch (Exception ex)
            {
                lock (exceptions)
                {
                    exceptions.Add(ex);
                }
            }
        });
        
        Assert.That(exceptions, Is.Empty);
    }
    
    [Test]
    public void ConcurrentTryGet_IsConsistent()
    {
        var map = new ExplorationMap();
        
        // Pre-populate some cells
        for (int i = 0; i < 50; i++)
        {
            map.Mark(i, 0, CellType.Wall);
        }
        
        var snapshots = new List<Dictionary<(int X, int Y), CellType>>();
        
        Parallel.For(0, 100, i =>
        {
            if (map.TryGet(out var snapshot))
            {
                lock (snapshots)
                {
                    snapshots.Add(snapshot);
                }
            }
        });
        
        // All snapshots should have at least the initial 50 cells
        Assert.That(snapshots.All(s => s.Count >= 50), Is.True);
    }
    #endregion

    #region Edge Cases
    [Test]
    public void Mark_NegativeCoordinates_Works()
    {
        var map = new ExplorationMap();
        
        map.Mark(-5, -10, CellType.Wall);
        
        Assert.That(map.Get(-5, -10), Is.EqualTo(CellType.Wall));
    }
    
    [Test]
    public void Claim_NegativeCoordinates_Works()
    {
        var map = new ExplorationMap();
        
        var result = map.TryClaim(-5, -10, ownerId: 1);
        
        Assert.That(result, Is.True);
        Assert.That(map.IsClaimed(-5, -10), Is.True);
    }
    
    [Test]
    public void Mark_LargeCoordinates_Works()
    {
        var map = new ExplorationMap();
        
        map.Mark(1000000, 1000000, CellType.Empty);
        
        Assert.That(map.Get(1000000, 1000000), Is.EqualTo(CellType.Empty));
    }
    #endregion
}
