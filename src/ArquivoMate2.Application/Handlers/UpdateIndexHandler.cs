using ArquivoMate2.Application.Commands;
using ArquivoMate2.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ArquivoMate2.Application.Handlers
{
    public class UpdateIndexHandler : IRequestHandler<UpdateIndexCommand, bool>
    {
        private readonly ISearchClient _searchClient;
        private readonly ILogger<UpdateIndexHandler> _logger;

        public UpdateIndexHandler(ISearchClient searchClient, ILogger<UpdateIndexHandler> logger)
        {
            _searchClient = searchClient;
            _logger = logger;
        }

        public async Task<bool> Handle(UpdateIndexCommand request, CancellationToken cancellationToken)
        {
            await _searchClient.UpdateDocument(request.Document);   

            return true;
        }
    }
}
