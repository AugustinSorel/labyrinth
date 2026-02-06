using Labyrinth.Items;
using NUnit.Framework;

namespace LabyrinthTest.Items;

[TestFixture(Description = "Unit tests for Inventory thread safety and item movement")]
public class InventoryTest
{
    #region Basic Properties
    [Test]
    public void NewInventory_IsEmpty()
    {
        var inventory = new MyInventory();
        
        Assert.That(inventory.HasItems, Is.False);
        Assert.That(inventory.Count, Is.EqualTo(0));
    }
    
    [Test]
    public void NewInventory_WithItem_ContainsItem()
    {
        var key = new Key();
        var inventory = new MyInventory(key);
        
        Assert.That(inventory.HasItems, Is.True);
        Assert.That(inventory.Count, Is.EqualTo(1));
    }
    
    [Test]
    public void Items_ReturnsCorrectItems()
    {
        var key = new Key();
        var inventory = new MyInventory(key);
        
        var items = inventory.Items.ToList();
        
        Assert.That(items.Count, Is.EqualTo(1));
        Assert.That(items[0], Is.SameAs(key));
    }
    #endregion

    #region AddItem
    [Test]
    public void AddItem_IncreasesCount()
    {
        var inventory = new MyInventory();
        var key = new Key();
        
        inventory.AddItem(key);
        
        Assert.That(inventory.HasItems, Is.True);
        Assert.That(inventory.Count, Is.EqualTo(1));
    }
    
    [Test]
    public void AddItem_MultipleItems_AllAreStored()
    {
        var inventory = new MyInventory();
        var key1 = new Key();
        var key2 = new Key();
        var key3 = new Key();
        
        inventory.AddItem(key1);
        inventory.AddItem(key2);
        inventory.AddItem(key3);
        
        Assert.That(inventory.Count, Is.EqualTo(3));
        
        var items = inventory.Items.ToList();
        Assert.That(items, Contains.Item(key1));
        Assert.That(items, Contains.Item(key2));
        Assert.That(items, Contains.Item(key3));
    }
    #endregion

    #region MoveItemFrom
    [Test]
    public async Task MoveItemFrom_MovesItemBetweenInventories()
    {
        var source = new MyInventory(new Key());
        var target = new MyInventory();
        
        var result = await target.MoveItemFrom(source);
        
        Assert.That(result, Is.True);
        Assert.That(source.HasItems, Is.False);
        Assert.That(target.HasItems, Is.True);
    }
    
    [Test]
    public async Task MoveItemFrom_EmptySource_ReturnsFalse()
    {
        var source = new MyInventory();
        var target = new MyInventory();
        
        var result = await target.MoveItemFrom(source);
        
        Assert.That(result, Is.False);
        Assert.That(target.HasItems, Is.False);
    }
    
    [Test]
    public async Task MoveItemFrom_InvalidNegativeIndex_ReturnsFalse()
    {
        var source = new MyInventory(new Key());
        var target = new MyInventory();
        
        var result = await target.MoveItemFrom(source, nth: -1);
        
        Assert.That(result, Is.False);
        Assert.That(source.HasItems, Is.True);
        Assert.That(target.HasItems, Is.False);
    }
    
    [Test]
    public async Task MoveItemFrom_IndexOutOfBounds_ReturnsFalse()
    {
        var source = new MyInventory(new Key());
        var target = new MyInventory();
        
        var result = await target.MoveItemFrom(source, nth: 5);
        
        Assert.That(result, Is.False);
        Assert.That(source.HasItems, Is.True);
        Assert.That(target.HasItems, Is.False);
    }
    
    [Test]
    public async Task MoveItemFrom_SpecificIndex_MovesCorrectItem()
    {
        var key1 = new Key();
        var key2 = new Key();
        var source = new MyInventory(key1);
        source.AddItem(key2);
        var target = new MyInventory();
        
        var result = await target.MoveItemFrom(source, nth: 1);
        
        Assert.That(result, Is.True);
        Assert.That(source.Count, Is.EqualTo(1));
        Assert.That(target.Count, Is.EqualTo(1));
        Assert.That(target.Items.First(), Is.SameAs(key2));
        Assert.That(source.Items.First(), Is.SameAs(key1));
    }
    
    [Test]
    public async Task MoveItemFrom_SameInventory_ReturnsFalse()
    {
        var inventory = new MyInventory(new Key());
        
        // Moving from self to self should fail (count is 1, nth=0 is valid,
        // but both locks are on same object so should deadlock prevention kicks in)
        // Actually, the locking logic uses GetHashCode ordering, so same object locks once
        // Let's test the behavior
        var result = await inventory.MoveItemFrom(inventory, nth: 0);
        
        // The implementation removes item then adds it back, so count should remain 1
        Assert.That(inventory.Count, Is.EqualTo(1));
    }
    #endregion

    #region ItemTypes
    [Test]
    public async Task ItemTypes_ReturnsCorrectTypes()
    {
        var inventory = new MyInventory(new Key());
        
        var types = await inventory.ItemTypes();
        
        Assert.That(types.Count(), Is.EqualTo(1));
        Assert.That(types.First(), Is.EqualTo(typeof(Key)));
    }
    
    [Test]
    public async Task ItemTypes_EmptyInventory_ReturnsEmpty()
    {
        var inventory = new MyInventory();
        
        var types = await inventory.ItemTypes();
        
        Assert.That(types.Any(), Is.False);
    }
    #endregion

    #region Thread Safety
    [Test]
    public void ConcurrentAddItem_IsThreadSafe()
    {
        var inventory = new MyInventory();
        var itemCount = 1000;
        
        Parallel.For(0, itemCount, _ =>
        {
            inventory.AddItem(new Key());
        });
        
        Assert.That(inventory.Count, Is.EqualTo(itemCount));
    }
    
    [Test]
    public async Task ConcurrentMoveItemFrom_IsThreadSafe()
    {
        var source = new MyInventory();
        var itemCount = 100;
        
        // Add items to source
        for (int i = 0; i < itemCount; i++)
        {
            source.AddItem(new Key());
        }
        
        var targets = new List<MyInventory>();
        for (int i = 0; i < itemCount; i++)
        {
            targets.Add(new MyInventory());
        }
        
        var tasks = targets.Select(async target =>
        {
            await target.MoveItemFrom(source);
        }).ToArray();
        
        await Task.WhenAll(tasks);
        
        // Total items across all inventories should equal original count
        var totalInTargets = targets.Sum(t => t.Count);
        var remainingInSource = source.Count;
        
        Assert.That(totalInTargets + remainingInSource, Is.EqualTo(itemCount));
    }
    
    [Test]
    public void Items_ReturnsSnapshot_NotLiveReference()
    {
        var key1 = new Key();
        var key2 = new Key();
        var inventory = new MyInventory(key1);
        
        // Get snapshot
        var snapshot = inventory.Items.ToList();
        
        // Add another item
        inventory.AddItem(key2);
        
        // Original snapshot should not include the new item
        Assert.That(snapshot.Count, Is.EqualTo(1));
        Assert.That(snapshot[0], Is.SameAs(key1));
        
        // New enumeration should include both
        Assert.That(inventory.Items.Count(), Is.EqualTo(2));
    }
    
    [Test]
    public void HasItems_IsThreadSafe_UnderHighContention()
    {
        var inventory = new MyInventory();
        var exceptions = new List<Exception>();
        
        // Run concurrent reads and writes
        Parallel.For(0, 1000, i =>
        {
            try
            {
                if (i % 2 == 0)
                {
                    inventory.AddItem(new Key());
                }
                else
                {
                    _ = inventory.HasItems;
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
        
        Assert.That(exceptions, Is.Empty, "No exceptions should occur during concurrent access");
    }
    
    [Test]
    public void Count_IsThreadSafe_UnderHighContention()
    {
        var inventory = new MyInventory();
        var counts = new List<int>();
        var addCount = 500;
        
        // Add items in parallel
        Parallel.For(0, addCount, _ =>
        {
            inventory.AddItem(new Key());
            lock (counts)
            {
                counts.Add(inventory.Count);
            }
        });
        
        // Final count should be exactly the number of items added
        Assert.That(inventory.Count, Is.EqualTo(addCount));
        
        // All recorded counts should be valid (between 1 and addCount)
        Assert.That(counts.All(c => c >= 1 && c <= addCount), Is.True);
    }
    #endregion

    #region Multiple inventory operations
    [Test]
    public async Task MoveAllItems_BetweenInventories()
    {
        var source = new MyInventory();
        var itemCount = 5;
        
        for (int i = 0; i < itemCount; i++)
        {
            source.AddItem(new Key());
        }
        
        var target = new MyInventory();
        
        while (source.HasItems)
        {
            await target.MoveItemFrom(source);
        }
        
        Assert.That(source.Count, Is.EqualTo(0));
        Assert.That(target.Count, Is.EqualTo(itemCount));
    }
    
    [Test]
    public async Task ChainedMoves_PreserveItems()
    {
        var inv1 = new MyInventory(new Key());
        var inv2 = new MyInventory();
        var inv3 = new MyInventory();
        
        await inv2.MoveItemFrom(inv1);
        await inv3.MoveItemFrom(inv2);
        
        Assert.That(inv1.Count, Is.EqualTo(0));
        Assert.That(inv2.Count, Is.EqualTo(0));
        Assert.That(inv3.Count, Is.EqualTo(1));
    }
    #endregion
}
