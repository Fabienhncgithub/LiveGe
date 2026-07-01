# Frontière Live GE — Backend

API ASP.NET Core (.NET 10) pour le MVP "Radar Frontalier Genève".

## Prérequis
- .NET SDK 10

## Lancer en local
```bash
dotnet restore
dotnet run
```

L'API écoute par défaut sur `http://localhost:5000` (Kestrel). Le worker tourne toutes les 5 minutes.

## Configuration
`appsettings.json` :
- `ConnectionStrings:Default` : base SQLite locale
- `Cors:AllowedOrigins` : origines autorisées (Vite)
- `BotWorker:IntervalMinutes` : intervalle du worker

## Architecture
- `Data/` : `AppDbContext` + seed
- `Models/` : entités EF Core
- `Dtos/` : DTOs exposés par l'API
- `Services/` : ingestion, analyse de tendance, moteur d'alertes, publisher
- `Endpoints/` : routes REST

## FakeTrafficDataProvider
Le provider simule des données réalistes selon l'heure locale Europe/Zurich :
- matin 06:30-09:00 : Bardonnex/Perly plus chargés
- soir 16:30-19:00 : retour plus chargé côté Moillesulaz/Thônex-Vallard
- week-end : trafic globalement réduit

Remplacez l'implémentation par un provider réel via `ITrafficDataProvider`.

## Publisher X/Twitter
Le publisher est abstrait via `IPostPublisher`. L'implémentation par défaut utilise OAuth2 (Bearer) via `XPostPublisher`.

Configuration minimale (User Secrets recommandé) :
```bash
dotnet user-secrets init
dotnet user-secrets set "X:ClientId" "CLIENT_ID"
dotnet user-secrets set "X:AccessToken" "ACCESS_TOKEN"
dotnet user-secrets set "X:RefreshToken" "REFRESH_TOKEN"
```

Scopes OAuth2 recommandés : `tweet.read tweet.write users.read offline.access`.
