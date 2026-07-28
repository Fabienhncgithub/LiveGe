# Politique de sécurité

## Signaler une vulnérabilité

N'ouvrez pas d'issue publique contenant des secrets, tokens ou détails d'exploitation.
Contactez directement le mainteneur du dépôt avec :

- la version ou le commit concerné ;
- les étapes de reproduction ;
- l'impact estimé ;
- une proposition de correction si disponible.

Les secrets X et la clé administrateur doivent rester dans User Secrets en local
ou dans le gestionnaire de secrets de l'hébergeur en production.

## Principes de déploiement

- Le dashboard de lecture peut être public.
- Les routes `/api/admin` exigent la clé `Admin:ApiKey`.
- `X:Enabled` reste à `false` tant que l'OAuth X n'est pas complètement configuré.
- La base SQLite doit être sauvegardée et stockée sur un volume persistant.
- Le serveur de développement Vite ne doit jamais être exposé à Internet.
