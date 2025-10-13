using ArquivoMate2.Application.Configuration;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Document;
using ArquivoMate2.Domain.ValueObjects;
using ArquivoMate2.Domain.ReadModels;
using ArquivoMate2.Shared.Models; // DocumentArtifact
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArquivoMate2.API.Results;
using ArquivoMate2.API.Utilities;
using Microsoft.Extensions.Logging; // added

namespace ArquivoMate2.API.Controllers
{
    [ApiController]
    [AllowAnonymous] // Access controlled solely via signed token
    [Route("api/delivery")]
    public class DeliveryController : ControllerBase
    {
        private readonly IQuerySession _query;
        private readonly IStorageProvider _storage;
        private readonly IDocumentArtifactStreamer _streamer;
        private readonly IFileAccessTokenService _tokenService;
        private readonly IEncryptionService _encryption;
        private readonly EncryptionSettings _settings;
        private readonly IAppCache _cache;
        private readonly ILogger<DeliveryController> _logger; // added
        private const int OneYearSeconds = 31536000; // 365 * 24 * 60 * 60

        public DeliveryController(IQuerySession query,
            IStorageProvider storage,
            IDocumentArtifactStreamer streamer,
            IFileAccessTokenService tokenService,
            IEncryptionService encryption,
            IOptions<EncryptionSettings> settings,
            IAppCache cache,
            ILogger<DeliveryController> logger) // added
        {
            _query = query;
            _storage = storage;
            _streamer = streamer;
            _tokenService = tokenService;
            _encryption = encryption;
            _settings = settings.Value;
            _cache = cache;
            _logger = logger; // added
        }

        /// <summary>
        /// Delivers an (optionally encrypted) artifact for a document when a valid access token is supplied.
        /// </summary>
        /// <param name="documentId">Identifier of the document to deliver.</param>
        /// <param name="artifact">Which artifact to return (file, preview, thumb, metadata, archive).</param>
        /// <param name="token">Signed access token that authorizes delivery of the requested artifact.</param>
        /// <param name="ct">Cancellation token for the request.</param>
        /// <returns>
        /// A streamed file result that writes the artifact directly into the HTTP response, or NotFound when validation fails
        /// or the artifact cannot be retrieved / decrypted.
        /// </returns>
        [HttpGet("{documentId:guid}/{artifact}")]
        public async Task<IActionResult> Get(Guid documentId, DocumentArtifact artifact, [FromQuery] string token, CancellationToken ct)
        {
            // Validate token first
            if (!_tokenService.TryValidate(token, out var tDoc, out var tArtifactStr) || tDoc != documentId)
                return NotFound();
            if (!DocumentArtifactExtensions.TryParse(tArtifactStr, out var tokenArtifact) || tokenArtifact != artifact)
                return NotFound();

            var view = await _query.Query<DocumentView>()
                .Where(d => d.Id == documentId && !d.Deleted)
                .FirstOrDefaultAsync(ct);
            if (view == null) return NotFound();

            string? fullPath = artifact switch
            {
                DocumentArtifact.File => view.FilePath,
                DocumentArtifact.Preview => view.PreviewPath,
                DocumentArtifact.Thumb => view.ThumbnailPath,
                DocumentArtifact.Metadata => view.MetadataPath,
                DocumentArtifact.Archive => view.ArchivePath,
                _ => null
            };
            if (fullPath == null) return NotFound();

            // Load encryption keys when necessary (before attempting small artifact shortcut)
            DocumentEncryptionKeysAdded? encryptionKeys = null;
            if (_settings.Enabled && view.Encrypted)
            {
                var events = await _query.Events.FetchStreamAsync(documentId, token: ct);
                encryptionKeys = events.Select(e => e.Data).OfType<DocumentEncryptionKeysAdded>().LastOrDefault();
                if (encryptionKeys == null) return NotFound();
            }

            // Prepare ETag / Last-Modified based on document timestamps (deterministic)
            DateTime baseTimestamp = (view.ProcessedAt ?? view.UploadedAt).ToUniversalTime();
            var etag = ComputeDeterministicEtag(view.Id, artifact.ToWireValue(), baseTimestamp);
            Response.Headers["ETag"] = etag;
            Response.Headers["Last-Modified"] = baseTimestamp.ToString("R");

            // Conditional request handling: If client already has the current representation, return 304
            if (Request.Headers.TryGetValue("If-None-Match", out var inm) && !string.IsNullOrEmpty(inm))
            {
                if (inm.ToString().Split(',').Select(s => s.Trim()).Any(s => s == etag || s == "*") )
                {
                    return StatusCode(StatusCodes.Status304NotModified);
                }
            }
            else if (Request.Headers.TryGetValue("If-Modified-Since", out var imsHeader) && DateTimeOffset.TryParse(imsHeader.ToString(), out var ims))
            {
                if (baseTimestamp <= ims.UtcDateTime) return StatusCode(StatusCodes.Status304NotModified);
            }

            // Small artifact fast-path (preview/thumb)  uses TryGetArtifactBytesAsync which already caches encrypted bytes
            if (artifact == DocumentArtifact.Preview || artifact == DocumentArtifact.Thumb)
            {
                try
                {
                    var plain = await TryGetArtifactBytesAsync(view, artifact, fullPath, encryptionKeys, ct);
                    if (plain != null && plain.Length > 0)
                    {
                        // Set content-related headers and return
                        ApplyClientCacheHeaders();
                        var mime = artifact == DocumentArtifact.Thumb ? "image/webp" : "application/pdf"; // preview is pdf
                        // Include ETag/Last-Modified on successful response (already set above)
                        return File(plain, mime);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Preview/Thumb fast-path failed for document {DocumentId} artifact {Artifact}", documentId, artifact);
                    // fall back to streaming below
                }
            }

            try
            {
                // Use the streaming artifact streamer to obtain a write delegate and content type
                var artifactWire = artifact.ToWireValue();
                var (writeToAsync, contentType) = await _streamer.GetAsync(documentId, artifactWire, ct);

                ApplyClientCacheHeaders();

                // Return a PushStreamResult which will invoke the provided delegate and stream directly to response
                return new PushStreamResult(contentType, async (stream, token) =>
                {
                    try
                    {
                        await writeToAsync(stream, token);
                    }
                    catch (CryptographicException cex)
                    {
                        _logger.LogWarning(cex, "Decrypt failed for document {DocumentId} artifact {Artifact}", documentId, artifactWire);
                        // swallow to avoid aborting server; client gets truncated/not found style
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Streaming failed for document {DocumentId} artifact {Artifact}", documentId, artifactWire);
                    }
                });
            }
            catch (FileNotFoundException)
            {
                return NotFound();
            }
            catch (OperationCanceledException)
            {
                return NotFound();
            }
            catch (CryptographicException cex)
            {
                _logger.LogWarning(cex, "Decrypt failed (pre-stream) for document {DocumentId} artifact {Artifact}", documentId, artifact);
                return NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delivery failed for document {DocumentId} artifact {Artifact}", documentId, artifact);
                return NotFound();
            }
        }

        /// <summary>
        /// Adds cache headers to the HTTP response instructing clients and proxies to cache the response
        /// for one year and treat it as immutable. Used for delivered artifacts (stable content).
        /// </summary>
        private void ApplyClientCacheHeaders()
        {
            Response.Headers["Cache-Control"] = $"public, max-age={OneYearSeconds}, immutable";
            Response.Headers["Expires"] = DateTime.UtcNow.AddSeconds(OneYearSeconds).ToString("R");

            // Help browsers/CDNs accept cross-origin embedding/fetching of delivered artifacts.
            // If an Origin header is present, echo it explicitly; otherwise allow all origins.
            var origin = Request.Headers.ContainsKey("Origin") ? Request.Headers["Origin"].ToString() : "*";
            // When echoing origin, also set Vary to Origin so caches key correctly
            if (origin != "*")
            {
                Response.Headers["Access-Control-Allow-Origin"] = origin;
                Response.Headers.Append("Vary", "Origin");
            }
            else
            {
                Response.Headers["Access-Control-Allow-Origin"] = "*";
            }

            // Cross-Origin-Resource-Policy to allow embedding/fetching from other origins (CDNs)
            Response.Headers["Cross-Origin-Resource-Policy"] = "cross-origin";
        }

        /// <summary>
        /// Attempts to retrieve the raw bytes for the requested artifact. When encryption is enabled this method
        /// will:
        /// - fetch an encrypted blob from the configured storage (with caching),
        /// - unwrap the DEK using the master key and the per-artifact wrap metadata,
        /// - decrypt the blob using the appropriate algorithm/version and return the plaintext bytes.
        ///
        /// When encryption is disabled or when the document is not encrypted the method simply returns the remote bytes from storage.
        /// </summary>
        /// <param name="view">The document projection containing artifact paths and encryption flag.</param>
        /// <param name="artifact">Requested artifact type.</param>
        /// <param name="fullPath">Full storage path/object key for the artifact.</param>
        /// <param name="encryptionKeys">Event payload that contains wrapped DEKs for artifacts or null.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Plaintext bytes of the artifact, or null when retrieval or decryption fails.</returns>
        private async Task<byte[]?> TryGetArtifactBytesAsync(DocumentView view, DocumentArtifact artifact, string fullPath, DocumentEncryptionKeysAdded? encryptionKeys, CancellationToken ct)
        {
            // Only attempt decryption when encryption feature is enabled AND the document actually has encryption keys
            if (_settings.Enabled && encryptionKeys != null)
            {
                byte[]? encrypted = null;
                var artifactWire = artifact.ToWireValue();
                var (cacheKey, shouldCache) = CacheKeyHelper.CacheKeyFor(artifactWire, view.Id);

                if (shouldCache)
                {
                    var cached = await _cache.GetAsync<byte[]>(cacheKey, ct);
                    if (cached != null && cached.Length > 0) encrypted = cached;
                }

                if (encrypted == null)
                {
                    encrypted = await _storage.GetFileAsync(fullPath, ct);
                    if (shouldCache && encrypted != null && encrypted.Length > 0)
                    {
                        await _cache.SetAsync(cacheKey, encrypted, TimeSpan.FromMinutes(_settings.CacheTtlMinutes), ct: ct);
                    }
                }

                if (encrypted == null || encrypted.Length < 1) return null;

                var entry = encryptionKeys.Artifacts.FirstOrDefault(a => a.Artifact == artifactWire);
                if (entry == null || entry.WrappedDek.Length < 32 + 16) return null;

                var wrapped = entry.WrappedDek.AsSpan(0, 32).ToArray();
                var wrapTag = entry.WrappedDek.AsSpan(32, 16).ToArray();

                var dek = new byte[32];
                try
                {
                    using var aesWrap = new AesGcm(Convert.FromBase64String(_settings.MasterKeyBase64), 16);
                    aesWrap.Decrypt(entry.WrapNonce, wrapped, wrapTag, dek, Encoding.UTF8.GetBytes("DEK:" + artifactWire));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to unwrap DEK for document {DocumentId} artifact {Artifact}", view.Id, artifactWire);
                    return null;
                }

                byte version = encrypted[0];
                if (version == 1)
                {
                    if (encrypted.Length < 1 + 12 + 16) return null;

                    var nonce = encrypted.AsSpan(1, 12).ToArray();
                    var tag = encrypted.AsSpan(encrypted.Length - 16, 16).ToArray();
                    var cipher = encrypted.AsSpan(13, encrypted.Length - 13 - 16).ToArray();

                    try
                    {
                        using var aes = new AesGcm(dek, 16);
                        var plain = new byte[cipher.Length];
                        aes.Decrypt(nonce, cipher, tag, plain);
                        return plain;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "AES-GCM decrypt failed for document {DocumentId} artifact {Artifact} (v1)", view.Id, artifactWire);
                        return null;
                    }
                }

                if (version == 2)
                {
                    if (encrypted.Length < 1 + 16 + 32) return null;
                    var iv = encrypted.AsSpan(1, 16).ToArray();
                    var mac = encrypted.AsSpan(encrypted.Length - 32, 32).ToArray();
                    var cipherLength = encrypted.Length - 1 - iv.Length - mac.Length;
                    if (cipherLength < 0) return null;
                    var cipher = encrypted.AsSpan(1 + iv.Length, cipherLength).ToArray();

                    var (encKey, macKey) = DeriveSubKeys(dek);
                    using (var hmac = new HMACSHA256(macKey))
                    {
                        hmac.TransformBlock(iv, 0, iv.Length, null, 0);
                        hmac.TransformBlock(cipher, 0, cipher.Length, null, 0);
                        hmac.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                        var computed = hmac.Hash;
                        if (computed == null || !CryptographicOperations.FixedTimeEquals(computed, mac))
                        {
                            _logger.LogWarning("MAC mismatch for document {DocumentId} artifact {Artifact}", view.Id, artifactWire);
                            return null;
                        }
                    }

                    try
                    {
                        using var aes = Aes.Create();
                        aes.Key = encKey;
                        aes.IV = iv;
                        aes.Mode = CipherMode.CBC;
                        aes.Padding = PaddingMode.PKCS7;
                        using var decryptor = aes.CreateDecryptor();
                        return decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "AES-CBC decrypt failed for document {DocumentId} artifact {Artifact} (v2)", view.Id, artifactWire);
                        return null;
                    }
                }

                return null;
            }

            // Fallback: return raw bytes from storage (no encryption/decryption required)
            try
            {
                return await _storage.GetFileAsync(fullPath, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch raw artifact {Artifact} for document {DocumentId}", artifact, view.Id);
                return null;
            }
        }

        /// <summary>
        /// Derives separate encryption and MAC keys from the document encryption key (DEK).
        /// Uses HMAC-SHA256 on the DEK with labels "enc" and "mac".
        /// </summary>
        /// <param name="dek">Document encryption key (32 bytes).</param>
        /// <returns>Tuple with encryption key and MAC key.</returns>
        private static (byte[] EncKey, byte[] MacKey) DeriveSubKeys(byte[] dek)
        {
            using var hmac = new HMACSHA256(dek);
            var encKey = hmac.ComputeHash(Encoding.UTF8.GetBytes("enc"));
            var macKey = hmac.ComputeHash(Encoding.UTF8.GetBytes("mac"));
            return (encKey, macKey);
        }

        /// <summary>
        /// Resolves the content type for the given artifact. For the main file artifact the method will try
        /// to read MIME type from metadata (if present); otherwise falls back to an artifact-specific default.
        /// </summary>
        /// <param name="view">Document projection containing metadata path</param>
        /// <param name="artifact">Requested artifact</param>
        /// <param name="encryptionKeys">Encryption key event payload (may be required to decrypt metadata)</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Content type (MIME) string to use for the HTTP response.</returns>
        private async Task<string> GetContentTypeAsync(DocumentView view, DocumentArtifact artifact, DocumentEncryptionKeysAdded? encryptionKeys, CancellationToken ct)
        {
            if (artifact == DocumentArtifact.File)
            {
                var mime = await TryGetMimeTypeFromMetadataAsync(view, encryptionKeys, ct);
                if (!string.IsNullOrWhiteSpace(mime)) return mime;
            }

            return GetDefaultContentType(artifact);
        }

        /// <summary>
        /// Attempts to read a JSON metadata artifact and extract the stored MimeType property.
        /// If the metadata cannot be read, deserialized or does not contain a MimeType the method returns null.
        /// </summary>
        /// <param name="view">Document projection containing the MetadataPath.</param>
        /// <param name="encryptionKeys">Encryption metadata required to decrypt the metadata artifact if encryption is enabled.</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Mime type string from metadata or null when unavailable.</returns>
        private async Task<string?> TryGetMimeTypeFromMetadataAsync(DocumentView view, DocumentEncryptionKeysAdded? encryptionKeys, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(view.MetadataPath)) return null;

            try
            {
                var metadataBytes = await TryGetArtifactBytesAsync(view, DocumentArtifact.Metadata, view.MetadataPath, encryptionKeys, ct);
                if (metadataBytes == null) return null;

                var metadata = JsonSerializer.Deserialize<DocumentMetadata>(metadataBytes, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return string.IsNullOrWhiteSpace(metadata?.MimeType) ? null : metadata.MimeType;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse metadata for document {DocumentId}", view.Id);
                return null;
            }
        }

        /// <summary>
        /// Returns a safe default content type for artifacts that do not provide explicit MIME information.
        /// </summary>
        /// <param name="artifact">Artifact type</param>
        /// <returns>Default MIME type for the artifact</returns>
        private static string GetDefaultContentType(DocumentArtifact artifact) => artifact switch
        {
            DocumentArtifact.Thumb => "image/webp",
            DocumentArtifact.Metadata => "application/json",
            DocumentArtifact.Preview or DocumentArtifact.Archive or DocumentArtifact.File => "application/pdf",
            _ => "application/octet-stream"
        };

        /******* Helper methods for ETag generation and conditional handling *******/
        private static string ComputeDeterministicEtag(Guid documentId, string artifactWire, DateTime baseTimestamp)
        {
            // Use SHA256 over a deterministic string; return quoted value suitable for ETag header
            var src = string.Concat(documentId.ToString("N"), "|", artifactWire, "|", baseTimestamp.ToString("O"));
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(src));
            var b64 = Convert.ToBase64String(hash).TrimEnd('=');
            // Use W/ for weak ETag? We use strong ETag here
            return '"' + b64 + '"';
        }
    }
}
