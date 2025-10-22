using ArquivoMate2.Application.Features.Processors.LabResults.Domain.Parsing;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Document;
using Marten;
using Microsoft.Extensions.Logging;

namespace ArquivoMate2.Application.Features.Processors.LabResults
{
    public class LabResultsFeatureProcessor : ISystemFeatureProcessor
    {
        private readonly IQuerySession _query;
        private readonly IDocumentSession _session;
        private readonly IStorageProvider _storageProvider;
        private readonly IFileMetadataService _fileMetadataService;
        private readonly ILogger<LabResultsFeatureProcessor> _logger;
        public string FeatureKey => "lab-results";

        public LabResultsFeatureProcessor(IQuerySession query, IDocumentSession session, IStorageProvider storageProvider, IFileMetadataService fileMetadataService, ILogger<LabResultsFeatureProcessor> logger)
        {
            _query = query;
            _session = session;
            _storageProvider = storageProvider;
            _fileMetadataService = fileMetadataService;
            _logger = logger;
        }

        public async Task ProcessAsync(SystemFeatureProcessingContext context, CancellationToken ct)
        {
            _logger.LogInformation("[LabResults] Start processing document {DocumentId}, ChatBotAvailable={ChatBotAvailable}", context.DocumentId, context.ChatBotAvailable);

            if (!context.ChatBotAvailable)
            {
                _logger.LogWarning("[LabResults] Chatbot needed for this Feature.");
                return; // Mark job completed without data
            }

            var docView = await _query.Events.AggregateStreamAsync<Document>(context.DocumentId, token: ct);
            if (docView == null)
            {
                _logger.LogWarning("[LabResults] Document {DocumentId} nicht gefunden.", context.DocumentId);
                throw new FileNotFoundException($"Document {context.DocumentId} not found.");
            }

            // load document content, always archive because its always pdf
            var content = await _storageProvider.GetFileAsync(docView.ArchivePath, ct);
            if (content == null)
            {
                _logger.LogWarning("[LabResults] Document content for {DocumentId} not found at {FilePath}.", context.DocumentId, docView.FilePath);
                throw new FileNotFoundException($"Document content for {context.DocumentId} not found at {docView.FilePath} with Provider {_storageProvider.GetType().Name}.");
            }

            var metaByte = await _storageProvider.GetFileAsync(docView.MetadataPath, ct);
            var metadata = await _fileMetadataService.ReadMetadataAsync(metaByte, ct);

            // Neu: Prüfe Metadaten und werfe bei Fehlenden Metadaten eine Ausnahme (mit Logging)
            if (metadata == null)
            {
                _logger.LogWarning("[LabResults] Metadata for document {DocumentId} not found (UserId={UserId}).", context.DocumentId, docView.UserId);
                throw new FileNotFoundException($"Document metadata for {context.DocumentId} not found for user {docView.UserId}.");
            }

            var chatBot = context.ChatBot!;

            // call the chatbot with the image and the lab report schema
            var labReportSchemaJson = Domain.LabReportSchemaFactory.BuildLabReportSchemaJson();
            
            string question = "Analyze the document and extract all lab values.\r\nOutput only structured JSON.\r\nIf the image contains multiple data columns, return each column as a separate JSON object within an array.";
            
            var rawData = await chatBot.AnalyzeDocumentFile<LabReport>(
                imageBytes: content,
                contentType: metadata.MimeType,
                question: question,
                systemPrompt: "You are a helpful assistant that extracts structured lab report data from medical documents.",
                structuredJsonSchema: labReportSchemaJson,
                cancellationToken: ct);


            _logger.LogInformation("[LabResults] Completed processing document {DocumentId}", context.DocumentId);
        }
    }
}
