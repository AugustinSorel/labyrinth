using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Labyrinth.Exploration
{
    public sealed class AStarPathfinder
    {
        private readonly ExplorationMap _map;

        public AStarPathfinder(ExplorationMap map)
        {
            _map = map;
        }

        public List<KnownCell>? FindPath(
            KnownCell start,
            KnownCell goal)
        {
            var openSet = new PriorityQueue<KnownCell, int>();
            var cameFrom = new Dictionary<KnownCell, KnownCell>();

            var gScore = new Dictionary<KnownCell, int>
            {
                [start] = 0
            };

            openSet.Enqueue(start, Heuristic(start, goal));

            while (openSet.Count > 0)
            {
                var current = openSet.Dequeue();

                if (current.X == goal.X && current.Y == goal.Y)
                    return ReconstructPath(cameFrom, current);

                foreach (var neighbor in _map.GetNeighbors(current))
                {
                    if (neighbor.Type == KnownCellType.Wall)
                        continue;

                    var tentativeG = gScore[current] + 1;

                    if (!gScore.TryGetValue(neighbor, out var g)
                        || tentativeG < g)
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        var fScore = tentativeG + Heuristic(neighbor, goal);
                        openSet.Enqueue(neighbor, fScore);
                    }
                }
            }

            return null; // Pas de chemin connu
        }

        private static int Heuristic(KnownCell a, KnownCell b)
            => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

        private static List<KnownCell> ReconstructPath(
            Dictionary<KnownCell, KnownCell> cameFrom,
            KnownCell current)
        {
            var path = new List<KnownCell> { current };

            while (cameFrom.TryGetValue(current, out var prev))
            {
                current = prev;
                path.Add(current);
            }

            path.Reverse();
            return path;
        }
    }
}

