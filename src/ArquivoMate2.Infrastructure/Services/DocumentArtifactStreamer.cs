using ArquivoMate2.Application.Configuration;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Document;
using ArquivoMate2.Domain.ReadModels;
using Marten;
using Marten.Events;
using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services
{
    public class DocumentArtifactStreamer : IDocumentArtifactStreamer
    {
        private readonly IQuerySession _query;
        private readonly IStorageProvider _storage;
        private readonly EncryptionSettings _enc;
        private readonly IDocumentEncryptionKeysProvider _keysProvider;

        public DocumentArtifactStreamer(IQuerySession query, IStorageProvider storage, IDocumentEncryptionKeysProvider keysProvider, EncryptionSettings enc)
        {
            _query = query;
            _storage = storage;
            _enc = enc;
            _keysProvider = keysProvider;
        }

        private static readonly string[] Artifacts = ["file", "preview", "thumb", "metadata", "archive"];

        public async Task<(Func<Stream, CancellationToken, Task> WriteToAsync, string ContentType)> GetAsync(Guid documentId, string artifact, CancellationToken ct)
        {
            if (!Artifacts.Contains(artifact)) throw new FileNotFoundException();

            var view = await _query.LoadAsync<DocumentView>(documentId, ct).ConfigureAwait(false);
            if (view == null || view.Deleted) throw new FileNotFoundException();

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
                var keysEvent = await _keysProvider.GetLatestAsync(documentId, ct).ConfigureAwait(false);
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

                return (async (destination, writeCt) =>
                {
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, writeCt);
                    await _storage.StreamFileAsync(path, (encryptedStream, token) => StreamEncryptedArtifactAsync(encryptedStream, destination, dek, token), linked.Token).ConfigureAwait(false);
                }, MapContentType(artifact, path));
            }

            return (async (destination, writeCt) =>
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, writeCt);
                await _storage.StreamFileAsync(path, (source, token) => source.CopyToAsync(destination, 81920, token), linked.Token).ConfigureAwait(false);
            }, MapContentType(artifact, path));
        }

        private static async Task StreamEncryptedArtifactAsync(Stream encryptedStream, Stream destination, byte[] dek, CancellationToken ct)
        {
            var version = await ReadByteAsync(encryptedStream, ct).ConfigureAwait(false);
            if (version == -1) throw new FileNotFoundException();

            try
            {
                switch (version)
                {
                    case 1:
                        await StreamEncryptedVersion1Async(encryptedStream, destination, dek, ct).ConfigureAwait(false);
                        break;
                    case 2:
                        await StreamEncryptedVersion2Async(encryptedStream, destination, dek, ct).ConfigureAwait(false);
                        break;
                    default:
                        throw new FileNotFoundException();
                }
            }
            catch (CryptographicException ex)
            {
                throw new FileNotFoundException("Unable to decrypt artifact.", ex);
            }
        }

        private static async Task StreamEncryptedVersion1Async(Stream encryptedStream, Stream destination, byte[] dek, CancellationToken ct)
        {
            var nonce = await ReadExactlyAsync(encryptedStream, 12, ct).ConfigureAwait(false);

            await using var bufferStream = new MemoryStream();
            await encryptedStream.CopyToAsync(bufferStream, 81920, ct).ConfigureAwait(false);
            if (bufferStream.Length < 16) throw new FileNotFoundException();

            if (!bufferStream.TryGetBuffer(out var segment))
            {
                segment = new ArraySegment<byte>(bufferStream.ToArray());
            }

            var array = segment.Array ?? throw new FileNotFoundException();
            var cipherLength = segment.Count - 16;
            if (cipherLength < 0) throw new FileNotFoundException();

            var cipher = new byte[cipherLength];
            Buffer.BlockCopy(array, segment.Offset, cipher, 0, cipherLength);
            var tag = new byte[16];
            Buffer.BlockCopy(array, segment.Offset + cipherLength, tag, 0, 16);

            using var aes = new AesGcm(dek, 16);
            var plaintext = new byte[cipher.Length];
            aes.Decrypt(nonce, cipher, tag, plaintext);
            await destination.WriteAsync(plaintext.AsMemory(0, plaintext.Length), ct).ConfigureAwait(false);
        }

        private static async Task StreamEncryptedVersion2Async(Stream encryptedStream, Stream destination, byte[] dek, CancellationToken ct)
        {
            var iv = await ReadExactlyAsync(encryptedStream, 16, ct).ConfigureAwait(false);
            var (encKey, macKey) = DeriveSubKeys(dek);

            using var hmac = new HMACSHA256(macKey);
            hmac.TransformBlock(iv, 0, iv.Length, null, 0);

            using var aes = Aes.Create();
            aes.Key = encKey;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            await using var cryptoStream = new CryptoStream(destination, aes.CreateDecryptor(), CryptoStreamMode.Write, leaveOpen: true);

            var buffer = ArrayPool<byte>.Shared.Rent(81920 + 32);
            var buffered = 0;
            int read;

            try
            {
                while ((read = await encryptedStream.ReadAsync(buffer.AsMemory(buffered, buffer.Length - buffered), ct).ConfigureAwait(false)) > 0)
                {
                    buffered += read;
                    if (buffered <= 32)
                    {
                        continue;
                    }

                    var cipherCount = buffered - 32;
                    hmac.TransformBlock(buffer, 0, cipherCount, null, 0);
                    await cryptoStream.WriteAsync(buffer.AsMemory(0, cipherCount), ct).ConfigureAwait(false);

                    Buffer.BlockCopy(buffer, cipherCount, buffer, 0, 32);
                    buffered = 32;
                }

                if (buffered != 32)
                {
                    throw new FileNotFoundException();
                }

                var mac = new byte[32];
                Buffer.BlockCopy(buffer, 0, mac, 0, 32);

                hmac.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                var computed = hmac.Hash ?? Array.Empty<byte>();
                if (!CryptographicOperations.FixedTimeEquals(computed, mac))
                {
                    throw new FileNotFoundException();
                }

                cryptoStream.FlushFinalBlock();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static async Task<int> ReadByteAsync(Stream stream, CancellationToken ct)
        {
            var buffer = new byte[1];
            var read = await stream.ReadAsync(buffer.AsMemory(0, 1), ct).ConfigureAwait(false);
            return read == 1 ? buffer[0] : -1;
        }

        private static async Task<byte[]> ReadExactlyAsync(Stream stream, int count, CancellationToken ct)
        {
            var buffer = new byte[count];
            var offset = 0;
            while (offset < count)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), ct).ConfigureAwait(false);
                if (read == 0)
                {
                    throw new FileNotFoundException();
                }
                offset += read;
            }
            return buffer;
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
