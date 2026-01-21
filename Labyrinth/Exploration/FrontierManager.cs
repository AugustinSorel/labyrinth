using System.Collections.Generic;
using System.Linq;
using Labyrinth.Crawl;
using System;

namespace Labyrinth.Exploration
{
    public enum FrontierSelectionPolicy
    {
        RoundRobin,
        Nearest
    }

    public class FrontierManager
    {
        private readonly ExplorationMap _map;
        private long _rrCounter = 0;

        public FrontierManager(ExplorationMap map)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
        }

        /// <summary>
        /// Compute current frontier cells: unknown cells adjacent to traversable known cells.
        /// </summary>
        private List<(int X, int Y)> ComputeFrontiers()
        {
            var res = new HashSet<(int X, int Y)>();

            if (!_map.TryGet(out var snap)) return res.ToList();

            foreach (var kv in snap)
            {
                var (x, y) = kv.Key;
                var type = kv.Value;
                if (type == CellType.Wall || type == CellType.Outside) continue;

                // for each neighbor, if unknown in map, it's a frontier coord
                var neigh = new (int X, int Y)[] { (x, y - 1), (x + 1, y), (x, y + 1), (x - 1, y) };
                foreach (var n in neigh)
                {
                    if (_map.Get(n.X, n.Y) == CellType.Unknown)
                    {
                        res.Add(n);
                    }
                }
            }
            return res.ToList();
        }

        /// <summary>
        /// Try to select and claim a frontier for the given ownerId using the selection policy.
        /// Returns (x,y) if claim succeeded, otherwise null.
        /// </summary>
        public (int X, int Y)? TrySelectFrontierForOwner(int ownerId, ICrawler crawler, FrontierSelectionPolicy policy)
        {
            var frontiers = ComputeFrontiers();
            if (frontiers.Count == 0) return null;

            // filter out already claimed
            var available = frontiers.Where(f => _map.GetClaimOwner(f.X, f.Y) == 0).ToList();
            if (available.Count == 0) return null;

            if (policy == FrontierSelectionPolicy.RoundRobin)
            {
                var idx = (int)(System.Threading.Interlocked.Increment(ref _rrCounter) % available.Count);
                // attempt to claim until success
                for (int i = 0; i < available.Count; i++)
                {
                    var candidate = available[(idx + i) % available.Count];
                    if (_map.TryClaim(candidate.X, candidate.Y, ownerId))
                        return candidate;
                }
                return null;
            }
            else // Nearest
            {
                var curX = crawler.X;
                var curY = crawler.Y;
                var ordered = available.OrderBy(f => Math.Abs(f.X - curX) + Math.Abs(f.Y - curY)).ToList();
                foreach (var candidate in ordered)
                {
                    if (_map.TryClaim(candidate.X, candidate.Y, ownerId))
                        return candidate;
                }
                return null;
            }
        }
    }
}
