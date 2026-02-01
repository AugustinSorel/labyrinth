using Labyrinth.Core.Domain;
using Labyrinth.Core.Services;
using Labyrinth.Crawl;
using Labyrinth.Dtos;
using Labyrinth.Items;

namespace Labyrinth.Examples;

/// <summary>
/// Exemple d'utilisation de la nouvelle architecture avec DTOs et mapping.
/// Démontre comment votre logique de domaine (ICrawler, Inventory) 
/// est maintenant connectée à l'API Syllab.
/// </summary>
public class UsageExample
{
    public async Task DemonstrateNewArchitectureAsync()
    {
        // ═══════════════════════════════════════════════════════════════
        // LAYER 1: Infrastructure - Configuration du client HTTP
        // ═══════════════════════════════════════════════════════════════
        
        var httpClient = new HttpClient 
        { 
            BaseAddress = new Uri("https://labyrinth.syllab.fr/api") 
        };
        var appKey = "your-app-key-guid-here";
        
        // Service d'infrastructure: retourne des DTOs
        var labyrinthService = new LabyrinthService(httpClient, appKey);

        // ═══════════════════════════════════════════════════════════════
        // LAYER 2: Domain - Service de domaine avec logique métier
        // ═══════════════════════════════════════════════════════════════
        
        var domainService = new CrawlerDomainService(labyrinthService);

        // ═══════════════════════════════════════════════════════════════
        // UTILISATION 1: Créer un crawler avec settings
        // ═══════════════════════════════════════════════════════════════
        
        var settings = new SettingsDto(
            RandomSeed: 42,
            CorridorWalls: new List<int> { 1, 2, 3 },
            WallDoors: null,
            KeyRooms: null
        );

        // ✅ Retourne un ICrawler (votre interface de domaine)
        ICrawler crawler = await domainService.CreateCrawlerAsync(settings);
        
        Console.WriteLine($"Crawler créé à la position ({crawler.X}, {crawler.Y})");
        Console.WriteLine($"Direction: {crawler.Direction}");

        // ═══════════════════════════════════════════════════════════════
        // UTILISATION 2: Utiliser avec votre logique de domaine existante
        // ═══════════════════════════════════════════════════════════════
        
        // Vos méthodes existantes fonctionnent maintenant !
        var facingTile = await crawler.GetFacingTileTypeAsync();
        Console.WriteLine($"Face à: {facingTile}");

        if (crawler.CanMoveForward)
        {
            var keyChain = new MyInventory();
            var pickedUpItems = await crawler.TryWalkAsync(keyChain);
            
            if (pickedUpItems != null)
            {
                Console.WriteLine("Mouvement réussi !");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // UTILISATION 3: Gérer l'inventaire avec mapping automatique
        // ═══════════════════════════════════════════════════════════════
        
        // Le service de domaine convertit automatiquement DTO ↔ Domain
        Guid crawlerId = Guid.Parse("some-crawler-id");
        Inventory bag = await domainService.GetBagAsync(crawlerId);
        
        if (bag.HasItems)
        {
            Console.WriteLine("Le crawler a des items dans son sac");
        }

        // ═══════════════════════════════════════════════════════════════
        // UTILISATION 4: Lister tous les crawlers
        // ═══════════════════════════════════════════════════════════════
        
        var allCrawlers = await domainService.GetAllCrawlersAsync();
        
        foreach (var c in allCrawlers)
        {
            Console.WriteLine($"Crawler: {c.X},{c.Y} facing {c.Direction}");
        }

        // ═══════════════════════════════════════════════════════════════
        // UTILISATION 5: Intégration avec vos explorateurs existants
        // ═══════════════════════════════════════════════════════════════
        
        // Vos classes RandExplorer, ConcurrentExplorer peuvent maintenant
        // travailler avec des RemoteCrawler !
        
        // Exemple (à adapter selon votre implémentation):
        // var explorer = new RandExplorer();
        // await explorer.ExploreLabyrinthAsync(crawler, keyChain);
    }

    /// <summary>
    /// Exemple d'utilisation directe du service HTTP (bas niveau).
    /// À utiliser seulement si vous avez besoin des DTOs bruts.
    /// </summary>
    public async Task DemonstrateLowLevelApiAsync()
    {
        var httpClient = new HttpClient 
        { 
            BaseAddress = new Uri("https://labyrinth.syllab.fr/api") 
        };
        var labyrinthService = new LabyrinthService(httpClient, "your-app-key");

        // Travail direct avec les DTOs (pas de mapping vers domain)
        CrawlerDto dto = await labyrinthService.CreateCrawlerAsync();
        
        Console.WriteLine($"DTO reçu: ID={dto.Id}, X={dto.X}, Y={dto.Y}");
        
        // Modifier avec 'with' expression (records)
        var movedDto = dto with { X = dto.X + 1 };
        
        // Envoyer à l'API
        await labyrinthService.UpdateCrawlerAsync(dto.Id, movedDto);
    }

    /// <summary>
    /// Exemple de comparaison: Ancien vs Nouveau pattern.
    /// </summary>
    public void CompareOldVsNew()
    {
        // ❌ ANCIEN: DTO mutable, pas de domain model
        /*
        var oldCrawler = new Crawler { Id = Guid.NewGuid(), X = 0 };
        oldCrawler.X = 10; // Mutable - risque de bugs
        // Impossible à utiliser avec ICrawler !
        */

        // ✅ NOUVEAU: Record immutable, mapping vers domain
        var newDto = new CrawlerDto(
            Id: Guid.NewGuid(), 
            X: 0, 
            Y: 0, 
            Direction: DirectionEnum.North,
            Walking: false,
            FacingTile: Tiles.TileType.Empty
        );
        
        // Immutable - doit créer une nouvelle instance
        var modified = newDto with { X = 10 };
        
        // Peut être mappé vers ICrawler pour utiliser votre logique de domaine !
    }
}
