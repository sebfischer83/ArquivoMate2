using ArquivoMate2.Application.Commands;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Models;
using ArquivoMate2.Domain.Document;
using ArquivoMate2.Domain.ValueObjects;
using Marten;
using MediatR;
using Microsoft.Extensions.FileProviders;
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
        private readonly IDocumentTextExtractor _documentTextExtractor;
        private readonly IFileMetadataService fileMetadataService;
        private readonly IPathService pathService;
        private readonly IStorageProvider _storage;
        private readonly IThumbnailService _thumbnailService;
        private readonly IChatBot _chatBot;

        public ProcessDocumentHandler(IDocumentSession session, ILogger<ProcessDocumentHandler> logger, IDocumentTextExtractor documentTextExtractor, IFileMetadataService fileMetadataService, IPathService pathService,
            IStorageProvider storage, IThumbnailService thumbnailService, IChatBot chatBot)
            => (_session, _logger, _documentTextExtractor, this.fileMetadataService, this.pathService, _storage, _thumbnailService, _chatBot) = (session, logger, documentTextExtractor, fileMetadataService, pathService, storage, thumbnailService, chatBot);

        public async Task<(Document? Document, string? TempFilePath)> Handle(ProcessDocumentCommand request, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var doc = await _session.Events.AggregateStreamAsync<Document>(request.DocumentId, token: cancellationToken);
                if (doc is null)
                {
                    _logger.LogWarning("Document {DocumentId} not found", request.DocumentId);
                    throw new KeyNotFoundException($"Document {request.DocumentId} not found");
                }

                // read the file
                var metadata = await fileMetadataService.ReadMetadataAsync(request.DocumentId, request.UserId);

                if (metadata is null)
                {
                    _logger.LogWarning("Metadata for document {DocumentId} not found", request.DocumentId);
                    throw new KeyNotFoundException($"Metadata for document {request.DocumentId} not found");
                }

                _session.Events.Append(request.DocumentId, new DocumentStartProcessing(request.DocumentId, DateTime.UtcNow));

                var path = pathService.GetDocumentUploadPath(request.UserId);
                path = Path.Combine(path, $"{request.DocumentId}{metadata.Extension}");

                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);

                // get the content
                var content = await ExtractTextAsync(stream, metadata, cancellationToken);

                _session.Events.Append(request.DocumentId, new DocumentContentExtracted(request.DocumentId, content, DateTime.UtcNow));

                var filePath = await _storage.SaveFile(request.UserId, request.DocumentId, Path.GetFileName(path), File.ReadAllBytes(path));
                var metaPath = await _storage.SaveFile(request.UserId, request.DocumentId, Path.ChangeExtension(Path.GetFileName(path), "metadata"), JsonSerializer.SerializeToUtf8Bytes(metadata));

                // thumbnail
                var thumbnail = _thumbnailService.GenerateThumbnail(stream);

                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
                string thumbnailFileName = $"{fileNameWithoutExtension}-thumb.webp";

                var thumbPath = await _storage.SaveFile(request.UserId, request.DocumentId, thumbnailFileName, thumbnail);

                _session.Events.Append(request.DocumentId, new DocumentFilesPrepared(request.DocumentId, filePath, metaPath, thumbPath, DateTime.UtcNow));

                // chatbot
                var chatbotResult = await _chatBot.AnalyzeDocumentContent(content, cancellationToken);

                await ProcessChatbotResultAsync(request.DocumentId, chatbotResult);

                doc.MarkAsProcessed();
                _session.Events.Append(request.DocumentId, new DocumentProcessed(request.DocumentId, DateTime.UtcNow));
                await _session.SaveChangesAsync(cancellationToken);

                sw.Stop();

                _logger.LogInformation("Processed document {DocumentId} in {ElapsedMs}ms", request.DocumentId, sw.ElapsedMilliseconds);

                doc = await _session.Events.AggregateStreamAsync<Document>(request.DocumentId, token: cancellationToken);

                return (doc, path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document {DocumentId}", request.DocumentId);
            }

            return (null, null);
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
