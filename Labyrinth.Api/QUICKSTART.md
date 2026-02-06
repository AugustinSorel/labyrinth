# Guide de démarrage rapide - Labyrinth API

## 🚀 Lancement de l'API

```bash
cd Labyrinth.Api
dotnet run
```

L'API sera accessible sur :
- **Swagger UI**: https://localhost:5001/swagger
- **HTTP**: http://localhost:5000
- **HTTPS**: https://localhost:5001

## 📝 Exemples d'utilisation

### 1. Créer un crawler

```bash
# Windows PowerShell
$appKey = "d98e5988-58e3-4bce-b050-46e1903e6777"
$response = Invoke-RestMethod -Uri "http://localhost:5000/crawlers?appKey=$appKey" -Method Post -ContentType "application/json" -Body "null"
$crawlerId = $response.id
Write-Host "Crawler créé avec l'ID: $crawlerId"
```

```bash
# Linux/Mac bash
APP_KEY="d98e5988-58e3-4bce-b050-46e1903e6777"
RESPONSE=$(curl -s -X POST "http://localhost:5000/crawlers?appKey=$APP_KEY" \
  -H "Content-Type: application/json" \
  -d "null")
CRAWLER_ID=$(echo $RESPONSE | jq -r '.id')
echo "Crawler créé avec l'ID: $CRAWLER_ID"
```

### 2. Lister tous les crawlers

```bash
# PowerShell
Invoke-RestMethod -Uri "http://localhost:5000/crawlers?appKey=$appKey" -Method Get

# bash
curl "http://localhost:5000/crawlers?appKey=$APP_KEY" | jq
```

### 3. Récupérer les informations d'un crawler

```bash
# PowerShell
Invoke-RestMethod -Uri "http://localhost:5000/crawlers/$crawlerId?appKey=$appKey" -Method Get

# bash
curl "http://localhost:5000/crawlers/$CRAWLER_ID?appKey=$APP_KEY" | jq
```

### 4. Changer la direction du crawler

Direction : 0=Nord, 1=Est, 2=Sud, 3=Ouest

```bash
# PowerShell - Tourner vers l'Est
$body = @{
    direction = 1
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5000/crawlers/$crawlerId?appKey=$appKey" `
  -Method Patch `
  -ContentType "application/json" `
  -Body $body

# bash - Tourner vers l'Est
curl -X PATCH "http://localhost:5000/crawlers/$CRAWLER_ID?appKey=$APP_KEY" \
  -H "Content-Type: application/json" \
  -d '{"direction": 1}'
```

### 5. Déplacer le crawler

```bash
# PowerShell - Activer la marche
$body = @{
    "is-walking" = $true
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5000/crawlers/$crawlerId?appKey=$appKey" `
  -Method Patch `
  -ContentType "application/json" `
  -Body $body

# bash - Activer la marche
curl -X PATCH "http://localhost:5000/crawlers/$CRAWLER_ID?appKey=$APP_KEY" \
  -H "Content-Type: application/json" \
  -d '{"is-walking": true}'
```

### 6. Changer direction ET marcher en une seule requête

```bash
# PowerShell - Tourner vers le Sud et marcher
$body = @{
    direction = 2
    "is-walking" = $true
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5000/crawlers/$crawlerId?appKey=$appKey" `
  -Method Patch `
  -ContentType "application/json" `
  -Body $body

# bash - Tourner vers le Sud et marcher
curl -X PATCH "http://localhost:5000/crawlers/$CRAWLER_ID?appKey=$APP_KEY" \
  -H "Content-Type: application/json" \
  -d '{"direction": 2, "is-walking": true}'
```

### 7. Gérer l'inventaire (sac)

```bash
# PowerShell - Récupérer l'inventaire
Invoke-RestMethod -Uri "http://localhost:5000/crawlers/$crawlerId/bag?appKey=$appKey" -Method Get

# PowerShell - Ajouter des clés dans l'inventaire
$items = @(
    @{ type = "Key" },
    @{ type = "Key" }
) | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5000/crawlers/$crawlerId/bag?appKey=$appKey" `
  -Method Put `
  -ContentType "application/json" `
  -Body $items

# bash - Récupérer l'inventaire
curl "http://localhost:5000/crawlers/$CRAWLER_ID/bag?appKey=$APP_KEY"

# bash - Ajouter des clés
curl -X PUT "http://localhost:5000/crawlers/$CRAWLER_ID/bag?appKey=$APP_KEY" \
  -H "Content-Type: application/json" \
  -d '[{"type": "Key"}, {"type": "Key"}]'
```

### 8. Gérer les items sur le sol (à la position actuelle)

```bash
# PowerShell - Voir les items au sol
Invoke-RestMethod -Uri "http://localhost:5000/crawlers/$crawlerId/items?appKey=$appKey" -Method Get

# bash - Voir les items au sol
curl "http://localhost:5000/crawlers/$CRAWLER_ID/items?appKey=$APP_KEY"
```

### 9. Supprimer un crawler

```bash
# PowerShell
Invoke-RestMethod -Uri "http://localhost:5000/crawlers/$crawlerId?appKey=$appKey" -Method Delete

# bash
curl -X DELETE "http://localhost:5000/crawlers/$CRAWLER_ID?appKey=$APP_KEY"
```

## 🎮 Scénario complet d'exploration

```powershell
# PowerShell - Script complet d'exploration
$appKey = "d98e5988-58e3-4bce-b050-46e1903e6777"
$baseUrl = "http://localhost:5000"

# 1. Créer un crawler
Write-Host "1. Création du crawler..." -ForegroundColor Green
$crawler = Invoke-RestMethod -Uri "$baseUrl/crawlers?appKey=$appKey" -Method Post -Body "null" -ContentType "application/json"
$id = $crawler.id
Write-Host "   Crawler créé: $id à position ($($crawler.x), $($crawler.y))" -ForegroundColor Cyan

# 2. Explorer en tournant et avançant
for ($i = 0; $i -lt 4; $i++) {
    Write-Host "`n2.$i. Tourner vers direction $i et avancer..." -ForegroundColor Green
    $update = @{ direction = $i; "is-walking" = $true } | ConvertTo-Json
    $crawler = Invoke-RestMethod -Uri "$baseUrl/crawlers/$id?appKey=$appKey" -Method Patch -Body $update -ContentType "application/json"
    Write-Host "   Position: ($($crawler.x), $($crawler.y)), Direction: $($crawler.direction)" -ForegroundColor Cyan
    Start-Sleep -Milliseconds 500
}

# 3. Vérifier l'inventaire
Write-Host "`n3. Vérification de l'inventaire..." -ForegroundColor Green
$bag = Invoke-RestMethod -Uri "$baseUrl/crawlers/$id/bag?appKey=$appKey" -Method Get
Write-Host "   Nombre d'items: $($bag.Count)" -ForegroundColor Cyan

# 4. Supprimer le crawler
Write-Host "`n4. Suppression du crawler..." -ForegroundColor Green
Invoke-RestMethod -Uri "$baseUrl/crawlers/$id?appKey=$appKey" -Method Delete
Write-Host "   Crawler supprimé!" -ForegroundColor Cyan
```

```bash
# bash - Script complet d'exploration
#!/bin/bash
APP_KEY="d98e5988-58e3-4bce-b050-46e1903e6777"
BASE_URL="http://localhost:5000"

# 1. Créer un crawler
echo "1. Création du crawler..."
RESPONSE=$(curl -s -X POST "$BASE_URL/crawlers?appKey=$APP_KEY" \
  -H "Content-Type: application/json" -d "null")
CRAWLER_ID=$(echo $RESPONSE | jq -r '.id')
echo "   Crawler créé: $CRAWLER_ID"
echo $RESPONSE | jq

# 2. Explorer en tournant et avançant
for i in {0..3}; do
  echo -e "\n2.$i. Tourner vers direction $i et avancer..."
  curl -s -X PATCH "$BASE_URL/crawlers/$CRAWLER_ID?appKey=$APP_KEY" \
    -H "Content-Type: application/json" \
    -d "{\"direction\": $i, \"is-walking\": true}" | jq
  sleep 0.5
done

# 3. Vérifier l'inventaire
echo -e "\n3. Vérification de l'inventaire..."
curl -s "$BASE_URL/crawlers/$CRAWLER_ID/bag?appKey=$APP_KEY" | jq

# 4. Supprimer le crawler
echo -e "\n4. Suppression du crawler..."
curl -s -X DELETE "$BASE_URL/crawlers/$CRAWLER_ID?appKey=$APP_KEY"
echo "   Crawler supprimé!"
```

## 🧪 Tester avec curl et jq

```bash
# Installation de jq (pour formater le JSON)
# Ubuntu/Debian: sudo apt install jq
# Mac: brew install jq
# Windows: choco install jq

# Créer 3 crawlers et les lister
APP_KEY="d98e5988-58e3-4bce-b050-46e1903e6777"
for i in {1..3}; do
  curl -s -X POST "http://localhost:5000/crawlers?appKey=$APP_KEY" \
    -H "Content-Type: application/json" -d "null" | jq '.id'
done

curl -s "http://localhost:5000/crawlers?appKey=$APP_KEY" | jq 'length'
```

## 🌐 Utilisation avec le client CLI existant

Le client CLI peut être modifié pour pointer vers cette API locale :

```bash
# Définir les variables d'environnement
$env:LAB_USE_REMOTE="true"
$env:LAB_APP_KEY="d98e5988-58e3-4bce-b050-46e1903e6777"
$env:LAB_BASE_URL="http://localhost:5000"

# Lancer le CLI
dotnet run --project Labyrinth
```

## 📊 Codes de statut HTTP

| Code | Signification |
|------|---------------|
| 200 | Succès (GET, PATCH) |
| 201 | Créé (POST) |
| 204 | Succès sans contenu (PUT, DELETE) |
| 400 | Mauvaise requête |
| 401 | Clé d'application manquante |
| 403 | Accès refusé (mauvaise clé ou limite atteinte) |
| 404 | Crawler non trouvé |

## 🔧 Configuration

Pour changer le port ou d'autres paramètres, modifiez [Labyrinth.Api/Properties/launchSettings.json](Labyrinth.Api/Properties/launchSettings.json).
