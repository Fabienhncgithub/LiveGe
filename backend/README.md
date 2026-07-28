# Backend Frontière Live GE

API ASP.NET Core 10 avec EF Core, SQLite et worker périodique.

## Sécurité

- Les lectures du dashboard sont publiques.
- `/api/admin/*` exige l'en-tête `X-Admin-Key`.
- La clé vient de `Admin:ApiKey`.
- X est désactivé par défaut.
- Les réponses administratives utilisent `Cache-Control: no-store`.
- Les routes publiques et administratives ont des limites de débit distinctes.

Les User Secrets conviennent uniquement au développement. En production, utilisez le
gestionnaire de secrets de l'hébergeur.

## Base de données

Les migrations sont dans `Data/Migrations`.

```bash
dotnet tool restore
dotnet tool run dotnet-ef database update --project backend --startup-project backend
```

`Database:AdoptLegacySchema` sert uniquement à adopter l'ancienne base locale créée avant
l'ajout des migrations. Il doit rester à `false` en production.

## Configuration principale

- `ConnectionStrings:Default`
- `Database:AdoptLegacySchema`
- `Cors:AllowedOrigins`
- `BotWorker:IntervalMinutes`
- `Admin:ApiKey`
- `Security:UseHttpsRedirection`
- `X:Enabled`
- `X:ClientId`
- `X:ClientSecret`
- `X:AccessToken`
- `X:RefreshToken`
