namespace Labyrinth.Crawl
{
    /// <summary>
    /// Direction in 2D space represented as cartesian deltas. 
    /// Only instanciated using properties 
    /// <see cref="North"/>, 
    /// <see cref="East"/>, 
    /// <see cref="South"/> and 
    /// <see cref="West"/>.
    /// </summary>
    public class Direction
    {
        public static Direction North => new(0, -1);
        public static Direction East => new(1, 0);
        public static Direction South => new(0, 1);
        public static Direction West => new(-1, 0);

        private Direction(int deltaX, int deltaY)
        {
            DeltaX = deltaX;
            DeltaY = deltaY;
        }

        /// <summary>
        /// Gets the horizontal shift for the direction.
        /// </summary>
        public int DeltaX { get; private set; }

        /// <summary>
        /// Gets the vertical shift for the direction.
        /// </summary>
        public int DeltaY { get; private set; }

        /// <summary>
        /// Turns the direction 90 degrees to the right (clockwise).
        /// </summary>
        public void TurnRight() => (DeltaX, DeltaY) = (-DeltaY, DeltaX);

        /// <summary>
        /// Turns the direction 90 degrees to the left (counter-clockwise).
        /// </summary>
        public void TurnLeft() => (DeltaX, DeltaY) = (DeltaY, -DeltaX);

        #region Convenience overrides
        public void Deconstruct(out int deltaX, out int deltaY)
        {
            deltaX = DeltaX;
            deltaY = DeltaY;
        }
        public override bool Equals(object? obj) =>
            obj is Direction dir &&
            dir.DeltaX == DeltaX &&
            dir.DeltaY == DeltaY;

        public override int GetHashCode() =>
            HashCode.Combine(DeltaX, DeltaY);

        public static bool operator ==(Direction left, Direction right) =>
            left.Equals(right);
        public static bool operator !=(Direction left, Direction right) =>
            !left.Equals(right);
        #endregion
    }
}
