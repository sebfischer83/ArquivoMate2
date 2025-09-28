## Overview
ArquivoMate2 is a lightweight, extensible .NET 9 document ingestion and processing platform. It tracks document processing lifecycle states (Pending, InProgress, Completed, Failed), provides history querying & counts, real-time status updates, background jobs, and localization.

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
- [ ] S3 compatible services
- [ ] BunnyCDN storage
- [ ] Local filesystem
- [ ] OneDrive integration

Delivery options:
- [ ] S3 pre‑signed URLs
- [ ] BunnyCDN direct delivery
- [ ] BunnyCDN as accelerator in front of S3
- [ ] Reverse proxy delivery via ArquivoMate2
- [ ] Amazon CloudFront integration

AI based enrichment:
- [ ] Document summaries
- [ ] Auto tagging

Ingestion channels:
- [ ] Browser uploads
- [ ] E‑mail ingestion
- [ ] Public API ingestion (authenticated)

Performance & reliability:
- [ ] Partial / conditional indexes for hot queries (UserId + Status + IsHidden=false)
- [ ] Caching layer for frequent count endpoints
- [ ] Resilience (Polly) policies for external services
- [ ] Projection rebuild monitoring dashboard
- [ ] Metrics dashboard (Prometheus / Grafana)

Bulk & admin tooling:
- [ ] Bulk hide/unhide operations
- [ ] Aggregated status summary endpoint
- [ ] Administrative UI for job & projection health
