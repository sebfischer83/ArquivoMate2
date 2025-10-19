namespace ArquivoMate2.Infrastructure.Configuration.Llm
{
    public class OpenRouterSettings : OpenAISettings
    {
        public OpenRouterSettings()
        {
            // OpenRouter can proxy to multiple model providers. "openrouter/auto" lets the
            // service select a default model while still allowing overrides via configuration.
            Model = "openrouter/auto";
        }

        public string Endpoint { get; set; } = "https://openrouter.ai/api/v1";

        public string? Referer { get; set; } = null;

        public string? SiteName { get; set; } = null;

        public string? EmbeddingsApiKey { get; set; } = null;
    }
}
