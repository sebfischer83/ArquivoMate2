using System;
using System.Collections.Generic;

namespace ArquivoMate2.Domain.Chat
{
    public record DocumentChatTurnRecorded(
        Guid DocumentId,
        string UserId,
        string Question,
        string Answer,
        string Model,
        List<DocumentChatCitation> Citations,
        List<DocumentChatReference> Documents,
        long? DocumentCount,
        DateTimeOffset OccurredAt);

    public record CatalogChatTurnRecorded(
        string UserId,
        string Question,
        string Answer,
        string Model,
        List<DocumentChatCitation> Citations,
        List<DocumentChatReference> Documents,
        long? DocumentCount,
        DateTimeOffset OccurredAt);

    public record DocumentChatCitation(string? Source, string Snippet);

    public record DocumentChatReference(
        Guid DocumentId,
        string? Title,
        string? Summary,
        DateTime? Date,
        double? Score,
        long? FileSizeBytes);
}
