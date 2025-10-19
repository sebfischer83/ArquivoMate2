using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Infrastructure.Configuration.IngestionProvider;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Renci.SshNet;
using Renci.SshNet.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services.IngestionProvider
{
    /// <summary>
    /// SFTP ingestion provider using SSH.NET.
    /// </summary>
    public class SftpIngestionProvider : IIngestionProvider
    {
        private readonly SftpIngestionProviderSettings _settings;
        private readonly ILogger<SftpIngestionProvider> _logger;

        public SftpIngestionProvider(IOptions<SftpIngestionProviderSettings> options, ILogger<SftpIngestionProvider> logger)
        {
            _settings = options.Value;
            _logger = logger;

            // Basic validation / defaults
            _settings.RootPrefix ??= "ingestion";
            _settings.ProcessingSubfolderName ??= "processing";
            _settings.ProcessedSubfolderName ??= "processed";
            _settings.FailedSubfolderName ??= "failed";
            if (_settings.PollingInterval <= TimeSpan.Zero) _settings.PollingInterval = TimeSpan.FromMinutes(5);
        }

        public string? IngestionEmailAddress => string.IsNullOrWhiteSpace(_settings.IngestionEmail) ? null : _settings.IngestionEmail;

        public async Task<IReadOnlyList<IngestionFileDescriptor>> ListPendingFilesAsync(CancellationToken cancellationToken)
        {
            // Ensure remote root and user subfolders exist before listing
            EnsureRootAndUserFolders();

            return await Task.Run(() =>
            {
                using var client = CreateClient(_settings);
                client.Connect();

                var root = NormalizeRoot(_settings);
                // Normalize for create/list: remove trailing slash except for root
                var rootForCreate = root.TrimEnd('/');
                if (string.IsNullOrEmpty(rootForCreate)) rootForCreate = "/";

                var list = (IEnumerable<Renci.SshNet.Sftp.ISftpFile>)null!;
                try
                {
                    list = client.ListDirectory(rootForCreate);
                }
                catch (Renci.SshNet.Common.SftpPathNotFoundException)
                {
                    // attempt to create the root and retry
                    try
                    {
                        if (rootForCreate != "/")
                        {
                            // Create directories recursively
                            EnsureDirectoryExists(client, rootForCreate);
                        }
                        list = client.ListDirectory(rootForCreate);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to list or create remote ingestion root {Root}", rootForCreate);
                        return (IReadOnlyList<IngestionFileDescriptor>)Array.Empty<IngestionFileDescriptor>();
                    }
                }
                var result = new List<IngestionFileDescriptor>();

                // Helper to process a file entry
                void ProcessFileEntry(Renci.SshNet.Sftp.ISftpFile file)
                {
                    if (file.IsDirectory) return;
                    var fullPath = file.FullName;
                    var relative = GetRelativeKey(_settings, fullPath);
                    if (string.IsNullOrEmpty(relative)) return;
                    var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Length < 2) return; // expect at least userId/file
                    var userId = segments[0];
                    if (string.IsNullOrWhiteSpace(userId)) return;

                    // skip already processing/processed/failed
                    if (segments.Length >= 3 && (string.Equals(segments[1], _settings.ProcessingSubfolderName, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(segments[1], _settings.ProcessedSubfolderName, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(segments[1], _settings.FailedSubfolderName, StringComparison.OrdinalIgnoreCase)))
                    {
                        return;
                    }

                    if (segments.Length == 2)
                    {
                        // move file to processing
                        var fileName = segments[1];
                        var dest = CombineSegments(_settings, userId, _settings.ProcessingSubfolderName, fileName);
                        // ensure destination directory exists
                        EnsureDirectoryExists(client, Path.GetDirectoryName(dest) ?? "/");
                        client.RenameFile(fullPath, dest);
                        result.Add(new IngestionFileDescriptor(userId, fileName, dest));
                    }
                }

                foreach (var item in list)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        // If item is a directory (likely a user folder), enumerate its children and process files inside
                        if (item.IsDirectory)
                        {
                            var name = item.Name;
                            if (string.IsNullOrEmpty(name) || name == "." || name == "..") continue;

                            var childPath = item.FullName;
                            IEnumerable<Renci.SshNet.Sftp.ISftpFile> children;
                            try
                            {
                                children = client.ListDirectory(childPath);
                            }
                            catch
                            {
                                continue;
                            }

                            foreach (var child in children)
                            {
                                ProcessFileEntry(child);
                            }
                        }
                        else
                        {
                            // file directly under root - still attempt to process (may encode userId/file structure)
                            ProcessFileEntry(item);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed processing remote entry {Entry}", item.FullName);
                    }
                }

                client.Disconnect();
                return (IReadOnlyList<IngestionFileDescriptor>)result;
            }, cancellationToken).ConfigureAwait(false);
        }

        public async Task MarkProcessedAsync(IngestionFileDescriptor descriptor, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                using var client = CreateClient(_settings);
                client.Connect();
                var destination = EnsureUniqueObjectKey(client, _settings, descriptor.UserId, _settings.ProcessedSubfolderName, descriptor.FileName);
                EnsureDirectoryExists(client, Path.GetDirectoryName(destination) ?? "/");
                client.RenameFile(descriptor.FullPath, destination);
                client.Disconnect();
                _logger.LogInformation("Moved ingested file {File} to processed prefix {Destination}.", descriptor.FullPath, destination);
            }, cancellationToken).ConfigureAwait(false);
        }

        public async Task MarkFailedAsync(IngestionFileDescriptor descriptor, string? reason, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                using var client = CreateClient(_settings);
                client.Connect();
                var destination = EnsureUniqueObjectKey(client, _settings, descriptor.UserId, _settings.FailedSubfolderName, descriptor.FileName);
                EnsureDirectoryExists(client, Path.GetDirectoryName(destination) ?? "/");
                client.RenameFile(descriptor.FullPath, destination);

                if (!string.IsNullOrWhiteSpace(reason))
                {
                    var errorPath = destination + ".error.txt";
                    using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(reason));
                    ms.Position = 0;
                    client.UploadFile(ms, errorPath);
                }

                client.Disconnect();
                _logger.LogWarning("Moved ingested file {File} to failed prefix {Destination}.", descriptor.FullPath, destination);
            }, cancellationToken).ConfigureAwait(false);
        }

        public async Task<byte[]> ReadFileAsync(IngestionFileDescriptor descriptor, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                using var client = CreateClient(_settings);
                client.Connect();
                using var ms = new MemoryStream();
                client.DownloadFile(descriptor.FullPath, ms);
                client.Disconnect();
                return ms.ToArray();
            }, cancellationToken).ConfigureAwait(false);
        }

        public async Task<string> SaveIncomingFileAsync(string userId, string fileName, Stream content, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                using var client = CreateClient(_settings);
                client.Connect();
                var safeFileName = Path.GetFileName(string.IsNullOrWhiteSpace(fileName) ? Guid.NewGuid().ToString("N") : fileName);
                var destination = EnsureUniqueObjectKey(client, _settings, userId, null, safeFileName);
                EnsureDirectoryExists(client, Path.GetDirectoryName(destination) ?? "/");
                // Ensure stream at position 0
                if (content.CanSeek) content.Position = 0;
                client.UploadFile(content, destination);
                client.Disconnect();
                _logger.LogInformation("Stored ingestion file for user {UserId} at {Destination}.", userId, destination);
                return destination;
            }, cancellationToken).ConfigureAwait(false);
        }

        #region SSH.NET helpers
        private static SftpClient CreateClient(SftpIngestionProviderSettings settings)
        {
            if (!string.IsNullOrEmpty(settings.Password))
            {
                // SftpClient supports host, port, username, password constructor
                return new SftpClient(settings.Host, settings.Port, settings.Username, settings.Password);
            }

            if (!string.IsNullOrEmpty(settings.PrivateKeyFilePath) && File.Exists(settings.PrivateKeyFilePath))
            {
                PrivateKeyFile pkf;
                if (!string.IsNullOrEmpty(settings.PrivateKeyPassphrase))
                {
                    pkf = new PrivateKeyFile(settings.PrivateKeyFilePath, settings.PrivateKeyPassphrase);
                }
                else
                {
                    pkf = new PrivateKeyFile(settings.PrivateKeyFilePath);
                }

                var auth = new PrivateKeyAuthenticationMethod(settings.Username, pkf);
                var conn = new ConnectionInfo(settings.Host, settings.Port, settings.Username, auth);
                return new SftpClient(conn);
            }

            throw new InvalidOperationException("Unable to construct SFTP client. Provide either Password or PrivateKeyFilePath.");
        }

        private static void EnsureDirectoryExists(SftpClient client, string remoteDirectory)
        {
            if (string.IsNullOrEmpty(remoteDirectory) || remoteDirectory == "/") return;

            // Build path parts without leading/trailing slashes
            var parts = remoteDirectory.Trim('/').Split('/');
            var cur = string.Empty;

            foreach (var p in parts)
            {
                cur = string.IsNullOrEmpty(cur) ? p : cur + "/" + p;

                // Try absolute form first (/a/b)
                var absolute = "/" + cur;

                // Check existence: try absolute, then relative
                var existsAbsolute = false;
                try
                {
                    existsAbsolute = client.Exists(absolute);
                }
                catch
                {
                    existsAbsolute = false;
                }

                if (existsAbsolute)
                {
                    continue;
                }

                var existsRelative = false;
                try
                {
                    existsRelative = client.Exists(cur);
                }
                catch
                {
                    existsRelative = false;
                }

                if (existsRelative)
                {
                    continue;
                }

                // Try creating absolute first, then fallback to relative
                try
                {
                    client.CreateDirectory(absolute);
                }
                catch (Exception ex) when (ex is SshException || ex is SftpPathNotFoundException || ex is SftpPermissionDeniedException)
                {
                    try
                    {
                        client.CreateDirectory(cur);
                    }
                    catch (Exception inner)
                    {
                        // If both attempts fail, rethrow original to allow higher-level logging
                        throw;
                    }
                }
            }
        }

        private static string NormalizeRoot(SftpIngestionProviderSettings settings)
        {
            var root = (settings.RootPrefix ?? string.Empty).Trim('/');
            return string.IsNullOrEmpty(root) ? "/" : "/" + root + "/";
        }

        private static string GetRelativeKey(SftpIngestionProviderSettings settings, string fullPath)
        {
            var root = NormalizeRoot(settings);
            if (fullPath.StartsWith(root, StringComparison.Ordinal))
            {
                var rel = fullPath[root.Length..];
                if (rel.StartsWith('/')) rel = rel[1..];
                return rel;
            }
            return fullPath;
        }

        private static string CombineSegments(SftpIngestionProviderSettings settings, string? userId, string? subfolder, string? fileName)
        {
            var segments = new List<string>();
            var root = (settings.RootPrefix ?? string.Empty).Trim('/');
            if (!string.IsNullOrEmpty(root)) segments.Add(root);
            if (!string.IsNullOrEmpty(userId)) segments.Add(userId);
            if (!string.IsNullOrEmpty(subfolder)) segments.Add(subfolder.Trim('/'));
            if (!string.IsNullOrEmpty(fileName)) segments.Add(fileName);
            return "/" + string.Join('/', segments);
        }

        private static string EnsureUniqueObjectKey(SftpClient client, SftpIngestionProviderSettings settings, string userId, string? subfolder, string fileName)
        {
            var prefix = CombineSegments(settings, userId, subfolder, null);
            var candidate = prefix + "/" + fileName;
            var counter = 1;
            while (client.Exists(candidate))
            {
                candidate = prefix + "/" + Path.GetFileNameWithoutExtension(fileName) + $"_{counter}" + Path.GetExtension(fileName);
                counter++;
            }
            return candidate;
        }
        #endregion

        private void EnsureRootAndUserFolders()
        {
            try
            {
                using var client = CreateClient(_settings);
                client.Connect();

                // Normalize to a root path without trailing slash for existence checks
                var rootPrefix = (_settings.RootPrefix ?? string.Empty).Trim('/');
                var rootPath = string.IsNullOrEmpty(rootPrefix) ? "/" : "/" + rootPrefix;

                if (!client.Exists(rootPath))
                {
                    client.CreateDirectory(rootPath);
                    _logger.LogInformation("Created remote ingestion root directory: {Root}", rootPath);
                }

                // Ensure per-user subfolders exist for each directory under the root
                var listing = client.ListDirectory(rootPath.EndsWith('/') ? rootPath : rootPath + "/");
                foreach (var item in listing)
                {
                    try
                    {
                        if (!item.IsDirectory) continue;
                        var name = item.Name;
                        if (string.IsNullOrEmpty(name) || name == "." || name == "..") continue;

                        var userDir = rootPath == "/" ? "/" + name : rootPath + "/" + name;
                        EnsureDirectoryExists(client, userDir + "/" + _settings.ProcessingSubfolderName);
                        EnsureDirectoryExists(client, userDir + "/" + _settings.ProcessedSubfolderName);
                        EnsureDirectoryExists(client, userDir + "/" + _settings.FailedSubfolderName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed ensuring subfolders for remote entry {Entry}", item.FullName);
                    }
                }

                client.Disconnect();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to ensure remote ingestion root and user folders");
            }
        }
    }
}
