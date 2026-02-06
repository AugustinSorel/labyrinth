using Labyrinth.Items;
using Labyrinth.Tiles;
using NUnit.Framework;

namespace LabyrinthTest.Tiles;

[TestFixture(Description = "Unit tests for Door lock/unlock mechanics")]
public class DoorTest
{
    #region Initial State
    [Test]
    public void NewDoor_IsUnlocked_AndTraversable()
    {
        var door = new Door();
        
        Assert.That(door.IsLocked, Is.False);
        Assert.That(door.IsOpened, Is.True);
        Assert.That(door.IsTraversable, Is.True);
    }
    
    [Test]
    public void NewDoor_ContainsKey()
    {
        var door = new Door();
        var inventory = door.Pass();
        
        Assert.That(inventory.HasItems, Is.True);
        Assert.That(inventory.Count, Is.EqualTo(1));
    }
    #endregion

    #region LockAndTakeKey
    [Test]
    public void LockAndTakeKey_LocksDoor_AndMovesKeyToTargetInventory()
    {
        var door = new Door();
        var targetInventory = new MyInventory();
        
        door.LockAndTakeKey(targetInventory);
        
        Assert.That(door.IsLocked, Is.True);
        Assert.That(door.IsOpened, Is.False);
        Assert.That(door.IsTraversable, Is.False);
        Assert.That(targetInventory.HasItems, Is.True);
        Assert.That(targetInventory.Items.First(), Is.TypeOf<Key>());
    }
    
    [Test]
    public void LockAndTakeKey_OnAlreadyLockedDoor_ThrowsInvalidOperationException()
    {
        var door = new Door();
        var inventory1 = new MyInventory();
        var inventory2 = new MyInventory();
        
        door.LockAndTakeKey(inventory1);
        
        Assert.Throws<InvalidOperationException>(() => door.LockAndTakeKey(inventory2));
    }
    #endregion

    #region Open (synchronous)
    [Test]
    public void Open_WithKeyInInventory_UnlocksDoor()
    {
        var door = new Door();
        var keyInventory = new MyInventory();
        door.LockAndTakeKey(keyInventory);
        
        var result = door.Open(keyInventory);
        
        Assert.That(result, Is.True);
        Assert.That(door.IsOpened, Is.True);
        Assert.That(door.IsLocked, Is.False);
        Assert.That(door.IsTraversable, Is.True);
    }
    
    [Test]
    public void Open_WithEmptyInventory_FailsAndDoorStaysLocked()
    {
        var door = new Door();
        var keyInventory = new MyInventory();
        var emptyInventory = new MyInventory();
        door.LockAndTakeKey(keyInventory);
        
        var result = door.Open(emptyInventory);
        
        Assert.That(result, Is.False);
        Assert.That(door.IsLocked, Is.True);
        Assert.That(door.IsTraversable, Is.False);
    }
    
    [Test]
    public void Open_OnAlreadyOpenedDoor_ReturnsTrue()
    {
        var door = new Door();
        var emptyInventory = new MyInventory();
        
        // Door is already open by default
        var result = door.Open(emptyInventory);
        
        Assert.That(result, Is.True);
        Assert.That(door.IsOpened, Is.True);
    }
    
    [Test]
    public void Open_ConsumesKeyFromSourceInventory()
    {
        var door = new Door();
        var keyInventory = new MyInventory();
        door.LockAndTakeKey(keyInventory);
        
        Assert.That(keyInventory.HasItems, Is.True);
        
        door.Open(keyInventory);
        
        Assert.That(keyInventory.HasItems, Is.False);
    }
    #endregion

    #region OpenAsync
    [Test]
    public async Task OpenAsync_WithKeyInInventory_UnlocksDoor()
    {
        var door = new Door();
        var keyInventory = new MyInventory();
        door.LockAndTakeKey(keyInventory);
        
        var result = await door.OpenAsync(keyInventory);
        
        Assert.That(result, Is.True);
        Assert.That(door.IsOpened, Is.True);
        Assert.That(door.IsLocked, Is.False);
    }
    
    [Test]
    public async Task OpenAsync_WithEmptyInventory_FailsAndDoorStaysLocked()
    {
        var door = new Door();
        var keyInventory = new MyInventory();
        var emptyInventory = new MyInventory();
        door.LockAndTakeKey(keyInventory);
        
        var result = await door.OpenAsync(emptyInventory);
        
        Assert.That(result, Is.False);
        Assert.That(door.IsLocked, Is.True);
    }
    
    [Test]
    public async Task OpenAsync_OnAlreadyOpenedDoor_ReturnsTrue()
    {
        var door = new Door();
        var emptyInventory = new MyInventory();
        
        var result = await door.OpenAsync(emptyInventory);
        
        Assert.That(result, Is.True);
        Assert.That(door.IsOpened, Is.True);
    }
    
    [Test]
    public async Task OpenAsync_ConsumesKeyFromSourceInventory()
    {
        var door = new Door();
        var keyInventory = new MyInventory();
        door.LockAndTakeKey(keyInventory);
        
        await door.OpenAsync(keyInventory);
        
        Assert.That(keyInventory.HasItems, Is.False);
    }
    #endregion

    #region Pass (traversal)
    [Test]
    public void Pass_OnUnlockedDoor_ReturnsInventory()
    {
        var door = new Door();
        
        var inventory = door.Pass();
        
        Assert.That(inventory, Is.Not.Null);
    }
    
    [Test]
    public void Pass_OnLockedDoor_ThrowsInvalidOperationException()
    {
        var door = new Door();
        var keyInventory = new MyInventory();
        door.LockAndTakeKey(keyInventory);
        
        Assert.Throws<InvalidOperationException>(() => door.Pass());
    }
    #endregion

    #region Multiple operations
    [Test]
    public void Door_CanBeLocked_ThenUnlocked_ThenPassedThrough()
    {
        var door = new Door();
        var keyInventory = new MyInventory();
        
        // Lock the door
        door.LockAndTakeKey(keyInventory);
        Assert.That(door.IsLocked, Is.True);
        
        // Unlock the door
        var unlocked = door.Open(keyInventory);
        Assert.That(unlocked, Is.True);
        Assert.That(door.IsOpened, Is.True);
        
        // Pass through the door
        var passInventory = door.Pass();
        Assert.That(passInventory, Is.Not.Null);
    }
    #endregion
}
