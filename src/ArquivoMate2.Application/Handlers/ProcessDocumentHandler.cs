using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArquivoMate2.Application.Commands;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Models;
using ArquivoMate2.Domain.Document;
using ArquivoMate2.Domain.Import;
using ArquivoMate2.Domain.ValueObjects;
using ArquivoMate2.Shared.Models;
using JasperFx.Core;
using Marten;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace ArquivoMate2.Application.Handlers
{
    public class ProcessDocumentHandler : IRequestHandler<ProcessDocumentCommand, (Document? Document, string? TempFilePath)>
    {
        private record LoadedContext(Document Document, DocumentMetadata Metadata, string PhysicalPath);
        private record ExtractionArtifacts(
            string Content,
            string OriginalFilePath,
            string MetadataFilePath,
            string ThumbnailPath,
            string PreviewPdfPath,
            string ArchivePdfPath,
            DocumentMetadata EffectiveMetadata,
            List<EncryptedArtifactKey> EncryptionKeys);

        private readonly IDocumentSession _session;
        private readonly ILogger<ProcessDocumentHandler> _logger;
        private readonly IDocumentProcessor _documentTextExtractor;
        private readonly IFileMetadataService fileMetadataService;
        private readonly IPathService pathService;
        private readonly IStorageProvider _storage;
        private readonly IThumbnailService _thumbnailService;
        private readonly IChatBot _chatBot;
        private readonly IDocumentProcessingNotifier _documentProcessingNotifier;
        private readonly ILanguageDetectionService _languageDetection;
        private readonly IEncryptionService _encryptionService;
        private readonly IDocumentVectorizationService _vectorizationService;

        private static readonly byte[] PdfMagicNumber = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF
        private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".bmp", ".webp" };

        public ProcessDocumentHandler(IDocumentSession session, ILogger<ProcessDocumentHandler> logger, IDocumentProcessor documentTextExtractor, IFileMetadataService fileMetadataService, IPathService pathService,
            IStorageProvider storage, IThumbnailService thumbnailService, IChatBot chatBot, IDocumentProcessingNotifier documentProcessingNotifier, ICurrentUserService currentUserService, ILanguageDetectionService languageDetection, IEncryptionService encryptionService, IDocumentVectorizationService vectorizationService)
            => (_session, _logger, _documentTextExtractor, this.fileMetadataService, this.pathService, _storage, _thumbnailService, _chatBot, _documentProcessingNotifier, _languageDetection, _encryptionService, _vectorizationService) = (session, logger, documentTextExtractor, fileMetadataService, pathService, storage, thumbnailService, chatBot, documentProcessingNotifier, languageDetection, encryptionService, vectorizationService);

        // Hilfsmethode zum Loggen des Speicherverbrauchs
        private void LogMemoryUsage(string step, Guid documentId)
        {
            var usedMB = GC.GetTotalMemory(false) / (1024 * 1024);
            var workingSetMB = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);
            _logger.LogInformation("Memory usage at {Step} for Document {DocumentId}: Managed={UsedMB} MB, WorkingSet={WorkingSetMB} MB", step, documentId, usedMB, workingSetMB);
        }

        public async Task<(Document? Document, string? TempFilePath)> Handle(ProcessDocumentCommand request, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            Append(request.ImportProcessId, new StartDocumentImport(request.ImportProcessId, DateTime.UtcNow));
            await _session.SaveChangesAsync(cancellationToken);

            ExtractionArtifacts? artifacts = null; // capture for cleanup

            try
            {
                LogMemoryUsage("Start Handle", request.DocumentId);
                await _documentProcessingNotifier.NotifyStatusChangedAsync(request.UserId,
                    new DocumentProcessingNotification(request.DocumentId.ToString(), DocumentProcessingStatus.InProgress, "Document processing started"));

                LogMemoryUsage("Before LoadAsync", request.DocumentId);
                var loaded = await LoadAsync(request, cancellationToken);
                LogMemoryUsage("After LoadAsync", request.DocumentId);

                var metaAfterLang = await DetectAndMaybeUpdateLanguageAsync(loaded, cancellationToken);
                loaded = loaded with { Metadata = metaAfterLang };
                LogMemoryUsage("After DetectAndMaybeUpdateLanguageAsync", request.DocumentId);

                artifacts = await ExtractAndGenerateAsync(request, loaded, cancellationToken);
                LogMemoryUsage("After ExtractAndGenerateAsync", request.DocumentId);

                if (_encryptionService.IsEnabled && artifacts.EncryptionKeys.Count > 0)
                {
                    Append(request.DocumentId, new DocumentEncryptionKeysAdded(request.DocumentId, artifacts.EncryptionKeys, DateTime.UtcNow));
                }
                await RunChatBotAsync(request.DocumentId, request.UserId, artifacts.Content, cancellationToken);
                await VectorizeDocumentAsync(request.DocumentId, request.UserId, artifacts.Content, cancellationToken);
                LogMemoryUsage("After RunChatBotAsync", request.DocumentId);

                Append(request.ImportProcessId, new MarkSucceededDocumentImport(request.ImportProcessId, request.DocumentId, DateTime.UtcNow));
                Append(request.DocumentId, new DocumentProcessed(request.DocumentId, DateTime.UtcNow));
                await _session.SaveChangesAsync(cancellationToken);

                var finalDoc = await _session.Events.AggregateStreamAsync<Document>(request.DocumentId, token: cancellationToken);
                sw.Stop();
                _logger.LogInformation("Processed document {DocumentId} in {ElapsedMs}ms", request.DocumentId, sw.ElapsedMilliseconds);
                LogMemoryUsage("End Handle", request.DocumentId);
                return (finalDoc, loaded.PhysicalPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document {DocumentId}", request.DocumentId);
                try
                {
                    Append(request.ImportProcessId, new MarkFailedDocumentImport(request.ImportProcessId, ex.Message, DateTime.UtcNow));
                    Append(request.DocumentId, new DocumentDeleted(request.DocumentId, DateTime.UtcNow));
                }
                catch (Exception appendEx)
                {
                    _logger.LogWarning(appendEx, "Failed appending failure events for {DocumentId}", request.DocumentId);
                }

                await TryDeleteVectorsAsync(request.DocumentId, request.UserId, cancellationToken);

                // attempt to remove already stored artifacts
                if (artifacts != null)
                {
                    await CleanupArtifactsAsync(artifacts, cancellationToken);
                }

                // also try to remove any metadata file written earlier separately
                await _session.SaveChangesAsync(cancellationToken);
                return (null, null);
            }
        }

        private async Task CleanupArtifactsAsync(ExtractionArtifacts artifacts, CancellationToken ct)
        {
            var paths = new[] { artifacts.OriginalFilePath, artifacts.MetadataFilePath, artifacts.ThumbnailPath, artifacts.PreviewPdfPath, artifacts.ArchivePdfPath };
            foreach (var p in paths.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                try
                {
                    await _storage.DeleteFileAsync(p, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed deleting artifact {Path} during cleanup", p);
                }
            }
        }

        #region Load & Validation
        private async Task<LoadedContext> LoadAsync(ProcessDocumentCommand request, CancellationToken ct)
        {
            var doc = await _session.Events.AggregateStreamAsync<Document>(request.DocumentId, token: ct);
            if (doc is null)
            {
                _logger.LogWarning("Document {DocumentId} not found", request.DocumentId);
                throw new KeyNotFoundException($"Document {request.DocumentId} not found");
            }

            var metadata = await fileMetadataService.ReadMetadataAsync(request.DocumentId, request.UserId, ct);
            if (metadata is null)
            {
                _logger.LogWarning("Metadata for document {DocumentId} not found", request.DocumentId);
                throw new KeyNotFoundException($"Metadata for document {request.DocumentId} not found");
            }

            var path = Path.Combine(pathService.GetDocumentUploadPath(request.UserId), $"{request.DocumentId}{metadata.Extension}");
            return new LoadedContext(doc, metadata, path);
        }
        #endregion

        #region Language Detection
        private async Task<DocumentMetadata> DetectAndMaybeUpdateLanguageAsync(LoadedContext ctx, CancellationToken ct)
        {
            var metadata = ctx.Metadata;
            var doc = ctx.Document;
            var effective = metadata;

            try
            {
                bool needDetection = string.IsNullOrWhiteSpace(doc.Language) || metadata.Languages == null || metadata.Languages.Length != 1;
                if (!needDetection || !File.Exists(ctx.PhysicalPath)) return effective;

                bool isPdf = await IsPdfFileAsync(ctx.PhysicalPath, metadata);
                (string? iso, string? tess) result = (null, null);
                using var detectStream = new FileStream(ctx.PhysicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (isPdf)
                {
                    result = await _languageDetection.DetectFromPdfAsync(detectStream, ct);
                    if (result.iso == null || result.tess == null)
                    {
                        detectStream.Position = 0;
                        // Pass a non-null array even if metadata.Languages is null
                        result = await _languageDetection.DetectFromImageOrPdfAsync(detectStream, metadata.Languages ?? Array.Empty<string>(), ct);
                    }
                }
                else if (IsSingleImage(metadata))
                {
                    // Pass a non-null array even if metadata.Languages is null
                    result = await _languageDetection.DetectFromImageOrPdfAsync(detectStream, metadata.Languages ?? Array.Empty<string>(), ct);
                }

                if (result.iso != null && result.tess != null)
                {
                    if (!string.Equals(doc.Language, result.iso, StringComparison.OrdinalIgnoreCase))
                        Append(ctx.Document.Id, new DocumentLanguageDetected(ctx.Document.Id, result.iso, DateTime.UtcNow));

                    // Use null-safe checks when accessing metadata.Languages
                    if ((metadata.Languages?.Length ?? 0) != 1 || !string.Equals(metadata.Languages != null ? metadata.Languages[0] : null, result.tess, StringComparison.OrdinalIgnoreCase))
                    {
                        effective = metadata with { Languages = new[] { result.tess } };
                        await fileMetadataService.WriteMetadataAsync(effective, ct);
                        _logger.LogInformation("Language detected for Document {DocumentId}: ISO={Iso} Tess={Tess}", ctx.Document.Id, result.iso, result.tess);
                    }
                }
                else
                {
                    _logger.LogInformation("Language detection skipped or failed for Document {DocumentId}", ctx.Document.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Language detection error for Document {DocumentId} - continuing", ctx.Document.Id);
            }

            return effective;
        }
        #endregion

        #region Extraction & Derivatives
        private async Task<ExtractionArtifacts> ExtractAndGenerateAsync(ProcessDocumentCommand request, LoadedContext ctx, CancellationToken ct)
        {
            bool isPdf = await IsPdfFileAsync(ctx.PhysicalPath, ctx.Metadata);
            if (isPdf) return await ProcessPdfAsync(request, ctx, ct);
            if (IsSingleImage(ctx.Metadata)) return await ProcessImageAsync(request, ctx, ct);

            _logger.LogWarning("Document {DocumentId} unsupported file type {Extension}", request.DocumentId, ctx.Metadata.Extension);
            await _documentProcessingNotifier.NotifyStatusChangedAsync(request.UserId, new DocumentProcessingNotification(request.DocumentId.ToString(), DocumentProcessingStatus.Failed, $"Unsupported file type: {ctx.Metadata.Extension}"));
            throw new NotSupportedException($"File type {ctx.Metadata.Extension} is not supported");
        }

        private async Task<ExtractionArtifacts> ProcessPdfAsync(ProcessDocumentCommand request, LoadedContext ctx, CancellationToken ct)
        {
            using var stream = OpenForSequentialRead(ctx.PhysicalPath);
            var content = await _documentTextExtractor.ExtractPdfTextAsync(stream, ctx.Metadata, false, ct);
            Append(request.DocumentId, new DocumentContentExtracted(request.DocumentId, content, DateTime.UtcNow));
            var artifacts = await PersistAndGenerateDerivativesAsync(request, ctx, stream, ct);
            return artifacts with { Content = content };
        }

        private async Task<ExtractionArtifacts> ProcessImageAsync(ProcessDocumentCommand request, LoadedContext ctx, CancellationToken ct)
        {
            using var stream = OpenForSequentialRead(ctx.PhysicalPath);
            var content = await _documentTextExtractor.ExtractImageTextAsync(stream, ctx.Metadata, ct);
            Append(request.DocumentId, new DocumentContentExtracted(request.DocumentId, content, DateTime.UtcNow));
            var artifacts = await PersistAndGenerateDerivativesAsync(request, ctx, stream, ct);
            return artifacts with { Content = content };
        }

        private async Task<ExtractionArtifacts> PersistAndGenerateDerivativesAsync(ProcessDocumentCommand request, LoadedContext ctx, FileStream openStream, CancellationToken ct)
        {
            var keys = new List<EncryptedArtifactKey>();

            string filePath;
            EncryptedArtifactKey? k1;
            await using (var originalStream = new FileStream(ctx.PhysicalPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan))
            {
                (filePath, k1) = await SaveMaybeEncryptedAsync(request.UserId, request.DocumentId, Path.GetFileName(ctx.PhysicalPath), originalStream, "file", ct);
            }
            if (k1 != null) keys.Add(k1);

            var metaBytes = JsonSerializer.SerializeToUtf8Bytes(ctx.Metadata);
            var (metaPath, kMeta) = await SaveMaybeEncryptedAsync(request.UserId, request.DocumentId, Path.ChangeExtension(Path.GetFileName(ctx.PhysicalPath), "metadata"), metaBytes, "metadata", ct);
            if (kMeta != null) keys.Add(kMeta);

            if (openStream.CanSeek)
            {
                openStream.Position = 0;
            }
            var thumbBytes = _thumbnailService.GenerateThumbnail(openStream);
            string baseName = Path.GetFileNameWithoutExtension(ctx.PhysicalPath);
            var (thumbPath, kThumb) = await SaveMaybeEncryptedAsync(request.UserId, request.DocumentId, $"{baseName}-thumb.webp", thumbBytes, "thumb", ct);
            if (kThumb != null) keys.Add(kThumb);

            string previewPath;
            EncryptedArtifactKey? kPrev;
            if (openStream.CanSeek)
            {
                openStream.Position = 0;
            }
            await using (var previewStream = CreateTempFileStream())
            {
                await _documentTextExtractor.GeneratePreviewPdf(openStream, ctx.Metadata, previewStream, ct);
                if (previewStream.CanSeek)
                {
                    previewStream.Position = 0;
                }
                (previewPath, kPrev) = await SaveMaybeEncryptedAsync(request.UserId, request.DocumentId, $"{baseName}-preview.pdf", previewStream, "preview", ct);
            }
            if (kPrev != null) keys.Add(kPrev);

            string archivePath;
            EncryptedArtifactKey? kArch;
            if (openStream.CanSeek)
            {
                openStream.Position = 0;
            }
            await using (var archiveStream = CreateTempFileStream())
            {
                await _documentTextExtractor.GenerateArchivePdf(openStream, ctx.Metadata, archiveStream, ct);
                if (archiveStream.CanSeek)
                {
                    archiveStream.Position = 0;
                }
                (archivePath, kArch) = await SaveMaybeEncryptedAsync(request.UserId, request.DocumentId, $"{baseName}-archive.pdf", archiveStream, "archive", ct);
            }
            if (kArch != null) keys.Add(kArch);

            Append(request.DocumentId, new DocumentFilesPrepared(request.DocumentId, filePath, metaPath, thumbPath, previewPath, archivePath, DateTime.UtcNow));

            return new ExtractionArtifacts(string.Empty, filePath, metaPath, thumbPath, previewPath, archivePath, ctx.Metadata, keys);
        }

        private async Task<(string path, EncryptedArtifactKey? key)> SaveMaybeEncryptedAsync(string userId, Guid docId, string filename, byte[] bytes, string artifact, CancellationToken ct)
        {
            using var ms = new MemoryStream(bytes, writable: false);
            return await SaveMaybeEncryptedAsync(userId, docId, filename, ms, artifact, ct).ConfigureAwait(false);
        }

        private async Task<(string path, EncryptedArtifactKey? key)> SaveMaybeEncryptedAsync(string userId, Guid docId, string filename, Stream content, string artifact, CancellationToken ct)
        {
            if (content.CanSeek)
            {
                content.Position = 0;
            }

            if (_encryptionService.IsEnabled)
            {
                return await _encryptionService.SaveAsync(userId, docId, filename, content, artifact, ct).ConfigureAwait(false);
            }

            var path = await _storage.SaveFileAsync(userId, docId, filename, content, artifact, ct).ConfigureAwait(false);
            return (path, null);
        }
        #endregion

        #region ChatBot
        private async Task RunChatBotAsync(Guid documentId, string userId, string content, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(content)) return;
            try
            {
                var result = await _chatBot.AnalyzeDocumentContent(content, ct);
                await ProcessChatbotResultAsync(documentId, userId, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chatbot analysis failed for {DocumentId}. Continuing without chatbot data.", documentId);
            }
        }

        private async Task VectorizeDocumentAsync(Guid documentId, string userId, string content, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                await TryDeleteVectorsAsync(documentId, userId, ct);
                return;
            }

            try
            {
                await _vectorizationService.StoreDocumentAsync(documentId, userId, content, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Vectorization failed for document {DocumentId}", documentId);
            }
        }

        private async Task TryDeleteVectorsAsync(Guid documentId, string userId, CancellationToken ct)
        {
            try
            {
                await _vectorizationService.DeleteDocumentAsync(documentId, userId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to delete vectors for document {DocumentId}", documentId);
            }
        }

        private async Task ProcessChatbotResultAsync(Guid documentId, string userId, DocumentAnalysisResult chatbotResult)
        {
            if (chatbotResult == null) return;
            Guid senderId = await ResolveOrCreatePartyAsync(chatbotResult.Sender, userId);
            Guid recipientId = await ResolveOrCreatePartyAsync(chatbotResult.Recipient, userId);

            Append(documentId, new DocumentChatBotDataReceived(documentId, senderId, recipientId, null, DateTime.UtcNow,
                chatbotResult.DocumentType, chatbotResult.CustomerNumber, chatbotResult.InvoiceNumber, chatbotResult.TotalPrice, chatbotResult.Keywords, chatbotResult.Summary, _chatBot.ModelName, _chatBot.GetType().Name));
            if (chatbotResult.Title.IsNotEmpty())
                Append(documentId, new DocumentTitleSuggested(documentId, chatbotResult.Title, DateTime.UtcNow));
        }

        private async Task<Guid> ResolveOrCreatePartyAsync(PartyInfo? party, string userId)
        {
            if (party == null) return Guid.Empty;
            party.UserId = userId;
            var search = party.CompanyName + " " + party.FirstName + " " + party.LastName;
            var matches = await _session.Query<PartyInfo>()
                .Where(x => x.UserId == userId && x.SearchText.NgramSearch(search))
                .ToListAsync();
            if (matches.Count > 0) return matches.First().Id;

            using var newSession = _session.DocumentStore.LightweightSession();
            party.Id = Guid.NewGuid();
            newSession.Store(party);
            await newSession.SaveChangesAsync();
            return party.Id;
        }
        #endregion

        #region Helpers
        private static FileStream CreateTempFileStream()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            return new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        }

        private void Append(Guid streamId, object @event) => _session.Events.Append(streamId, @event);
        private bool IsSingleImage(DocumentMetadata metadata) => SupportedImageExtensions.Contains(metadata.Extension);

        private async Task<bool> IsPdfFileAsync(string filePath, DocumentMetadata metadata)
        {
            try
            {
                var hasValidExtension = string.Equals(metadata.Extension, ".pdf", StringComparison.OrdinalIgnoreCase);
                var hasValidMimeType = metadata.MimeType != null && (string.Equals(metadata.MimeType, "application/pdf", StringComparison.OrdinalIgnoreCase) || string.Equals(metadata.MimeType, "application/x-pdf", StringComparison.OrdinalIgnoreCase));
                var hasValidMagicNumber = await HasPdfMagicNumberAsync(filePath);
                _logger.LogDebug("PDF validation for {FilePath}: ext={Ext} okExt={OkExt}, mime={Mime} okMime={OkMime}, magic={Magic}", filePath, metadata.Extension, hasValidExtension, metadata.MimeType, hasValidMimeType, hasValidMagicNumber);
                return hasValidMagicNumber && (hasValidExtension || hasValidMimeType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating PDF file {FilePath}", filePath);
                return false;
            }
        }

        private async Task<bool> HasPdfMagicNumberAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return false;
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var buffer = new byte[PdfMagicNumber.Length];
                var bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead < PdfMagicNumber.Length) return false;
                return buffer.SequenceEqual(PdfMagicNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading magic number from file {FilePath}", filePath);
                return false;
            }
        }

        private FileStream OpenForSequentialRead(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
        #endregion
    }
}
