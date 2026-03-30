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

> **ARM64 users (Apple Silicon, Raspberry Pi, etc.):** The published image is built for `linux/amd64`. It will still run via emulation, but you can suppress the warning by adding `--platform linux/amd64` to the command. For native performance, build the image locally:
> ```bash
> docker build -t podcast-scrobbler .
> ```

## Token Authentication

The server uses a static token you define yourself. Set it via the `SCROBBLER_TOKEN` environment variable:

```bash
docker run -d \
  -p 5000:5000 \
  -v podcast-data:/data \
  -e SCROBBLER_TOKEN=my-secret-token \
  ghcr.io/lqdev/podcast-scrobbler:latest
```

To generate a random token:

```powershell
# PowerShell
$bytes = New-Object byte[] 32
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
[System.Convert]::ToBase64String($bytes)
```

```bash
# openssl
openssl rand -hex 32
```

Clients pass the token via the `Authorization: Token <your-token>` header or `?token=` query parameter. Use `GET /1/validate-token` to confirm the token is valid.

If `SCROBBLER_TOKEN` is not set, the server accepts all tokens (useful for trusted networks).

## Status

🚧 **IN PROGRESS** — See [SCAFFOLD.md](SCAFFOLD.md) for the project specification.

## License

MIT

---
*Maintainer: [@lqdev](https://github.com/lqdev)*
