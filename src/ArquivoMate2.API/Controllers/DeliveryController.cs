using ArquivoMate2.Application.Configuration;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Document;
using ArquivoMate2.Infrastructure.Persistance;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using EasyCaching.Core;
using ArquivoMate2.Shared.Models; // DocumentArtifact

namespace ArquivoMate2.API.Controllers
{
    [ApiController]
    [AllowAnonymous] // Access controlled solely via signed token
    [Route("api/delivery")]    
    public class DeliveryController : ControllerBase
    {
        private readonly IQuerySession _query;
        private readonly IStorageProvider _storage;
        private readonly IFileAccessTokenService _tokenService;
        private readonly IEncryptionService _encryption;
        private readonly EncryptionSettings _settings;
        private readonly IEasyCachingProvider _cache;
        private const int OneYearSeconds = 31536000; // 365 * 24 * 60 * 60

        public DeliveryController(IQuerySession query,
            IStorageProvider storage,
            IFileAccessTokenService tokenService,
            IEncryptionService encryption,
            IOptions<EncryptionSettings> settings,
            IEasyCachingProviderFactory cacheFactory)
        {
            _query = query;
            _storage = storage;
            _tokenService = tokenService;
            _encryption = encryption;
            _settings = settings.Value;
            _cache = cacheFactory.GetCachingProvider(EasyCachingConstValue.DefaultRedisName);
        }

        /// <summary>
        /// Delivers an (optionally encrypted) artifact for a document when a valid access token is supplied.
        /// </summary>
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
            if (_settings.Enabled && !view.Encrypted) return NotFound();

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

            var artifactWire = artifact.ToWireValue();
            var cacheKey = $"encfile:{documentId}:{artifactWire}";
            if (_settings.Enabled)
            {
                byte[]? encrypted = null;
                var cached = await _cache.GetAsync<byte[]>(cacheKey);
                if (cached.HasValue) encrypted = cached.Value;
                if (encrypted == null)
                {
                    encrypted = await _storage.GetFileAsync(fullPath, ct);
                    await _cache.SetAsync(cacheKey, encrypted, TimeSpan.FromMinutes(_settings.CacheTtlMinutes));
                }
                if (encrypted.Length < 1 + 12 + 16) return NotFound();
                if (encrypted[0] != 1) return NotFound();
                var nonce = encrypted.AsSpan(1, 12).ToArray();
                var tag = encrypted.AsSpan(encrypted.Length - 16, 16).ToArray();
                var cipher = encrypted.AsSpan(13, encrypted.Length - 13 - 16).ToArray();

                var events = await _query.Events.FetchStreamAsync(documentId, token: ct);
                var keysEvent = events.Select(e => e.Data).OfType<DocumentEncryptionKeysAdded>().LastOrDefault();
                if (keysEvent == null) return NotFound();
                var entry = keysEvent.Artifacts.FirstOrDefault(a => a.Artifact == artifactWire);
                if (entry == null) return NotFound();
                if (entry.WrappedDek.Length < 32 + 16) return NotFound();
                var wrapped = entry.WrappedDek.AsSpan(0, 32).ToArray();
                var wrapTag = entry.WrappedDek.AsSpan(32, 16).ToArray();

                var dek = new byte[32];
                try
                {
                    using var aesWrap = new System.Security.Cryptography.AesGcm(Convert.FromBase64String(_settings.MasterKeyBase64), 16);
                    aesWrap.Decrypt(entry.WrapNonce, wrapped, wrapTag, dek, System.Text.Encoding.UTF8.GetBytes("DEK:" + artifactWire));
                }
                catch { return NotFound(); }

                var plain = new byte[cipher.Length];
                try
                {
                    using var aes = new System.Security.Cryptography.AesGcm(dek, 16);
                    aes.Decrypt(nonce, cipher, tag, plain);
                }
                catch { return NotFound(); }

                ApplyClientCacheHeaders();
                return File(plain, GetContentType(artifact, fullPath));
            }
            else
            {
                var plainBytes = await _storage.GetFileAsync(fullPath, ct);
                ApplyClientCacheHeaders();
                return File(plainBytes, GetContentType(artifact, fullPath));
            }
        }

        private void ApplyClientCacheHeaders()
        {
            Response.Headers["Cache-Control"] = $"public, max-age={OneYearSeconds}, immutable";
            Response.Headers["Expires"] = DateTime.UtcNow.AddSeconds(OneYearSeconds).ToString("R");
        }

        private static string GetContentType(DocumentArtifact artifact, string path) => artifact switch
        {
            DocumentArtifact.Thumb => "image/webp",
            DocumentArtifact.Metadata => "application/json",
            DocumentArtifact.Preview or DocumentArtifact.Archive or DocumentArtifact.File => "application/pdf",
            _ => "application/octet-stream"
        };
    }
}
