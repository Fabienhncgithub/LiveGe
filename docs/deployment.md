# Deployment and provider safeguards

## Free public preview

The repository Dockerfile serves the compiled React frontend and the .NET API from one
container. A Render `free` web service can deploy the complete demo from the included
`render.yaml` Blueprint:

[![Deploy to Render](https://render.com/images/deploy-to-render-button.svg)](https://render.com/deploy?repo=https%3A%2F%2Fgithub.com%2FFabienhncgithub%2FLiveGe)

Before confirming the deployment:

1. verify that the instance plan is `free`;
2. deploy first with TomTom disabled;
3. add a dedicated TomTom key as the secret `Traffic__TomTom__ApiKey`;
4. set `Traffic__TomTom__Enabled=true`;
5. keep `Admin__EndpointsEnabled=false` and `X__Enabled=false` for the public preview.

Free Render services scale down after inactivity and have ephemeral local storage.
Consequently, SQLite history and the local TomTom budget counter disappear after sleep,
restart, or deployment. This tier is suitable for user testing, not for a durable production
service. Keep the TomTom account on its free plan and monitor its provider-side usage.

## TomTom quota safeguards

The repository defaults are intentionally conservative:

- one shared 30-minute cache;
- one refresh lock to prevent concurrent request bursts;
- 14 route requests per complete collection cycle, sent sequentially;
- a 45-minute background interval (at most 448 scheduled calls in 24 hours);
- a persistent local ceiling of 18,000 TomTom requests per UTC month;
- fail-closed behaviour if the local budget state cannot be read;
- warning at 75% and critical status at 90%.

These controls keep this application's scheduled use below TomTom's documented free allowance:

- the counter is local to this application and does not see calls made with the same key
  elsewhere;
- deleting or replacing its persistent state can reset the local count;
- every extra application instance has its own counter unless a shared store is implemented;
- TomTom pricing, plan allowances, and account settings remain authoritative.

Use a dedicated TomTom application/key, keep the budget state on a persistent volume, do not
scale the worker horizontally, and verify the current free allowance in the TomTom account.

Relevant settings can be overridden with environment variables:

```text
Traffic__TomTom__Enabled
Traffic__TomTom__ApiKey
Traffic__TomTom__CacheSeconds
Traffic__TomTom__MaxRequestsPerMonth
Traffic__TomTom__BudgetStatePath
Traffic__TomTom__WarningThresholdPercent
Traffic__TomTom__CriticalThresholdPercent
BotWorker__IntervalMinutes
```

Do not reduce `BotWorker__IntervalMinutes` without recalculating the monthly request budget.

## Public-feed and parser safeguards

- Source URLs are fixed HTTPS endpoints, not user-supplied URLs.
- Traffic ingestion has no demo/simulation branch: missing TomTom data stays unavailable.
- Response size limits are 1 MB for TomTom, 2 MB for SITG, 8 MB for Bison Futé, and 500 KB for MeteoSwiss.
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
