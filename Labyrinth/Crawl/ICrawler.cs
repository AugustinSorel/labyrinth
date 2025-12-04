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
        /// Gets the tile in front of the crawler.
        /// </summary>
        TileType FacingTile { get; }

        // NOUVEAU : Comme on n'a plus accès à Tile.IsTraversable, 
        // le crawler doit nous dire si la voie est libre.
        bool CanMoveForward { get; }

        // NOUVEAU : On ne peut plus faire door.Open(bag). 
        // C'est le crawler qui envoie la commande au "serveur".
        bool TryUnlock(Inventory keyChain);

        /// <summary>
        /// Pass the tile in front of the crawler and move into it.
        /// </summary>
        /// <returns>An inventory of the collectable items in the place reached.</returns>
        Inventory Walk();
    }
}
