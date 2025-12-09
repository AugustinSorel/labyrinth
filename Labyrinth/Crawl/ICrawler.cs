using Labyrinth.Items;
using Labyrinth.Tiles;

namespace Labyrinth.Crawl
{
    /// <summary>
    /// Labyrinth crawler interface.
    /// </summary>
    public interface ICrawler
    {
        /// <summary>
        /// Gets the current X position.
        /// </summary>
        int X { get; }

        /// <summary>
        /// Gets the current Y position.
        /// </summary>
        int Y { get; }

        /// <summary>
        /// Gets the current direction.
        /// </summary>
        Direction Direction { get; }

        /// <summary>
        /// Gets the type of tile in front of the crawler.
        /// </summary>
        Task<TileType> GetFacingTileTypeAsync();

        // NOUVEAU : Comme on n'a plus accès à Tile.IsTraversable, 
        // le crawler doit nous dire si la voie est libre.
        bool CanMoveForward { get; }

        // NOUVEAU : On ne peut plus faire door.Open(bag). 
        // C'est le crawler qui envoie la commande au "serveur".
        Task<bool> TryUnlockAsync(Inventory keyChain);

        /// <summary>
        /// Attempts to walk forward into the tile in front of the crawler.
        /// </summary>
        /// <param name="keyChain">The inventory containing keys for unlocking doors.</param>
        /// <returns>An inventory of the collectable items in the place reached, or null if the operation failed (wall or locked door).</returns>
        Task<Inventory?> TryWalkAsync(Inventory? keyChain);
    }
}
