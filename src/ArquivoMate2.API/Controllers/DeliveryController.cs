using ArquivoMate2.Application.Configuration;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Document;
using ArquivoMate2.Infrastructure.Persistance;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using EasyCaching.Core;

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
        private static readonly string[] Artifacts = new[] {"file","preview","thumb","metadata","archive"};

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
        /// Delivers an encrypted artifact for a document when a valid access token is supplied.
        /// </summary>
        /// <param name="documentId">Identifier of the document whose artifact should be downloaded.</param>
        /// <param name="artifact">Artifact type such as file, preview, thumb, metadata or archive.</param>
        /// <param name="token">Signed delivery token that authorises the download.</param>
        /// <param name="ct">Cancellation token forwarded from the HTTP request.</param>
        [HttpGet("{documentId:guid}/{artifact}")]
        public async Task<IActionResult> Get(Guid documentId, string artifact, [FromQuery] string token, CancellationToken ct)
        {
            // Token validation (primary security boundary)
            if (!_tokenService.TryValidate(token, out var tDoc, out var tArtifact) || tDoc != documentId || !string.Equals(artifact, tArtifact, StringComparison.OrdinalIgnoreCase))
                return NotFound();
            if (!Artifacts.Contains(artifact)) return NotFound();

            var view = await _query.Query<DocumentView>()
                .Where(d => d.Id == documentId && !d.Deleted)
                .FirstOrDefaultAsync(ct);
            if (view == null) return NotFound();
            if (_settings.Enabled && !view.Encrypted) return NotFound(); // token darf nicht unverschlüsselte Artefakte liefern

            string? fullPath = artifact switch
            {
                "file" => view.FilePath,
                "preview" => view.PreviewPath,
                "thumb" => view.ThumbnailPath,
                "metadata" => view.MetadataPath,
                "archive" => view.ArchivePath,
                _ => null
            };
            if (fullPath == null) return NotFound();

            var cacheKey = $"encfile:{documentId}:{artifact}";
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
                var entry = keysEvent.Artifacts.FirstOrDefault(a => a.Artifact == artifact);
                if (entry == null) return NotFound();
                if (entry.WrappedDek.Length < 32 + 16) return NotFound();
                var wrapped = entry.WrappedDek.AsSpan(0, 32).ToArray();
                var wrapTag = entry.WrappedDek.AsSpan(32, 16).ToArray();

                var dek = new byte[32];
                try
                {
                    using var aesWrap = new System.Security.Cryptography.AesGcm(Convert.FromBase64String(_settings.MasterKeyBase64), 16);
                    aesWrap.Decrypt(entry.WrapNonce, wrapped, wrapTag, dek, System.Text.Encoding.UTF8.GetBytes("DEK:" + artifact));
                }
                catch { return NotFound(); }

                var plain = new byte[cipher.Length];
                try
                {
                    using var aes = new System.Security.Cryptography.AesGcm(dek, 16);
                    aes.Decrypt(nonce, cipher, tag, plain);
                }
                catch { return NotFound(); }

                return File(plain, GetContentType(artifact, fullPath));
            }
            else
            {
                var plainBytes = await _storage.GetFileAsync(fullPath, ct);
                return File(plainBytes, GetContentType(artifact, fullPath));
            }
        }

        private static string GetContentType(string artifact, string path) => artifact switch
        {
            "thumb" => "image/webp",
            "metadata" => "application/json",
            "preview" or "archive" or "file" => "application/pdf",
            _ => "application/octet-stream"
        };
    }
}
