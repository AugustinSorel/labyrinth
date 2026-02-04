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
    public class RandExplorer
    {
        private readonly ICrawler _crawler;
        private Inventory _inventory = new MyInventory();

        public event EventHandler<CrawlingEventArgs>? PositionChanged;
        public event EventHandler<CrawlingEventArgs>? DirectionChanged;

        public ExplorationMap Map { get; }

        private readonly Stack<(int X, int Y)> _path = new();
        
        // Track cells that failed to move to avoid infinite retries
        private readonly HashSet<(int X, int Y)> _failedCells = new();

        private readonly int _ownerId;

        /// <summary>
        /// Returns true if this explorer has at least one key in inventory.
        /// </summary>
        public bool HasKey => _inventory.HasItems;

        /// <summary>
        /// Returns the number of keys in the inventory.
        /// </summary>
        public int KeyCount => _inventory.Count;

        /// <summary>
        /// Returns true if this explorer has started moving (passed at least one cell).
        /// /// </summary>
        public bool HasStartedMoving { get; private set; }

        /// <summary>
        /// Returns true if this explorer is blocked (cannot move anywhere).
        /// </summary>
        public bool IsBlocked { get; private set; }

        public RandExplorer(ICrawler crawler) : this(crawler, null, 0) { }

        public RandExplorer(ICrawler crawler, ExplorationMap? map) : this(crawler, map, 0) { }

        public RandExplorer(ICrawler crawler, ExplorationMap? map, int ownerId)
        {
            _crawler = crawler ?? throw new ArgumentNullException(nameof(crawler));
            Map = map ?? new ExplorationMap();
            _ownerId = ownerId;
        }

        /// <summary>
        /// Check if there are unexplored frontier cells adjacent to visited cells.
        /// This helps detect if exploration should continue even when DFS returns false.
        /// </summary>
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
                    
                    // Skip cells we already failed to reach
                    if (_failedCells.Contains((nx, ny))) continue;
                    
                    var cellType = snapshot.TryGetValue((nx, ny), out var ct) ? ct : CellType.Unknown;
                    
                    // If adjacent cell is Unknown or Empty (not yet visited), there's frontier to explore
                    if (cellType == CellType.Unknown || cellType == CellType.Empty)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Get all unexplored neighbors of the current position.
        /// </summary>
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

        /// <summary>
        /// Find a random visited cell that has unexplored neighbors and navigate to it.
        /// After navigating, attempt to explore the unexplored neighbors.
        /// </summary>
        private async Task<bool> NavigateToFrontierAsync(CancellationToken cancellationToken)
        {
            if (!Map.TryGet(out var snapshot)) return false;

            var directions = new[] { (0, -1), (1, 0), (0, 1), (-1, 0) };

            // Find all visited cells with unexplored neighbors (excluding failed cells)
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

            // Try to navigate to the closest frontier cell
            foreach (var (targetX, targetY) in frontierCells)
            {
                if (cancellationToken.IsCancellationRequested) return false;

                if (_crawler.X == targetX && _crawler.Y == targetY)
                {
                    // Already at this cell, try to explore unexplored neighbors directly
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
                        // Now try to explore unexplored neighbors from this position
                        await TryExploreUnexploredNeighborsAsync(cancellationToken);
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Try to explore unexplored neighbors from current position.
        /// </summary>
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
                    // Continue DFS from new position
                    break;
                }
                else
                {
                    // Mark as failed to avoid retrying
                    _failedCells.Add((nx, ny));
                }
            }
            
            return anyExplored;
        }

        /// <summary>
        /// Run DFS exploration until all reachable cells are visited or safety iterations exhausted.
        /// </summary>
        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            int maxIterations = 500000;
            int iterations = 0;
            int noProgressRounds = 0;
            const int maxNoProgressRounds = 100;

            Map.Mark(_crawler.X, _crawler.Y, CellType.Start);
            _failedCells.Clear(); // Reset failed cells at start

            // Main loop: alternate between exploration and door opening until no more progress
            while (noProgressRounds < maxNoProgressRounds && iterations < maxIterations)
            {
                if (cancellationToken.IsCancellationRequested) break;
                
                bool madeProgress = false;

                // Phase 1: If we have keys, prioritize finding and opening doors
                if (HasKey)
                {
                    var openedWithKey = await TryOpenDoorsWithKeysAsync(cancellationToken);
                    if (openedWithKey) 
                    {
                        madeProgress = true;
                        noProgressRounds = 0;
                        _failedCells.Clear(); // Reset failed cells after opening a door
                        
                        // After opening a door, do extended exploration to find the exit
                        await ExploreFullyAsync(cancellationToken);
                    }
                }

                // Phase 2: DFS exploration - explore as much as possible
                int explorationSteps = 0;
                while (explorationSteps < 50000)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    iterations++;
                    explorationSteps++;

                    // If we collected a key during exploration, try to use it immediately
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
                        // DFS returned false, but check if there's unexplored frontier
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

                // Phase 3: Try to open any locked doors we know about
                var openedAny = await OpenLockedDoorsAsync(cancellationToken);
                if (openedAny) 
                {
                    madeProgress = true;
                    noProgressRounds = 0;
                    _failedCells.Clear();
                }

                // Phase 4: Check for unexplored frontier even after DFS completes
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

                // Check current state on the SHARED map
                if (Map.TryGet(out var snapshot))
                {
                    var remainingDoors = snapshot.Count(kvp => kvp.Value == CellType.Door);
                    var hasUnexplored = HasUnexploredFrontier();
                    
                    if (remainingDoors > 0)
                    {
                        if (HasKey)
                        {
                            Console.WriteLine($"[Explorer {_ownerId}] Has {KeyCount} key(s), actively seeking {remainingDoors} door(s)...");
                            
                            foreach (var door in snapshot.Where(kvp => kvp.Value == CellType.Door)
                                .Select(kvp => kvp.Key)
                                .OrderBy(d => Math.Abs(d.X - _crawler.X) + Math.Abs(d.Y - _crawler.Y)))
                            {
                                if (cancellationToken.IsCancellationRequested) break;
                                
                                var opened = await TryReachAndOpenDoorAsync(door.X, door.Y, cancellationToken);
                                if (opened)
                                {
                                    Console.WriteLine($"[Explorer {_ownerId}] Opened door at ({door.X},{door.Y})!");
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
                                await Task.Delay(100, cancellationToken).ContinueWith(_ => { });
                            }
                        }
                        else
                        {
                            if (noProgressRounds % 20 == 0)
                            {
                                Console.WriteLine($"[Explorer {_ownerId}] No keys, waiting for others... {remainingDoors} doors on map");
                            }
                            await Task.Delay(200, cancellationToken).ContinueWith(_ => { });
                            noProgressRounds++;
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
                            Console.WriteLine($"[Explorer {_ownerId}] No more doors or unexplored areas. Finishing.");
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

        /// <summary>
        /// Explore fully until no more progress can be made.
        /// Used after opening a door to discover new areas.
        /// </summary>
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

        /// <summary>
        /// Actively try to find and open doors when we have keys in inventory.
        /// This prioritizes using keys immediately rather than waiting.
        /// Returns true if at least one door was successfully opened.
        /// </summary>
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

            Console.WriteLine($"[Explorer {_ownerId}] Has {KeyCount} key(s), targeting {lockedDoors.Count} door(s)");

            foreach (var (doorX, doorY) in lockedDoors)
            {
                if (cancellationToken.IsCancellationRequested) return false;
                if (!HasKey) break;

                var opened = await TryReachAndOpenDoorAsync(doorX, doorY, cancellationToken);
                if (opened)
                {
                    Console.WriteLine($"[Explorer {_ownerId}] Successfully opened door at ({doorX},{doorY}), {KeyCount} key(s) remaining");
                    _path.Clear();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Find all known locked doors and attempt to open them.
        /// </summary>
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

        /// <summary>
        /// Try to navigate to a specific door location and open it.
        /// </summary>
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

        /// <summary>
        /// Find a path from current position to the target using BFS.
        /// </summary>
        private List<(int X, int Y)>? FindPathTo(int targetX, int targetY)
        {
            if (!Map.TryGet(out var snapshot)) return null;

            var start = (_crawler.X, _crawler.Y);
            var target = (targetX, targetY);

            if (start == target) return new List<(int X, int Y)>();

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

        /// <summary>
        /// Greedy approach to reach a door (fallback).
        /// </summary>
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

                // Skip failed cells
                if (_failedCells.Contains((fx, fy))) continue;

                var gt = Map.Get(fx, fy);
                
                // Skip cells that are already visited, walls, or outside
                if (gt == CellType.Visited || gt == CellType.Wall || gt == CellType.Outside)
                    continue;
                
                // Skip doors for now (they'll be handled separately with keys)
                // But don't skip Unknown or Empty cells - those should be explored
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
                    // Mark as failed to avoid retrying indefinitely
                    _failedCells.Add((fx, fy));
                    if (claimed)
                    {
                        Map.TryRelease(fx, fy, _ownerId);
                    }
                }
            }

            // Backtracking: try to go back and explore other directions
            while (_path.Count > 0)
            {
                var (tx, ty) = _path.Pop();
                var movedBack = await MoveToAsync(tx, ty, cancellationToken);
                if (movedBack)
                {
                    // After going back, check if there are unexplored neighbors from this position
                    var unexplored = GetUnexploredNeighbors();
                    if (unexplored.Count > 0)
                    {
                        // There are unexplored neighbors, return true to continue exploration
                        return true;
                    }
                    // No unexplored neighbors here, continue backtracking
                }
                // If move failed, continue popping the stack
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

            Console.WriteLine($"[Explorer {_ownerId}] Inventory now has {_inventory.Count} items. HasKey={HasKey}");
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
