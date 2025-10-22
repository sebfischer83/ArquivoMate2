using System.Text.RegularExpressions;
using ArquivoMate2.Application.Features.Processors.LabResults.Domain;
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

        // Support numbers like ".4" or "0.4" as well as "11" and use comma or dot as decimal separator
        private static readonly Regex RangeRegex = new(@"^\s*(?<from>\d*[\.,]?\d+)\s*[-–—]\s*(?<to>\d*[\.,]?\d+)\s*$", RegexOptions.Compiled);
        private static readonly Regex SingleNumberRegex = new(@"(?<num>\d*[\.,]?\d+)", RegexOptions.Compiled);
        private static readonly Regex ResultRegex = new(@"^\s*(?<op><=|>=|<|>)?\s*(?<num>\d*[\.,]?\d+)\s*$", RegexOptions.Compiled);
        private static readonly Regex ReferenceWithComparatorRegex = new(@"^\s*(?<op><=|>=|<|>)\s*(?<rest>.+)$", RegexOptions.Compiled);

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

            ProcessRawData(rawData, context.DocumentId);


            _logger.LogInformation("[LabResults] Completed processing document {DocumentId}", context.DocumentId);
        }

        private void ProcessRawData(LabReport rawData, Guid documentId)
        {
            foreach (var row in rawData.Values)
            {
                LabResult labResult = new LabResult
                {
                    Id = Guid.NewGuid(),
                    Patient = rawData.Patient,
                    LabName = rawData.LabName,
                    Date = DateOnly.ParseExact(row.Date, "yyyy-MM-dd"),
                    Points = new List<LabResultPoint>()
                };

                foreach (var point in row.Measurements)
                {
                    // parse result like "<0.6" or "1.23" into comparator and numeric value
                    decimal? numericResult = null;
                    string? resultComparator = null;
                    if (!string.IsNullOrWhiteSpace(point.Result))
                    {
                        var rm = ResultRegex.Match(point.Result);
                        if (rm.Success)
                        {
                            resultComparator = rm.Groups["op"].Success ? rm.Groups["op"].Value : null;
                            var numStr = rm.Groups["num"].Value.Replace(',', '.');
                            if (decimal.TryParse(numStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                                numericResult = parsed;
                        }
                    }

                    // parse reference string like "0.70-1.25" into ReferenceFrom and ReferenceTo
                    decimal? referenceFrom = null;
                    decimal? referenceTo = null;
                    string? referenceRaw = point.Reference;
                    string? referenceComparator = null;

                    if (!string.IsNullOrWhiteSpace(referenceRaw))
                    {
                        // detect leading comparator in reference like "<0.6" or ">= 1.0"
                        var rc = ReferenceWithComparatorRegex.Match(referenceRaw);
                        if (rc.Success)
                        {
                            referenceComparator = rc.Groups["op"].Value;
                            referenceRaw = rc.Groups["rest"].Value;
                        }

                        var m = RangeRegex.Match(referenceRaw);
                        if (m.Success)
                        {
                            var gFrom = m.Groups["from"].Value.Replace(',', '.');
                            var gTo = m.Groups["to"].Value.Replace(',', '.');
                            if (decimal.TryParse(gFrom, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var f))
                                referenceFrom = f;
                            if (decimal.TryParse(gTo, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var t))
                                referenceTo = t;
                        }
                        else
                        {
                            // fallback: try to parse single number (e.g. ">= 5" or "< 1.2"), pick a sensible value
                            var s = SingleNumberRegex.Match(referenceRaw);
                            if (s.Success && decimal.TryParse(s.Groups["num"].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var single))
                            {
                                // if the original string (before removing comparator) contained a less-than sign, treat as upper bound
                                if (referenceComparator != null && (referenceComparator.Contains('<') || referenceComparator.Contains('≤')))
                                    referenceTo = single;
                                else if (referenceComparator != null && (referenceComparator.Contains('>') || referenceComparator.Contains('≥')))
                                    referenceFrom = single;
                                else if (referenceRaw.Contains('<') || referenceRaw.Contains('≤'))
                                    referenceTo = single;
                                else if (referenceRaw.Contains('>') || referenceRaw.Contains('≥'))
                                    referenceFrom = single;
                                else
                                    referenceFrom = single; // ambiguous: set as From
                            }
                        }
                    }

                    LabResultPoint labResultPoint = new LabResultPoint
                    {
                        ResultRaw = point.Result,
                        ResultNumeric = numericResult,
                        ResultComparator = resultComparator,
                        Unit = point.Unit,
                        Reference = point.Reference,
                        ReferenceComparator = referenceComparator,
                        ReferenceFrom = referenceFrom,
                        ReferenceTo = referenceTo
                    };
                    labResult.Points.Add(labResultPoint);
                }
            }
        }
    }
}
