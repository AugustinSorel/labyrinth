namespace Labyrinth.Build
{
	/// <summary>
	/// Provides data for an event that is triggered when a start position is found.
	/// </summary>
	/// <param name="x">X coordinate of the starting position</param>
	/// <param name="y">Y coordinate of the starting position</param>
	public class StartEventArgs(int x, int y) : EventArgs
	{
		public int X { get; } = x;
		public int Y { get; } = y;
    }
}
