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

            public Tile FacingTile => ProcessFacingTile((x, y, tile) => tile);

            Direction ICrawler.Direction => _direction;

            public TileType FacingType => ProcessFacingTile((x, y, tile) => tile switch
                        {
                            Outside => TileType.Outside, // Si c'est la tuile singleton Outside
                            Door => TileType.Door,
                            Wall => TileType.Wall,
                            _ => TileType.Empty    // Corridor, Room, etc.
                        });

            public bool CanMoveForward => ProcessFacingTile((x, y, tile) => tile.IsTraversable);

            public bool TryUnlock(Inventory keyChain)
            {
                return ProcessFacingTile((x, y, tile) =>
                {
                    // Si ce n'est pas une porte, on ne peut pas déverrouiller
                    if (tile is not Door door) return false;

                    // Si elle n'est pas verrouillée, c'est un succès immédiat
                    if (!door.IsLocked) return true;

                    // Sinon, on tente d'ouvrir avec l'inventaire fourni
                    return door.Open(keyChain);
                });
            }

            public Inventory Walk() =>
                ProcessFacingTile((facingX, facingY, tile) =>
                {
                    var inventory = tile.Pass();

                    _x = facingX;
                    _y = facingY;
                    return inventory;
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
