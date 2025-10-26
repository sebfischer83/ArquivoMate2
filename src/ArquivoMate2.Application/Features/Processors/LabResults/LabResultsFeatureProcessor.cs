using System.Text.RegularExpressions;
using ArquivoMate2.Application.Features.Processors.LabResults.Domain;
using ArquivoMate2.Application.Features.Processors.LabResults.Domain.Parsing;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Document;
using Marten;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using ArquivoMate2.Application.Features.Processors.LabResults.Services;

namespace ArquivoMate2.Application.Features.Processors.LabResults
{
    public partial class LabResultsFeatureProcessor : ISystemFeatureProcessor
    {
        private readonly IQuerySession _query;
        private readonly IDocumentSession _session;
        private readonly IStorageProvider _storageProvider;
        private readonly IFileMetadataService _fileMetadataService;
        private readonly ILogger<LabResultsFeatureProcessor> _logger;
        private readonly ILabPivotUpdater _pivotUpdater;
        private readonly IParameterNormalizer _parameterNormalizer;
        private readonly IUnitNormalizer _unitNormalizer;
        public string FeatureKey => "lab-results";

        // Support numbers like ".4" or "0.4" as well as "11" and use comma or dot as decimal separator
        private static readonly Regex RangeRegex = new(@"^\s*(?<from>\d*[\.,]?\d+)\s*[-–—]\s*(?<to>\d*[\.,]?\d+)\s*$", RegexOptions.Compiled);
        private static readonly Regex SingleNumberRegex = new(@"(?<num>\d*[\.,]?\d+)", RegexOptions.Compiled);
        private static readonly Regex ResultRegex = new(@"^\s*(?<op><=|>=|<|>)?\s*(?<num>\d*[\.,]?\d+)\s*$", RegexOptions.Compiled);
        private static readonly Regex ReferenceWithComparatorRegex = new(@"^\s*(?<op><=|>=|<|>)\s*(?<rest>.+)$", RegexOptions.Compiled);

        // Note: legacy unit/parameter string normalization moved to IParameterNormalizer implementation

        public LabResultsFeatureProcessor(IQuerySession query, IDocumentSession session, IStorageProvider storageProvider, IFileMetadataService fileMetadataService, ILogger<LabResultsFeatureProcessor> logger, ILabPivotUpdater? pivotUpdater = null, IParameterNormalizer? parameterNormalizer = null, IUnitNormalizer? unitNormalizer = null)
        {
            _query = query;
            _session = session;
            _storageProvider = storageProvider;
            _fileMetadataService = fileMetadataService;
            _logger = logger;
            _pivotUpdater = pivotUpdater ?? new ArquivoMate2.Application.Features.Processors.LabResults.Services.NoopLabPivotUpdater();
            _parameterNormalizer = parameterNormalizer ?? new ArquivoMate2.Application.Features.Processors.LabResults.Services.DefaultParameterNormalizer();
            _unitNormalizer = unitNormalizer ?? new ArquivoMate2.Application.Features.Processors.LabResults.Services.DefaultUnitNormalizer();
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

            var labResults = ProcessRawData(rawData, context.DocumentId);

            // Persist lab results to Marten and update pivot in same session/transaction
            if (labResults != null && labResults.Count > 0)
            {
                foreach (var lr in labResults)
                {
                    // Store and persist the LabResult first so it is available in the database
                    _session.Store(lr);
                    await _session.SaveChangesAsync(ct);

                    // Update pivot using same session; pivot updater will store pivot but not save
                    await _pivotUpdater.AddOrUpdateAsync(_session, lr, _parameterNormalizer, ct);

                    // Persist pivot changes immediately after each insertion to make incremental progress durable
                    await _session.SaveChangesAsync(ct);
                }
            }


            _logger.LogInformation("[LabResults] Completed processing document {DocumentId}", context.DocumentId);
        }

        private List<LabResult> ProcessRawData(LabReport rawData, Guid documentId)
        {
            var results = new List<LabResult>();
            foreach (var row in rawData.Values)
            {
                LabResult labResult = new LabResult
                {
                    Id = Guid.NewGuid(),
                    DocumentId = documentId,
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

                    // Additional fallback: if comparator existed but bounds still null, attempt to find a number anywhere in the original reference string
                    if (!referenceFrom.HasValue && !referenceTo.HasValue && !string.IsNullOrWhiteSpace(point.Reference))
                    {
                        var s2 = SingleNumberRegex.Match(point.Reference);
                        if (s2.Success && decimal.TryParse(s2.Groups["num"].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var single2))
                        {
                            if (referenceComparator != null)
                            {
                                if (referenceComparator.Contains('<') || referenceComparator.Contains('≤'))
                                    referenceTo = single2;
                                else
                                    referenceFrom = single2;
                            }
                            else
                            {
                                referenceFrom = single2;
                            }
                        }
                    }

                    // Normalize unit: use same NormalizeString + case normalization and simple mapping
                    // Use IParameterNormalizer for unit normalization as well to centralize normalization logic
                    string? unit = null;
                    if (!string.IsNullOrWhiteSpace(point.Unit))
                    {
                        var u = _parameterNormalizer.Normalize(point.Unit ?? string.Empty);
                        // Map common variants to preferred casing using unit normalizer
                        var mapped = _unitNormalizer.Normalize(u);
                        unit = mapped;
                    }

                    LabResultPoint labResultPoint = new LabResultPoint
                    {
                        Parameter = point.Parameter ?? string.Empty,
                        ResultRaw = point.Result,
                        ResultNumeric = numericResult,
                        ResultComparator = resultComparator,
                        Unit = point.Unit,
                        Reference = point.Reference,
                        ReferenceComparator = referenceComparator,
                        ReferenceFrom = referenceFrom,
                        ReferenceTo = referenceTo
                    };

                    // normalize fields using instance helper that uses the injected normalizer
                    NormalizeLabResultPoint(labResultPoint);

                    // override NormalizedUnit with our unit mapping produced above
                    labResultPoint.NormalizedUnit = unit ?? labResultPoint.NormalizedUnit;

                    labResult.Points.Add(labResultPoint);
                }
                results.Add(labResult);
            }

            return results;
        }

        private void NormalizeLabResultPoint(LabResultPoint p)
        {
            if (p == null) return;

            // Normalized numeric result: currently just the parsed numeric value
            p.NormalizedResult = p.ResultNumeric;

            // Normalize unit using the injected normalizer
            if (!string.IsNullOrWhiteSpace(p.Unit))
            {
                var normalized = _parameterNormalizer.Normalize(p.Unit ?? string.Empty);
                p.NormalizedUnit = string.IsNullOrWhiteSpace(normalized) ? null : normalized;
            }
            else
            {
                p.NormalizedUnit = null;
            }

            // Reference normalization: copy parsed numeric bounds
            p.NormalizedReferenceFrom = p.ReferenceFrom;
            p.NormalizedReferenceTo = p.ReferenceTo;

            // If reference bounds are missing but comparator exists, attempt sensible fill
            if (!p.NormalizedReferenceFrom.HasValue && !p.NormalizedReferenceTo.HasValue && !string.IsNullOrWhiteSpace(p.ReferenceComparator) && p.ResultNumeric.HasValue)
            {
                var v = p.ResultNumeric.Value;
                if (p.ReferenceComparator!.Contains('<') || p.ReferenceComparator.Contains('='))
                    p.NormalizedReferenceTo = v;
                else if (p.ReferenceComparator.Contains('>') || p.ReferenceComparator.Contains('='))
                    p.NormalizedReferenceFrom = v;
            }
        }
    }
}
