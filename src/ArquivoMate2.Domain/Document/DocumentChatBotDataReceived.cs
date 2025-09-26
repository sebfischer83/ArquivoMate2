namespace ArquivoMate2.Domain.Document
{
    public record DocumentChatBotDataReceived(
            Guid AggregateId,
            Guid SenderId,
            Guid RecipientId,
            DateTime? Date,
            DateTime OccurredOn,
            string Type,
            string CustomerNumber,
            string InvoiceNumber,
            decimal? TotalPrice,
            List<string> Keywords,
            string Summary,
            string ModelName,
            string ChatBotClass) : IDomainEvent;

}
