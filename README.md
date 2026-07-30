# Frontière Live GE

Dashboard public et bot de surveillance des principaux passages frontaliers genevois.
Les délais directionnels proviennent de HERE Routing avec trafic. La simulation est désactivée
par défaut et n'est jamais utilisée comme repli silencieux.

## État

- Backend ASP.NET Core 10, EF Core et SQLite
- Frontend React, Vite et TypeScript strict
- Routes de lecture publiques
- Routes d'administration protégées par clé
- Historique HERE par passage et par sens
- Prévisions locales après au moins 7 jours et 100 mesures
- Quota HERE protégé et visible dans l'onglet Alertes
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
dotnet user-secrets set "Traffic:Here:ApiKey" "CLE_HERE" --project backend
dotnet user-secrets set "Traffic:Here:Enabled" "true" --project backend
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
- `GET /api/live/directions`
- `GET /api/here/quota`
- `GET /api/here/history`
- `GET /api/here/forecast`

## Garde-fous HERE

- cache de 30 minutes partagé ;
- verrou anti-rafale : un seul rafraîchissement complet à la fois ;
- 14 routes interrogées séquentiellement pour rester sous la limite RPS ;
- plafond local de 600 requêtes par jour, sous la limite Limited Plan de 1 000 ;
- compteur persistant et blocage fermé si son état est illisible ;
- avertissement à 75 %, alerte critique à 90 % ;
- notification navigateur optionnelle dans l'onglet Alertes.

Le plafond local ne remplace pas la configuration du compte HERE. Pour éviter une facturation,
utilisez le **Limited Plan**, configurez les alertes dans **Billing & Usage**, réservez un App ID
à cette application et révoquez immédiatement toute clé exposée.

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
