namespace ArquivoMate2.Infrastructure.Configuration.Llm
{
    public class OpenRouterSettings : ChatBotSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://api.openrouter.ai/";
        public string Model { get; set; } = "gpt-4o";
        public string EmbeddingModel { get; set; } = "text-embedding-3-small";
        public bool UseBatch { get; set; } = false;
        // New: allow disabling embeddings
        public bool EnableEmbeddings { get; set; } = true;
    }
}