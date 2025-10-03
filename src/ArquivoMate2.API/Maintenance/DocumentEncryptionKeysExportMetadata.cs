using System;

namespace ArquivoMate2.API.Maintenance;

public sealed class DocumentEncryptionKeysExportMetadata
{
    public Guid OperationId { get; set; }

    public MaintenanceExportState State { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? CompletedUtc { get; set; }

    public string? ArchiveFileName { get; set; }

    public string? ErrorMessage { get; set; }
}
