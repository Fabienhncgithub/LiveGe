# Algorithmes et visibilité

## Améliorations algorithmiques

### Priorité 1 — données réelles et qualité de source

- Remplacer le provider simulé par plusieurs sources réelles.
- Attribuer à chaque mesure une fraîcheur, une fiabilité et une provenance.
- Refuser ou dégrader visuellement les données trop anciennes.
- Comparer plusieurs sources avant de déclencher une alerte.

### Priorité 2 — tendance robuste

La tendance actuelle utilise trois points et exige une hausse ou baisse strictement monotone.
Une version plus robuste devrait :

- utiliser six à douze mesures ;
- calculer une régression sur les timestamps réels ;
- filtrer les valeurs aberrantes avec médiane ou MAD ;
- produire une pente, un horizon et un score de confiance ;
- ne publier que si la variation dépasse le bruit historique.

### Priorité 3 — seuils adaptatifs

Les seuils devraient dépendre :

- du poste frontière ;
- du jour de semaine ;
- de l'heure ;
- des vacances et événements connus ;
- du niveau habituel observé sur les semaines précédentes.

Une alerte devient alors : « délai anormal par rapport à cette heure », plutôt qu'un simple
dépassement d'un seuil identique partout.

### Priorité 4 — recommandations d'itinéraire

Pour chaque corridor, comparer les postes substituables avec :

- délai actuel ;
- distance supplémentaire ;
- tendance à vingt minutes ;
- confiance des données.

Ne recommander une alternative que si le gain estimé dépasse un minimum stable, par exemple
dix minutes après prise en compte du détour.

## Gagner en visibilité

### Contenu utile

- Publier seulement les changements significatifs.
- Donner le lieu, le délai, la tendance, l'heure de mesure et une alternative crédible.
- Ajouter une image ou une carte simple avec texte alternatif.
- Publier un récapitulatif matin et soir, même en l'absence d'incident majeur.
- Maintenir une page publique rapide et partageable par poste frontière.

### Référencement et partage

- Ajouter des pages indexables pour Bardonnex, Perly, Moillesulaz, Meyrin et Ferney.
- Ajouter titres, descriptions, Open Graph, sitemap et données structurées.
- Employer des URL stables et lisibles.
- Créer un flux RSS ou Atom pour les alertes.
- Permettre des notifications web opt-in.

### Distribution locale

- Nouer des partenariats avec médias, communautés de pendulaires et groupes locaux.
- Publier en français et proposer une version anglaise.
- Garder un nombre réduit de hashtags géographiques pertinents.
- Mesurer les clics vers le dashboard, les abonnements et la précision des alertes.

### Indicateurs à suivre

- délai entre mesure et publication ;
- taux d'alertes réellement utiles ;
- faux positifs et alertes dupliquées ;
- taux de clic vers le dashboard ;
- abonnements obtenus par type de contenu ;
- rétention des visiteurs à sept et trente jours.
