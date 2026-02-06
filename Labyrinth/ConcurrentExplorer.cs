using global::Labyrinth.Core;
using global::Labyrinth.ValueObjects;
using global::Labyrinth.Entities;
using Labyrinth.DomainServices;

namespace Labyrinth
{
    namespace Labyrinth.DomainServices
    {
        public sealed class ConcurrentExplorer
        {
            private readonly ICrawler _crawler;
            private readonly ExplorationMap _map;
            private readonly int _crawlerId;
            private readonly AStarPathfinder _pathfinder;


            private Inventory? _inventory;

            public ConcurrentExplorer(
                ICrawler crawler,
                ExplorationMap map,
                int crawlerId,
                AStarPathfinder pathfinder)
            {
                _crawler = crawler;
                _map = map;
                _crawlerId = crawlerId;
                _pathfinder = pathfinder;
            }

            public async Task ExploreAsync(CancellationToken token)
            {
                // Marquer la position initiale comme visitée
                _map.MarkVisited(_crawler.X, _crawler.Y);

                while (!token.IsCancellationRequested)
                {
                    // 1. Observer la case située devant le crawler
                    var facingType = await _crawler.GetFacingTileTypeAsync();

                    int fx = _crawler.X + _crawler.Direction.DeltaX;
                    int fy = _crawler.Y + _crawler.Direction.DeltaY;

                    _map.UpdateCell(fx, fy, ConvertTileType(facingType));

                    // 2. Tentative de déplacement direct
                    if (_crawler.CanMoveForward)
                    {
                        var newInventory = await _crawler.TryWalkAsync(_inventory);
                        if (newInventory != null)
                        {
                            _inventory = newInventory;
                            _map.MarkVisited(_crawler.X, _crawler.Y);
                            continue;
                        }
                    }

                    // 3. Recherche d’une frontière à explorer
                    var frontier = _map.GetFrontiers()
                                       .OrderBy(c => Distance(c, _crawler.X, _crawler.Y))
                                       .FirstOrDefault(c => _map.TryReserveFrontier(c, _crawlerId));

                    if (frontier == null)
                    {
                        // Plus rien à explorer pour ce crawler
                        await Task.Delay(50, token);
                        continue;
                    }

                    // 4. Déplacement optimisé vers la frontière
                    await MoveUsingAStar(frontier, token);
                }
            }


            private async Task MoveNaivelyToward(KnownCell target, CancellationToken token)
            {
                while ((_crawler.X != target.X || _crawler.Y != target.Y)
                       && !token.IsCancellationRequested)
                {
                    // Pour l’instant : simple avance tant que possible
                    if (_crawler.CanMoveForward)
                    {
                        var inv = await _crawler.TryWalkAsync(_inventory);
                        if (inv != null)
                        {
                            _inventory = inv;
                            _map.MarkVisited(_crawler.X, _crawler.Y);
                            continue;
                        }
                    }

                    // Bloqué ? on abandonne la cible
                    break;
                }
            }

            private static int Distance(KnownCell cell, int x, int y)
                => Math.Abs(cell.X - x) + Math.Abs(cell.Y - y);

            private static KnownCellType ConvertTileType(TileType type) =>
                type switch
                {
                    TileType.Wall => KnownCellType.Wall,
                    TileType.Door => KnownCellType.Door,
                    TileType.Outside => KnownCellType.Wall,
                    _ => KnownCellType.Room
                };

            private async Task MoveUsingAStar(
                            KnownCell target,
                            CancellationToken token)
            {
                var start = _map.GetOrCreate(_crawler.X, _crawler.Y);
                var path = _pathfinder.FindPath(start, target);

                if (path == null || path.Count < 2)
                    return;

                foreach (var step in path.Skip(1))
                {
                    if (token.IsCancellationRequested)
                        return;

                    // À ce stade, on suppose la direction déjà cohérente
                    if (_crawler.CanMoveForward)
                    {
                        var inv = await _crawler.TryWalkAsync(_inventory);
                        if (inv != null)
                        {
                            _inventory = inv;
                            _map.MarkVisited(_crawler.X, _crawler.Y);
                        }
                        else
                            return;
                    }
                    else
                        return;
                }
            }
        }
    }

}
