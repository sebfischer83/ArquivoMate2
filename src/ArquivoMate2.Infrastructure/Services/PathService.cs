using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services
{
    /// <summary>
    /// Provides helpers for building and parsing storage paths for document artifacts.
    /// </summary>
    public class PathService : IPathService
    {
        private readonly Paths _paths;

        /// <summary>
        /// Initializes a new instance of the <see cref="PathService"/> class.
        /// </summary>
        /// <param name="paths">Application path configuration.</param>
        public PathService(Paths paths)
        {
            _paths = paths;
        }
    
        /// <summary>
        /// Builds the canonical storage path segments for a document artifact.
        /// </summary>
        /// <param name="userId">The owner of the document.</param>
        /// <param name="documentId">The document identifier.</param>
        /// <param name="fileName">The file name including extension.</param>
        /// <returns>Path segments that form the storage location.</returns>
        public string[] GetStoragePath(string userId, Guid documentId, string fileName)
        {
            string[] strings = new string[6];

            var hash = GetHash(userId, documentId.ToString());

            string prefix1 = hash[0].ToString("x2");
            string prefix2 = hash[1].ToString("x2");
            string prefix3 = hash[2].ToString("x2");

            strings[0] = userId;
            strings[1] = prefix1;
            strings[2] = prefix2;
            strings[3] = prefix3;
            strings[4] = documentId.ToString();
            strings[5] = fileName;


            return strings;
        }

        /// <summary>
        /// Extracts the user portion of a persisted path.
        /// </summary>
        /// <param name="fullPath">The full storage path.</param>
        /// <returns>The first two path segments representing the user.</returns>
        public string GetUserPartFromPath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
                return string.Empty;

            // Split the path into segments
            string[] segments = fullPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            // Require at least two segments
            if (segments.Length < 2)
                return string.Empty;

            // Return the first two segments
            return $"{segments[0]}/{segments[1]}";
        }

        /// <summary>
        /// Builds a SHA1 hash used to distribute files across subdirectories.
        /// </summary>
        /// <param name="userId">User identifier.</param>
        /// <param name="documentId">Document identifier.</param>
        /// <returns>Hash bytes that determine the folder prefixes.</returns>
        private byte[] GetHash(string userId, string documentId)
        {
            var input = userId.ToString() + documentId.ToString();
            byte[] hash;
            using (var sha1 = SHA1.Create())
            {
                hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));
            }

            return hash;
        }

        /// <summary>
        /// Resolves the upload directory for the specified user.
        /// </summary>
        /// <param name="userId">User identifier.</param>
        /// <returns>The absolute path where uploads should be written.</returns>
        public string GetDocumentUploadPath(string userId)
        {
            return Path.Combine(_paths.Upload, userId);
        }
    }
}
