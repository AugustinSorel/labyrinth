using System.Collections.Generic;
using System.Threading;

namespace Labyrinth.Exploration
{
    using Labyrinth.Crawl;
    using Labyrinth.Tiles;
    using Labyrinth.Items;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Coordinate multiple crawlers (explorers) sharing a single ExplorationMap.
    /// Simple pool: each crawler runs its own RandExplorer instance but they update the same map.
    /// </summary>
    public class MultiExplorer
    {
        private readonly Maze _maze;

        public ExplorationMap Map { get; } = new();

        private FrontierManager _frontierManager;

        public MultiExplorer(Maze maze)
        {
            _maze = maze ?? throw new ArgumentNullException(nameof(maze));
            _frontierManager = new FrontierManager(Map);
        }

        /// <summary>
        /// Start N crawlers in parallel and wait until all finish or the timeout elapses.
        /// Each crawler will try to select frontiers using the provided policy.
        /// Returns true if all explorers completed before the timeout.
        /// </summary>
        public async Task<bool> StartAsync(int crawlerCount, FrontierSelectionPolicy policy = FrontierSelectionPolicy.RoundRobin, int timeoutMs = 5000)
        {
            if (crawlerCount <= 0) throw new ArgumentOutOfRangeException(nameof(crawlerCount));

            var explorers = new List<RandExplorer>();
            var tasks = new List<Task>();

            for (int i = 0; i < crawlerCount; i++)
            {
                var crawler = _maze.NewCrawler();
                // Provide an owner id so the explorer can attempt claims
                var explorer = new RandExplorer(crawler, Map, ownerId: i + 1);
                explorers.Add(explorer);

                // Start each explorer in its own task
                tasks.Add(Task.Run(async () =>
                {
                    // Each crawler tries to repeatedly claim frontiers and move toward them.
                    while (true)
                    {
                        var selected = _frontierManager.TrySelectFrontierForOwner(i + 1, crawler, policy);
                        if (selected == null)
                        {
                            // fallback to local DFS exploration; run until it finishes
                            await explorer.RunAsync();
                            break;
                        }

                        // Move step by step toward the selected frontier using simple greedy moves
                        var (tx, ty) = selected.Value;

                        // naive approach: while not at target, move to an adjacent cell closer to it
                        while (!(crawler.X == tx && crawler.Y == ty))
                        {
                            var dx = tx - crawler.X;
                            var dy = ty - crawler.Y;
                            int stepX, stepY;
                            // Choose a single-axis step towards the target to guarantee adjacency
                            if (dx != 0)
                            {
                                stepX = crawler.X + Math.Sign(dx);
                                stepY = crawler.Y;
                            }
                            else
                            {
                                stepX = crawler.X;
                                stepY = crawler.Y + Math.Sign(dy);
                            }

                            // ensure adjacency (safety)
                            if (Math.Abs(stepX - crawler.X) + Math.Abs(stepY - crawler.Y) != 1)
                            {
                                break; // cannot make a valid adjacent step
                            }

                            var moved = await explorer.MoveToAsync(stepX, stepY);
                            if (!moved)
                            {
                                // couldn't move toward selected frontier; release claim and break to reselect
                                Map.TryRelease(tx, ty, i + 1);
                                break;
                            }
                        }

                        // If reached, claim will be removed by Map.Mark when visited; continue loop to get new frontier
                    }
                }));
            }

            var all = Task.WhenAll(tasks);
            var delay = Task.Delay(timeoutMs);

            var completed = await Task.WhenAny(all, delay);
            if (completed == all)
            {
                // completed within timeout
                await all; // propagate exceptions if any
                return true;
            }
            else
            {
                return false; // timeout
            }
        }
    }
}
