# Delivery (Streaming) – How-to and examples

This document explains how to implement and consume the streaming delivery pipeline used by ArquivoMate2. It complements the high-level notes in `docs/ProjectOverview.md` and provides concrete implementation patterns for controllers, storage providers and clients.

## Goals
- Deliver document artifacts (file, preview, thumb, metadata, archive) without buffering the entire object in memory on the API host.
- Support on-the-fly decryption when artifacts are stored encrypted.
- Preserve cancellation and backpressure from the HTTP request pipeline.

## Key Contracts

- `IStorageProvider.StreamFileAsync(string fullPath, Func<Stream, CancellationToken, Task> streamConsumer, CancellationToken ct = default)`
  - Storage implementations must call `streamConsumer` with a stream that yields the object bytes.
  - Implementation must respect the supplied cancellation token and avoid copying the full object into memory where possible.

- `IDocumentArtifactStreamer` (application-facing)
  - `Task<(Func<Stream, CancellationToken, Task> WriteToAsync, string ContentType)> GetAsync(Guid documentId, string artifact, CancellationToken ct);`
  - Returns a delegate that writes the artifact bytes into any `Stream` provided by the caller and a content type string.

- `PushStreamResult` (API helper)
  - A small `FileResult` that invokes a `Func<Stream, CancellationToken, Task>` with the response body stream.

## Controller usage (server side)

The recommended controller flow is:
1. Validate delivery access token (`IFileAccessTokenService`).
2. Resolve the `DocumentView` and verify (deleted/encrypted) state.
3. Call `IDocumentArtifactStreamer.GetAsync(documentId, artifact, ct)`.
4. Optionally set cache headers and other response headers.
5. Return a `PushStreamResult(contentType, writeDelegate)` (or manually invoke the delegate with `Response.Body` for more control).

Example (simplified):

```csharp
[HttpGet("{id}/{artifact}")]
public async Task<IActionResult> Get(Guid id, string artifact, [FromQuery] string token, CancellationToken ct)
{
    // token validation omitted for brevity
    var (writeToAsync, contentType) = await _streamer.GetAsync(id, artifact, ct);
    ApplyClientCacheHeaders();
    return new PushStreamResult(contentType, writeToAsync);
}
```

Notes:
- `PushStreamResult` will invoke `writeToAsync(Response.Body, RequestAborted)` so the delegate should honor cancellation.
- Do not call `await writeToAsync(Response.Body, ct)` before returning — let the result execute within ASP.NET Core pipeline so headers and life-cycle are properly managed.

## Delivery provider options and behavior

The application supports multiple delivery provider models via `IDeliveryProvider`. Typical built-in providers include:

- NoopDeliveryProvider
  - Returns the storage `fullPath` unchanged. Use this when the client is expected to fetch the file directly from the storage endpoint (for example the app stores absolute CDN URLs or you expose S3 object paths to the client).

- S3DeliveryProvider
  - Returns a presigned URL for S3/Minio objects or a direct URL for public buckets. Preferred when you want the client to download straight from object storage/CDN.

- BunnyCdnDeliveryProvider
  - Builds CDN-hosted URLs and optionally signs them with BunnyCDN token auth when configured.

- ServerDeliveryProvider (server-side delivery)
  - NEW: A provider that returns a server-routed URL of the form `/api/delivery/{documentId}/{artifact}?token={token}`. It is useful when you want the server to always be the entry-point for downloads (for example to uniformly apply access control, logging, metrics or on-the-fly decryption), even for non-encrypted artifacts.

### When to use each
- Use S3/Bunny for large public downloads where you prefer direct client <-> storage traffic.
- Use ServerDeliveryProvider when the server must remain in the control path (eg to stream and decrypt contents, apply additional authorization or to keep internal storage paths private).
- Use Noop only if client logic or external systems already know how to fetch the `fullPath` exposed by the server.

## ServerDeliveryProvider – behavior and configuration

The `ServerDeliveryProvider` is a small `IDeliveryProvider` implementation included in the infrastructure assembly. Its behavior:

- Accepts a storage `fullPath` (as returned by the storage provider) and attempts to infer the `documentId` and artifact type from the path/filename.
- Creates a short-lived delivery token via `IFileAccessTokenService` and returns a URL pointing to the API delivery route (e.g. `/api/delivery/{documentId}/{artifact}?token={token}`).
- If `AppSettings.PublicBaseUrl` is configured the returned URL is absolute, otherwise it returns a relative path.
- Heuristics for artifact detection:
  - `*-thumb.webp` -> `thumb`
  - `*-preview.pdf` -> `preview`
  - `*-archive.pdf` -> `archive`
  - filename suffixes like `.metadata` or `.meta` -> `metadata`
  - otherwise `file`

### How to enable ServerDeliveryProvider

Out of the box the project's `DependencyInjectionConfiguration` registers delivery providers based on `DeliveryProvider` section in configuration and will select `Noop`, `S3` or `Bunny` providers. To use `ServerDeliveryProvider` you have two options:

1) Quick DI swap (recommended when you want server routing globally)

Open `ArquivoMate2.Infrastructure/Configuration/DependencyInjectionConfiguration.cs` and replace the `NoopDeliveryProvider` registration with `ServerDeliveryProvider` registration, e.g.:

```csharp
// Replace this line:
// services.AddScoped<IDeliveryProvider, NoopDeliveryProvider>();

// With:
services.AddScoped<IDeliveryProvider, ServerDeliveryProvider>();
```

Restart the application. From now on calls to `IDeliveryProvider.GetAccessUrl(fullPath)` will return a `/api/delivery/...` URL that routes through the server and includes a short-lived token.

2) Add a new DeliveryProviderType entry and wire it up (more invasive)

If you want to select the server-based provider purely from configuration you can add a new enum value (for example `Server`) to `DeliveryProviderType`, extend `DeliveryProviderSettingsFactory` to bind a new settings type (if needed) and register the provider when that `Type` is selected. This requires code changes similar to the DI swap above but makes the behavior configurable via `appsettings`.

### Important configuration entries

- `App:PublicBaseUrl` – optional base URL used by `ServerDeliveryProvider` to produce absolute URLs in environments where the app is reverse-proxied.
- `Encryption:TokenTtlMinutes` – used when generating the token lifetime (the `ServerDeliveryProvider` uses the same TTL as other token generation code via `EncryptionSettings`).
- `IFileAccessTokenService` must be registered (the project registers the built-in `FileAccessTokenService`).

Example `appsettings.json` snippets (server delivery uses default DI change as above):

```json
{
  "App": { "PublicBaseUrl": "https://api.example.com" },
  "Encryption": { "TokenTtlMinutes": 60 }
}
```

If you choose the DI swap method, no additional configuration keys are required beyond `App` (optional) and existing `Encryption` settings.

## Storage provider guidance (S3 / Minio)

Prefer a zero-copy approach using the storage SDK's streaming APIs.

Minio example (pseudocode):

```csharp
public override async Task StreamFileAsync(string fullPath, Func<Stream, CancellationToken, Task> streamConsumer, CancellationToken ct = default)
{
    var args = new GetObjectArgs()
        .WithBucket(_settings.BucketName)
        .WithObject(fullPath)
        .WithCallbackStream(stream => {
            // Forward stream directly to consumer, avoid buffering entire object
            streamConsumer(stream, ct).GetAwaiter().GetResult();
        });
    await _minioClient.GetObjectAsync(args, ct).ConfigureAwait(false);
}
```

Important:
- The consumer may be executed on a thread-pool callback depending on SDK behavior. Ensure synchronous waiting is acceptable or use an adapter that pipes bytes asynchronously.
- Avoid building up a MemoryStream for the entire object; use streaming MAC/HMAC verification or a rolling buffer if the format requires trailing bytes for verification.

## Decrypting while streaming

`DocumentArtifactStreamer` supports two encryption formats:
- version 1 (AES-GCM with explicit nonce and trailing tag) – requires buffering ciphertext to obtain tag if the SDK doesn't provide direct access; version in the code buffers after reading nonce since tag is trailing.
- version 2 (AES-CBC with HMAC-SHA256) – implemented streaming-friendly approach: HMAC is computed incrementally while decrypting blocks; trailing 32-byte MAC is validated at the end.

If your storage SDK delivers a stream where you can read sequentially and you implement `StreamFileAsync` to pass that stream to the streamer, decryption will happen on-the-fly without full buffering (version 2 works well with streaming). Version 1 requires reading the tag which is trailing — the current implementation buffers the remainder after the nonce into a MemoryStream to access tag; you may optimize by using a small ring buffer.

## Client examples

Download using curl:

```bash
curl -L "https://api.example.com/api/delivery/{documentId}/file?token={token}" -o document.pdf
```

Browser fetch (download as blob):

```js
fetch('/api/delivery/{id}/file?token={token}')
  .then(res => res.blob())
  .then(blob => {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'document.pdf';
    document.body.appendChild(a);
    a.click();
    URL.revokeObjectURL(url);
  });
```

If artifacts are routed through a CDN or presigned URL, prefer the CDN for large downloads and use the API only when on-the-fly decryption or access-control is required by the server.

## Testing guidance

- Unit tests can mock `IDocumentArtifactStreamer` or `IStorageProvider.StreamFileAsync` to supply `MemoryStream` objects with prepared encrypted payloads (see `tests/ArquivoMate2.Infrastructure.Tests/DocumentArtifactStreamerTests.cs`).
- Integration tests should run against a real Minio/S3 instance to validate streaming, cancellation, and behavior with large files.

## Security

- Delivery remains gated by short-lived tokens issued by `IFileAccessTokenService`.
- DEK unwrapping uses AES-GCM with a master key stored in configuration; protect this key using your platform's secret storage.
- On decryption failures return generic 404 to avoid revealing metadata about existence or encryption state.

---

If you want I can also add a small integration test that demonstrates streaming from Minio into the streamer using a temporary Minio container in CI. Let me know.