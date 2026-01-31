namespace Labyrinth.Items
{
    /// <summary>
    /// Inventory class that exposes the item it contains.
    /// </summary>
    /// <param name="item">Optional initial item in the inventory.</param>
    public class MyInventory(ICollectable? item = null) : Inventory(item)
    {
        /// <summary>
        /// Items in the inventory. Returns a snapshot for thread-safety.
        /// </summary>
        public IEnumerable<ICollectable> Items => GetItemsSnapshot();

        private IEnumerable<ICollectable> GetItemsSnapshot()
        {
            // Return a copy of the items list for thread-safety
            return _items.ToList();
        }
    }
}
