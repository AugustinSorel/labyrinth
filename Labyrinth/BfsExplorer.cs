using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Labyrinth.Crawl;
using Labyrinth.Items;
using Labyrinth.Sys;
using Labyrinth.Tiles;
using Labyrinth.Exploration;

namespace Labyrinth
{
    public class BfsExplorer
    {
        private readonly ICrawler _crawler;
        private Inventory _inventory = new MyInventory();

        public event EventHandler<CrawlingEventArgs>? PositionChanged;
        public event EventHandler<CrawlingEventArgs>? DirectionChanged;

        public ExplorationMap Map { get; }

        private readonly Stack<(int X, int Y)> _path = new();
        private readonly HashSet<(int X, int Y)> _failedCells = new();
        private readonly int _ownerId;

        public bool HasKey => _inventory.HasItems;
        public int KeyCount => _inventory.Count;
        public bool HasStartedMoving { get; private set; }
        public bool IsBlocked { get; private set; }

        public BfsExplorer(ICrawler crawler) : this(crawler, null, 0) { }

        public BfsExplorer(ICrawler crawler, ExplorationMap? map) : this(crawler, map, 0) { }

        public BfsExplorer(ICrawler crawler, ExplorationMap? map, int ownerId)
        {
            _crawler = crawler ?? throw new ArgumentNullException(nameof(crawler));
            Map = map ?? new ExplorationMap();
            _ownerId = ownerId;
        }

        private bool HasUnexploredFrontier()
        {
            if (!Map.TryGet(out var snapshot)) return false;

            var visitedCells = snapshot
                .Where(kvp => kvp.Value == CellType.Visited || kvp.Value == CellType.Start)
                .Select(kvp => kvp.Key)
                .ToList();

            var directions = new[] { (0, -1), (1, 0), (0, 1), (-1, 0) };

            foreach (var (x, y) in visitedCells)
            {
                foreach (var (dx, dy) in directions)
                {
                    var nx = x + dx;
                    var ny = y + dy;

                    if (_failedCells.Contains((nx, ny))) continue;

                    var cellType = snapshot.TryGetValue((nx, ny), out var ct) ? ct : CellType.Unknown;

                    if (cellType == CellType.Unknown || cellType == CellType.Empty)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HasPotentiallyReachableUnknownCells()
        {
            if (!Map.TryGet(out var snapshot)) return false;

            var directions = new[] { (0, -1), (1, 0), (0, 1), (-1, 0) };

            var doorCells = snapshot
                .Where(kvp => kvp.Value == CellType.Door)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var (doorX, doorY) in doorCells)
            {
                foreach (var (dx, dy) in directions)
                {
                    var nx = doorX + dx;
                    var ny = doorY + dy;

                    var cellType = snapshot.TryGetValue((nx, ny), out var ct) ? ct : CellType.Unknown;

                    if (cellType == CellType.Unknown)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void LogExplorationState(string context)
        {
            if (!Map.TryGet(out var snapshot)) return;

            var visitedCount = snapshot.Count(kvp => kvp.Value == CellType.Visited || kvp.Value == CellType.Start);
            var doorCount = snapshot.Count(kvp => kvp.Value == CellType.Door);
            var wallCount = snapshot.Count(kvp => kvp.Value == CellType.Wall);
            var unknownAdjacentToVisited = 0;

            var directions = new[] { (0, -1), (1, 0), (0, 1), (-1, 0) };
            var visitedCells = snapshot
                .Where(kvp => kvp.Value == CellType.Visited || kvp.Value == CellType.Start)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var (x, y) in visitedCells)
            {
                foreach (var (dx, dy) in directions)
                {
                    var nx = x + dx;
                    var ny = y + dy;
                    if (_failedCells.Contains((nx, ny))) continue;

                    var cellType = snapshot.TryGetValue((nx, ny), out var ct) ? ct : CellType.Unknown;
                    if (cellType == CellType.Unknown || cellType == CellType.Empty)
                    {
                        unknownAdjacentToVisited++;
                    }
                }
            }
        }

        private List<(int X, int Y, Direction Dir)> GetUnexploredNeighbors()
        {
            var result = new List<(int X, int Y, Direction Dir)>();
            var dirs = new[] { Direction.North, Direction.East, Direction.South, Direction.West };

            foreach (var d in dirs)
            {
                var nx = _crawler.X + d.DeltaX;
                var ny = _crawler.Y + d.DeltaY;

                if (_failedCells.Contains((nx, ny))) continue;

                var cellType = Map.Get(nx, ny);
                if (cellType == CellType.Unknown || cellType == CellType.Empty)
                {
                    result.Add((nx, ny, d));
                }
            }

            return result;
        }

        private async Task<bool> NavigateToFrontierAsync(CancellationToken cancellationToken)
        {
            if (!Map.TryGet(out var snapshot)) return false;

            var directions = new[] { (0, -1), (1, 0), (0, 1), (-1, 0) };

            var frontierCells = snapshot
                .Where(kvp => kvp.Value == CellType.Visited || kvp.Value == CellType.Start)
                .Where(kvp =>
                {
                    var (x, y) = kvp.Key;
                    return directions.Any(d =>
                    {
                        var nx = x + d.Item1;
                        var ny = y + d.Item2;
                        if (_failedCells.Contains((nx, ny))) return false;
                        var ct = snapshot.TryGetValue((nx, ny), out var cellType) ? cellType : CellType.Unknown;
                        return ct == CellType.Unknown || ct == CellType.Empty;
                    });
                })
                .Select(kvp => kvp.Key)
                .OrderBy(c => Math.Abs(c.X - _crawler.X) + Math.Abs(c.Y - _crawler.Y))
                .ToList();

            if (frontierCells.Count == 0) return false;

            foreach (var (targetX, targetY) in frontierCells)
            {
                if (cancellationToken.IsCancellationRequested) return false;

                if (_crawler.X == targetX && _crawler.Y == targetY)
                {
                    _path.Clear();
                    var explored = await TryExploreUnexploredNeighborsAsync(cancellationToken);
                    return explored;
                }

                var path = FindPathTo(targetX, targetY);
                if (path != null && path.Count > 0)
                {
                    bool reachedTarget = true;
                    foreach (var (nextX, nextY) in path)
                    {
                        if (cancellationToken.IsCancellationRequested) return false;
                        var moved = await MoveToAsync(nextX, nextY, cancellationToken);
                        if (!moved)
                        {
                            reachedTarget = false;
                            break;
                        }
                    }
                    if (reachedTarget)
                    {
                        _path.Clear();
                        await TryExploreUnexploredNeighborsAsync(cancellationToken);
                        return true;
                    }
                }
            }

            return false;
        }

        private async Task<bool> TryExploreUnexploredNeighborsAsync(CancellationToken cancellationToken)
        {
            var unexplored = GetUnexploredNeighbors();
            bool anyExplored = false;

            foreach (var (nx, ny, dir) in unexplored)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var moved = await MoveToAsync(nx, ny, cancellationToken);
                if (moved)
                {
                    anyExplored = true;
                    break;
                }
                else
                {
                    _failedCells.Add((nx, ny));
                }
            }

            return anyExplored;
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            int maxIterations = 500000;
            int iterations = 0;
            int noProgressRounds = 0;
            const int maxNoProgressRounds = 300;

            Map.Mark(_crawler.X, _crawler.Y, CellType.Start);
            _failedCells.Clear();

            while (noProgressRounds < maxNoProgressRounds && iterations < maxIterations)
            {
                if (cancellationToken.IsCancellationRequested) break;

                bool madeProgress = false;

                if (HasKey)
                {
                    var openedWithKey = await TryOpenDoorsWithKeysAsync(cancellationToken);
                    if (openedWithKey)
                    {
                        madeProgress = true;
                        noProgressRounds = 0;
                        _failedCells.Clear();

                        await ExploreFullyAsync(cancellationToken);
                    }
                }

                int explorationSteps = 0;
                while (explorationSteps < 50000)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    iterations++;
                    explorationSteps++;

                    if (HasKey && explorationSteps % 10 == 0)
                    {
                        var openedMidExplore = await TryOpenDoorsWithKeysAsync(cancellationToken);
                        if (openedMidExplore)
                        {
                            madeProgress = true;
                            _failedCells.Clear();
                            await ExploreFullyAsync(cancellationToken);
                        }
                    }

                    var cont = await ExploreStepAsync(cancellationToken);
                    if (!cont)
                    {
                        if (HasUnexploredFrontier())
                        {
                            var navigated = await NavigateToFrontierAsync(cancellationToken);
                            if (navigated)
                            {
                                madeProgress = true;
                                continue;
                            }
                        }
                        break;
                    }
                    madeProgress = true;
                }

                var openedAny = await OpenLockedDoorsAsync(cancellationToken);
                if (openedAny)
                {
                    madeProgress = true;
                    noProgressRounds = 0;
                    _failedCells.Clear();
                }

                if (!madeProgress && HasUnexploredFrontier())
                {
                    var navigated = await NavigateToFrontierAsync(cancellationToken);
                    if (navigated)
                    {
                        madeProgress = true;
                        noProgressRounds = 0;
                        continue;
                    }
                }

                if (Map.TryGet(out var snapshot))
                {
                    var remainingDoors = snapshot.Count(kvp => kvp.Value == CellType.Door);
                    var hasUnexplored = HasUnexploredFrontier();
                    var hasPotentialUnknown = HasPotentiallyReachableUnknownCells();

                    var exitFound = snapshot.Any(kvp => kvp.Value == CellType.Outside);
                    if (exitFound)
                    {
                        break;
                    }

                    if (remainingDoors > 0)
                    {
                        if (HasKey)
                        {
                            foreach (var door in snapshot.Where(kvp => kvp.Value == CellType.Door)
                                .Select(kvp => kvp.Key)
                                .OrderBy(d => Math.Abs(d.X - _crawler.X) + Math.Abs(d.Y - _crawler.Y)))
                            {
                                if (cancellationToken.IsCancellationRequested) break;

                                var opened = await TryReachAndOpenDoorAsync(door.X, door.Y, cancellationToken);
                                if (opened)
                                {
                                    noProgressRounds = 0;
                                    madeProgress = true;
                                    _failedCells.Clear();

                                    await ExploreFullyAsync(cancellationToken);
                                    break;
                                }
                            }

                            if (!madeProgress)
                            {
                                noProgressRounds++;
                                await Task.Delay(50, cancellationToken).ContinueWith(_ => { });
                            }
                        }
                        else
                        {
                            if (hasUnexplored)
                            {
                                var navigated = await NavigateToFrontierAsync(cancellationToken);
                                if (navigated)
                                {
                                    madeProgress = true;
                                    noProgressRounds = 0;
                                    continue;
                                }
                            }

                            if (hasPotentialUnknown && noProgressRounds < maxNoProgressRounds / 2)
                            {
                                await Task.Delay(100, cancellationToken).ContinueWith(_ => { });
                                noProgressRounds++;
                            }
                            else
                            {
                                await Task.Delay(100, cancellationToken).ContinueWith(_ => { });
                                noProgressRounds++;
                            }
                        }
                    }
                    else if (hasUnexplored)
                    {
                        var navigated = await NavigateToFrontierAsync(cancellationToken);
                        if (navigated)
                        {
                            madeProgress = true;
                            noProgressRounds = 0;
                        }
                        else
                        {
                            noProgressRounds++;
                        }
                    }
                    else
                    {
                        if (!madeProgress)
                        {
                            break;
                        }
                    }
                }
                else if (!madeProgress)
                {
                    noProgressRounds++;
                }

                if (madeProgress)
                {
                    noProgressRounds = 0;
                }
            }
        }

        private async Task ExploreFullyAsync(CancellationToken cancellationToken)
        {
            int steps = 0;
            int maxSteps = 10000;

            while (steps < maxSteps && !cancellationToken.IsCancellationRequested)
            {
                steps++;
                var cont = await ExploreStepAsync(cancellationToken);
                if (!cont)
                {
                    if (HasUnexploredFrontier())
                    {
                        var navigated = await NavigateToFrontierAsync(cancellationToken);
                        if (navigated)
                        {
                            steps = 0;
                            continue;
                        }
                    }
                    break;
                }

                if (HasKey && steps % 20 == 0)
                {
                    if (Map.TryGet(out var snap) && snap.Any(kvp => kvp.Value == CellType.Door))
                    {
                        var opened = await TryOpenDoorsWithKeysAsync(cancellationToken);
                        if (opened)
                        {
                            steps = 0;
                            _failedCells.Clear();
                        }
                    }
                }
            }
        }

        private async Task<bool> TryOpenDoorsWithKeysAsync(CancellationToken cancellationToken)
        {
            if (!HasKey) return false;
            if (cancellationToken.IsCancellationRequested) return false;

            if (!Map.TryGet(out var snapshot)) return false;

            var lockedDoors = snapshot
                .Where(kvp => kvp.Value == CellType.Door)
                .Select(kvp => kvp.Key)
                .OrderBy(d => Math.Abs(d.X - _crawler.X) + Math.Abs(d.Y - _crawler.Y))
                .ToList();

            if (lockedDoors.Count == 0) return false;

            foreach (var (doorX, doorY) in lockedDoors)
            {
                if (cancellationToken.IsCancellationRequested) return false;
                if (!HasKey) break;

                var opened = await TryReachAndOpenDoorAsync(doorX, doorY, cancellationToken);
                if (opened)
                {
                    _path.Clear();
                    return true;
                }
            }

            return false;
        }

        private async Task<bool> OpenLockedDoorsAsync(CancellationToken cancellationToken)
        {
            int maxAttempts = 200;
            int attempts = 0;
            bool anyOpened = false;

            while (attempts < maxAttempts)
            {
                if (cancellationToken.IsCancellationRequested) break;
                attempts++;

                if (!Map.TryGet(out var snapshot)) break;

                var lockedDoors = snapshot
                    .Where(kvp => kvp.Value == CellType.Door)
                    .Select(kvp => kvp.Key)
                    .OrderBy(d => Math.Abs(d.X - _crawler.X) + Math.Abs(d.Y - _crawler.Y))
                    .ToList();

                if (lockedDoors.Count == 0) break;

                bool openedThisRound = false;

                foreach (var (dx, dy) in lockedDoors)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    var canReach = await TryReachAndOpenDoorAsync(dx, dy, cancellationToken);
                    if (canReach)
                    {
                        openedThisRound = true;
                        anyOpened = true;
                        _path.Clear();
                        _failedCells.Clear();

                        int exploreMore = 0;
                        while (exploreMore < 50000)
                        {
                            if (cancellationToken.IsCancellationRequested) break;
                            exploreMore++;
                            var cont = await ExploreStepAsync(cancellationToken);
                            if (!cont)
                            {
                                if (HasUnexploredFrontier())
                                {
                                    var navigated = await NavigateToFrontierAsync(cancellationToken);
                                    if (navigated) continue;
                                }
                                break;
                            }
                        }
                    }
                }

                if (!openedThisRound) break;
            }

            return anyOpened;
        }

        private async Task<bool> TryReachAndOpenDoorAsync(int doorX, int doorY, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return false;

            var path = FindPathTo(doorX, doorY);
            if (path == null || path.Count == 0)
            {
                return await TryReachAndOpenDoorGreedyAsync(doorX, doorY, cancellationToken);
            }

            foreach (var (nextX, nextY) in path)
            {
                if (cancellationToken.IsCancellationRequested) return false;

                var moved = await MoveToAsync(nextX, nextY, cancellationToken);
                if (!moved)
                {
                    return await TryReachAndOpenDoorGreedyAsync(doorX, doorY, cancellationToken);
                }
            }

            return true;
        }

        private List<(int X, int Y)>? FindPathTo(int targetX, int targetY)
        {
            if (!Map.TryGet(out var snapshot))
            {
                return null;
            }

            var start = (_crawler.X, _crawler.Y);
            var target = (targetX, targetY);

            if (start == target)
            {
                return new List<(int X, int Y)>();
            }

            var queue = new Queue<(int X, int Y)>();
            var visited = new HashSet<(int X, int Y)>();
            var parent = new Dictionary<(int X, int Y), (int X, int Y)>();

            queue.Enqueue(start);
            visited.Add(start);

            var directions = new[] { (0, -1), (1, 0), (0, 1), (-1, 0) };

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                foreach (var (dx, dy) in directions)
                {
                    var next = (current.Item1 + dx, current.Item2 + dy);

                    if (visited.Contains(next)) continue;

                    var cellType = snapshot.TryGetValue(next, out var ct) ? ct : CellType.Unknown;

                    bool canTraverse = cellType == CellType.Visited ||
                                       cellType == CellType.Start ||
                                       cellType == CellType.Empty ||
                                       next == target;

                    if (!canTraverse && cellType != CellType.Door) continue;

                    visited.Add(next);
                    parent[next] = current;

                    if (next == target)
                    {
                        var path = new List<(int X, int Y)>();
                        var node = target;
                        while (node != start)
                        {
                            path.Add(node);
                            node = parent[node];
                        }
                        path.Reverse();
                        return path;
                    }

                    if (canTraverse)
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            return null;
        }

        private async Task<bool> TryReachAndOpenDoorGreedyAsync(int doorX, int doorY, CancellationToken cancellationToken)
        {
            for (int i = 0; i < 200; i++)
            {
                if (cancellationToken.IsCancellationRequested) return false;

                int curX = _crawler.X;
                int curY = _crawler.Y;

                if (Math.Abs(doorX - curX) + Math.Abs(doorY - curY) <= 1)
                {
                    if (curX == doorX && curY == doorY)
                    {
                        return true;
                    }
                    var canMove = await MoveToAsync(doorX, doorY, cancellationToken);
                    return canMove;
                }

                int stepX = curX;
                int stepY = curY;

                if (curX < doorX) stepX++;
                else if (curX > doorX) stepX--;
                else if (curY < doorY) stepY++;
                else if (curY > doorY) stepY--;

                var movedStep = await MoveToAsync(stepX, stepY, cancellationToken);
                if (!movedStep) break;
            }

            return false;
        }

        public Task RunAsync() => RunAsync(CancellationToken.None);

        public async Task<bool> ExploreStepAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested) return false;

            Map.Mark(_crawler.X, _crawler.Y, CellType.Visited);

            var dirs = new[] { Direction.North, Direction.East, Direction.South, Direction.West };
            foreach (var d in dirs)
            {
                if (cancellationToken.IsCancellationRequested) return false;

                var fx = _crawler.X + d.DeltaX;
                var fy = _crawler.Y + d.DeltaY;

                if (_failedCells.Contains((fx, fy))) continue;

                var gt = Map.Get(fx, fy);

                if (gt == CellType.Visited || gt == CellType.Wall || gt == CellType.Outside)
                    continue;

                if (gt == CellType.Door)
                    continue;

                var claimed = false;
                if (_ownerId > 0)
                {
                    var currentOwner = Map.GetClaimOwner(fx, fy);
                    if (currentOwner != 0 && currentOwner != _ownerId) continue;

                    claimed = Map.TryClaim(fx, fy, _ownerId);
                    if (!claimed) continue;
                }

                var prev = (_crawler.X, _crawler.Y);
                var moved = await MoveToAsync(fx, fy, cancellationToken);
                if (moved)
                {
                    _path.Push(prev);
                    return true;
                }
                else
                {
                    _failedCells.Add((fx, fy));
                    if (claimed)
                    {
                        Map.TryRelease(fx, fy, _ownerId);
                    }
                }
            }

            while (_path.Count > 0)
            {
                var (tx, ty) = _path.Pop();
                var movedBack = await MoveToAsync(tx, ty, cancellationToken);
                if (movedBack)
                {
                    var unexplored = GetUnexploredNeighbors();
                    if (unexplored.Count > 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private async Task CollectItemsAsync(Inventory? collected)
        {
            if (collected == null || !collected.HasItems) return;

            while (collected.HasItems)
            {
                var moved = await _inventory.MoveItemFrom(collected, 0);
                if (!moved) break;
            }
        }

        public async Task<bool> StepForwardAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested) return false;
            if (!_crawler.CanMoveForward) return false;

            var collected = await _crawler.TryWalkAsync(_inventory);
            if (collected == null)
            {
                return false;
            }

            await CollectItemsAsync(collected);

            Map.Mark(_crawler.X, _crawler.Y, CellType.Visited);
            PositionChanged?.Invoke(this, new CrawlingEventArgs(_crawler));
            HasStartedMoving = true;
            return true;
        }

        public async Task<bool> MoveToAsync(int targetX, int targetY, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested) return false;

            var curX = _crawler.X;
            var curY = _crawler.Y;

            var dx = targetX - curX;
            var dy = targetY - curY;

            if (Math.Abs(dx) + Math.Abs(dy) != 1)
                throw new ArgumentException("Target cell must be adjacent");

            Direction desired = dx == 1 ? Direction.East : dx == -1 ? Direction.West : dy == 1 ? Direction.South : Direction.North;

            await TurnToAsync(desired);

            var facing = await _crawler.GetFacingTileTypeAsync();
            var facingType = facing switch
            {
                TileType.Door => CellType.Door,
                TileType.Wall => CellType.Wall,
                TileType.Outside => CellType.Outside,
                _ => CellType.Empty
            };
            Map.Mark(targetX, targetY, facingType);

            if (facingType == CellType.Outside)
            {
                Map.NotifyExitFound(curX, curY, targetX, targetY, _ownerId);
                return false;
            }

            if (facingType == CellType.Door)
            {
                int maxAttempts = 50;
                int attempts = 0;

                while (attempts < maxAttempts)
                {
                    if (cancellationToken.IsCancellationRequested) return false;
                    attempts++;

                    var walked = await TryForceStepForwardAsync(cancellationToken);
                    if (walked)
                    {
                        Map.Mark(targetX, targetY, CellType.Visited);
                        return true;
                    }

                    var unlocked = await _crawler.TryUnlockAsync(_inventory);
                    if (unlocked)
                    {
                        var afterUnlock = await TryForceStepForwardAsync(cancellationToken);
                        if (afterUnlock)
                        {
                            Map.Mark(targetX, targetY, CellType.Visited);
                            return true;
                        }
                    }

                    await Task.Delay(100, cancellationToken).ContinueWith(_ => { });
                }

                return false;
            }

            var moved = await StepForwardAsync(cancellationToken);
            return moved;
        }

        private Task TurnToAsync(Direction desired)
        {
            int safety = 0;
            while (!_crawler.Direction.Equals(desired) && safety < 4)
            {
                _crawler.TurnRight();
                DirectionChanged?.Invoke(this, new CrawlingEventArgs(_crawler));
                safety++;
            }
            return Task.CompletedTask;
        }

        public async Task<bool> TryForceStepForwardAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested) return false;

            var prevX = _crawler.X;
            var prevY = _crawler.Y;

            var collected = await _crawler.TryWalkAsync(_inventory);
            if (collected != null)
            {
                var didMove = (_crawler.X != prevX || _crawler.Y != prevY);

                await CollectItemsAsync(collected);

                if (didMove)
                {
                    Map.Mark(_crawler.X, _crawler.Y, CellType.Visited);
                    PositionChanged?.Invoke(this, new CrawlingEventArgs(_crawler));
                    HasStartedMoving = true;
                    return true;
                }
            }

            return false;
        }
    }
}
