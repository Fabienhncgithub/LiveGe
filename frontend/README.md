# Frontière Live GE — Frontend

Interface React + Vite pour le dashboard "Frontière Live GE".

## Prérequis
- Node.js 18+ (ou 20+)
- npm/pnpm/yarn

## Lancer en local
```bash
npm install
npm run dev
```

Par défaut, l'app appelle `http://localhost:5000`. Vous pouvez changer la base API :

```bash
cp .env.example .env
```

Puis modifiez `VITE_API_BASE_URL`.

## Structure
- `src/api` : client HTTP et appels API
- `src/components` : composants UI réutilisables
- `src/pages` : pages Dashboard / Alerts / Settings
- `src/types` : types TypeScript
- `src/styles` : styles globaux
