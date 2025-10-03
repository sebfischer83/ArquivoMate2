using System;
using System.Threading;
using ArquivoMate2.API.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ArquivoMate2.API.Maintenance;

namespace ArquivoMate2.API.Controllers;

[ApiController]
[Route("api/maintenance")]
[ServiceFilter(typeof(ApiKeyAuthorizationFilter))]
public class MaintenanceController : ControllerBase
{
    private readonly IDocumentEncryptionKeysExportService _exportService;

    public MaintenanceController(IDocumentEncryptionKeysExportService exportService)
    {
        _exportService = exportService;
    }

    /// <summary>
    /// Starts preparing a ZIP archive containing all DocumentEncryptionKeysAdded events for backup purposes.
    /// </summary>
    [HttpPost("document-encryption-keys/export")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> StartDocumentEncryptionKeysExportAsync(CancellationToken cancellationToken)
    {
        var operationId = await _exportService.StartExportAsync(cancellationToken);
        return AcceptedAtAction(nameof(GetDocumentEncryptionKeysExportStatusAsync), new { operationId }, new DocumentEncryptionKeysExportCreatedResponse(operationId));
    }

    /// <summary>
    /// Returns the current status for a previously started document encryption key export.
    /// </summary>
    [HttpGet("document-encryption-keys/export/{operationId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentEncryptionKeysExportStatusResponse>> GetDocumentEncryptionKeysExportStatusAsync(Guid operationId, CancellationToken cancellationToken)
    {
        var metadata = await _exportService.GetExportStatusAsync(operationId, cancellationToken);
        if (metadata is null)
        {
            return NotFound();
        }

        string? downloadUrl = null;
        if (metadata.State == MaintenanceExportState.Completed)
        {
            downloadUrl = Url.ActionLink(nameof(DownloadDocumentEncryptionKeysExportAsync), values: new { operationId });
        }

        var response = new DocumentEncryptionKeysExportStatusResponse(
            metadata.OperationId,
            metadata.State,
            metadata.CreatedUtc,
            metadata.CompletedUtc,
            metadata.ErrorMessage,
            downloadUrl,
            metadata.ArchiveFileName);

        return Ok(response);
    }

    /// <summary>
    /// Downloads the generated ZIP archive after the export is complete.
    /// </summary>
    [HttpGet("document-encryption-keys/export/{operationId:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadDocumentEncryptionKeysExportAsync(Guid operationId, CancellationToken cancellationToken)
    {
        var download = await _exportService.GetDownloadAsync(operationId, cancellationToken);
        if (download is null)
        {
            return NotFound();
        }

        var contentType = "application/zip";
        return PhysicalFile(download.FilePath, contentType, download.DownloadFileName);
    }
}
