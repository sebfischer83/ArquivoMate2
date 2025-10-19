using Microsoft.Extensions.Configuration;

namespace ArquivoMate2.Infrastructure.Configuration.Llm
{
    public class ChatBotSettingsFactory
    {
        private readonly IConfiguration _configuration;

        public ChatBotSettingsFactory(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public ChatBotSettings GetChatBotSettings()
        {
            var section = _configuration.GetSection("ChatBot");
            var type = section.GetValue<ChatbotType?>("Type")
                       ?? throw new InvalidOperationException("ChatBot:Type ist nicht konfiguriert.");

            return type switch
            {
                ChatbotType.OpenAI => section.GetSection("Args").Get<OpenAISettings>()
                                               ?? throw new InvalidOperationException("OpenAISettings fehlt."),
                ChatbotType.OpenRouter => section.GetSection("Args").Get<OpenRouterSettings>()
                                                   ?? throw new InvalidOperationException("OpenRouterSettings fehlt."),
                //FileProviderType.NFS => section.Get<NfsFileProviderSettings>()
                //                               ?? throw new InvalidOperationException("NfsFileProviderSettings fehlt."),
                //FileProviderType.Bunny => section.Get<BunnyFileProviderSettings>()
                //                               ?? throw new InvalidOperationException("BunnyFileProviderSettings fehlt."),
                _ => throw new InvalidOperationException($"Unbekannter ChatBot-Typ: {type}")
            };
        }
    }
}