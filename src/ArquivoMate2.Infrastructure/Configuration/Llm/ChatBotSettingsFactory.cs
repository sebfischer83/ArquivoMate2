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
            // Create the settings object from ChatBot:Args as before
            var settings = type switch
            {
                ChatbotType.OpenAI => section.GetSection("Args").Get<OpenAISettings>()
                                               ?? throw new InvalidOperationException("OpenAISettings fehlt."),
                _ => throw new InvalidOperationException($"Unbekannter ChatBot-Typ: {type}")
            };

            // If a centralized ServerConfig.ServerLanguage is defined, prefer that value
            var serverLang = _configuration["ServerConfig:ServerLanguage"]; 
            if (!string.IsNullOrWhiteSpace(serverLang))
            {
                // Use the new ServerLanguage property directly
                settings.ServerLanguage = serverLang;
            }

            return settings;
        }
    }
}