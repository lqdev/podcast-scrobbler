# SCAFFOLD.md — Project Specification

> **Purpose**: This document provides an exhaustive specification for scaffolding the podcast-scrobbler project. An AI coding agent should be able to read this file and produce a complete, buildable project without additional context.

---

## Project Setup

### Initialize .NET 10 Project

```bash
dotnet new webapi -n PodcastScrobbler --no-openapi
cd PodcastScrobbler
```

### NuGet Packages

```bash
dotnet add package DuckDB.NET.Data.Full    # DuckDB ADO.NET provider
dotnet add package Swashbuckle.AspNetCore  # Optional: OpenAPI/Swagger for dev
```

### Project File (`PodcastScrobbler.csproj`)

Target framework: `net10.0`. Enable nullable reference types. Enable implicit usings.

---

## File Tree

```
PodcastScrobbler/
├── PodcastScrobbler.csproj
├── Program.cs
├── appsettings.json
├── appsettings.Development.json
├── Models/
│   ├── Listen.cs
│   ├── PlayingNow.cs
│   ├── TrackMetadata.cs
│   ├── AdditionalInfo.cs
│   ├── SubmitListensRequest.cs
│   ├── SubmitListensResponse.cs
│   ├── ValidateTokenResponse.cs
│   └── StatsModels.cs
├── Endpoints/
│   ├── SubmitListensEndpoint.cs
│   ├── GetListensEndpoint.cs
│   ├── PlayingNowEndpoint.cs
│   ├── ValidateTokenEndpoint.cs
│   ├── StatsEndpoints.cs
│   └── HealthEndpoints.cs
├── Data/
│   ├── DuckDbContext.cs
│   ├── ListenRepository.cs
│   └── PlayingNowStore.cs
├── Middleware/
│   └── OptionalTokenAuth.cs
├── Config/
│   └── ScrobblerConfig.cs
├── Dockerfile
├── docker-compose.yml
└── containerapp.yaml
```

Test project:
```
PodcastScrobbler.Tests/
├── PodcastScrobbler.Tests.csproj
├── Endpoints/
│   ├── SubmitListensTests.cs
│   ├── GetListensTests.cs
│   ├── PlayingNowTests.cs
│   └── ValidateTokenTests.cs
├── Data/
│   ├── ListenRepositoryTests.cs
│   └── PlayingNowStoreTests.cs
└── Middleware/
    └── OptionalTokenAuthTests.cs
```

---

## Models

### Listen.cs
```csharp
public record Listen
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Username { get; init; }
    public required long ListenedAt { get; init; }      // Unix timestamp (seconds)
    public required string ArtistName { get; init; }     // Podcast name
    public required string TrackName { get; init; }      // Episode title
    public AdditionalInfo? AdditionalInfo { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
```

### PlayingNow.cs
```csharp
public record PlayingNow
{
    public required string Username { get; init; }
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
    public required string ArtistName { get; init; }
    public required string TrackName { get; init; }
    public AdditionalInfo? AdditionalInfo { get; init; }
}
```

### TrackMetadata.cs
```csharp
public record TrackMetadata
{
    [JsonPropertyName("artist_name")]
    public required string ArtistName { get; init; }

    [JsonPropertyName("track_name")]
    public required string TrackName { get; init; }

    [JsonPropertyName("additional_info")]
    public AdditionalInfo? AdditionalInfo { get; init; }
}
```

### AdditionalInfo.cs
```csharp
public record AdditionalInfo
{
    [JsonPropertyName("media_player")]
    public string? MediaPlayer { get; init; }

    [JsonPropertyName("podcast_feed_url")]
    public string? PodcastFeedUrl { get; init; }

    [JsonPropertyName("episode_guid")]
    public string? EpisodeGuid { get; init; }

    [JsonPropertyName("duration_ms")]
    public long? DurationMs { get; init; }

    [JsonPropertyName("position_ms")]
    public long? PositionMs { get; init; }

    [JsonPropertyName("percent_complete")]
    public double? PercentComplete { get; init; }
}
```

### SubmitListensRequest.cs
```csharp
public record SubmitListensRequest
{
    [JsonPropertyName("listen_type")]
    public required string ListenType { get; init; }  // "playing_now", "single", "import"

    [JsonPropertyName("payload")]
    public required List<ListenPayload> Payload { get; init; }
}

public record ListenPayload
{
    [JsonPropertyName("listened_at")]
    public long? ListenedAt { get; init; }  // null for playing_now

    [JsonPropertyName("track_metadata")]
    public required TrackMetadata TrackMetadata { get; init; }
}
```

### SubmitListensResponse.cs
```csharp
public record SubmitListensResponse
{
    [JsonPropertyName("status")]
    public required string Status { get; init; }  // "ok"
}
```

### ValidateTokenResponse.cs
```csharp
public record ValidateTokenResponse
{
    [JsonPropertyName("code")]
    public required int Code { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("valid")]
    public required bool Valid { get; init; }

    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }
}
```

### StatsModels.cs
```csharp
public record PodcastStats
{
    public required string PodcastName { get; init; }
    public required int ListenCount { get; init; }
    public required long TotalListenTimeMs { get; init; }
}

public record WeeklyStats
{
    public required string WeekStart { get; init; }  // ISO date
    public required int ListenCount { get; init; }
    public required long TotalListenTimeMs { get; init; }
}

public record ListenSummary
{
    public required int TotalListens { get; init; }
    public required long TotalListenTimeMs { get; init; }
    public required int UniquePodcasts { get; init; }
    public required int UniqueEpisodes { get; init; }
}
```

---

## Data Layer

### DuckDbContext.cs

Singleton registered in DI. Manages a single DuckDB connection for writes.

Key responsibilities:
- `Initialize()`: Create tables if not exist (run the schema SQL below)
- `GetConnection()`: Return the singleton write connection
- `GetReadConnection()`: Create a new read-only connection

**Schema SQL** (run on Initialize):
```sql
CREATE TABLE IF NOT EXISTS listens (
    id            UUID DEFAULT gen_random_uuid() PRIMARY KEY,
    username      VARCHAR NOT NULL,
    listened_at   BIGINT NOT NULL,
    artist_name   VARCHAR NOT NULL,
    track_name    VARCHAR NOT NULL,
    additional_info JSON,
    created_at    TIMESTAMPTZ DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_listens_user_time ON listens (username, listened_at DESC);

CREATE TABLE IF NOT EXISTS playing_now (
    username      VARCHAR PRIMARY KEY,
    updated_at    TIMESTAMPTZ DEFAULT now(),
    artist_name   VARCHAR NOT NULL,
    track_name    VARCHAR NOT NULL,
    additional_info JSON
);
```

**DuckDB connection string**: `Data Source=/data/scrobbles.db` (configurable via `DATABASE_PATH` env var, default: `./data/scrobbles.db`).

**IMPORTANT**: DuckDB is single-writer. Register DuckDbContext as a **singleton** in DI. All write operations go through the singleton connection. Read operations can use separate connections.

### ListenRepository.cs

Methods:
- `InsertListen(Listen listen)` — INSERT into listens table
- `InsertListens(IEnumerable<Listen> listens)` — Batch INSERT (for `import` type)
- `GetListens(string username, int count, long? maxTs, long? minTs)` — SELECT with pagination
- `GetListenCount(string username)` — SELECT COUNT(*)
- `GetTopPodcasts(string username, int limit)` — GROUP BY artist_name, ORDER BY count DESC
- `GetWeeklyStats(string username, int weeks)` — GROUP BY week, using DuckDB date functions
- `GetRecentPodcasts(string username, int limit)` — DISTINCT artist_name, ORDER BY max(listened_at) DESC
- `GetSummary(string username)` — Aggregate: total listens, total time, unique podcasts, unique episodes

Serialize `AdditionalInfo` to JSON string for the `additional_info` column. Deserialize on read.

### PlayingNowStore.cs

Hybrid in-memory + persisted:
- `ConcurrentDictionary<string, PlayingNow>` for fast reads
- On `Set(PlayingNow)`: update dictionary AND UPSERT to DuckDB `playing_now` table
- On `Get(string username)`: read from dictionary (O(1))
- On startup: call `LoadFromDb()` to populate dictionary from DuckDB
- Register as **singleton** in DI

---

## Endpoints

### SubmitListensEndpoint.cs

`POST /1/submit-listens`

- Validate request body (listen_type must be "playing_now", "single", or "import")
- If `listen_type == "playing_now"`: update PlayingNowStore (do NOT insert into listens table)
- If `listen_type == "single"`: insert one Listen record
- If `listen_type == "import"`: batch insert all Listen records from payload
- Return `{ "status": "ok" }` with 200
- Return 400 for invalid JSON
- Return 401 if auth is required and token is invalid

### GetListensEndpoint.cs

`GET /1/user/{username}/listens?count=25&max_ts=...&min_ts=...`

- Default count: 25, max count: 100
- Only max_ts OR min_ts, not both
- Return JSON matching LB format:
```json
{
  "payload": {
    "count": 25,
    "listens": [
      {
        "listened_at": 1740098330,
        "track_metadata": {
          "artist_name": "Podcast Name",
          "track_name": "Episode Title",
          "additional_info": { ... }
        }
      }
    ]
  }
}
```

`GET /1/user/{username}/listen-count`

- Return `{ "payload": { "count": 1234 } }`

### PlayingNowEndpoint.cs

`GET /1/user/{username}/playing-now`

- Read from PlayingNowStore (in-memory, fast)
- Return same format as listens but with single item, no `listened_at` field
- Return empty payload if nothing playing

### ValidateTokenEndpoint.cs

`GET /1/validate-token`

- Read token from `Authorization: Token <token>` header OR `?token=` query param
- Compare against `SCROBBLER_TOKEN` env var
- Return:
  - Valid: `{ "code": 200, "message": "Token valid.", "valid": true, "user_name": "<username>" }`
  - Invalid: `{ "code": 200, "message": "Token invalid.", "valid": false }`
  - Missing: 400
- Note: username for valid tokens can default to "default" (single-user system)

### StatsEndpoints.cs

All read-only, no auth required:

- `GET /1/user/{username}/stats/podcasts?limit=10` → top podcasts by listen count + total time
- `GET /1/user/{username}/stats/weekly?weeks=12` → listens per week
- `GET /1/user/{username}/stats/recent-podcasts?limit=10` → recently active podcasts
- `GET /1/user/{username}/stats/summary` → lifetime totals

### HealthEndpoints.cs

- `GET /health` → always returns 200 `{ "status": "healthy" }` (liveness)
- `GET /ready` → returns 200 if DuckDB connection works, 503 otherwise (readiness)

---

## Middleware

### OptionalTokenAuth.cs

Custom middleware that conditionally enforces auth:

1. Read `SCROBBLER_TOKEN` from env var (or `IConfiguration`)
2. If empty/null: skip auth entirely (all requests pass through)
3. If set: check `Authorization` header on write endpoints
   - Header format: `Token <value>` (NOT `Bearer`)
   - Compare `<value>` against configured token
   - Return 401 if mismatch
4. Read endpoints (GET) skip auth unless `SCROBBLER_REQUIRE_AUTH_FOR_READS` is `true`

---

## Configuration

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "DatabasePath": "./data/scrobbles.db",
  "AllowedHosts": "*"
}
```

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `PORT` | `5000` | Server port |
| `DATABASE_PATH` | `./data/scrobbles.db` | DuckDB file path |
| `SCROBBLER_TOKEN` | (empty) | Auth token. Empty = auth disabled |
| `SCROBBLER_REQUIRE_AUTH_FOR_READS` | `false` | Require auth on GET endpoints too |

---

## Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

// Port configuration
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://+:{port}");

// DI registrations
builder.Services.AddSingleton<DuckDbContext>();
builder.Services.AddSingleton<PlayingNowStore>();
builder.Services.AddScoped<ListenRepository>();

var app = builder.Build();

// Initialize database
var db = app.Services.GetRequiredService<DuckDbContext>();
await db.Initialize();

// Load playing_now state
var playingNowStore = app.Services.GetRequiredService<PlayingNowStore>();
await playingNowStore.LoadFromDb();

// Middleware
app.UseMiddleware<OptionalTokenAuth>();

// Map endpoints
SubmitListensEndpoint.Map(app);
GetListensEndpoint.Map(app);
PlayingNowEndpoint.Map(app);
ValidateTokenEndpoint.Map(app);
StatsEndpoints.Map(app);
HealthEndpoints.Map(app);

app.Run();
```

---

## Docker

### Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY *.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

RUN mkdir -p /data
VOLUME /data

ENV PORT=5000
ENV DATABASE_PATH=/data/scrobbles.db
EXPOSE 5000

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "PodcastScrobbler.dll"]
```

### docker-compose.yml

```yaml
version: '3.8'
services:
  scrobbler:
    build: .
    ports:
      - "5000:5000"
    volumes:
      - scrobbler-data:/data
    environment:
      - PORT=5000
      - SCROBBLER_TOKEN=change-me-in-production
      - DATABASE_PATH=/data/scrobbles.db
    restart: unless-stopped

volumes:
  scrobbler-data:
```

### containerapp.yaml (Azure Container Apps)

```yaml
properties:
  managedEnvironmentId: /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.App/managedEnvironments/{env}
  configuration:
    ingress:
      external: true
      targetPort: 5000
  template:
    containers:
      - name: podcast-scrobbler
        image: ghcr.io/lqdev/podcast-scrobbler:latest
        resources:
          cpu: 0.25
          memory: 0.5Gi
        env:
          - name: PORT
            value: "5000"
          - name: DATABASE_PATH
            value: "/data/scrobbles.db"
          - name: SCROBBLER_TOKEN
            secretRef: scrobbler-token
        volumeMounts:
          - volumeName: scrobbler-data
            mountPath: /data
    volumes:
      - name: scrobbler-data
        storageType: AzureFile
        storageName: scrobblerdata
    scale:
      minReplicas: 0
      maxReplicas: 1
```

---

## Testing

Use xUnit + `WebApplicationFactory<Program>` for integration tests.

### Test patterns:
- Create in-memory DuckDB for tests (use `Data Source=:memory:` connection string)
- Override DI to use test configuration
- Test each endpoint with valid/invalid inputs
- Test auth middleware with/without token
- Test PlayingNowStore in-memory + persistence round-trip
- Test ListenRepository CRUD and aggregation queries

---

## Build and Run

```bash
# Build
dotnet build

# Run locally
dotnet run

# Run tests
dotnet test

# Build Docker image
docker build -t podcast-scrobbler .

# Run Docker container
docker run -d -p 5000:5000 -v scrobbler-data:/data podcast-scrobbler
```
