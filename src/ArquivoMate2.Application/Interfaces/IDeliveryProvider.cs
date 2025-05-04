namespace ArquivoMate2.Application.Interfaces
{
    public interface IDeliveryProvider
    {
        Task<string> GetAccessUrl(string fullPath);
    }
}
