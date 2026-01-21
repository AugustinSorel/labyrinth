using Labyrinth;
using Labyrinth.Exploration;
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;

namespace LabyrinthTest.Integration
{
    public class KeyDoorIntegrationTest
    {
        [Test]
        public async Task Explorer_PicksKey_OpensDoor_And_FindsExit()
        {
            // Map: key next to start, door to outside
            var map = """
+--+
|xk/
+--+
""";
            var maze = new Labyrinth.Maze(map);
            var explorer = new RandExplorer(maze.NewCrawler());

            var tcs = new TaskCompletionSource<bool>();
            explorer.Map.ExitFound += (s, e) => tcs.TrySetResult(true);

            using var cts = new CancellationTokenSource(2000);
            var run = Task.Run(async () => await explorer.RunAsync(cts.Token));

            var completed = await Task.WhenAny(tcs.Task, run, Task.Delay(1500));

            Assert.That(tcs.Task.IsCompleted, Is.True, "Explorer should have found the exit after getting key and opening door");
        }

        [Test]
        public async Task Explorer_CannotOpenLockedDoor_WhenNoKeyAccessible()
        {
            // Map: door to outside but no key present in accessible area
            var map = """
+--+
|x/|
+--+
""";
            var maze = new Labyrinth.Maze(map);
            var explorer = new RandExplorer(maze.NewCrawler());

            var found = false;
            explorer.Map.ExitFound += (s, e) => found = true;

            using var cts = new CancellationTokenSource(1000);
            await explorer.RunAsync(cts.Token);

            Assert.That(found, Is.False, "Explorer should not find exit because door is locked and no key available");
        }
    }
}
