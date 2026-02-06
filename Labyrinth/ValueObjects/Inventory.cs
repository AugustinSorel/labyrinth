namespace Labyrinth.ValueObjects
{
    /// <summary>
    /// Inventory of collectable items for rooms and players.
    /// </summary>
    /// <param name="item">Optional initial item in the inventory.</param>
    public abstract class Inventory
    {
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
        public bool HasItems => _items.Count > 0;

        /// <summary>
        /// Gets the type of the item in the room.
        /// </summary>
        public Task<IEnumerable<Type>> ItemTypes()
        {
            return Task.FromResult(
                _items.Select(item => item.GetType())
            );
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
            if (!from.HasItems)
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


        protected IList<ICollectable> _items = new List<ICollectable>();
    }
}
