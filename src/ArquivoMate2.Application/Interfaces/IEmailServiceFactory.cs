namespace ArquivoMate2.Application.Interfaces
{
    public interface IEmailServiceFactory
    {
        Task<IEmailService> CreateEmailServiceAsync(CancellationToken cancellationToken = default);

        Task<List<IEmailService>> CreateEmailServicesAsync(CancellationToken cancellationToken = default);
    }
}