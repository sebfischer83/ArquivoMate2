using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Infrastructure.Configuration.StorageProvider;
using Microsoft.Extensions.Options;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services.StorageProvider
{
    public abstract class StorageProviderBase<TSettings> : IStorageProvider
        where TSettings : StorageProviderSettings
    {
        protected readonly TSettings _settings;
        protected readonly IPathService _pathService;

        protected StorageProviderBase(IOptions<TSettings> opts, IPathService pathService)
        {
            _settings = opts.Value;
            _pathService = pathService;
        }

        // Construct the full object key/path using configured RootPath and the path service.
        protected string BuildObjectPath(string userId, Guid documentId, string filename)
        {
            var parts = _pathService.GetStoragePath(userId, documentId, filename);
            // Ensure RootPath does not have leading/trailing slashes
            var root = (_settings.RootPath ?? string.Empty).Trim('/');
            if (string.IsNullOrEmpty(root))
                return string.Join('/', parts);
            return root + "/" + string.Join('/', parts);
        }

        public virtual async Task<string> SaveFile(string userId, Guid documentId, string filename, byte[] file, string artifact = "file")
        {
            using var stream = new MemoryStream(file, writable: false);
            return await SaveFileAsync(userId, documentId, filename, stream, artifact).ConfigureAwait(false);
        }

        public abstract Task<string> SaveFileAsync(string userId, Guid documentId, string filename, Stream content, string artifact = "file", CancellationToken ct = default);
        public abstract Task<byte[]> GetFileAsync(string fullPath, CancellationToken ct = default);
    }
}
