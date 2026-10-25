# Frontière Live GE

[![CI](https://github.com/Fabienhncgithub/LiveGe/actions/workflows/ci.yml/badge.svg)](https://github.com/Fabienhncgithub/LiveGe/actions/workflows/ci.yml)

Frontière Live GE is a public dashboard and alerting bot for the main road crossings
between France and Geneva. It compares the two travel directions and combines measured
travel times with official roadworks, road incidents, and weather observations.

The application does not silently fall back to simulated data. If a source is unavailable,
the API reports that source as unavailable or stale.

## What the app does

- Measures both `France → Geneva` and `Geneva → France` approaches at seven crossings.
- Forces each TomTom route through the selected crossing instead of letting the router choose
  a neighbouring border.
- Adds nearby roadworks and incidents plus weather conditions to the decision.
- Explains each recommendation and exposes the status, scope, and attribution of every source.
- Stores TomTom measurements for alerts, trends, and local time-slot forecasts.
- Can publish selected alerts to X; publishing is disabled by default.

The `fusion-v1` score is deliberately simple and auditable:

```text
decision cost = TomTom delay in minutes × 4 + contextual risk points
```

Routes are compared only within the same direction. A route is marked as recommended only
when it has enough source coverage and a meaningful lead over the next option. A critical
nearby event or a delay of at least 15 minutes marks an approach as one to avoid.

## Data sources

| Source | Purpose | Refresh/cache | Cost and licence | Important coverage limit |
| --- | --- | --- | --- | --- |
| [TomTom Routing](https://www.tomtom.com/products/routing-apis/) | Directional live-traffic and free-flow times | Collected in the background; 30-minute cache | Free monthly allowance with an API key; no payment card required for the free plan | Measures the configured approach segments, not a user's full trip |
| [SITG InfoMobilité](https://sitg.ge.ch/donnees/pcmob-chantier-consult) | Important Geneva roadworks | 30 minutes | [SITG level A open-data terms](https://sitg.ge.ch/ressources/conditions-utilisation-donnees): commercial reuse is allowed with source, extraction frequency, and transformation attribution | Announced roadworks, updated daily; not a live congestion feed. The dataset is indicative and restricted to coordination purposes |
| [Bison Futé open DATEX II feed](https://transport.data.gouv.fr/datasets/evenements-routiers-sur-le-reseau-routier-national-non-concede) | French road events | 10 minutes | Free, Licence Ouverte 2.0; attribution retained | Covers the non-concession national network. It does **not** provide reliable coverage of concession roads such as the A40/A41, or every departmental border road |
| [MeteoSwiss Open Data](https://opendatadocs.meteoswiss.ch/general/terms-of-use) | Rain, gusts, temperature, and snow observations | 10 minutes | Free, CC BY 4.0; `Source: MeteoSwiss` attribution retained | One Geneva/Cointrin station is used; it cannot describe every local microclimate |

SITG, Bison Futé, and MeteoSwiss require no API key. TomTom uses a free account key. The
application enforces a conservative monthly request ceiling below the documented free allowance.

The interactive basemap uses [OpenFreeMap](https://openfreemap.org/) vector tiles. Its
public instance currently requires no key, allows commercial use, and has no request limit,
with OpenFreeMap/OpenMapTiles/OpenStreetMap attribution shown on the map. It is a
best-effort service without an SLA and does not contribute to the recommendation score.

### Honest scope of a recommendation

A recommendation compares the **border approaches** known to the service. It is not yet a
door-to-door route because the API does not receive the user's origin or destination.
An approach that is best at the border can still be worse for the complete journey.

The feeds can be delayed, incomplete, or temporarily unavailable. Frontière Live GE is a
decision aid, not an emergency or safety navigation service. Always follow road signs,
police instructions, and official closure notices.

## Architecture

```text
Background collector ── TomTom directional routes ── local history / alerts / X
                                                │
Public advice request ─ cached TomTom ──────────┼── fusion-v1 ── /api/live/advice
                      ├ SITG roadworks ──────┤
                      ├ Bison Futé events ───┤
                      └ MeteoSwiss weather ──┘
```

The public advice endpoint reads TomTom data from the shared cache; it does not initiate
upstream routing calls. Free public providers use independent caches and an anti-stampede lock.
If a refresh fails, the last successful result is returned as `Stale` when available.

Main components:

- ASP.NET Core 10 API and background worker
- EF Core with SQLite
- React, Vite, and strict TypeScript frontend
- xUnit tests and GitHub Actions CI

SQLite is appropriate for one application instance with a persistent volume. Multiple
instances require a shared database, a distributed refresh lock, and a global TomTom budget
counter.

## Prerequisites

- .NET SDK 10
- Node.js 22 recommended
- pnpm 10.15.1

## Local setup

Install locked dependencies:

```bash
dotnet tool restore
dotnet restore --locked-mode
pnpm --dir frontend install --frozen-lockfile
```

Store secrets outside the repository:

```bash
dotnet user-secrets set "Admin:ApiKey" "A_LONG_RANDOM_VALUE" --project backend
dotnet user-secrets set "Traffic:TomTom:ApiKey" "YOUR_TOMTOM_KEY" --project backend
dotnet user-secrets set "Traffic:TomTom:Enabled" "true" --project backend
```

Never put API keys, OAuth secrets, or production passwords in `appsettings.json`, frontend
environment files, commits, screenshots, or support messages. Rotate a key immediately if it
has been exposed.

Run the API:

```bash
dotnet run --project backend
```

Local API: `http://127.0.0.1:5090`

Run the frontend in another terminal:

```bash
VITE_API_BASE_URL=http://127.0.0.1:5090 pnpm --dir frontend dev
```

Local UI: `http://127.0.0.1:5173`

Local administration requires `Admin:ApiKey`; the public frontend does not expose an administration screen.

## Public API

- `GET /health`
- `GET /api/border-points`
- `GET /api/live`
- `GET /api/live/directions` — latest cached TomTom measurements
- `GET /api/live/advice` — fused recommendations, reasons, signals, and source status
- `GET /api/alerts`
- `GET /api/history/{borderPointId}`
- `GET /api/traffic/quota`
- `GET /api/traffic/history`
- `GET /api/traffic/forecast`

The route-time forecast appears only after at least seven days and 100 TomTom measurements.
It is a historical pattern, not a guarantee of future traffic.

## Protected administration API

The public frontend contains no administration navigation or screen. Administration routes
are not mapped by default, so `/api/admin/*` returns `404` in production.

For local maintenance only, `appsettings.Development.json` enables these routes. They still
require the `X-Admin-Key` header:

- `GET /api/admin/settings`
- `PUT /api/admin/settings`
- `POST /api/admin/run-once`
- `POST /api/admin/publish-test`
- `GET /api/admin/x/me`

For an internet deployment, enable HTTPS redirection behind a correctly configured reverse
proxy, set explicit CORS origins, use a long random admin key, and optionally enable preview
basic authentication. The Vite development server must never be exposed publicly.

Never set `Admin__EndpointsEnabled=true` on the public demo unless the administration API is
strictly required and a new random `Admin__ApiKey` has been stored as a platform secret.

## Deployment

The Dockerfile serves the compiled React app and API from one container.
See [deployment and provider safeguards](docs/deployment.md) for the Render
preview, persistence requirements, request budget, parser limits and optional
X publishing. A free preview loses local history when its storage is reset;
it is not a durable production deployment.

## Verification

```bash
dotnet build --configuration Release
dotnet test --configuration Release
dotnet list backend/FrontiereLiveGe.Api.csproj package --vulnerable --include-transitive
pnpm --dir frontend run build
pnpm --dir frontend audit --audit-level high
```

## Repository layout

- `backend/`: API, worker, migrations, providers, and publishing
- `frontend/`: public dashboard and administration
- `tests/backend/`: xUnit unit tests
- `.github/`: CI and dependency automation
- `docs/`: architecture, algorithms, and product roadmap

See [SECURITY.md](SECURITY.md) for the vulnerability reporting policy.
