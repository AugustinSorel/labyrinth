namespace Labyrinth.Exploration
{
    public class ExitFoundEventArgs : EventArgs
    {
        public ExitFoundEventArgs(int crawlerX, int crawlerY, int exitX, int exitY, int ownerId)
        {
            CrawlerX = crawlerX;
            CrawlerY = crawlerY;
            ExitX = exitX;
            ExitY = exitY;
            OwnerId = ownerId;
        }

        public int CrawlerX { get; }
        public int CrawlerY { get; }
        public int ExitX { get; }
        public int ExitY { get; }
        public int OwnerId { get; }
    }
}