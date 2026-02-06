using Labyrinth.Crawl;
using Labyrinth.Items;
using Labyrinth.Tiles;

namespace Labyrinth
{
    public partial class Maze
    {
        private class LabyrinthCrawler(int x, int y, Tile[,] tiles) : ICrawler
        {
            public int X => _x;

            public int Y => _y;

            public Tile FacingTile => ProcessFacingTile((x, y, tile) => tile);

            Direction ICrawler.Direction => _direction;

            public Task<TileType> GetFacingTileTypeAsync() => 
                Task.FromResult(ProcessFacingTile((x, y, tile) => tile switch
                        {
                            Outside => TileType.Outside, // Si c'est la tuile singleton Outside
                            Door => TileType.Door,
                            Wall => TileType.Wall,
                            _ => TileType.Empty    // Corridor, Room, etc.
                        }));

            public bool CanMoveForward => ProcessFacingTile((x, y, tile) => tile.IsTraversable);

            public void TurnRight() => _direction.TurnRight();

            public void TurnLeft() => _direction.TurnLeft();

            public async Task<bool> TryUnlockAsync(Inventory keyChain)
            {
                var tile = ProcessFacingTile((x, y, t) => t);
                
                // Si ce n'est pas une porte, on ne peut pas d�verrouiller
                if (tile is not Door door) return false;

                // Si elle n'est pas verrouill�e, c'est un succ�s imm�diat
                if (!door.IsLocked) return true;

                // Sinon, on tente d'ouvrir avec l'inventaire fourni
                return await door.OpenAsync(keyChain);
            }

            public async Task<Inventory?> TryWalkAsync(Inventory? keyChain)
            {
                return await ProcessFacingTileAsync(async (facingX, facingY, tile) =>
                {
                    // Si c'est un mur, l'opération échoue
                    if (tile is Wall) return null;

                    // Si c'est une porte, vérifier qu'elle peut être traversée
                    if (tile is Door door)
                    {
                        // Si la porte est verrouillée, essayer de l'ouvrir avec l'inventaire fourni
                        if (door.IsLocked)
                        {
                            if (keyChain == null) return null;
                            var unlocked = await door.OpenAsync(keyChain);
                            if (!unlocked) return null;
                        }
                    }

                    // Si c'est Outside, l'opération échoue
                    if (tile is Outside) return null;

                    // Si c'est une pièce ou un corridor, on récupère l'inventaire
                    var inventory = tile.Pass();

                    _x = facingX;
                    _y = facingY;
                    return inventory;
                });
            }

            private async Task<T> ProcessFacingTileAsync<T>(Func<int, int, Tile, Task<T>> process)
            {
                int facingX = _x + _direction.DeltaX,
                    facingY = _y + _direction.DeltaY;

                return await process(
                    facingX, facingY,
                    IsOut(facingX, dimension: 0) ||
                    IsOut(facingY, dimension: 1)
                        ? Outside.Singleton
                        : _tiles[facingX, facingY]
                 );
            }

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

            private Direction _direction = Direction.North;
            private readonly Tile[,] _tiles = tiles;
        }
    }
}
