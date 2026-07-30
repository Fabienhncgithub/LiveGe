# Frontière Live GE

Frontière Live GE is a public dashboard and alerting bot for the main road crossings
between France and Geneva. It compares the two travel directions and combines measured
travel times with official roadworks, road incidents, and weather observations.

The application does not silently fall back to simulated data. If a source is unavailable,
the API reports that source as unavailable or stale.

## What the app does

- Measures both `France → Geneva` and `Geneva → France` approaches at seven crossings.
- Forces each HERE route through the selected crossing instead of letting the router choose
  a neighbouring border.
- Adds nearby roadworks and incidents plus weather conditions to the decision.
- Explains each recommendation and exposes the status, scope, and attribution of every source.
- Stores HERE measurements for alerts, trends, and local time-slot forecasts.
- Can publish selected alerts to X; publishing is disabled by default.

The `fusion-v1` score is deliberately simple and auditable:

```text
decision cost = HERE delay in minutes × 4 + contextual risk points
```

Routes are compared only within the same direction. A route is marked as recommended only
when it has enough source coverage and a meaningful lead over the next option. A critical
nearby event or a delay of at least 15 minutes marks an approach as one to avoid.

## Data sources

| Source | Purpose | Refresh/cache | Cost and licence | Important coverage limit |
| --- | --- | --- | --- | --- |
| [HERE Routing](https://www.here.com/) | Directional travel and free-flow times | Collected in the background; 30-minute cache | API key and HERE account plan required; billing is possible | Measures the configured approach segments, not a user's full trip |
| [SITG InfoMobilité](https://sitg.ge.ch/donnees/pcmob-chantier-consult) | Important Geneva roadworks | 30 minutes | [SITG level A open-data terms](https://sitg.ge.ch/ressources/conditions-utilisation-donnees): commercial reuse is allowed with source, extraction frequency, and transformation attribution | Announced roadworks, updated daily; not a live congestion feed. The dataset is indicative and restricted to coordination purposes |
| [Bison Futé open DATEX II feed](https://transport.data.gouv.fr/datasets/evenements-routiers-sur-le-reseau-routier-national-non-concede) | French road events | 10 minutes | Free, Licence Ouverte 2.0; attribution retained | Covers the non-concession national network. It does **not** provide reliable coverage of concession roads such as the A40/A41, or every departmental border road |
| [MeteoSwiss Open Data](https://opendatadocs.meteoswiss.ch/general/terms-of-use) | Rain, gusts, temperature, and snow observations | 10 minutes | Free, CC BY 4.0; `Source: MeteoSwiss` attribution retained | One Geneva/Cointrin station is used; it cannot describe every local microclimate |

SITG, Bison Futé, and MeteoSwiss require no API key and have no billing path in this
application. HERE is the only configured source with billing risk.

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
Background collector ── HERE directional routes ── local history / alerts / X
                                             │
Public advice request ─ cached HERE ─────────┼── fusion-v1 ── /api/live/advice
                      ├ SITG roadworks ──────┤
                      ├ Bison Futé events ───┤
                      └ MeteoSwiss weather ──┘
```

The public advice endpoint reads HERE data from the shared cache; it does not initiate
billable HERE calls. Free public providers use independent caches and an anti-stampede lock.
If a refresh fails, the last successful result is returned as `Stale` when available.

Main components:

- ASP.NET Core 10 API and background worker
- EF Core with SQLite
- React, Vite, and strict TypeScript frontend
- xUnit tests and GitHub Actions CI

SQLite is appropriate for one application instance with a persistent volume. Multiple
instances require a shared database, a distributed refresh lock, and a global HERE budget
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
dotnet user-secrets set "Traffic:Here:ApiKey" "YOUR_HERE_KEY" --project backend
dotnet user-secrets set "Traffic:Here:Enabled" "true" --project backend
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

The Administration page asks for `Admin:ApiKey` and keeps it only in the browser session.

## Public API

- `GET /health`
- `GET /api/border-points`
- `GET /api/live`
- `GET /api/live/directions` — latest cached HERE measurements
- `GET /api/live/advice` — fused recommendations, reasons, signals, and source status
- `GET /api/alerts`
- `GET /api/history/{borderPointId}`
- `GET /api/here/quota`
- `GET /api/here/history`
- `GET /api/here/forecast`

The route-time forecast appears only after at least seven days and 100 HERE measurements.
It is a historical pattern, not a guarantee of future traffic.

## Protected administration API

These routes require the `X-Admin-Key` header:

- `GET /api/admin/settings`
- `PUT /api/admin/settings`
- `POST /api/admin/run-once`
- `POST /api/admin/publish-test`
- `GET /api/admin/x/me`

For an internet deployment, enable HTTPS redirection behind a correctly configured reverse
proxy, set explicit CORS origins, use a long random admin key, and optionally enable preview
basic authentication. The Vite development server must never be exposed publicly.

## HERE cost safeguards

The repository defaults are intentionally conservative:

- one shared 30-minute cache;
- one refresh lock to prevent concurrent request bursts;
- 14 route requests per complete collection cycle, sent sequentially;
- a 40-minute background interval (at most 504 scheduled calls in 24 hours);
- a persistent local ceiling of 600 HERE requests per UTC day;
- fail-closed behaviour if the local budget state cannot be read;
- warning at 75% and critical status at 90%.

These controls reduce risk, but **cannot guarantee a zero invoice**:

- the counter is local to this application and does not see calls made with the same key
  elsewhere;
- deleting or replacing its persistent state can reset the local count;
- every extra application instance has its own counter unless a shared store is implemented;
- HERE pricing, plan allowances, and account settings remain authoritative.

To keep the risk controlled, use a dedicated HERE application/key, keep the budget state on
a persistent volume, do not scale the worker horizontally, configure provider-side usage and
billing alerts, and verify the current HERE plan and hard limits in the HERE account before
deployment.

Relevant settings can be overridden with environment variables:

```text
Traffic__Here__Enabled
Traffic__Here__ApiKey
Traffic__Here__CacheSeconds
Traffic__Here__MaxRequestsPerDay
Traffic__Here__BudgetStatePath
Traffic__Here__WarningThresholdPercent
Traffic__Here__CriticalThresholdPercent
BotWorker__IntervalMinutes
```

Do not reduce `BotWorker__IntervalMinutes` without recalculating the daily request budget.

## Public-feed and parser safeguards

- Source URLs are fixed HTTPS endpoints, not user-supplied URLs.
- Traffic ingestion has no demo/simulation branch: missing HERE data stays unavailable.
- Response size limits are 2 MB for SITG, 8 MB for Bison Futé, and 500 KB for MeteoSwiss.
- DATEX II XML disables DTD processing and external entity resolution.
- Bison Futé direction metadata is matched to the travel direction; future events beyond
  24 hours are ignored and nearer planned events cannot trigger a closure-level decision early.
- GeoJSON parsing has a bounded depth and validates Geneva-area coordinates.
- Links from SITG records are returned only when they use HTTPS on a `ge.ch` host.
- Free-provider caches prevent an API request from becoming an uncontrolled upstream burst.
- The OpenFreeMap host and the bundled same-origin MapLibre worker are explicitly allow-listed
  by the production content-security policy.

## X publishing

Enable X only after every OAuth value is configured:

```bash
dotnet user-secrets set "X:ClientId" "CLIENT_ID" --project backend
dotnet user-secrets set "X:ClientSecret" "CLIENT_SECRET" --project backend
dotnet user-secrets set "X:AccessToken" "ACCESS_TOKEN" --project backend
dotnet user-secrets set "X:RefreshToken" "REFRESH_TOKEN" --project backend
dotnet user-secrets set "X:Enabled" "true" --project backend
```

Recommended scopes: `tweet.read tweet.write users.read offline.access`.

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
