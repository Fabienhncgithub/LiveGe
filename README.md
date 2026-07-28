# Frontière Live GE

Dashboard public et bot de surveillance des principaux passages frontaliers genevois.
Les données sont actuellement simulées ; le provider est remplaçable sans modifier l'API.

## État

- Backend ASP.NET Core 10, EF Core et SQLite
- Frontend React, Vite et TypeScript strict
- Routes de lecture publiques
- Routes d'administration protégées par clé
- Publication X désactivée par défaut
- Tests et CI GitHub

L'application fonctionne en local. Aucun hébergement public n'est encore configuré.

## Prérequis

- .NET SDK 10
- Node.js 22 recommandé
- pnpm 10.15.1

## Installation

```bash
dotnet tool restore
dotnet restore --locked-mode
pnpm --dir frontend install --frozen-lockfile
```

Créer les secrets locaux :

```bash
dotnet user-secrets set "Admin:ApiKey" "UNE_CLE_LONGUE_ET_ALEATOIRE" --project backend
```

## Démarrage

Terminal 1 :

```bash
dotnet run --project backend
```

API : `http://127.0.0.1:5090`

Terminal 2 :

```bash
VITE_API_BASE_URL=http://127.0.0.1:5090 pnpm --dir frontend dev
```

Interface : `http://127.0.0.1:5173`

La page Administration demande la valeur de `Admin:ApiKey`. Elle la conserve uniquement
dans la session du navigateur.

## API

Routes publiques :

- `GET /health`
- `GET /api/border-points`
- `GET /api/live`
- `GET /api/alerts`
- `GET /api/history/{borderPointId}`

Routes protégées par l'en-tête `X-Admin-Key` :

- `GET /api/admin/settings`
- `PUT /api/admin/settings`
- `POST /api/admin/run-once`
- `POST /api/admin/publish-test`
- `GET /api/admin/x/me`

## Publication X

Ne passez `X:Enabled` à `true` qu'après avoir configuré tous les secrets :

```bash
dotnet user-secrets set "X:ClientId" "CLIENT_ID" --project backend
dotnet user-secrets set "X:ClientSecret" "CLIENT_SECRET" --project backend
dotnet user-secrets set "X:AccessToken" "ACCESS_TOKEN" --project backend
dotnet user-secrets set "X:RefreshToken" "REFRESH_TOKEN" --project backend
dotnet user-secrets set "X:Enabled" "true" --project backend
```

Scopes recommandés : `tweet.read tweet.write users.read offline.access`.

Le compte X configuré actuellement nécessite un `ClientSecret`. Sans lui, le renouvellement
OAuth échoue avec `Missing valid authorization header`.

## Qualité

```bash
dotnet build --configuration Release
dotnet test --configuration Release
dotnet list backend/FrontiereLiveGe.Api.csproj package --vulnerable --include-transitive
pnpm --dir frontend run build
pnpm --dir frontend audit --audit-level high
```

## Structure

- `backend/` : API, worker, migrations, providers et publication
- `frontend/` : dashboard public et administration
- `tests/backend/` : tests xUnit
- `.github/` : CI et mises à jour automatiques
- `docs/` : architecture, algorithmes et feuille de route

Consultez également [SECURITY.md](SECURITY.md).
