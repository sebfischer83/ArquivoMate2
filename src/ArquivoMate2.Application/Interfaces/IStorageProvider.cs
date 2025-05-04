using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Interfaces
{
    public interface IStorageProvider
    {
        public Task<string> SaveFile(string userId, Guid documentId, string filename, byte[] file);
    }
}
