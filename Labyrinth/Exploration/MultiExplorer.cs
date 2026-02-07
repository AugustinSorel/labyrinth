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

        public async Task<bool> StartAsync(int crawlerCount, FrontierSelectionPolicy policy = FrontierSelectionPolicy.RoundRobin, int timeoutMs = 5000)
        {
            if (crawlerCount <= 0) throw new ArgumentOutOfRangeException(nameof(crawlerCount));

            var explorers = new List<BfsExplorer>();
            var tasks = new List<Task>();

            for (int i = 0; i < crawlerCount; i++)
            {
                var crawler = _maze.NewCrawler();
                var explorer = new BfsExplorer(crawler, Map, ownerId: i + 1);
                explorers.Add(explorer);

                tasks.Add(Task.Run(async () =>
                {
                    while (true)
                    {
                        var selected = _frontierManager.TrySelectFrontierForOwner(i + 1, crawler, policy);
                        if (selected == null)
                        {
                            await explorer.RunAsync();
                            break;
                        }

                        var (tx, ty) = selected.Value;

                        while (!(crawler.X == tx && crawler.Y == ty))
                        {
                            var dx = tx - crawler.X;
                            var dy = ty - crawler.Y;
                            int stepX, stepY;
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

                            if (Math.Abs(stepX - crawler.X) + Math.Abs(stepY - crawler.Y) != 1)
                            {
                                break;
                            }

                            var moved = await explorer.MoveToAsync(stepX, stepY);
                            if (!moved)
                            {
                                Map.TryRelease(tx, ty, i + 1);
                                break;
                            }
                        }
                    }
                }));
            }

            var all = Task.WhenAll(tasks);
            var delay = Task.Delay(timeoutMs);

            var completed = await Task.WhenAny(all, delay);
            if (completed == all)
            {
                await all;
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
