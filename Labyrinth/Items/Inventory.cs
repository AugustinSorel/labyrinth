namespace Labyrinth.Items
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
        public async Task<IEnumerable<Type>> ItemTypes()
        {
            return await Task.Run(() =>
            {
                return _items.Select(item => item.GetType());

            });
        }

        /// <summary>
        /// Places an item in the inventory, removing it from another one.
        /// </summary>
        /// <param name="from">The inventory from which the item is taken. The item is removed from this inventory.</param>
        /// <exception cref="InvalidOperationException">Thrown if the room already contains an item (check with <see cref="HasItem"/>).</exception>
        public void MoveItemFrom(Inventory from, int nth = 0)
        {
            if (!from.HasItems)
            {
                throw new InvalidOperationException("No item to take from the source inventory");
            }
            _items.Add(from._items[nth]);
            from._items.RemoveAt(nth);
        }

        protected IList<ICollectable> _items = new List<ICollectable>();
    }
}
