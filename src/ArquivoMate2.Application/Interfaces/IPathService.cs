using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Interfaces
{
    public interface IPathService
    {
        string GetDocumentUploadPath(string userId);

        string[] GetStoragePath(string userId, Guid documentId, string fileName);

    }
}
