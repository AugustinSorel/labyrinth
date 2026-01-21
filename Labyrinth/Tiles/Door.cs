using Labyrinth.Items;

namespace Labyrinth.Tiles
{
    /// <summary>
    /// A door tile in the labyrinth either locked with no key or opened with key (no "closed and unlocked" state).
    /// </summary>
    public class Door : Tile
    {
        public Door() : base(new Key()) =>
            _key = (Key)LocalInventory.Items.First();

        public override bool IsTraversable => IsOpened;

        /// <summary>
        /// True if the door is opened, false if closed and locked.
        /// </summary>
        public bool IsOpened => !IsLocked;

        /// <summary>
        /// True if the door is locked, false if unlocked and opened.
        /// </summary>
        public bool IsLocked => !LocalInventory.HasItems; // A key in the door

        /// <summary>
        /// Opens the door with the provided key.
        /// </summary>
        /// <param name="keySource">Inventory containing the key to open the door.</param>
        /// <returns>True if the key opens the door, false otherwise.</returns>
        /// <remarks>The key is removed from the inventory only if it opens the door.</remarks>
        public bool Open(Inventory keySource)
        {
            if (IsOpened)
            {
                // Already opened -> treat as success
                return true;
            }
            LocalInventory.MoveItemFrom(keySource);
            var movedKey = LocalInventory.Items.FirstOrDefault();
            var matches = movedKey is Key;
            if (!matches)
            {
                // move back
                keySource.MoveItemFrom(LocalInventory).Wait();
            }
            return IsOpened;
        }

        /// <summary>
        /// Opens the door with the provided key asynchronously.
        /// </summary>
        /// <param name="keySource">Inventory containing the key to open the door.</param>
        /// <returns>True if the key opens the door, false otherwise.</returns>
        /// <remarks>The key is removed from the inventory only if it opens the door.</remarks>
        public async Task<bool> OpenAsync(Inventory keySource)
        {
            if (IsOpened)
            {
                // Already opened -> treat as success
                return true;
            }
            var moved = await LocalInventory.MoveItemFrom(keySource);
            if (!moved)
            {
                return false;
            }
            var movedKey = LocalInventory.Items.FirstOrDefault();
            var matchesAsync = movedKey is Key;
            if (!matchesAsync)
            {
                await keySource.MoveItemFrom(LocalInventory);
            }
            return IsOpened;
        }

        /// <summary>
        /// Lock the door and removes the key.
        /// </summary>
        /// <exception cref="InvalidOperationException">The door is already closed (check with <see cref="IsLocked"/>).</exception>
        public void LockAndTakeKey(Inventory whereKeyGoes)
        {
            if (IsLocked)
            {
                throw new InvalidOperationException("Door is already locked.");
            }
            // Synchronous version for initialization - MoveItemFrom is async but we use .Result for initialization
            whereKeyGoes.MoveItemFrom(LocalInventory).Wait();
        }

        private readonly Key _key;
    }
}
