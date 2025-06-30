using ArquivoMate2.Application.Commands;
using ArquivoMate2.Shared.Models;
using ArquivoMate2.Domain.Import;
using Marten;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Handlers
{
    public class HideAllFromImportHistoryByStatusHandler : IRequestHandler<HideAllFromImportHistoryByStatusCommand, bool>
    {
        private readonly IDocumentSession _session;
        private readonly ILogger<HideAllFromImportHistoryByStatusHandler> _logger;

        public HideAllFromImportHistoryByStatusHandler(IDocumentSession session, ILogger<HideAllFromImportHistoryByStatusHandler> logger)
        {
            _session = session;
            _logger = logger;
        }

        public async Task<bool> Handle(HideAllFromImportHistoryByStatusCommand request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting to hide all import history entries with status {Status} for user {UserId}", 
                    request.DocumentProcessingStatus, request.UserId);

                // Query all ImportProcess entities that match the status and userId
                var importProcessesToHide = await _session.Query<ImportProcess>()
                    .Where(ip => ip.Status == request.DocumentProcessingStatus && 
                                ip.UserId == request.UserId && 
                                !ip.IsHidden)
                    .ToListAsync(cancellationToken);

                if (!importProcessesToHide.Any())
                {
                    _logger.LogInformation("No import processes found with status {Status} for user {UserId}", 
                        request.DocumentProcessingStatus, request.UserId);
                    return true;
                }

                _logger.LogInformation("Found {Count} import processes to hide", importProcessesToHide.Count);

                // Append HideDocumentImport event to each matching import process stream
                foreach (var importProcess in importProcessesToHide)
                {
                    var hideEvent = new HideDocumentImport(importProcess.Id, DateTime.UtcNow);
                    _session.Events.Append(importProcess.Id, hideEvent);
                    
                    _logger.LogDebug("Appended HideDocumentImport event to stream {StreamId}", importProcess.Id);
                }

                // Save all changes
                await _session.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Successfully hidden {Count} import history entries with status {Status} for user {UserId}", 
                    importProcessesToHide.Count, request.DocumentProcessingStatus, request.UserId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hiding import history entries with status {Status} for user {UserId}", 
                    request.DocumentProcessingStatus, request.UserId);
                return false;
            }
        }
    }
}
