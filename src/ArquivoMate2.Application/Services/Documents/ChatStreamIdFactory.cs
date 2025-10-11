using System;
using System.Security.Cryptography;
using System.Text;

namespace ArquivoMate2.Application.Services.Documents
{
    internal static class ChatStreamIdFactory
    {
        public static Guid ForDocument(Guid documentId, string userId)
        {
            if (documentId == Guid.Empty)
            {
                throw new ArgumentException("Document id must be provided", nameof(documentId));
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("User id must be provided", nameof(userId));
            }

            using var md5 = MD5.Create();
            var bytes = Encoding.UTF8.GetBytes($"document-chat:{documentId:N}:{userId}");
            var hash = md5.ComputeHash(bytes);
            return new Guid(hash);
        }

        public static Guid ForCatalog(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("User id must be provided", nameof(userId));
            }

            using var md5 = MD5.Create();
            var bytes = Encoding.UTF8.GetBytes($"catalog-chat:{userId}");
            var hash = md5.ComputeHash(bytes);
            return new Guid(hash);
        }
    }
}
