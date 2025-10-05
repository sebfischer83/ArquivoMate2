using System.Text.Json.Serialization;

namespace ArquivoMate2.Shared.Models.Sharing;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ShareTargetType
{
    User = 0,
    Group = 1
}
