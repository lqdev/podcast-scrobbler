# podcast-scrobbler

A self-hosted, ListenBrainz-compatible scrobble server optimized for podcast listening.

## What is this?

A lightweight REST API server that tracks what you listen to — podcasts, episodes, timestamps, and progress. It speaks the [ListenBrainz API protocol](https://listenbrainz.readthedocs.io/en/latest/users/api/core.html), so any ListenBrainz-compatible client can submit listens to it.

Built as a companion to [podcast-tui](https://github.com/lqdev/podcast-tui), but works with any client that supports the ListenBrainz API.

## Tech Stack

- **C# / .NET 10** — ASP.NET Core minimal Web API
- **DuckDB** — Embedded analytical database (zero external dependencies)
- **Docker** — Single container, deployable anywhere

## Features

- ✅ ListenBrainz-compatible API (submit-listens, get-listens, playing-now, validate-token)
- ✅ Podcast-aware `additional_info` fields (feed URL, episode GUID, position, duration)
- ✅ Aggregation endpoints (top podcasts, weekly stats, recent activity, lifetime summary)
- ✅ Optional token auth (trusted network = no auth needed)
- ✅ Health check endpoints for container orchestrators
- ✅ Single Docker image — works on Azure Container Apps, VPS, or local

## Quick Start

```bash
docker run -d \
  -p 5000:5000 \
  -v podcast-data:/data \
  -e SCROBBLER_TOKEN=my-secret-token \
  ghcr.io/lqdev/podcast-scrobbler:latest
```

## Status

🚧 **IN PROGRESS** — See [SCAFFOLD.md](SCAFFOLD.md) for the project specification.

## License

MIT

---
*Maintainer: [@lqdev](https://github.com/lqdev)*
