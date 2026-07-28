# Frontend Frontière Live GE

Interface React, Vite et TypeScript.

## Installation

```bash
pnpm install --frozen-lockfile
```

## Démarrage

```bash
VITE_API_BASE_URL=http://127.0.0.1:5090 pnpm dev
```

La page Administration demande la clé API. Celle-ci est conservée dans `sessionStorage`
et supprimée lorsque la session du navigateur se termine ou après une réponse 401.

Ne placez jamais `Admin:ApiKey` dans une variable `VITE_*` : ces variables sont intégrées
au bundle public.

## Build

```bash
pnpm run build
pnpm audit --audit-level high
```
