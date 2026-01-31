namespace Labyrinth.Items
{
    /// <summary>
    /// Inventory of collectable items for rooms and players.
    /// </summary>
    /// <param name="item">Optional initial item in the inventory.</param>
    public abstract class Inventory
    {
        private readonly object _lock = new();

        protected Inventory(ICollectable? item = null)
        {
            if (item is not null)
            {
                _items.Add(item);
            }
        }

        /// <summary>
        /// True if the room has an items, false otherwise.
        /// </summary>
        public bool HasItems
        {
            get
            {
                lock (_lock)
                {
                    return _items.Count > 0;
                }
            }
        }

        /// <summary>
        /// Gets the type of the item in the room.
        /// </summary>
        public Task<IEnumerable<Type>> ItemTypes()
        {
            lock (_lock)
            {
                return Task.FromResult(
                    _items.Select(item => item.GetType()).ToList().AsEnumerable()
                );
            }
        }

        /// <summary>
        /// Places an item in the inventory, removing it from another one.
        /// The operation can fail if the inventory has changed since it was consulted.
        /// </summary>
        /// <param name="from">The inventory from which the item is taken. The item is removed from this inventory.</param>
        /// <param name="nth">The index of the item to move (default: 0).</param>
        /// <returns>True if the item was successfully moved, false if the operation failed (e.g., inventory changed).</returns>
        /// <exception cref="InvalidOperationException">Thrown if the room already contains an item (check with <see cref="HasItem"/>).</exception>
        public Task<bool> MoveItemFrom(Inventory from, int nth = 0)
        {
            // Lock both inventories to prevent concurrent modifications
            // Always lock in a consistent order to avoid deadlocks
            var first = this.GetHashCode() < from.GetHashCode() ? this : from;
            var second = this.GetHashCode() < from.GetHashCode() ? from : this;

            lock (first._lock)
            {
                lock (second._lock)
                {
                    if (!from._items.Any())
                    {
                        return Task.FromResult(false);
                    }

                    // Check if the index is still valid (inventory may have changed)
                    if (nth < 0 || nth >= from._items.Count)
                    {
                        return Task.FromResult(false);
                    }

                    _items.Add(from._items[nth]);
                    from._items.RemoveAt(nth);

                    return Task.FromResult(true);
                }
            }
        }

        /// <summary>
        /// Adds an item directly to this inventory in a thread-safe manner.
        /// </summary>
        /// <param name="item">The item to add.</param>
        public void AddItem(ICollectable item)
        {
            lock (_lock)
            {
                _items.Add(item);
            }
        }

        /// <summary>
        /// Gets the count of items in the inventory.
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _items.Count;
                }
            }
        }

        protected IList<ICollectable> _items = new List<ICollectable>();
    }
}
