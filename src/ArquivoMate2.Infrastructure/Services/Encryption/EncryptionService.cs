using System.Security.Cryptography;
using System.Text;
using ArquivoMate2.Application.Configuration;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Document;
using Microsoft.Extensions.Options;

namespace ArquivoMate2.Infrastructure.Services.Encryption
{
    public class EncryptionService : IEncryptionService
    {
        private readonly byte[] _kek;
        public bool IsEnabled { get; }
        private readonly IStorageProvider _inner;

        public EncryptionService(IOptions<EncryptionSettings> settings, IStorageProvider inner)
        {
            var s = settings.Value;
            IsEnabled = s.Enabled && !string.IsNullOrWhiteSpace(s.MasterKeyBase64);
            if (IsEnabled)
            {
                _kek = Convert.FromBase64String(s.MasterKeyBase64);
                if (_kek.Length != 32)
                    throw new InvalidOperationException("MasterKey must decode to 32 bytes for AES-256.");
            }
            else
            {
                _kek = Array.Empty<byte>();
            }
            _inner = inner;
        }

        public async Task<(string fullPath, EncryptedArtifactKey? key)> SaveAsync(string userId, Guid documentId, string filename, byte[] content, string artifact, CancellationToken ct = default)
        {
            if (!IsEnabled)
            {
                var pPlain = await _inner.SaveFile(userId, documentId, filename, content, artifact);
                return (pPlain, null);
            }

            // Generate DEK (persist as byte[] because we cross an await)
            var dek = RandomNumberGenerator.GetBytes(32); // 256 bit

            // Encrypt payload with DEK (AES-GCM)
            var nonce = RandomNumberGenerator.GetBytes(12);
            var tag = new byte[16];
            var cipher = new byte[content.Length];
            using (var aes = new AesGcm(dek, 16))
            {
                aes.Encrypt(nonce, content, cipher, tag);
            }

            // Layout: [1][nonce][cipher][tag]
            byte version = 1;
            var encryptedBytes = new byte[1 + nonce.Length + cipher.Length + tag.Length];
            encryptedBytes[0] = version;
            Buffer.BlockCopy(nonce, 0, encryptedBytes, 1, nonce.Length);
            Buffer.BlockCopy(cipher, 0, encryptedBytes, 1 + nonce.Length, cipher.Length);
            Buffer.BlockCopy(tag, 0, encryptedBytes, 1 + nonce.Length + cipher.Length, tag.Length);

            var encryptedFilename = filename + ".enc";
            var fullPath = await _inner.SaveFile(userId, documentId, encryptedFilename, encryptedBytes, artifact);

            // Wrap DEK with KEK (AES-GCM); AD = "DEK:" + artifact
            var wrapNonce = RandomNumberGenerator.GetBytes(12);
            var wrapped = new byte[dek.Length];
            var wrapTag = new byte[16];
            using (var aesWrap = new AesGcm(_kek, 16))
            {
                aesWrap.Encrypt(wrapNonce, dek, wrapped, wrapTag, Encoding.UTF8.GetBytes("DEK:" + artifact));
            }
            var wrappedConcat = new byte[wrapped.Length + wrapTag.Length];
            Buffer.BlockCopy(wrapped, 0, wrappedConcat, 0, wrapped.Length);
            Buffer.BlockCopy(wrapTag, 0, wrappedConcat, wrapped.Length, wrapTag.Length);

            var keyRecord = new EncryptedArtifactKey(artifact, wrappedConcat, wrapNonce, "AES-256-GCM", "1");
            return (fullPath, keyRecord);
        }
    }
}
