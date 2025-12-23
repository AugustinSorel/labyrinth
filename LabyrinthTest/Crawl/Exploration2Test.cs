using Labyrinth.Exploration;
using NUnit.Framework;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LabyrinthTest.Crawl
{
    public class ConcurrentExplorerTest
    {
        [Test]
        public void Frontier_IsDetected_OnKnownCellWithUnknownNeighbor()
        {
            var map = new ExplorationMap();

            map.UpdateCell(0, 0, KnownCellType.Room);

            var frontiers = map.GetFrontiers().ToList();

            Assert.That(frontiers.Count, Is.EqualTo(1));
            Assert.That(frontiers[0].X, Is.EqualTo(0));
            Assert.That(frontiers[0].Y, Is.EqualTo(0));
        }

        [Test]
        public async Task Frontier_IsReservedByOnlyOneCrawler()
        {
            var map = new ExplorationMap();
            map.UpdateCell(1, 1, KnownCellType.Room);

            var cell = map.GetOrCreate(1, 1);

            bool? r1 = null;
            bool? r2 = null;

            await Task.WhenAll(
                Task.Run(() => r1 = map.TryReserveFrontier(cell, 1)),
                Task.Run(() => r2 = map.TryReserveFrontier(cell, 2))
            );

            Assert.That(r1 ^ r2, Is.True);
        }

        [Test]
        public void AStar_FindsShortestPath_InKnownGrid()
        {
            var map = new ExplorationMap();

            for (int x = 0; x < 3; x++)
                for (int y = 0; y < 3; y++)
                    map.UpdateCell(x, y, KnownCellType.Room);

            var start = map.GetOrCreate(0, 0);
            var goal = map.GetOrCreate(2, 2);

            var pathfinder = new AStarPathfinder(map);
            var path = pathfinder.FindPath(start, goal);

            Assert.That(path, Is.Not.Null);
            Assert.That(path.Count, Is.EqualTo(5));
        }


        [Test]
        public void MultipleFrontiers_AreDistributedAcrossCrawlers()
        {
            var map = new ExplorationMap();

            map.UpdateCell(0, 0, KnownCellType.Room);
            map.UpdateCell(10, 0, KnownCellType.Room);

            var frontiers = map.GetFrontiers().ToList();
            Assert.That(frontiers.Count, Is.EqualTo(2));

            var r1 = map.TryReserveFrontier(frontiers[0], 1);
            var r2 = map.TryReserveFrontier(frontiers[1], 2);

            Assert.That(r1, Is.True);
            Assert.That(r2, Is.True);
        }
    }
}   

