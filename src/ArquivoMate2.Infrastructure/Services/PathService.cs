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
    public class PathService : IPathService
    {
        private readonly Paths _paths;

        public PathService(Paths paths)
        {
            _paths = paths;
        }
    
        /// <summary>
        /// 
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="documentId"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
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

        public string GetDocumentUploadPath(string userId)
        {
            return Path.Combine(_paths.Upload, userId);
        }
    }
}
