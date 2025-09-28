namespace ArquivoMate2.Domain.Document
{
    public record DocumentNoteDeleted(Guid AggregateId, Guid NoteId, string UserId, DateTime OccurredOn) : IDomainEvent;
}
