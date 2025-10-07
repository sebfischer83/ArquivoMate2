using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Interfaces
{
    public interface IStorageProvider
    {
        Task<string> SaveFile(string userId, Guid documentId, string filename, byte[] file, string artifact = "file");
        Task<string> SaveFileAsync(string userId, Guid documentId, string filename, Stream content, string artifact = "file", CancellationToken ct = default);
        Task<byte[]> GetFileAsync(string fullPath, CancellationToken ct = default); // NEW
        Task StreamFileAsync(string fullPath, Func<Stream, CancellationToken, Task> streamConsumer, CancellationToken ct = default);
    }
}
