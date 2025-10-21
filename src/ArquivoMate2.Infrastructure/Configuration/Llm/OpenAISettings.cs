using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Configuration.Llm
{
    public class OpenAISettings : ChatBotSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gpt-4";

        public bool UseBatch { get; set; } = false;

        public string EmbeddingModel { get; set; } = "text-embedding-3-small";

        public int EmbeddingDimensions { get; set; } = 1536;

        // New: allow disabling embeddings/vectorization completely via config (ChatBot:Args:EnableEmbeddings=false)
        public bool EnableEmbeddings { get; set; } = true;

        // Vision / Assistants usage
        public bool UseAssistantsVision { get; set; } = false;
        // Optional model to use for vision-capable assistant (e.g. "gpt-4o" or specific vision model)
        public string? VisionModel { get; set; } = null;
        // If set, reuse this assistant id instead of creating a new one on every request
        public string? AssistantId { get; set; } = null;
    }
}
