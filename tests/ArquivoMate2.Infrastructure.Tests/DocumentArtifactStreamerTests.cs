using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Configuration;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Document;
using ArquivoMate2.Infrastructure.Persistance;
using ArquivoMate2.Infrastructure.Services;
using Marten;
using Marten.Events;
using Moq;
using Xunit;

namespace ArquivoMate2.Infrastructure.Tests
{
    public class DocumentArtifactStreamerTests
    {
        [Fact]
        public async Task GetAsync_ReturnsPlainArtifactStream()
        {
            var documentId = Guid.NewGuid();
            var artifact = "file";
            var view = new DocumentView
            {
                Id = documentId,
                FilePath = "doc/file.pdf",
                Encrypted = false,
                Deleted = false
            };

            var query = new Mock<IQuerySession>();
            query.Setup(q => q.LoadAsync<DocumentView>(documentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(view);

            var storage = new Mock<IStorageProvider>();
            var plainBytes = Encoding.UTF8.GetBytes("plain document content");
            storage.Setup(s => s.StreamFileAsync(view.FilePath, It.IsAny<Func<Stream, CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
                .Returns<string, Func<Stream, CancellationToken, Task>, CancellationToken>((_, callback, token) =>
                    Task.Run(async () =>
                    {
                        await using var source = new MemoryStream(plainBytes, writable: false);
                        await callback(source, token).ConfigureAwait(false);
                    }));

            var streamer = new DocumentArtifactStreamer(query.Object, storage.Object, new EncryptionSettings { Enabled = false });

            var (writeAsync, contentType) = await streamer.GetAsync(documentId, artifact, CancellationToken.None);

            await using var destination = new MemoryStream();
            await writeAsync(destination, CancellationToken.None);

            Assert.Equal("application/pdf", contentType);
            Assert.Equal(plainBytes, destination.ToArray());

            storage.Verify(s => s.StreamFileAsync(view.FilePath, It.IsAny<Func<Stream, CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetAsync_DecryptsVersion2ArtifactStream()
        {
            var documentId = Guid.NewGuid();
            var artifact = "file";
            var masterKey = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
            var encryptionSettings = new EncryptionSettings
            {
                Enabled = true,
                MasterKeyBase64 = Convert.ToBase64String(masterKey)
            };

            var view = new DocumentView
            {
                Id = documentId,
                FilePath = "doc/file.enc",
                Encrypted = true,
                Deleted = false
            };

            var query = new Mock<IQuerySession>();
            query.Setup(q => q.LoadAsync<DocumentView>(documentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(view);

            var eventStore = new Mock<IEventStore>();
            query.SetupGet(q => q.Events).Returns(eventStore.Object);

            var dek = Enumerable.Range(33, 32).Select(i => (byte)i).ToArray();
            var wrapNonce = Enumerable.Range(65, 12).Select(i => (byte)i).ToArray();
            var wrapped = new byte[dek.Length];
            var wrapTag = new byte[16];
            using (var aesWrap = new AesGcm(masterKey, 16))
            {
                aesWrap.Encrypt(wrapNonce, dek, wrapped, wrapTag, Encoding.UTF8.GetBytes("DEK:" + artifact));
            }
            var wrappedDek = new byte[wrapped.Length + wrapTag.Length];
            Buffer.BlockCopy(wrapped, 0, wrappedDek, 0, wrapped.Length);
            Buffer.BlockCopy(wrapTag, 0, wrappedDek, wrapped.Length, wrapTag.Length);

            var keysEvent = new DocumentEncryptionKeysAdded(documentId,
                new[] { new EncryptedArtifactKey(artifact, wrappedDek, wrapNonce, "AES-256-CBC-HMACSHA256", "2") },
                DateTime.UtcNow);

            var eventMock = new Mock<IEvent>();
            eventMock.SetupGet(e => e.Data).Returns(keysEvent);

            eventStore.Setup(e => e.FetchStreamAsync(documentId, 0, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<IEvent> { eventMock.Object });

            var plainBytes = Encoding.UTF8.GetBytes("super secret document");
            var (encKey, macKey) = DeriveSubKeys(dek);
            var iv = Enumerable.Range(97, 16).Select(i => (byte)i).ToArray();
            byte[] cipher;
            using (var aes = Aes.Create())
            {
                aes.Key = encKey;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using var encryptor = aes.CreateEncryptor();
                cipher = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            }

            byte[] mac;
            using (var hmac = new HMACSHA256(macKey))
            {
                hmac.TransformBlock(iv, 0, iv.Length, null, 0);
                hmac.TransformBlock(cipher, 0, cipher.Length, null, 0);
                hmac.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                mac = hmac.Hash ?? Array.Empty<byte>();
            }

            var encrypted = new byte[1 + iv.Length + cipher.Length + mac.Length];
            encrypted[0] = 2;
            Buffer.BlockCopy(iv, 0, encrypted, 1, iv.Length);
            Buffer.BlockCopy(cipher, 0, encrypted, 1 + iv.Length, cipher.Length);
            Buffer.BlockCopy(mac, 0, encrypted, 1 + iv.Length + cipher.Length, mac.Length);

            var storage = new Mock<IStorageProvider>();
            storage.Setup(s => s.StreamFileAsync(view.FilePath, It.IsAny<Func<Stream, CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
                .Returns<string, Func<Stream, CancellationToken, Task>, CancellationToken>((_, callback, token) =>
                    Task.Run(async () =>
                    {
                        await using var source = new MemoryStream(encrypted, writable: false);
                        await callback(source, token).ConfigureAwait(false);
                    }));

            var streamer = new DocumentArtifactStreamer(query.Object, storage.Object, encryptionSettings);

            var (writeAsync, contentType) = await streamer.GetAsync(documentId, artifact, CancellationToken.None);

            await using var destination = new MemoryStream();
            await writeAsync(destination, CancellationToken.None);

            Assert.Equal("application/pdf", contentType);
            Assert.Equal(plainBytes, destination.ToArray());
        }

        private static (byte[] EncKey, byte[] MacKey) DeriveSubKeys(byte[] dek)
        {
            using var hmac = new HMACSHA256(dek);
            var encKey = hmac.ComputeHash(Encoding.UTF8.GetBytes("enc"));
            var macKey = hmac.ComputeHash(Encoding.UTF8.GetBytes("mac"));
            return (encKey, macKey);
        }
    }
}
