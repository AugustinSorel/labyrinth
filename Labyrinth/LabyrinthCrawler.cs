using Labyrinth.Crawl;
using Labyrinth.Items;
using Labyrinth.Tiles;

namespace Labyrinth
{
<<<<<<< HEAD
    public partial class Labyrinth
=======
    public partial class Maze
>>>>>>> c114e44 (changing concurent explorer using dfs algorithm instead of A*)
    {
        private class LabyrinthCrawler(int x, int y, Tile[,] tiles) : ICrawler
        {
            public int X => _x;

            public int Y => _y;

<<<<<<< HEAD
            public Tile FacingTile => ProcessFacingTile((x, y, tile) => tile);

            Direction ICrawler.Direction => _direction;

            public Inventory Walk() => 
                ProcessFacingTile((facingX, facingY, tile) => 
                {
=======
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
                
                // Si ce n'est pas une porte, on ne peut pas déverrouiller
                if (tile is not Door door) return false;

                // Si elle n'est pas verrouillée, c'est un succès immédiat
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
>>>>>>> c114e44 (changing concurent explorer using dfs algorithm instead of A*)
                    var inventory = tile.Pass();

                    _x = facingX;
                    _y = facingY;
                    return inventory;
                });
<<<<<<< HEAD
=======
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
>>>>>>> c114e44 (changing concurent explorer using dfs algorithm instead of A*)

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

<<<<<<< HEAD
            private readonly Direction _direction = Direction.North;
            private readonly Tile[,] _tiles = tiles;
        }
    }
}
=======
            private Direction _direction = Direction.North;
            private readonly Tile[,] _tiles = tiles;
        }
    }
}
>>>>>>> c114e44 (changing concurent explorer using dfs algorithm instead of A*)
