using System.Collections.Concurrent;
using System.Text;

namespace Labyrinth.Exploration
{
    public enum CellType
    {
        Unknown,
        Wall,
        Door,
        Empty,
        Outside,
        Start,
        Visited
    }

    /// <summary>
    /// Thread-safe exploration map for a single or multiple crawlers. Stores discovered cell types by coordinates.
    /// Supports claiming frontier cells to coordinate multiple crawlers.
    /// </summary>
    public class ExplorationMap
    {
        private readonly ConcurrentDictionary<(int X, int Y), CellType> _cells = new();
        private readonly ConcurrentDictionary<(int X, int Y), int> _claims = new();

        public event EventHandler<ExitFoundEventArgs>? ExitFound;

        public ExplorationMap()
        {
        }

        public ExplorationMap(int startX, int startY)
        {
            Mark(startX, startY, CellType.Start);
        }

        /// <summary>
        /// Atomically mark a cell with a given type. Merge rules:
        /// - Start/Visited always override existing value.
        /// - If existing is Unknown, the new value is set.
        /// - Otherwise the existing value is kept (prefer the most informative already-known value).
        /// </summary>
        public void Mark(int x, int y, CellType type)
        {
            _cells.AddOrUpdate((x, y), type, (k, old) => Merge(old, type));
            // If a cell becomes Visited or Start, free any claim on it
            if (type == CellType.Visited || type == CellType.Start)
            {
                _claims.TryRemove((x, y), out _);
            }

            // NOTE: Do NOT raise ExitFound here. The explorer that observed the outside
            // has the context (crawler position and owner id) and will raise the event.
        }

        private static CellType Merge(CellType old, CellType incoming)
        {
            // Start/Visited are highest priority (they represent actual presence)
            if (incoming == CellType.Start || incoming == CellType.Visited)
                return incoming;

            if (old == CellType.Unknown)
                return incoming;

            // If existing is Start/Visited keep it
            if (old == CellType.Start || old == CellType.Visited)
                return old;

            // Prefer Door over Empty, Wall stays Wall, Outside stays Outside
            if (old == CellType.Wall || old == CellType.Outside)
                return old;

            if (incoming == CellType.Wall || incoming == CellType.Outside)
                return incoming;

            if (old == CellType.Door || incoming == CellType.Door)
                return CellType.Door;

            // otherwise keep existing
            return old;
        }

        public CellType Get(int x, int y)
        {
            return _cells.TryGetValue((x, y), out var t) ? t : CellType.Unknown;
        }

        /// <summary>
        /// Attempt to claim a cell for an owner (crawler id). Returns true if claim succeeded.
        /// If the cell is already claimed by another owner, returns false.
        /// </summary>
        public bool TryClaim(int x, int y, int ownerId)
        {
            if (ownerId <= 0) throw new ArgumentOutOfRangeException(nameof(ownerId));
            return _claims.TryAdd((x, y), ownerId);
        }

        /// <summary>
        /// Release a claim if held by the given owner. Returns true if claim was removed.
        /// </summary>
        public bool TryRelease(int x, int y, int ownerId)
        {
            return _claims.TryGetValue((x, y), out var owner) && owner == ownerId && _claims.TryRemove((x, y), out _);
        }

        /// <summary>
        /// Get the owner id of a claim, or 0 if not claimed.
        /// </summary>
        public int GetClaimOwner(int x, int y)
        {
            return _claims.TryGetValue((x, y), out var owner) ? owner : 0;
        }

        /// <summary>
        /// True if the cell is currently claimed by any owner.
        /// </summary>
        public bool IsClaimed(int x, int y) => _claims.ContainsKey((x, y));

        /// <summary>
        /// Get a snapshot dictionary of the current known cells. Thread-safe.
        /// </summary>
        public bool TryGet(out Dictionary<(int X, int Y), CellType> snapshot)
        {
            snapshot = _cells.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            return snapshot.Count > 0;
        }

        public override string ToString()
        {
            var cells = _cells;
            if (cells.IsEmpty) return string.Empty;

            var keys = cells.Keys.ToArray();
            var minX = keys.Min(k => k.X);
            var maxX = keys.Max(k => k.X);
            var minY = keys.Min(k => k.Y);
            var maxY = keys.Max(k => k.Y);

            var sb = new StringBuilder();
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var c = Get(x, y);
                    sb.Append(c switch
                    {
                        CellType.Unknown => '?',
                        CellType.Wall => '#',
                        CellType.Door => '/',
                        CellType.Empty => ' ',
                        CellType.Outside => 'O',
                        CellType.Start => 'S',
                        CellType.Visited => 'v',
                        _ => '?'
                    });
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        /// <summary>
        /// Notify listeners that an exit has been found by a crawler. External callers should use this
        /// method instead of invoking the event directly (events can only be raised by the declaring type).
        /// </summary>
        public void NotifyExitFound(int crawlerX, int crawlerY, int exitX, int exitY, int ownerId)
        {
            ExitFound?.Invoke(this, new ExitFoundEventArgs(crawlerX, crawlerY, exitX, exitY, ownerId));
        }
    }
}
