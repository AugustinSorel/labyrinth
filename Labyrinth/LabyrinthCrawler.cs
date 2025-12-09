using Labyrinth.Crawl;
using Labyrinth.Items;
using Labyrinth.Tiles;

namespace Labyrinth
{
    public partial class Labyrinth
    {
        private class LabyrinthCrawler(int x, int y, Tile[,] tiles) : ICrawler
        {
            public int X => _x;
            public int Y => _y;

            Direction ICrawler.Direction => _direction;

            public Task<Type> GetFacingTileTypeAsync() =>
                ProcessFacingTile((_, _, tile) => Task.FromResult(tile.GetType()));

            public Task<Inventory?> TryWalkAsync(Inventory crawlerInventory) =>
                ProcessFacingTile(async (facingX, facingY, tile) =>
                {
                    bool canPass = tile.IsTraversable;

                    if (!canPass && tile is Door door)
                    {
                        canPass = await door.OpenAsync(crawlerInventory);
                    }

                    if (canPass)
                    {
                        var loot = tile.Pass();
                        _x = facingX;
                        _y = facingY;
                        return loot;
                    }

                    return null; // Hit a wall or a door we couldn't open
                });

            private bool IsOut(int pos, int dimension) =>
                pos < 0 || pos >= _tiles.GetLength(dimension);

            private T ProcessFacingTile<T>(Func<int, int, Tile, T> process)
            {
                int facingX = _x + _direction.DeltaX,
                    facingY = _y + _direction.DeltaY;

                return process(
                    facingX, facingY,
                    IsOut(facingX, dimension: 0) ||
                    IsOut(facingY, dimension: 1)
                        ? Outside.Singleton
                        : _tiles[facingX, facingY]
                 );
            }

            private int _x = x;
            private int _y = y;

            private readonly Direction _direction = Direction.North;
            private readonly Tile[,] _tiles = tiles;
        }
    }
}
