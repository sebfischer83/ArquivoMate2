using System.Text.Json.Serialization;

namespace ArquivoMate2.Shared.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum EmailProviderType
    {
        IMAP,
        POP3,
        Exchange,
        Null
    }
}
