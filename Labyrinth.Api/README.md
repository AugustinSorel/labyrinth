# Labyrinth.Api

API minimale ASP.NET Core pour le serveur d'entraînement de labyrinthe.

## Description

Ce projet implémente une API REST conforme à la spécification OpenAPI documentée sur https://labyrinth.syllab.com/swagger. L'API permet de créer et gérer des crawlers (explorateurs) dans un labyrinthe pré-défini.

## Architecture

Le projet suit les principes SOLID et les bonnes pratiques C# :

### Structure

- **Models/** : DTOs (Data Transfer Objects) sous forme de records immuables
  - `CrawlerDto` : Représente un crawler dans l'API
  - `SettingsDto` : Configuration pour le labyrinthe
  - `InventoryItemDto` : Item d'inventaire
  - `CrawlerUpdateDto` : Payload de mise à jour d'un crawler

- **Services/** : Logique métier
  - `ILabyrinthService` : Interface d'abstraction pour le service de labyrinthe
  - `LocalLabyrinthService` : Implémentation locale utilisant la logique métier du projet Labyrinth

- **Abstractions/** : Abstractions pour futures intégrations
  - `ICrawlerFactory` : Factory pour créer des crawlers (local ou distant)
  - `LocalCrawlerFactory` : Implémentation locale
  - `RemoteCrawlerFactory` : Implémentation distante (utilise ApiCrawler)

### Endpoints

- `GET /crawlers` : Liste tous les crawlers d'une clé d'application
- `POST /crawlers` : Crée un nouveau crawler (max 3 par clé)
- `GET /crawlers/{id}` : Récupère un crawler spécifique
- `PATCH /crawlers/{id}` : Met à jour la direction/état de marche (MOUVEMENT)
- `DELETE /crawlers/{id}` : Supprime un crawler
- `GET /crawlers/{id}/bag` : Récupère l'inventaire d'un crawler
- `PUT /crawlers/{id}/bag` : Met à jour l'inventaire
- `GET /crawlers/{id}/items` : Récupère les items à la position actuelle
- `PUT /crawlers/{id}/items` : Met à jour les items à la position actuelle
- `GET /Groups` : Liste les groupes (retourne une liste vide)

## Utilisation

### Lancer l'API en mode développement

```bash
dotnet run --project Labyrinth.Api
```

L'API sera accessible sur :
- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001
- Swagger UI: https://localhost:5001/swagger

### Exemple d'utilisation

```bash
# Créer un crawler
curl -X POST "https://localhost:5001/crawlers?appKey=d98e5988-58e3-4bce-b050-46e1903e6777" \
  -H "Content-Type: application/json" \
  -d "null"

# Lister les crawlers
curl "https://localhost:5001/crawlers?appKey=d98e5988-58e3-4bce-b050-46e1903e6777"

# Déplacer un crawler (changer direction puis activer la marche)
curl -X PATCH "https://localhost:5001/crawlers/{id}?appKey=d98e5988-58e3-4bce-b050-46e1903e6777" \
  -H "Content-Type: application/json" \
  -d '{"direction": 1, "is-walking": true}'
```

## Principes de conception

### SOLID

- **Single Responsibility** : Chaque classe a une responsabilité unique
  - `LocalLabyrinthService` : Gestion des crawlers
  - DTOs : Représentation des données
  - Controllers (endpoints) : Gestion des requêtes HTTP

- **Open/Closed** : Extensible via interfaces sans modification
  - `ILabyrinthService` peut avoir plusieurs implémentations
  - `ICrawlerFactory` permet d'ajouter de nouveaux types de crawlers

- **Liskov Substitution** : Les implémentations respectent leurs contrats
  - `LocalLabyrinthService` implémente parfaitement `ILabyrinthService`

- **Interface Segregation** : Interfaces spécifiques et minimales
  - Interfaces métier séparées des DTOs

- **Dependency Inversion** : Dépendances sur des abstractions
  - Injection de `ILabyrinthService` plutôt que classe concrète

### Autres bonnes pratiques

- **Records pour DTOs** : Immuabilité et concision
- **Async/Await** : Opérations asynchrones
- **Thread-safety** : Utilisation de `ConcurrentDictionary`
- **Validation** : Vérification des paramètres (appKey, etc.)
- **Documentation** : XML comments et Swagger

## Différences avec l'API distante

Cette implémentation d'entraînement ne gère pas :
- Les équipes (un seul programme connecté à la fois)
- La génération de labyrinthes aléatoires (utilise un labyrinthe pré-défini)
- La persistance (tout est en mémoire)

## Intégration future avec le CLI

L'architecture actuelle permet de facilement switcher entre :
- Mode distant : Utilise `RemoteCrawlerFactory` → `ApiCrawler`
- Mode local : Utilise `LocalCrawlerFactory` → Logique métier interne

La phase de refactorisation du CLI pour utiliser ces abstractions viendra ultérieurement.
