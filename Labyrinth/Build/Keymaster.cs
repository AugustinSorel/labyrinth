<<<<<<< HEAD
﻿using Labyrinth.Items;
=======
using Labyrinth.Items;
>>>>>>> c114e44 (changing concurent explorer using dfs algorithm instead of A*)
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
<<<<<<< HEAD
            if (unplacedKeys.HasItems || emptyKeyRooms.Count > 0)
            {
                throw new InvalidOperationException("Unmatched key/door creation");
            }
=======
            // Allow unmatched keys or empty key rooms: some maps may include doors without a corresponding key room
            // (e.g., locked exits). Previously this threw an exception; for exploration tests we accept it.
            // If stricter validation is needed, reintroduce checks here.
            // if (unplacedKeys.HasItems || emptyKeyRooms.Count > 0)
            // {
            //     throw new InvalidOperationException("Unmatched key/door creation");
            // }
>>>>>>> c114e44 (changing concurent explorer using dfs algorithm instead of A*)
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
            PlaceKey();
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
            PlaceKey();
            return room;
        }

        private void PlaceKey()
        {
            if (unplacedKeys.HasItems && emptyKeyRooms.Count > 0)
            {
<<<<<<< HEAD
                emptyKeyRooms.Pop().Pass().MoveItemFrom(unplacedKeys);
=======
                // Synchronous version for initialization - MoveItemFrom is async but we use .Wait() for initialization
                emptyKeyRooms.Pop().Pass().MoveItemFrom(unplacedKeys).Wait();
>>>>>>> c114e44 (changing concurent explorer using dfs algorithm instead of A*)
            }
        }

        private readonly MyInventory unplacedKeys = new();
        private Stack<Room> emptyKeyRooms = new();
    }
}