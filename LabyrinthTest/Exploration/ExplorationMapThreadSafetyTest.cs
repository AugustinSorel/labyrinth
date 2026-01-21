using Labyrinth.Exploration;
using NUnit.Framework;
using System.Threading.Tasks;
using System.Linq;

namespace LabyrinthTest.Exploration;

public class ExplorationMapThreadSafetyTest
{
    [Test]
    public void ConcurrentMarks_MergeConsistent()
    {
        var map = new ExplorationMap();
        var coords = (0,0);

        Parallel.For(0, 1000, i =>
        {
            // alternate marking different types
            var t = (i % 4) switch
            {
                0 => CellType.Empty,
                1 => CellType.Door,
                2 => CellType.Wall,
                _ => CellType.Visited
            };
            map.Mark(coords.Item1, coords.Item2, t);
        });

        var final = map.Get(coords.Item1, coords.Item2);
        // Final value should be one of the known types (not throw)
        Assert.That(final, Is.Not.EqualTo(CellType.Unknown));
    }

    [Test]
    public void Snapshot_IsConsistent()
    {
        var map = new ExplorationMap();
        map.Mark(0,0, CellType.Start);
        map.Mark(1,0, CellType.Empty);

        var ok = map.TryGet(out var snap);
        Assert.That(ok, Is.True);
        Assert.That(snap[(0,0)], Is.EqualTo(CellType.Start));
        Assert.That(snap[(1,0)], Is.EqualTo(CellType.Empty));
    }
}
