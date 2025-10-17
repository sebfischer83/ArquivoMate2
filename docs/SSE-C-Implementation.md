# SSE-C Encryption Support Implementation

## Summary
ArquivoMate2 supports Amazon S3–compatible server-side encryption with customer-provided keys (SSE-C). This capability enables defence-in-depth when combined with existing client-side encryption and keeps encryption keys under tenant control.

## Current Status
SSE-C is available across storage, ingestion, and delivery providers. When enabled, presigned URLs are replaced by a server-proxy streaming flow because browsers cannot attach the required headers automatically. Client-side encryption continues to function independently and can be combined with SSE-C.

## Key Concepts
- **DocumentEncryptionType:** A flags enum stored on aggregates, read models, and DTOs to indicate which encryption modes apply.
- **SSE-C Headers:** S3 requires `X-Amz-Server-Side-Encryption-Customer-*` headers on every GET/PUT/COPY/HEAD request. The MinIO SDK generates these headers when configured with `SSEC`/`SSECopy` objects.
- **Marker URLs:** Delivery providers emit `sse-c://{path}` placeholders so the API can detect SSE-C artifacts and stream them through the storage provider without exposing keys to clients.

## Implementation Details
### Domain and Shared Layers
- `Document` aggregate raises `DocumentEncryptionTypeSet` events that persist the selected flags.
- `DocumentView` projection and associated DTOs expose the `EncryptionType` integer for API responses.

### Infrastructure Layer
- **Configuration:** `SseCConfiguration` objects hang off S3 storage, ingestion, and delivery settings. Each configuration validates a 256-bit Base64 key.
- **Storage & Ingestion Providers:** Construct `SSEC` and `SSECopy` helpers from the configured key and apply them to PUT/GET/COPY calls. Operations transparently include the required headers.
- **Delivery Provider:** Returns marker URLs when SSE-C is active so the API streams artifacts via `IDocumentArtifactStreamer` instead of presigned URLs.
- **Projections & Mapping:** `DocumentProjection` listens for `DocumentEncryptionTypeSet` to update `DocumentView.EncryptionType`; AutoMapper maps the integer back to the enum for DTOs.

## Configuration
Example `appsettings.json` snippet:

```json
{
  "StorageProvider": {
    "Type": "S3",
    "Args": {
      "Endpoint": "s3.hetzner.cloud",
      "BucketName": "documents",
      "SseC": {
        "Enabled": true,
        "CustomerKeyBase64": "<base64-encoded-32-byte-key>"
      }
    }
  }
}
```

The same `SseC` block applies to `IngestionProvider` and `DeliveryProvider`. Generate a compliant key with `openssl rand -base64 32` or an equivalent cryptographically secure generator.

## Operational Guidance
1. Enable SSE-C in configuration and restart services.
2. Upload documents normally; the storage provider adds SSE-C headers and raises `DocumentEncryptionTypeSet` events.
3. When clients request downloads, the delivery controller intercepts `sse-c://` URLs, validates access tokens, and streams content through the storage provider (which attaches SSE-C headers internally).
4. Background processors (thumbnailing, conversion) continue to read via the storage provider and automatically send the proper headers.

## Security Considerations
- Store the SSE-C key in a managed secrets system (e.g., Azure Key Vault, AWS Secrets Manager, Kubernetes Secrets) and rotate it according to policy.
- Rotation requires rewriting objects with the new key; schedule maintenance windows and verify backups before rotation.
- Always use TLS when transmitting the SSE-C key between services.
- Back up the key securely—losing it renders encrypted objects unrecoverable.

## Testing Checklist
- Upload and download an SSE-C enabled document and verify that direct S3 access without the key fails.
- Confirm the delivery API returns `sse-c://` URLs and streams content successfully.
- Validate ingestion copy operations and background processing pipelines continue to succeed.

## References
- `src/ArquivoMate2.Shared/Models/DocumentEncryptionType.cs`
- `src/ArquivoMate2.Domain/Documents/Events/DocumentEncryptionTypeSet.cs`
- `src/ArquivoMate2.Infrastructure/Services/StorageProvider/S3StorageProvider.cs`
- `src/ArquivoMate2.Infrastructure/Services/DeliveryProvider/S3DeliveryProvider.cs`
- `docs/EncryptionRecoveryKey.md`
