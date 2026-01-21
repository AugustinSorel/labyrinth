namespace Labyrinth.Tiles
{
    /// <summary>
    /// A wall tile in the labyrinth.
    /// </summary>
    public class Wall : Tile
    {
        private Wall() { }

        /// <summary>
        /// The singleton instance of the Wall class (memory optimization).
        /// </summary>
        public static Wall Singleton { get; } = new();

        public override bool IsTraversable => false;
    }
}
