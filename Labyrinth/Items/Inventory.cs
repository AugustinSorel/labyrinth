namespace Labyrinth.Items
{
    /// <summary>
    /// Inventory of collectable items for rooms and players.
    /// </summary>
    public abstract class Inventory
    {
        protected IList<ICollectable> _items = new List<ICollectable>();

        protected Inventory(ICollectable? item = null)
        {
            if (item is not null)
            {
                _items.Add(item);
            }
        }

        /// <summary>
        /// True if the room has items, false otherwise.
        /// (Peut rester une propriété si la vérification est locale, 
        /// sinon il faudrait aussi la passer en méthode async).
        /// </summary>
        public bool HasItems => _items.Count > 0;

        /// <summary>
        /// Gets the types of the items in the inventory asynchronously.
        /// </summary>
        public virtual Task<IEnumerable<Type>> GetItemTypesAsync()
        {
            // Dans un contexte réel, cela pourrait impliquer une requête DB.
            // On utilise Task.FromResult pour simuler l'asynchronisme sur la liste locale.
            var types = _items.Select(item => item.GetType());
            return Task.FromResult(types);
        }

        /// <summary>
        /// Attempts to move an item from another inventory to this one.
        /// </summary>
        /// <param name="from">The source inventory.</param>
        /// <param name="nth">The index of the item to take.</param>
        /// <returns>True if the transfer was successful, False if the item was no longer available.</returns>
        public virtual async Task<bool> TryMoveItemFromAsync(Inventory from, int nth = 0)
        {
            // Simulation d'un délai réseau ou accès BDD (optionnel, pour l'exemple)
            // await Task.Delay(10);

            // // Vérification de sécurité : L'inventaire source a-t-il encore l'objet ?
            // C'est ici que l'on gère le cas où l'inventaire a changé entre temps.
            if (!from.HasItems || from._items.Count <= nth)
            {
                return false; // Échec : L'item n'existe plus ou l'index est invalide
            }

            // Section critique : Dans un vrai serveur multi-threadé, 
            // il faudrait probablement un lock ici.
            var itemToMove = from._items[nth];

            _items.Add(itemToMove);

            from._items.RemoveAt(nth);

            return true; // Succès
        }
    }
}
