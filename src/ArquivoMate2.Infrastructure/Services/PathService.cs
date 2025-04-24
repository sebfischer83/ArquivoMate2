using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public string GetDocumentUploadFilePath(Guid documentId, string userId)
        {
            throw new NotImplementedException();
        }

        public string GetDocumentUploadPath(string userId)
        {
            return Path.Combine(_paths.Upload, userId);
        }
    }
}
