using Labyrinth.Crawl;
using Labyrinth.Items;
using Labyrinth.Sys;
using Labyrinth.Tiles;

namespace Labyrinth
{
    public class RandExplorer(ICrawler crawler, IEnumRandomizer<RandExplorer.Actions> rnd)
    {
        private readonly ICrawler _crawler = crawler;
        private readonly IEnumRandomizer<Actions> _rnd = rnd;

        public enum Actions
        {
            TurnLeft,
            Walk
        }

        public async Task<int> GetOutAsync(int n)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(n, 0, "n must be strictly positive");
            MyInventory bag = new();

            while (n > 0 && await _crawler.GetFacingTileTypeAsync() != typeof(Outside))
            {
                bool moved = false;

                if (_rnd.Next() == Actions.Walk)
                {
                    var roomContent = await _crawler.TryWalkAsync(bag);

                    if (roomContent is not null)
                    {
                        moved = true;
                        while (roomContent.HasItems)
                        {
                            await bag.TryMoveItemFromAsync(roomContent);
                        }
                        PositionChanged?.Invoke(this, new CrawlingEventArgs(_crawler));
                    }
                }

                if (!moved)
                {
                    _crawler.Direction.TurnLeft();
                    DirectionChanged?.Invoke(this, new CrawlingEventArgs(_crawler));
                }

                n--;
            }
            return n;
        }

        public event EventHandler<CrawlingEventArgs>? PositionChanged;
        public event EventHandler<CrawlingEventArgs>? DirectionChanged;
    }
}
