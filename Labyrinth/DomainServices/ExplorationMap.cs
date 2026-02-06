using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Labyrinth.DomainServices;

namespace Labyrinth.DomainServices
{
    public sealed class ExplorationMap
    {
        private readonly ConcurrentDictionary<(int x, int y), KnownCell> _cells
            = new();

        private readonly ConcurrentDictionary<(int x, int y), int> _reservedFrontiers
            = new();

        public bool TryReserveFrontier(KnownCell cell, int crawlerId)
        {
            // Réservation atomique : true si personne ne l’avait prise
            return _reservedFrontiers.TryAdd((cell.X, cell.Y), crawlerId);
        }


        public KnownCell GetOrCreate(int x, int y)
        {
            return _cells.GetOrAdd((x, y), key => new KnownCell(key.x, key.y));
        }

        public bool IsKnown(int x, int y)
        {
            return _cells.ContainsKey((x, y));
        }

        public void UpdateCell(int x, int y, KnownCellType type)
        {
            var cell = GetOrCreate(x, y);
            cell.Type = type;
        }

        public void MarkVisited(int x, int y)
        {
            var cell = GetOrCreate(x, y);
            cell.Visited = true;
        }

        public IEnumerable<KnownCell> GetNeighbors(KnownCell cell)
        {
            static IEnumerable<(int dx, int dy)> Dirs()
            {
                yield return (0, 1);
                yield return (1, 0);
                yield return (0, -1);
                yield return (-1, 0);
            }

            foreach (var (dx, dy) in Dirs())
            {
                var pos = (cell.X + dx, cell.Y + dy);
                if (_cells.TryGetValue(pos, out var neighbor))
                    yield return neighbor;
            }
        }

        public IEnumerable<KnownCell> GetFrontiers()
        {
            foreach (var cell in _cells.Values)
            {
                if (cell.Type == KnownCellType.Wall)
                    continue;

                foreach (var (dx, dy) in new[] { (0, 1), (1, 0), (0, -1), (-1, 0) })
                {
                    if (!_cells.ContainsKey((cell.X + dx, cell.Y + dy)))
                    {
                        yield return cell;
                        break;
                    }
                }
            }
        }
    }
}

