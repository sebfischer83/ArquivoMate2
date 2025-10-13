using ArquivoMate2.Application.Configuration;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Document;
using Microsoft.Extensions.Options;
using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services.Encryption
{
    public class CustomEncryptionService : ICustomEncryptionService
    {
        private readonly byte[] _kek;
        public bool IsEnabled { get; }
        private readonly IStorageProvider _inner;

        public CustomEncryptionService(IOptions<CustomEncryptionSettings> settings, IStorageProvider inner)
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
            using var ms = new MemoryStream(content, writable: false);
            return await SaveAsync(userId, documentId, filename, ms, artifact, ct).ConfigureAwait(false);
        }

        public async Task<(string fullPath, EncryptedArtifactKey? key)> SaveAsync(string userId, Guid documentId, string filename, Stream content, string artifact, CancellationToken ct = default)
        {
            if (content.CanSeek)
            {
                content.Position = 0;
            }

            if (!IsEnabled)
            {
                var pPlain = await _inner.SaveFileAsync(userId, documentId, filename, content, artifact, ct).ConfigureAwait(false);
                return (pPlain, null);
            }

            var dek = RandomNumberGenerator.GetBytes(32); // 256 bit
            var (encKey, macKey) = DeriveSubKeys(dek);
            var iv = RandomNumberGenerator.GetBytes(16);

            await using var cipherStream = CreateTempFileStream();
            using (var aes = Aes.Create())
            {
                aes.Key = encKey;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var encryptor = aes.CreateEncryptor();
                using var cryptoStream = new CryptoStream(cipherStream, encryptor, CryptoStreamMode.Write, leaveOpen: true);
                await content.CopyToAsync(cryptoStream, 81920, ct).ConfigureAwait(false);
                cryptoStream.FlushFinalBlock();
            }

            cipherStream.Position = 0;

            var buffer = ArrayPool<byte>.Shared.Rent(81920);
            try
            {
                using var hmac = new HMACSHA256(macKey);
                hmac.TransformBlock(iv, 0, iv.Length, null, 0);

                int read;
                while ((read = await cipherStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
                {
                    hmac.TransformBlock(buffer, 0, read, null, 0);
                }
                hmac.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                var mac = hmac.Hash ?? throw new InvalidOperationException("Failed to compute HMAC for encrypted content.");

                cipherStream.Position = 0;

                await using var envelopeStream = CreateTempFileStream();
                await envelopeStream.WriteAsync(new[] { (byte)2 }, ct).ConfigureAwait(false);
                await envelopeStream.WriteAsync(iv, ct).ConfigureAwait(false);

                while ((read = await cipherStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
                {
                    await envelopeStream.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                }

                await envelopeStream.WriteAsync(mac, ct).ConfigureAwait(false);
                envelopeStream.Position = 0;

                var encryptedFilename = filename + ".enc";
                var fullPath = await _inner.SaveFileAsync(userId, documentId, encryptedFilename, envelopeStream, artifact, ct).ConfigureAwait(false);

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

                var keyRecord = new EncryptedArtifactKey(artifact, wrappedConcat, wrapNonce, "AES-256-CBC-HMACSHA256", "2");
                return (fullPath, keyRecord);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static (byte[] EncKey, byte[] MacKey) DeriveSubKeys(byte[] dek)
        {
            using var hmac = new HMACSHA256(dek);
            var encKey = hmac.ComputeHash(Encoding.UTF8.GetBytes("enc"));
            var macKey = hmac.ComputeHash(Encoding.UTF8.GetBytes("mac"));
            return (encKey, macKey);
        }

        private static FileStream CreateTempFileStream()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            return new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        }
    }
}
