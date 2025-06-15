using ArquivoMate2.Domain.Document;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Interfaces
{
    public interface ISearchClient
    {
        Task<bool> AddDocument(Document document);
        Task<Dictionary<string, int>> GetFacetsAsync(string userId, CancellationToken cancellationToken);
        Task<List<string>> ListUserIdsAsync(CancellationToken cancellationToken);
    }
}
