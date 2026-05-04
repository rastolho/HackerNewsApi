# Hacker News — Best Stories API

A small ASP.NET Core (.NET 9) MVC Web API that returns the top *N* stories from the
[Hacker News public API](https://github.com/HackerNews/API), ordered by score
descending.

The exercise is deliberately straightforward — the interesting part is the line
*"efficiently service large numbers of requests without risking overloading
the Hacker News API"*. Most of the design notes below are about that.

---

## Running the application

### Option A — Docker (recommended for a quick demo)

Prerequisites: **Docker** with Compose v2.

```bash
# build and start
docker compose up --build

# in another terminal
curl http://localhost:8080/api/best-stories/10
```

The container listens on **`http://localhost:8080`** and includes a
`HEALTHCHECK` that probes `/health` every 30 s. Stop with `docker compose down`.

The image is built in two stages (SDK → runtime), runs as the non-root `app`
user (UID 1654) baked into the official `mcr.microsoft.com/dotnet/aspnet`
image, and excludes tests/IDE artefacts from the build context via
`.dockerignore`.

Configuration can be overridden through environment variables in
`docker-compose.yml` — `IConfiguration` uses double-underscore for nested keys,
e.g. `HackerNews__FetchConcurrency=20`.

### Option B — Local SDK

Prerequisites: **.NET 9 SDK** (or newer — the SDK rolls forward).

```bash
# from the repo root
dotnet run --project src/HackerNews.Api
```

The API listens on `http://localhost:5080` (and `https://localhost:5081` via the
`https` launch profile). Then:

```bash
curl http://localhost:5080/api/best-stories/10
```

In `Development`, the API exposes:

- **`/openapi/v1.json`** — raw OpenAPI 3.0 document (consumed by tools / clients).
- **`/scalar/v1`** — a browsable [Scalar](https://scalar.com) UI rendering the
  OpenAPI doc. The root URL (`/`) redirects here. The schema picks up XML
  doc-comments, `[EndpointSummary]` / `[EndpointDescription]`, and per-property
  `[Description]` attributes.

### Tests

```bash
dotnet test
```

12 unit + integration tests covering ordering, DTO mapping, cache behaviour,
input validation, and HTTP responses.

---

## API

### `GET /api/best-stories/{count}`

| Param | Notes                                            |
|-------|--------------------------------------------------|
| count | Integer, `1..200`. Returns `400` if out of range |

Response — `200 OK`, `application/json`:

```json
[
  {
    "title": "A uBlock Origin update was rejected from the Chrome Web Store",
    "uri": "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
    "postedBy": "ismaildonmez",
    "time": "2019-10-12T13:43:01+00:00",
    "score": 1716,
    "commentCount": 572
  }
]
```

`GET /health` returns `{ "status": "ok" }`.

---

## Architecture

The solution is split into four projects following Clean Architecture:

```
HackerNews.Api            (Web)            ── controllers, OpenAPI, output cache, composition root
       │
       ▼
HackerNews.Application                     ── application services (IBestStoriesService) + ports (IHackerNewsClient)
       │
       ▼
HackerNews.Domain                          ── domain types (Story record). Zero dependencies.

HackerNews.Infrastructure                  ── adapters: typed HttpClient, HybridCache decorator,
                                              cache-warming BackgroundService.
       │
       ▼  (implements ports defined in Application)
HackerNews.Application
```

**Dependency rule.** Inner layers know nothing about outer layers.
`Domain` has no dependencies; `Application` depends only on `Domain`;
`Infrastructure` and `Api` depend on `Application` (and transitively on
`Domain`). The cache, the HTTP client, and the BackgroundService all live in
Infrastructure — the application service is unaware they exist.

### Runtime view

```
Client ─▶ BestStoriesController            (injects IBestStoriesService; output cache ~15s, vary-by count)
            │
            ▼
        IBestStoriesService                (Application — orchestrates, no caching/HTTP)
        └─ BestStoriesService
            │
            ▼  IHackerNewsClient (port)
        CachedHackerNewsClient             (Infrastructure — HybridCache decorator,
            │                                stampede-protected, IDs ~60s, items ~10min)
            ▼
        HackerNewsHttpClient               (Infrastructure — typed HttpClient,
            │                                resilience: timeout · retry+jitter · circuit breaker)
            ▼
        Hacker News public API

        CacheWarmerService                 (Infrastructure BackgroundService)
            └─ every 60s: invalidate IDs cache, then call IBestStoriesService to
               re-warm the per-item cache via the cached decorator.
```

### Why HybridCache (and not `IMemoryCache` or Redis directly)?

`HybridCache` is the .NET 9 caching abstraction Microsoft now recommends for new
code. It buys us, *for free*, several things that would otherwise be hand-rolled:

- **Stampede protection (single-flight).** When a cache entry expires under
  load, only **one** request fetches from upstream — the rest await the same
  in-flight task. Plain `IMemoryCache` has no such guarantee, so a popular key
  expiring while serving 1 000 rps produces 1 000 simultaneous upstream calls.
  This is exactly the failure mode the exercise asks us to avoid.
- **Two-tier ready.** It transparently combines an L1 in-process cache with an
  optional L2 distributed store (`IDistributedCache`). Today we run a single
  instance with L1 only; to scale horizontally, we just register a Redis
  `IDistributedCache` — **the cache call sites don't change**.
- **Tag-based invalidation.** `RemoveByTagAsync("hn:ids", ct)` is how the
  cache warmer forces a fresh ID-list pull. With `IMemoryCache` you'd track
  keys by hand.
- **Generic typed API.** `GetOrCreateAsync<T>` removes manual byte-array
  serialisation — a real ergonomic win for a small domain.

We could have written this on top of `IMemoryCache` + a `SemaphoreSlim`
dictionary, but `HybridCache` is exactly the right primitive — using it
demonstrates current-generation .NET caching, not a 2015 pattern.

### Why no SQL database?

Tempting, but unjustified for this problem:

- **Hacker News is the source of truth.** We don't author or own stories — we
  read them. There's nothing to *persist*; only to *cache*.
- **A DB implies durability we explicitly don't want.** Story `score` and
  `commentCount` drift continuously upstream; cached copies need to be
  short-lived and replaceable. A relational schema for ephemeral data is the
  wrong tool — we'd be building a stale read-through with extra ops cost
  (migrations, backups, connection pools, deployment coupling).
- **HybridCache already gives us what a DB would give us, but bounded.**
  In-memory L1 today, Redis L2 tomorrow. Both are explicitly transient, which
  matches the data's actual lifecycle.
- **It would dilute the exercise.** The brief asks us to *not overload Hacker
  News*; that's a caching/concurrency problem, not a persistence problem.
  Adding Postgres/EF Core would add code, dependencies, and review surface
  without solving anything the exercise cares about.

If the requirements were different — e.g., "store an audit log of every story
served", "compute analytics over historical scores", or "let users save
favourites" — then a database would be appropriate, and EF Core against
Postgres would be my first choice. None of those are in scope here.

### Caching strategy

Three cooperating layers:

1. **Output cache (15 s, vary-by `count`)** — collapses bursts of identical
   requests into a single rendered response. Cheap absorbent.
2. **HybridCache for the ID list (60 s TTL)** — the best-stories list re-orders
   frequently, so we keep this short. HybridCache also gives us **single-flight**
   protection: under load, only one request fetches when the entry expires.
3. **HybridCache per-item (10 min TTL)** — title/url/by/time are immutable for a
   given item; only `score` and `descendants` drift. A 10-minute window is a
   pragmatic freshness/load trade-off.

A `BackgroundService` (`CacheWarmerService`) refreshes both layers every 60 s,
so user requests almost always hit a warm cache.

### Bounded concurrency

Item fetches use `Parallel.ForEachAsync` with `MaxDegreeOfParallelism = 10`
(configurable). This caps simultaneous outbound requests to HN even on a cold
cache.

### Resilience

`AddStandardResilienceHandler` from
`Microsoft.Extensions.Http.Resilience` wraps the typed `HttpClient` with the
recommended pipeline: per-attempt timeout, retry with exponential backoff +
jitter, and a circuit breaker. A total request timeout sits on top.

### Validation

`count` is constrained to `1..MaxStories` (default 200, matching what
`beststories.json` returns). Invalid values produce a standard
`ValidationProblemDetails` (`400`).

---

## Configuration

All knobs live under the `HackerNews` section of `appsettings.json` and can be
overridden by environment variables (e.g. `HackerNews__FetchConcurrency=20`).

| Setting                    | Default | Purpose                                |
|----------------------------|---------|----------------------------------------|
| `BaseUrl`                  | HN v0   | Base URL for the upstream API          |
| `HttpTimeoutSeconds`       | 10      | Per-`HttpClient` timeout               |
| `MaxStories`               | 200     | Hard cap for the `count` parameter     |
| `IdListCacheSeconds`       | 60      | TTL for the cached best-stories list   |
| `ItemCacheMinutes`         | 10      | TTL for each cached item               |
| `FetchConcurrency`         | 10      | Max parallel outbound item fetches     |
| `CacheWarmIntervalSeconds` | 60      | Background warm cadence                |
| `WarmTopN`                 | 200     | How many items to pre-warm each cycle  |
| `EnableCacheWarmer`        | true    | Disable in tests / specific deployments|

---

## Assumptions

- "Best stories" = the IDs returned by `beststories.json` (max 200), filtered
  to `type == "story"`. Jobs, polls, comments, and deleted items are excluded.
- `commentCount` maps to HN's `descendants` field (total reply count, not just
  top-level comments — the documented HN convention).
- Story `time` is mapped from Unix seconds to ISO 8601 with offset
  (`DateTimeOffset` round-trip format), matching the example in the brief.
- A single-instance deployment is acceptable for the exercise. For horizontal
  scale, see *Future work*.
- It's preferable to serve a slightly-stale top list than to overload HN. Two
  user requests in the same 60 s window get the same ranking — this is the
  point.

---

## What I would do with more time

- **Distributed cache layer.** Swap the HybridCache L2 to Redis so multiple
  instances behind a load balancer share warm data instead of each one
  hammering HN independently.
- **`stale-while-error` / `stale-while-revalidate`.** Keep the previous good
  response in a long-TTL bucket and serve it if a refresh fails or hasn't
  finished — survives a full HN outage gracefully.
- **OpenTelemetry.** Traces around the HN client and metrics for cache
  hit-ratio, fetch latency, and outbound call volume. Today only `ILogger` is
  wired up.
- **Health checks.** Add an `IHealthCheck` that pings HN with a tight timeout,
  exposed at `/health/ready` so a load balancer can pull traffic if HN is down.
- **Stronger contract tests.** Use `WireMock.Net` to script realistic upstream
  scenarios (slow responses, 5xx, malformed JSON) and assert the resilience
  pipeline behaves.
- **Rate-limiting middleware.** Defend the API itself against abusive callers
  (`AddRateLimiter` with a token bucket per IP).
- **Source-generated JSON serialization.** `[JsonSerializable]` on the DTOs to
  cut allocations on the hot path.
- **CI.** A trivial GitHub Actions workflow running `dotnet test` on push.
- **Containerise.** A multi-stage `Dockerfile` and a `docker-compose.yml` for
  one-command local runs.

---

## Project layout

```
src/
  HackerNews.Domain/                       ── pure domain (Story record). No dependencies.
    Stories/Story.cs

  HackerNews.Application/                  ── application services + ports.
    Abstractions/IHackerNewsClient.cs      port — implementations live in Infrastructure
    Configuration/BestStoriesOptions.cs    MaxStories, FetchConcurrency
    Services/
      IBestStoriesService.cs               service abstraction (injected by the controller)
      BestStoriesService.cs                orchestration + bounded-concurrency fan-out
    DependencyInjection.cs                 AddHackerNewsApplication()

  HackerNews.Infrastructure/               ── adapters.
    Configuration/HackerNewsConfig.cs      HTTP / cache settings
    HackerNews/
      Models/HackerNewsItem.cs             upstream raw model — internal only
      HackerNewsHttpClient.cs              typed HttpClient implementing the port
      CachedHackerNewsClient.cs            HybridCache decorator around the HTTP client
    BackgroundJobs/CacheWarmerService.cs   periodic cache refresh
    DependencyInjection.cs                 AddHackerNewsInfrastructure()

  HackerNews.Api/                          ── web layer.
    Controllers/BestStoriesController.cs   thin controller, validation, output cache
    Models/StoryResponse.cs                outbound DTO + mapping from Story
    Program.cs                             composition root

tests/HackerNews.Api.Tests/
  Application/BestStoriesServiceTests      unit tests against the service (mock IHackerNewsClient)
  Infrastructure/CachedHackerNewsClientTests
                                           verifies the cache decorator collapses upstream calls
  Api/BestStoriesEndpointTests             WebApplicationFactory integration tests
```
