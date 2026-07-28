# Architecture

## Flux principal

1. `BotWorker` déclenche un cycle toutes les cinq minutes.
2. `ITrafficDataProvider` fournit les mesures.
3. `TrafficIngestionService` persiste les snapshots.
4. `TrendAnalyzer` calcule tendance et projection.
5. `AlertEngine` applique seuils et antidoublons.
6. `IPostPublisher` simule ou publie sur X.

`RadarRunGate` empêche deux cycles locaux de s'exécuter simultanément.

## Frontières de sécurité

- Les données live et alertes sont publiques.
- Les réglages, déclenchements manuels et opérations X sont administratifs.
- L'API admin utilise une clé transmise dans `X-Admin-Key`.
- CORS limite les navigateurs autorisés, mais ne remplace jamais l'authentification.

## Production

SQLite convient à une seule instance avec volume persistant. Pour plusieurs instances,
prévoir PostgreSQL, un verrou distribué et un worker séparé de l'API web.

La publication fiable nécessite ensuite un modèle outbox :

1. enregistrer l'alerte et le message à publier dans la même transaction ;
2. publier en tâche de fond ;
3. enregistrer l'identifiant du post X ;
4. réessayer avec backoff sans créer de doublon.
