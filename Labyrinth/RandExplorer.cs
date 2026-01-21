using System;
using System.Collections.Generic;
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

        private readonly int _ownerId;

        public RandExplorer(ICrawler crawler) : this(crawler, null, 0) { }

        public RandExplorer(ICrawler crawler, ExplorationMap? map) : this(crawler, map, 0) { }

        public RandExplorer(ICrawler crawler, ExplorationMap? map, int ownerId)
        {
            _crawler = crawler ?? throw new ArgumentNullException(nameof(crawler));
            Map = map ?? new ExplorationMap();
            _ownerId = ownerId;
        }

        /// <summary>
        /// Run DFS exploration until all reachable cells are visited or safety iterations exhausted.
        /// </summary>
        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            int maxIterations = 100000; // safety
            int iterations = 0;

            Map.Mark(_crawler.X, _crawler.Y, CellType.Start);

            // Phase 1: Initial DFS exploration
            while (iterations < maxIterations)
            {
                if (cancellationToken.IsCancellationRequested) break;
                iterations++;

                var cont = await ExploreStepAsync(cancellationToken);
                if (!cont) break; // finished initial exploration
            }

            // Phase 2: Find and open any locked doors we know about
            await OpenLockedDoorsAsync(cancellationToken);
        }

        /// <summary>
        /// Find all known locked doors and attempt to open them by moving to them and trying to unlock.
        /// This phase allows exploration to continue past locked doors once keys are acquired.
        /// </summary>
        private async Task OpenLockedDoorsAsync(CancellationToken cancellationToken)
        {
            int maxAttempts = 1000;
            int attempts = 0;

            while (attempts < maxAttempts)
            {
                if (cancellationToken.IsCancellationRequested) break;
                attempts++;

                // Find all cells marked as Door in our map
                if (!Map.TryGet(out var snapshot)) break;

                var lockedDoors = snapshot
                    .Where(kvp => kvp.Value == CellType.Door)
                    .Select(kvp => kvp.Key)
                    .ToList();

                if (lockedDoors.Count == 0) break; // no more doors to try

                bool anyOpened = false;

                foreach (var (dx, dy) in lockedDoors)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    // Try to reach this door and open it
                    var canReach = await TryReachAndOpenDoorAsync(dx, dy, cancellationToken);
                    if (canReach)
                    {
                        anyOpened = true;
                        // Continue exploring from here
                        _path.Clear(); // reset path for new exploration phase
                        Map.Mark(_crawler.X, _crawler.Y, CellType.Visited);

                        int exploreMore = 0;
                        while (exploreMore < 10000)
                        {
                            if (cancellationToken.IsCancellationRequested) break;
                            exploreMore++;
                            var cont = await ExploreStepAsync(cancellationToken);
                            if (!cont) break;
                        }
                    }
                }

                if (!anyOpened) break; // couldn't open any doors, we're done
            }
        }

        /// <summary>
        /// Try to navigate to a specific door location and open it with current inventory.
        /// Returns true if the door was successfully opened.
        /// </summary>
        private async Task<bool> TryReachAndOpenDoorAsync(int doorX, int doorY, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return false;

            // Simple pathfinding: try to get adjacent to the door
            int curX = _crawler.X;
            int curY = _crawler.Y;

            if (Math.Abs(doorX - curX) + Math.Abs(doorY - curY) <= 1)
            {
                // Already adjacent; try to open it
                var canMove = await MoveToAsync(doorX, doorY, cancellationToken);
                return canMove;
            }

            // Try to move closer using simple greedy pathfinding (Manhattan distance)
            for (int i = 0; i < 100; i++)
            {
                if (cancellationToken.IsCancellationRequested) return false;

                curX = _crawler.X;
                curY = _crawler.Y;

                // If now adjacent to door, try to open it
                if (Math.Abs(doorX - curX) + Math.Abs(doorY - curY) <= 1)
                {
                    var canMove = await MoveToAsync(doorX, doorY, cancellationToken);
                    return canMove;
                }

                // Step towards the door (greedy)
                int stepX = curX;
                int stepY = curY;

                if (curX < doorX) stepX++;
                else if (curX > doorX) stepX--;
                else if (curY < doorY) stepY++;
                else if (curY > doorY) stepY--;

                var moved = await MoveToAsync(stepX, stepY, cancellationToken);
                if (!moved) break; // stuck, can't reach this door
            }

            return false;
        }

        /// <summary>
        /// Run DFS exploration until all reachable cells are visited or safety iterations exhausted.
        /// This overload allows specifying a custom map.
        /// </summary>
        // (Removed overload that attempted to replace the read-only Map property.)

        /// <summary>
        /// Run DFS exploration until all reachable cells are visited or safety iterations exhausted.
        /// This overload allows specifying a custom map.
        /// </summary>
        public Task RunAsync() => RunAsync(CancellationToken.None);

        /// <summary>
        /// Performs one exploration step: move to an unvisited adjacent cell if any, otherwise backtrack.
        /// Returns true if exploration should continue, false if finished.
        /// </summary>
        public async Task<bool> ExploreStepAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested) return false;

            // Mark current as visited
            Map.Mark(_crawler.X, _crawler.Y, CellType.Visited);

            // Check 4 directions for unvisited reachable neighbors (don't pre-turn)
            var dirs = new[] { Direction.North, Direction.East, Direction.South, Direction.West };
            foreach (var d in dirs)
            {
                if (cancellationToken.IsCancellationRequested) return false;

                var fx = _crawler.X + d.DeltaX;
                var fy = _crawler.Y + d.DeltaY;

                var gt = Map.Get(fx, fy);
                // Consider any traversable neighbor that is not already marked Visited.
                if (gt == CellType.Visited || gt == CellType.Wall || gt == CellType.Outside)
                    continue;

                // Try to claim this frontier if ownerId provided
                var claimed = false;
                if (_ownerId > 0)
                {
                    // If already claimed by someone else, skip
                    var currentOwner = Map.GetClaimOwner(fx, fy);
                    if (currentOwner != 0 && currentOwner != _ownerId) continue;

                    claimed = Map.TryClaim(fx, fy, _ownerId);
                    if (!claimed) continue; // couldn't claim
                }

                // Attempt to move to neighbor; MoveToAsync will turn and observe.
                var prev = (_crawler.X, _crawler.Y);
                var moved = await MoveToAsync(fx, fy, cancellationToken);
                if (moved)
                {
                    _path.Push(prev);
                    return true;
                }
                else
                {
                    // release claim if we had one
                    if (claimed)
                    {
                        Map.TryRelease(fx, fy, _ownerId);
                    }
                }
                // on failure, continue scanning other directions
            }

            // No unvisited neighbor found: backtrack if possible
            if (_path.Count > 0)
            {
                var (tx, ty) = _path.Pop();
                // Move back to previous cell
                var movedBack = await MoveToAsync(tx, ty, cancellationToken);
                // If unable to move back, exploration cannot continue
                return movedBack;
            }

            // Nothing to do: exploration finished
            return false;
        }

        /// <summary>
        /// Attempts to move the crawler forward one cell in its current facing.
        /// Raises PositionChanged on success and updates the internal inventory.
        /// </summary>
        public async Task<bool> StepForwardAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested) return false;
            if (!_crawler.CanMoveForward) return false;

            var collected = await _crawler.TryWalkAsync(_inventory);
            if (collected != null)
            {
                // Merge collected items into our inventory
                while (collected.HasItems)
                {
                    var moved = await _inventory.MoveItemFrom(collected, 0);
                    if (!moved) break; // safety
                }
            }

            Map.Mark(_crawler.X, _crawler.Y, CellType.Visited);
            PositionChanged?.Invoke(this, new CrawlingEventArgs(_crawler));
            return true;
        }

        /// <summary>
        /// Turns the crawler to face the adjacent target cell (must be adjacent) then attempts to move into it.
        /// Raises DirectionChanged for each rotation and PositionChanged on successful move.
        /// Returns true if the move succeeded.
        /// </summary>
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

            // Turn to desired direction using shortest sequence of turns
            await TurnToAsync(desired);

            // Observe before moving
            var facing = await _crawler.GetFacingTileTypeAsync();
            var facingType = facing switch
            {
                TileType.Door => CellType.Door,
                TileType.Wall => CellType.Wall,
                TileType.Outside => CellType.Outside,
                _ => CellType.Empty
            };
            Map.Mark(targetX, targetY, facingType);

            // If it's a door, try to unlock with our inventory first
            if (facingType == CellType.Door)
            {
                var unlocked = await _crawler.TryUnlockAsync(_inventory);
                if (!unlocked)
                {
                    // can't open the door now
                    return false;
                }
                // otherwise door unlocked; proceed to step forward
            }

            // If facing outside, consider it the exit and notify
            if (facingType == CellType.Outside)
            {
                Map.NotifyExitFound(curX, curY, targetX, targetY, _ownerId);
                return false; // cannot move into outside
            }

            // Try to step forward
            var moved = await StepForwardAsync(cancellationToken);
            return moved;
        }

        private Task TurnToAsync(Direction desired)
        {
            // Determine minimal turns (right turns) until facing desired
            // We do at most 3 turns
            int safety = 0;
            while (!_crawler.Direction.Equals(desired) && safety < 4)
            {
                _crawler.TurnRight();
                DirectionChanged?.Invoke(this, new CrawlingEventArgs(_crawler));
                safety++;
            }
            return Task.CompletedTask;
        }
    }
}
