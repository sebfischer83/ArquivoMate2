namespace ArquivoMate2.Domain.Document
{
    public record DocumentNoteAdded(Guid AggregateId, Guid NoteId, string UserId, DateTime OccurredOn) : IDomainEvent;
}
