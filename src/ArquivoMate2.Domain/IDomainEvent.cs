namespace ArquivoMate2.Domain
{
    public interface IDomainEvent
    {
        Guid AggregateId { get; }
        DateTime OccurredOn { get; }
    }
}
