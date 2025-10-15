 ![logo](https://github.com/user-attachments/assets/14e0441c-cb8d-47e1-bc4e-4a8dbea67d3a)

 ## Status

[![CI](https://github.com/sebfischer83/ArquivoMate2/actions/workflows/ci.yml/badge.svg)](https://github.com/sebfischer83/ArquivoMate2/actions/workflows/ci.yml) [![Docker Hub](https://img.shields.io/docker/v/sebfischer83/arquivomate2?sort=semver&label=Docker%20Hub)](https://hub.docker.com/r/sebfischer83/arquivomate2) [![Coverage](https://img.shields.io/website?down_message=down&label=coverage&up_message=available&url=https%3A%2F%2Fsebfischer83.github.io%2FArquivoMate2%2F)](https://sebfischer83.github.io/ArquivoMate2/)

- CI / tests: click the CI badge to open the latest workflow runs and test results. Individual run artifacts (TRX, coverage XML) are attached to each job run under "Artifacts".
- Coverage report: published to GitHub Pages at: https://sebfischer83.github.io/ArquivoMate2/ (contains HTML coverage-report/index.html and Cobertura XML).
- Docker image: tags and latest published version visible on Docker Hub (link above).

---

## Overview
ArquivoMate2 is a lightweight, extensible .NET 9 document ingestion and processing platform. It tracks document processing lifecycle states (Pending, InProgress, Completed, Failed), provides history querying & counts, real-time status updates, background jobs, and localization.

For detailed architecture, grouping and sharing documentation see `docs/ProjectOverview.md`.

## Current Features
- Document lifecycle tracking (upload → processing → completion/failure)
- History listing with pagination & status filtering
- Per-status count endpoints (e.g. /api/history/inprogress/count)
- Hide history entries by status
- Real-time updates via SignalR hub (`/hubs/documents`)
- Background processing with Hangfire (queue: `documents`)
- Structured logging (Serilog) & optional tracing (OpenTelemetry)
- Localization (de, en)
- CQRS style handlers (MediatR) & clean layering
- PostgreSQL persistence via Marten

## Planned Features
Multiple storage backends:
- [X] S3 compatible services
- [ ] BunnyCDN storage
- [ ] Local filesystem
- [ ] OneDrive integration

## Privacy
- [X] Encrypted files on the storage backend

Delivery options:
- [X] S3 pre‑signed URLs
- [ ] BunnyCDN direct delivery
- [X] BunnyCDN as accelerator in front of S3
- [ ] Reverse proxy delivery via ArquivoMate2
- [ ] Amazon CloudFront integration

AI based enrichment:
- [X] Document summaries
- [X] Auto tagging

Ingestion channels:
- [ ] Browser uploads
- [ ] E‑mail ingestion
- [ ] Public API ingestion (authenticated)

Performance & reliability:
- [ ] Partial / conditional indexes for hot queries (UserId + Status + IsHidden=false)
- [ ] Caching layer for frequent count endpoints
- [ ] Resilience (Polly) policies for external services
- [ ] Projection rebuild monitoring dashboard

Bulk & admin tooling:
- [ ] Bulk hide/unhide operations
- [ ] Aggregated status summary endpoint
- [ ] Administrative UI for job & projection health

## Delivery Provider configuration

ArquivoMate2 supports multiple delivery provider strategies via the `DeliveryProvider:Type` configuration. Available built-in types:

- `Noop` — returns the storage `fullPath` unchanged (client is expected to fetch directly from storage/CDN).
- `S3` — returns presigned S3/Minio URLs or direct public URLs when a bucket is public.
- `Bunny` — builds BunnyCDN URLs; can optionally sign them using token authentication.
- `Server` — routes delivery through the API server. The provider returns a URL of the form `/api/delivery/{documentId}/{artifact}?token={token}` and is useful when you want the server to remain in the control path (uniform access control, logging, on-the-fly decryption).

Choosing a provider

- Use `S3`/`Bunny` when you prefer client ↔ storage direct downloads (best for large files and CDN offload).
- Use `Server` when the API must be the download entry-point (for decryption, additional auth, or to keep storage paths private).
- Use `Noop` only when downstream clients already know how to resolve the `fullPath` values the app exposes.

How to enable via `appsettings.json`

Set the `DeliveryProvider:Type` to the desired provider name (case-insensitive). Example that enables server-side routing:

```json
{
  "DeliveryProvider": {
    "Type": "Server"
  },
  "App": { "PublicBaseUrl": "https://api.example.com" },
  "Encryption": { "TokenTtlMinutes": 60 }
}
```

Notes

- When `Server` is selected the DI container registers `ServerDeliveryProvider` and `IDeliveryProvider.GetAccessUrl(fullPath)` will return a server URL with a short-lived token. If you prefer a programmatic swap instead of configuration you can replace the DI registration in the infrastructure DI file.
- `App:PublicBaseUrl` is optional but recommended in reverse-proxy deployments to produce absolute URLs.
- `Encryption:TokenTtlMinutes` controls token lifetime used for delivery URLs.

For more details about streaming, decryption and controller usage see `docs/delivery.md`.
