using ArquivoMate2.Application.Interfaces;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Services.DeliveryProvider
{
    public class NoopDeliveryProvider : IDeliveryProvider
    {
        public Task<string> GetAccessUrl(string fullPath)
        {
            return Task.FromResult(fullPath);
        }
    }
}
