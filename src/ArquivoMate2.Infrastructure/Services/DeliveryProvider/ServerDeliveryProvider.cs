using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Configuration;
using ArquivoMate2.Infrastructure.Configuration.DeliveryProvider;
using ArquivoMate2.Application.Models;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services.DeliveryProvider
{
    /// <summary>
    /// Delivery provider that routes access through the application API. Useful when the server
    /// should always deliver artifacts (for example to apply authorization or decryption).
    /// </summary>
    public class ServerDeliveryProvider : IDeliveryProvider
    {
        private readonly IFileAccessTokenService _tokenService;
        private readonly AppSettings _appSettings;
        private readonly CustomEncryptionSettings _encryptionSettings;
        private readonly ArquivoMate2.Application.Interfaces.IPathService _pathService;

        public ServerDeliveryProvider(
            IFileAccessTokenService tokenService,
            AppSettings appSettings,
            CustomEncryptionSettings encryptionSettings,
            ArquivoMate2.Application.Interfaces.IPathService pathService)
        {
            _tokenService = tokenService;
            _appSettings = appSettings;
            _encryptionSettings = encryptionSettings;
            _pathService = pathService;
        }

        public Task<string> GetAccessUrl(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath)) return Task.FromResult(string.Empty);

            // Attempt to extract the document id and determine artifact from the storage path
            var segments = fullPath.Split(new[] { '/' , '\\' }, StringSplitOptions.RemoveEmptyEntries);
            Guid documentId = Guid.Empty;
            string filename = segments.Length > 0 ? segments[^1] : string.Empty;

            if (segments.Length >= 5)
            {
                // Storage path formation: userId / p1 / p2 / p3 / documentId / filename
                var candidate = segments[4];
                Guid.TryParse(candidate, out documentId);
            }

            // fallback: try to find any GUID segment
            if (documentId == Guid.Empty)
            {
                foreach (var seg in segments)
                {
                    if (Guid.TryParse(seg, out var g))
                    {
                        documentId = g;
                        break;
                    }
                }
            }

            string artifact = DetermineArtifactFromFilename(filename);

            // Build token and url
            var ttlMinutes = _encryptionSettings?.TokenTtlMinutes ?? 60;
            var expires = DateTimeOffset.UtcNow.AddMinutes(ttlMinutes);
            var token = _tokenService.Create(documentId, artifact, expires);

            var baseUrl = !string.IsNullOrWhiteSpace(_appSettings?.PublicBaseUrl)
                ? _appSettings.PublicBaseUrl.TrimEnd('/')
                : string.Empty; // allow relative URL when no base configured

            var path = $"/api/delivery/{documentId}/{artifact}?token={Uri.EscapeDataString(token)}";
            var result = string.IsNullOrEmpty(baseUrl) ? path : baseUrl + path;
            return Task.FromResult(result);
        }

        private static string DetermineArtifactFromFilename(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename)) return "file";
            var lower = filename.ToLowerInvariant();
            if (lower.EndsWith("-thumb.webp")) return "thumb";
            if (lower.EndsWith("-preview.pdf")) return "preview";
            if (lower.EndsWith("-archive.pdf")) return "archive";
            if (lower.EndsWith(".metadata") || lower.EndsWith(".meta") ) return "metadata";
            return "file";
        }
    }
}
