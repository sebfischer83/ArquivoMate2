namespace ArquivoMate2.Shared.Models
{
    public sealed class FeaturesDto
    {
        public bool ChatBotConfigured { get; init; }
        public bool ChatBotAvailable { get; init; }
        public string ChatBotProvider { get; init; } = string.Empty;
        public string ChatBotModel { get; init; } = string.Empty;
        public bool EmbeddingsEnabled { get; init; }
        public bool EmbeddingsClientAvailable { get; init; }
        public string EmbeddingsModel { get; init; } = string.Empty;
        public bool VectorStoreConfigured { get; init; }
        public bool VectorizationAvailable { get; init; }
    }
}
