using ArquivoMate2.Application.Commands;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Models;
using ArquivoMate2.Domain.Document;
using ArquivoMate2.Domain.Import;
using ArquivoMate2.Domain.ValueObjects;
using ArquivoMate2.Shared.Models;
using Marten;
using MediatR;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArquivoMate2.Application.Handlers
{
    public class ProcessDocumentHandler : IRequestHandler<ProcessDocumentCommand, (Document? Document, string? TempFilePath)>
    {
        private readonly IDocumentSession _session;
        private readonly ILogger<ProcessDocumentHandler> _logger;
        private readonly IDocumentProcessor _documentTextExtractor;
        private readonly IFileMetadataService fileMetadataService;
        private readonly IPathService pathService;
        private readonly IStorageProvider _storage;
        private readonly IThumbnailService _thumbnailService;
        private readonly IChatBot _chatBot;
        private readonly IDocumentProcessingNotifier _documentProcessingNotifier;

        // PDF Magic Number (first 4 bytes)
        private static readonly byte[] PdfMagicNumber = [0x25, 0x50, 0x44, 0x46]; // %PDF

        public ProcessDocumentHandler(IDocumentSession session, ILogger<ProcessDocumentHandler> logger, IDocumentProcessor documentTextExtractor, IFileMetadataService fileMetadataService, IPathService pathService,
            IStorageProvider storage, IThumbnailService thumbnailService, IChatBot chatBot, IDocumentProcessingNotifier documentProcessingNotifier, ICurrentUserService currentUserService)
            => (_session, _logger, _documentTextExtractor, this.fileMetadataService, this.pathService, _storage, _thumbnailService, _chatBot, _documentProcessingNotifier) = (session, logger, documentTextExtractor, fileMetadataService, pathService, storage, thumbnailService, chatBot, documentProcessingNotifier);

        public async Task<(Document? Document, string? TempFilePath)> Handle(ProcessDocumentCommand request, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();

            _session.Events.Append(request.ImportProcessId, new StartDocumentImport(request.ImportProcessId, DateTime.Now));
            await _session.SaveChangesAsync(cancellationToken);

            try
            {
                await _documentProcessingNotifier.NotifyStatusChangedAsync(request.UserId, 
                    new DocumentProcessingNotification(request.DocumentId.ToString(), DocumentProcessingStatus.InProgress, "Document processing started"));
                var doc = await _session.Events.AggregateStreamAsync<Document>(request.DocumentId, token: cancellationToken);
                if (doc is null)
                {
                    _logger.LogWarning("Document {DocumentId} not found", request.DocumentId);
                    throw new KeyNotFoundException($"Document {request.DocumentId} not found");
                }

                var metadata = await fileMetadataService.ReadMetadataAsync(request.DocumentId, request.UserId);

                if (metadata is null)
                {
                    _logger.LogWarning("Metadata for document {DocumentId} not found", request.DocumentId);
                    throw new KeyNotFoundException($"Metadata for document {request.DocumentId} not found");
                }

                var path = pathService.GetDocumentUploadPath(request.UserId);
                path = Path.Combine(path, $"{request.DocumentId}{metadata.Extension}");

                if (await IsPdfFileAsync(path, metadata))
                {
                    await ProcessPdfFiles(request, metadata, path, cancellationToken);
                }
                else
                {
                    _logger.LogWarning("Document {DocumentId} is not a supported PDF file. Extension: {Extension}, MimeType: {MimeType}", 
                        request.DocumentId, metadata.Extension, metadata.MimeType);
                    
                    await _documentProcessingNotifier.NotifyStatusChangedAsync(request.UserId,
                        new DocumentProcessingNotification(request.DocumentId.ToString(), DocumentProcessingStatus.Failed, 
                            $"Unsupported file type: {metadata.Extension}"));
                    
                    throw new NotSupportedException($"File type {metadata.Extension} is not supported for processing");
                }

                _session.Events.Append(request.ImportProcessId, new MarkSuccededDocumentImport(request.ImportProcessId, request.DocumentId, DateTime.Now));
                _session.Events.Append(request.DocumentId, new DocumentProcessed(request.DocumentId, DateTime.Now));

                await _session.SaveChangesAsync(cancellationToken);

                doc = await _session.Events.AggregateStreamAsync<Document>(request.DocumentId, token: cancellationToken);

                return (doc, path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document {DocumentId}", request.DocumentId);

                _session.Events.Append(request.ImportProcessId, new MarkFailedDocumentImport(request.ImportProcessId, ex.Message, DateTime.Now));
                _session.Events.Append(request.DocumentId, new DocumentDeleted(request.DocumentId, DateTime.UtcNow));
                await _session.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                sw.Stop();

                _logger.LogInformation("Processed document {DocumentId} in {ElapsedMs}ms", request.DocumentId, sw.ElapsedMilliseconds);
            }

            return (null, null);
        }

        /// <summary>
        /// Comprehensive PDF file validation using multiple methods
        /// </summary>
        /// <param name="filePath">Path to the file to validate</param>
        /// <param name="metadata">File metadata containing extension and MIME type</param>
        /// <returns>True if the file is a valid PDF, false otherwise</returns>
        private async Task<bool> IsPdfFileAsync(string filePath, DocumentMetadata metadata)
        {
            try
            {
                // 1. Check file extension (basic check)
                var hasValidExtension = string.Equals(metadata.Extension, ".pdf", StringComparison.OrdinalIgnoreCase);
                
                // 2. Check MIME type if available
                var hasValidMimeType = metadata.MimeType != null && 
                    (string.Equals(metadata.MimeType, "application/pdf", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(metadata.MimeType, "application/x-pdf", StringComparison.OrdinalIgnoreCase));

                // 3. Check magic number (file signature) - most reliable method
                var hasValidMagicNumber = await HasPdfMagicNumberAsync(filePath);

                // Log validation results for debugging
                _logger.LogDebug("PDF validation for {FilePath}: Extension={Extension} ({ValidExtension}), " +
                    "MimeType={MimeType} ({ValidMimeType}), MagicNumber={ValidMagicNumber}",
                    filePath, metadata.Extension, hasValidExtension, 
                    metadata.MimeType, hasValidMimeType, hasValidMagicNumber);

                // File is considered PDF if:
                // - Magic number matches (most important) AND
                // - At least one of: valid extension OR valid MIME type
                return hasValidMagicNumber && (hasValidExtension || hasValidMimeType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating PDF file {FilePath}", filePath);
                return false;
            }
        }

        /// <summary>
        /// Checks if file starts with PDF magic number (%PDF)
        /// </summary>
        /// <param name="filePath">Path to the file to check</param>
        /// <returns>True if file has PDF magic number, false otherwise</returns>
        private async Task<bool> HasPdfMagicNumberAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var buffer = new byte[PdfMagicNumber.Length];
                var bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length);

                if (bytesRead < PdfMagicNumber.Length)
                    return false;

                return buffer.SequenceEqual(PdfMagicNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading magic number from file {FilePath}", filePath);
                return false;
            }
        }

        private async Task ProcessPdfFiles(ProcessDocumentCommand request, DocumentMetadata metadata, string path, CancellationToken cancellationToken)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);

            var content = await ExtractTextAsync(stream, metadata, cancellationToken);

            _session.Events.Append(request.DocumentId, new DocumentContentExtracted(request.DocumentId, content, DateTime.UtcNow));

            var filePath = await _storage.SaveFile(request.UserId, request.DocumentId, Path.GetFileName(path), File.ReadAllBytes(path));
            var metaPath = await _storage.SaveFile(request.UserId, request.DocumentId, Path.ChangeExtension(Path.GetFileName(path), "metadata"), JsonSerializer.SerializeToUtf8Bytes(metadata));

            var thumbnail = _thumbnailService.GenerateThumbnail(stream);

            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
            string thumbnailFileName = $"{fileNameWithoutExtension}-thumb.webp";

            var thumbPath = await _storage.SaveFile(request.UserId, request.DocumentId, thumbnailFileName, thumbnail);

            var previewPdf = await _documentTextExtractor.GeneratePreviewPdf(stream, metadata, cancellationToken);

            string previewPdfFileName = $"{fileNameWithoutExtension}-preview.pdf";
            var previewPath = await _storage.SaveFile(request.UserId, request.DocumentId, previewPdfFileName, previewPdf);

            _session.Events.Append(request.DocumentId, new DocumentFilesPrepared(request.DocumentId, filePath, metaPath, thumbPath, previewPath, DateTime.UtcNow));

            var chatbotResult = await _chatBot.AnalyzeDocumentContent(content, cancellationToken);

            await ProcessChatbotResultAsync(request.DocumentId, chatbotResult);
        }

        private async Task ProcessChatbotResultAsync(Guid documentId, DocumentAnalysisResult chatbotResult)
        {
            if (chatbotResult == null)
                return;

            var all = _session.Query<PartyInfo>().ToList();

            var sender = chatbotResult.Sender;
            Guid senderId = Guid.Empty;

            if (sender != null)
            {
                var matches = await _session.Query<PartyInfo>()
                .Where(x => x.SearchText.NgramSearch(sender.CompanyName + " " + sender.FirstName + " " + sender.LastName))
                .ToListAsync();

                if (matches != null && matches.Count > 0)
                {
                    var bestMatch = matches.FirstOrDefault();
                    if (bestMatch != null)
                    {
                        senderId = bestMatch.Id;
                    }
                }
                else
                {
                    using var newSession = _session.DocumentStore.LightweightSession();
                    PartyInfo newSender = new PartyInfo()
                    {
                        Id = Guid.NewGuid(),
                        City = sender.City,
                        CompanyName = sender.CompanyName,
                        FirstName = sender.FirstName,
                        HouseNumber = sender.HouseNumber,
                        LastName = sender.LastName,
                        PostalCode = sender.PostalCode,
                        Street = sender.Street,
                    };
                    newSession.Store(newSender);
                    senderId = newSender.Id;
                    await newSession.SaveChangesAsync();
                }
            }

            var recipient = chatbotResult.Recipient;
            Guid recipientId = Guid.Empty;

            if (recipient != null)
            {
                var matches = await _session.Query<PartyInfo>()
                .Where(x => x.SearchText.NgramSearch(recipient.CompanyName + " " + recipient.FirstName + " " + recipient.LastName))
                .ToListAsync();

                if (matches != null && matches.Count > 0)
                {
                    var bestMatch = matches.FirstOrDefault();
                    if (bestMatch != null)
                    {
                        recipientId = bestMatch.Id;
                    }
                }
                else
                {
                    using var newSession = _session.DocumentStore.LightweightSession();
                    PartyInfo newRecipient = new PartyInfo()
                    {
                        Id = Guid.NewGuid(),
                        City = recipient.City,
                        CompanyName = recipient.CompanyName,
                        FirstName = recipient.FirstName,
                        HouseNumber = recipient.HouseNumber,
                        LastName = recipient.LastName,
                        PostalCode = recipient.PostalCode,
                        Street = recipient.Street,
                    };
                    _session.Store(newRecipient);
                    recipientId = newRecipient.Id;
                    newSession.Store(newRecipient);
                    await newSession.SaveChangesAsync();
                }
            }
            
            _session.Events.Append(documentId, new DocumentChatBotDataReceived(documentId, senderId, recipientId, null, DateTime.Now,
                chatbotResult.DocumentType, chatbotResult.CustomerNumber, chatbotResult.InvoiceNumber, chatbotResult.TotalPrice, chatbotResult.Keywords, chatbotResult.Summary));
        }

        private async Task<string> ExtractTextAsync(FileStream stream, DocumentMetadata metadata, CancellationToken cancellationToken)
        {
            // prüfen nach Dateiendung
            switch (metadata.Extension.ToLowerInvariant())
            {
                case ".pdf":
                    // PDF Text extrahieren
                    return await _documentTextExtractor.ExtractPdfTextAsync(stream, metadata, false, cancellationToken);
                default:
                    _logger.LogError("Unsupported file type: {Extension}", metadata.Extension);
                    return string.Empty;

            }
        }
    }
}
