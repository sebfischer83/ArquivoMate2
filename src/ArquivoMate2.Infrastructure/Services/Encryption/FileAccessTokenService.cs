using System.Security.Cryptography;
using System.Text;
using ArquivoMate2.Application.Configuration;
using ArquivoMate2.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace ArquivoMate2.Infrastructure.Services.Encryption
{
    public class FileAccessTokenService : IFileAccessTokenService
    {
        private readonly byte[] _kek;
        private readonly CustomEncryptionSettings _settings;

        public FileAccessTokenService(IOptions<CustomEncryptionSettings> settings)
        {
            _settings = settings.Value;
            if (_settings.Enabled && !string.IsNullOrWhiteSpace(_settings.MasterKeyBase64))
            {
                _kek = Convert.FromBase64String(_settings.MasterKeyBase64);
            }
            else
            {
                _kek = Array.Empty<byte>();
            }
        }

        public string Create(Guid documentId, string artifact, DateTimeOffset expiresAt)
        {
            var exp = expiresAt.ToUnixTimeSeconds();
            var payload = $"{documentId}|{artifact}|{exp}";
            var sig = Sign(payload);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload + "|" + sig));
        }

        public bool TryValidate(string token, out Guid documentId, out string artifact)
        {
            documentId = Guid.Empty;
            artifact = string.Empty;
            try
            {
                var raw = Encoding.UTF8.GetString(Convert.FromBase64String(token));
                var parts = raw.Split('|');
                if (parts.Length != 4) return false;
                if (!Guid.TryParse(parts[0], out documentId)) return false;
                artifact = parts[1];
                if (!long.TryParse(parts[2], out var exp)) return false;
                var sig = parts[3];
                var payload = string.Join('|', parts.Take(3));
                if (!TimingSafeEquals(sig, Sign(payload))) return false;
                if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp) return false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public string CreateShareToken(Guid shareId, DateTimeOffset expiresAt)
        {
            var exp = expiresAt.ToUnixTimeSeconds();
            var payload = $"S|{shareId}|{exp}"; // Prefix distinguishes share tokens
            var sig = Sign(payload);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload + "|" + sig));
        }

        public bool TryValidateShareToken(string token, out Guid shareId, out DateTimeOffset expiresAt)
        {
            shareId = Guid.Empty;
            expiresAt = DateTimeOffset.MinValue;
            try
            {
                var raw = Encoding.UTF8.GetString(Convert.FromBase64String(token));
                var parts = raw.Split('|');
                // Expected layout after decoding: S | shareId | exp | sig -> 4 parts
                if (parts.Length != 4) return false;
                if (parts[0] != "S") return false;
                if (!Guid.TryParse(parts[1], out shareId)) return false;
                if (!long.TryParse(parts[2], out var exp)) return false;
                var sig = parts[3];
                var payload = string.Join('|', parts[0], parts[1], parts[2]);
                if (!TimingSafeEquals(sig, Sign(payload))) return false;
                expiresAt = DateTimeOffset.FromUnixTimeSeconds(exp);
                if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp) return false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private string Sign(string payload)
        {
            if (_kek.Length == 0) return string.Empty;
            using var hmac = new HMACSHA256(_kek);
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return Convert.ToBase64String(hash);
        }

        private static bool TimingSafeEquals(string a, string b)
        {
            var ba = Convert.FromBase64String(a);
            var bb = Convert.FromBase64String(b);
            if (ba.Length != bb.Length) return false;
            int diff = 0;
            for (int i = 0; i < ba.Length; i++) diff |= ba[i] ^ bb[i];
            return diff == 0;
        }
    }
}
