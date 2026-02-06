using Labyrinth.Core;
using Labyrinth.ValueObjects;
using Labyrinth.Utils;
using Labyrinth.Entities;

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

            // Modification 1 : On vérifie le Type au lieu de l'objet
            for (; n > 0 && await _crawler.GetFacingTileTypeAsync() != TileType.Outside; n--)
            {
                EventHandler<CrawlingEventArgs>? changeEvent;

                // Modification 3 : Logique de déverrouillage abstraite
                // Si c'est une porte, on tente de l'ouvrir via le crawler avant de marcher
                var facingTileType = await _crawler.GetFacingTileTypeAsync();
                if (facingTileType == TileType.Door)
                {
                    // Le Crawler gère en interne si la porte est déjà ouverte ou non.
                    // Si elle est fermée, il essaie les clés du sac.
                    while (bag.HasItems && !await _crawler.TryUnlockAsync(bag))
                        ;
                }

                // Modification 2 : On utilise CanMoveForward au lieu de FacingTile.IsTraversable
                if (_crawler.CanMoveForward
                    && _rnd.Next() == Actions.Walk)
                {
                    var roomContent = await _crawler.TryWalkAsync(bag);

                    if (roomContent != null)
                    {
                        while (roomContent.HasItems)
                        {
                            await bag.MoveItemFrom(roomContent);
                        }
                        changeEvent = PositionChanged;
                    }
                    else
                    {
                        _crawler.Direction.TurnLeft();
                        changeEvent = DirectionChanged;
                    }
                }
                else
                {
                    _crawler.Direction.TurnLeft();
                    changeEvent = DirectionChanged;
                }

                changeEvent?.Invoke(this, new CrawlingEventArgs(_crawler));
            }
            return n;
        }

        public event EventHandler<CrawlingEventArgs>? PositionChanged;
        public event EventHandler<CrawlingEventArgs>? DirectionChanged;
    }
}
