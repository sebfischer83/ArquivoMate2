using ArquivoMate2.Application.Configuration;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Document;
using ArquivoMate2.Infrastructure.Persistance;
using Marten;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ArquivoMate2.Infrastructure.Services
{
    public class DocumentArtifactStreamer : IDocumentArtifactStreamer
    {
        private readonly IQuerySession _query;
        private readonly IStorageProvider _storage;
        private readonly EncryptionSettings _enc;

        public DocumentArtifactStreamer(IQuerySession query, IStorageProvider storage, EncryptionSettings enc)
        {
            _query = query;
            _storage = storage;
            _enc = enc;
        }

        private static readonly string[] Artifacts = ["file","preview","thumb","metadata","archive"];

        public async Task<(byte[] Content, string ContentType)> GetAsync(Guid documentId, string artifact, CancellationToken ct)
        {
            if (!Artifacts.Contains(artifact)) throw new FileNotFoundException();
            var view = await _query.Query<DocumentView>().Where(d => d.Id == documentId && !d.Deleted).FirstOrDefaultAsync(ct);
            if (view == null) throw new FileNotFoundException();

            string? path = artifact switch
            {
                "file" => view.FilePath,
                "preview" => view.PreviewPath,
                "thumb" => view.ThumbnailPath,
                "metadata" => view.MetadataPath,
                "archive" => view.ArchivePath,
                _ => null
            };
            if (string.IsNullOrEmpty(path)) throw new FileNotFoundException();

            if (_enc.Enabled && view.Encrypted)
            {
                var bytes = await _storage.GetFileAsync(path, ct);
                if (bytes.Length < 1) throw new FileNotFoundException();

                var events = await _query.Events.FetchStreamAsync(documentId, token: ct);
                var keysEvent = events.Select(e => e.Data).OfType<DocumentEncryptionKeysAdded>().LastOrDefault();
                if (keysEvent == null) throw new FileNotFoundException();
                var entry = keysEvent.Artifacts.FirstOrDefault(a => a.Artifact == artifact);
                if (entry == null || entry.WrappedDek.Length < 48) throw new FileNotFoundException();
                var wrapped = entry.WrappedDek.AsSpan(0, 32).ToArray();
                var wrapTag = entry.WrappedDek.AsSpan(32, 16).ToArray();
                var dek = new byte[32];
                using (var aesWrap = new AesGcm(Convert.FromBase64String(_enc.MasterKeyBase64), 16))
                {
                    aesWrap.Decrypt(entry.WrapNonce, wrapped, wrapTag, dek, Encoding.UTF8.GetBytes("DEK:" + artifact));
                }

                byte version = bytes[0];
                if (version == 1)
                {
                    if (bytes.Length < 1 + 12 + 16) throw new FileNotFoundException();
                    var nonce = bytes.AsSpan(1, 12).ToArray();
                    var tag = bytes.AsSpan(bytes.Length - 16, 16).ToArray();
                    var cipher = bytes.AsSpan(13, bytes.Length - 13 - 16).ToArray();

                    var plain = new byte[cipher.Length];
                    using (var aes = new AesGcm(dek, 16))
                    {
                        aes.Decrypt(nonce, cipher, tag, plain);
                    }
                    return (plain, MapContentType(artifact, path));
                }

                if (version == 2)
                {
                    if (bytes.Length < 1 + 16 + 32) throw new FileNotFoundException();
                    var iv = bytes.AsSpan(1, 16).ToArray();
                    var mac = bytes.AsSpan(bytes.Length - 32, 32).ToArray();
                    var cipherLength = bytes.Length - 1 - iv.Length - mac.Length;
                    if (cipherLength < 0) throw new FileNotFoundException();
                    var cipher = bytes.AsSpan(1 + iv.Length, cipherLength).ToArray();

                    var (encKey, macKey) = DeriveSubKeys(dek);
                    using (var hmac = new HMACSHA256(macKey))
                    {
                        hmac.TransformBlock(iv, 0, iv.Length, null, 0);
                        hmac.TransformBlock(cipher, 0, cipher.Length, null, 0);
                        hmac.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                        var computed = hmac.Hash ?? Array.Empty<byte>();
                        if (!CryptographicOperations.FixedTimeEquals(computed, mac)) throw new FileNotFoundException();
                    }

                    using var aes = Aes.Create();
                    aes.Key = encKey;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    using var decryptor = aes.CreateDecryptor();
                    var plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
                    return (plain, MapContentType(artifact, path));
                }

                throw new FileNotFoundException();
            }

            var plainBytes = await _storage.GetFileAsync(path, ct);
            return (plainBytes, MapContentType(artifact, path));
        }

        private static string MapContentType(string artifact, string path) => artifact switch
        {
            "thumb" => "image/webp",
            "metadata" => "application/json",
            "preview" or "archive" or "file" => "application/pdf",
            _ => "application/octet-stream"
        };

        private static (byte[] EncKey, byte[] MacKey) DeriveSubKeys(byte[] dek)
        {
            using var hmac = new HMACSHA256(dek);
            var encKey = hmac.ComputeHash(Encoding.UTF8.GetBytes("enc"));
            var macKey = hmac.ComputeHash(Encoding.UTF8.GetBytes("mac"));
            return (encKey, macKey);
        }
    }
}
