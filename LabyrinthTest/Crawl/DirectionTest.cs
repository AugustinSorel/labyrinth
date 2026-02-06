using Labyrinth.Core;

namespace LabyrinthTest.Crawl;

[TestFixture(Description = "Direction unit test class")]
public class DirectionTest
{
    private static void AssertThatXY(Direction dir, int x, int y)
    {
        using var all = Assert.EnterMultipleScope();

        Assert.That(dir.DeltaX, Is.EqualTo(x));
        Assert.That(dir.DeltaY, Is.EqualTo(y));
    }

    [Test]
    public void TestInitNorth() => AssertThatXY(Direction.North, 0, -1);

    [Test]
    public void TestInitSouth() => AssertThatXY(Direction.South, 0, 1);

    [Test]
    public void TestInitEast() => AssertThatXY(Direction.East, 1, 0);

    [Test]
    public void TestInitWest() => AssertThatXY(Direction.West, -1, 0);

    [Test]
    public void TestTurnRightFromNorthGoesEast()
    {
        var test = Direction.North;

        test.TurnRight();
        Assert.That(test, Is.EqualTo(Direction.East));
    }

    [Test]
    public void TestTurnRightThenLeftStillTheSame()
    {
        var test = Direction.East;

        test.TurnRight();
        test.TurnLeft();
        Assert.That(test, Is.EqualTo(Direction.East));
    }

    [Test]
    public void TestTurnLeftFromNorthGoesWest()
    {
        var test = Direction.North;

        test.TurnLeft();
        Assert.That(test, Is.EqualTo(Direction.West));
    }

    [Test]
    public void TestTurnLeftTwiceFromWestGoesEast()
    {
        var test = Direction.West;

        test.TurnLeft();
        test.TurnLeft();
        Assert.That(test, Is.EqualTo(Direction.East));
    }

    [Test]
    public void TestTurnRightForTimesStillTheSame()
    {
        var test = Direction.East;

        test.TurnRight();
        test.TurnRight();
        test.TurnRight();
        test.TurnRight();
        Assert.That(test, Is.EqualTo(Direction.East));
    }

}
