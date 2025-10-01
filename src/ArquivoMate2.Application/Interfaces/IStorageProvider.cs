using System;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Interfaces
{
    public interface IStorageProvider
    {
        Task<string> SaveFile(string userId, Guid documentId, string filename, byte[] file, string artifact = "file");
        Task<byte[]> GetFileAsync(string fullPath, CancellationToken ct = default); // NEW
    }
}
