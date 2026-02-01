# Labyrinth API

API minimale .NET qui encapsule l'API Labyrinth de Syllab.

## Configuration

1. Mettez à jour `appsettings.json` avec votre clé :
   ```json
   {
     "Labyrinth": {
       "AppKey": "votre-clé-guid-ici"
     }
   }
   ```

## Démarrage

```bash
cd src/Labyrinth.Api
dotnet run
```

L'API sera disponible sur https://localhost:5001

## Documentation

La documentation Swagger est accessible à : https://localhost:5001/swagger

## Endpoints disponibles

- `GET /crawlers` - Récupère tous les crawlers
- `POST /crawlers` - Crée un nouveau crawler
- `GET /crawlers/{id}` - Récupère un crawler par ID
- `PATCH /crawlers/{id}` - Met à jour un crawler (déplacement)
- `DELETE /crawlers/{id}` - Supprime un crawler
- `GET /crawlers/{id}/bag` - Récupère l'inventaire du crawler
- `PUT /crawlers/{id}/bag` - Met à jour l'inventaire du crawler
- `GET /crawlers/{id}/items` - Récupère les items de la tuile actuelle
- `PUT /crawlers/{id}/items` - Met à jour les items de la tuile
- `GET /groups` - Récupère les groupes de joueurs
