using ArquivoMate2.Shared.Models;
using ArquivoMate2.Application.Queries.Features;
using ArquivoMate2.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Handlers.Features
{
    public class GetFeaturesHandler : IRequestHandler<GetFeaturesQuery, FeaturesDto>
    {
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;

        public GetFeaturesHandler(IConfiguration configuration, IServiceProvider serviceProvider)
        {
            _configuration = configuration;
            _serviceProvider = serviceProvider;
        }

        public Task<FeaturesDto> Handle(GetFeaturesQuery request, CancellationToken cancellationToken)
        {
            var chatBotSection = _configuration.GetSection("ChatBot");
            var providerType = chatBotSection.GetValue<string>("Type");
            var argsSection = chatBotSection.GetSection("Args");

            bool chatBotConfigured = chatBotSection.Exists() && !string.IsNullOrWhiteSpace(providerType);

            bool? enableEmbeddings = null;
            if (argsSection.Exists())
            {
                var emb = argsSection.GetValue<bool?>("EnableEmbeddings");
                if (emb.HasValue) enableEmbeddings = emb.Value;
            }
            var embeddingsEnabled = enableEmbeddings ?? true;

            var vectorStoreConn = _configuration.GetConnectionString("VectorStore");
            var vectorStoreConfigured = !string.IsNullOrWhiteSpace(vectorStoreConn) && embeddingsEnabled;

            var chatBot = _serviceProvider.GetService(typeof(IChatBot)) as IChatBot;
            var vectorSvc = _serviceProvider.GetService(typeof(ArquivoMate2.Application.Interfaces.IDocumentVectorizationService));
            var embeddingsClient = _serviceProvider.GetService(typeof(ArquivoMate2.Application.Interfaces.IEmbeddingsClient));

            bool chatBotAvailable = chatBot != null && chatBot.GetType().Name != "NullChatBot";
            bool vectorizationAvailable = vectorSvc != null && vectorSvc.GetType().Name != "NullDocumentVectorizationService" && vectorStoreConfigured;
            bool embeddingsClientAvailable = embeddingsClient != null && embeddingsClient.GetType().Name != "NullEmbeddingsClient" && embeddingsEnabled;

            var model = argsSection.GetValue<string>("Model") ?? argsSection.GetValue<string>("model") ?? string.Empty;
            var embeddingModel = argsSection.GetValue<string>("EmbeddingModel") ?? argsSection.GetValue<string>("embeddingModel") ?? string.Empty;

            var result = new FeaturesDto
            {
                ChatBotConfigured = chatBotConfigured,
                ChatBotAvailable = chatBotAvailable,
                ChatBotProvider = providerType ?? string.Empty,
                ChatBotModel = model,
                EmbeddingsEnabled = embeddingsEnabled,
                EmbeddingsClientAvailable = embeddingsClientAvailable,
                EmbeddingsModel = embeddingModel,
                VectorStoreConfigured = vectorStoreConfigured,
                VectorizationAvailable = vectorizationAvailable
            };

            return Task.FromResult(result);
        }
    }
}
