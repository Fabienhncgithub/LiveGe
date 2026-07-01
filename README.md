# Frontière Live GE

MVP "Radar Frontalier Genève" pour suivre le trafic simulé aux points frontières : Bardonnex, Perly, Moillesulaz, Thônex-Vallard.

## Stack
- Backend : ASP.NET Core Web API (.NET 10), EF Core + SQLite, BackgroundService
- Frontend : React + Vite + TypeScript (strict)

## Démarrage rapide

### Backend
```bash
cd backend
dotnet restore
dotnet run
```

Par défaut : `http://localhost:5000`

### Frontend
```bash
cd frontend
npm install
npm run dev
```

Par défaut : `http://localhost:5173`

> Configurez la base API dans `frontend/.env` (voir `frontend/.env.example`).

## Architecture

### Backend
- `Data/` : `AppDbContext`, seed initial (points frontières + BotSettings + snapshots)
- `Models/` : entités EF Core (BorderPoint, TrafficSnapshot, AlertEvent, BotSettings)
- `Dtos/` : DTOs exposés par l'API
- `Services/` : ingestion, analyse de tendance, moteur d'alertes, publisher, worker
- `Endpoints/` : Minimal API

### Frontend
- `src/api` : appels REST
- `src/components` : UI réutilisable
- `src/pages` : Dashboard / Alerts / Settings
- `src/types` : types TypeScript

## FakeTrafficDataProvider
Le provider génère des données plausibles selon l'heure locale Europe/Zurich :
- matin 06:30–09:00 : Bardonnex/Perly plus chargés
- soir 16:30–19:00 : Moillesulaz/Thônex-Vallard plus chargés
- week-end : trafic réduit avec petits pics

## Brancher un provider réel
Implémentez `ITrafficDataProvider` puis remplacez l'enregistrement dans `backend/Program.cs`.

## Brancher un publisher X
Le publisher X est prêt via OAuth2 (Bearer) : configurez les secrets puis lancez `POST /api/run-once`.

Configuration (User Secrets recommandé) :
```bash
cd backend
dotnet user-secrets init
dotnet user-secrets set "X:ClientId" "CLIENT_ID"
dotnet user-secrets set "X:AccessToken" "ACCESS_TOKEN"
dotnet user-secrets set "X:RefreshToken" "REFRESH_TOKEN"
```

## Configuration utile
`backend/appsettings.json` :
- `ConnectionStrings:Default` : SQLite locale
- `Cors:AllowedOrigins` : origines frontend autorisées
- `BotWorker:IntervalMinutes` : fréquence du worker

## Endpoints API
- `GET /health`
- `GET /api/border-points`
- `GET /api/live`
- `GET /api/alerts`
- `GET /api/history/{borderPointId}`
- `GET /api/settings`
- `PUT /api/settings`
- `POST /api/run-once`

## Notes
- SQLite est créée automatiquement au démarrage.
- Seed initial pour afficher rapidement des données sur le dashboard.
