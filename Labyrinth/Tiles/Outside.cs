namespace Labyrinth.Tiles
{
    /// <summary>
    /// Outside tile used for labyrinth bounds detection only, not for walkthrough.
    /// </summary>
    public class Outside : Tile
    {
        private Outside() { }

        /// <summary>
        /// The singleton instance of the Wall class (memory optimization).
        /// </summary>
        public static Outside Singleton { get; } = new();

        public override bool IsTraversable => false;
    }
}
