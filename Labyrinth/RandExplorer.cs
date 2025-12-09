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

            for (; n > 0 && _crawler.FacingTile is not Outside; n--)
            {
                EventHandler<CrawlingEventArgs>? changeEvent;

                if (_crawler.FacingTile.IsTraversable
                    && _rnd.Next() == Actions.Walk)
                {
                    var roomContent = _crawler.Walk();

                    while (roomContent.HasItems)
                    {
                        await bag.TryMoveItemFromAsync(roomContent);
                    }
                    changeEvent = PositionChanged;
                }
                else
                {
                    _crawler.Direction.TurnLeft();
                    changeEvent = DirectionChanged;
                }
                if (_crawler.FacingTile is Door door && door.IsLocked)
                {
                    while (bag.HasItems && !await door.OpenAsync(bag))
                        ;
                }
                changeEvent?.Invoke(this, new CrawlingEventArgs(_crawler));
            }
            return n;
        }

        public event EventHandler<CrawlingEventArgs>? PositionChanged;

        public event EventHandler<CrawlingEventArgs>? DirectionChanged;
    }

}
