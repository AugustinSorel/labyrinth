using Labyrinth.Items;
using Labyrinth.Tiles;

namespace Labyrinth.Build
{
    /// <summary>
    /// Manage the creation of doors and key rooms ensuring each door has a corresponding key room.
    /// </summary>
    public sealed class Keymaster : IDisposable
    {
        /// <summary>
        /// Ensure all created doors have a corresponding key room and vice versa.
        /// </summary>
        /// <exception cref="InvalidOperationException">Some keys are missing or are not placed.</exception>
        public void Dispose()
        {
            if (unplacedKeys.HasItems || emptyKeyRooms.Count > 0)
            {
                throw new InvalidOperationException("Unmatched key/door creation");
            }
        }

        /// <summary>
        /// Create a new door and place its key in a previously created empty key room (if any).
        /// </summary>
        /// <returns>Created door</returns>
        /// <exception cref="NotSupportedException">Multiple doors before key placement</exception>
        public Door NewDoor()
        {
            var door = new Door();

            door.LockAndTakeKey(unplacedKeys);
            PlaceKeyAsync();
            return door;
        }

        /// <summary>
        /// Create a new room with key and place the key if a door was previously created.
        /// </summary>
        /// <returns>Created key room</returns>
        /// <exception cref="NotSupportedException">Multiple keyss before key placement</exception>
        public Room NewKeyRoom()
        {
            var room = new Room();

            emptyKeyRooms.Push(room);
            PlaceKeyAsync();
            return room;
        }

        private async void PlaceKeyAsync()
        {
            if (unplacedKeys.HasItems && emptyKeyRooms.Count > 0)
            {
                await emptyKeyRooms.Pop().Pass().TryMoveItemFromAsync(unplacedKeys);
            }
        }

        private readonly MyInventory unplacedKeys = new();
        private Stack<Room> emptyKeyRooms = new();
    }
}
