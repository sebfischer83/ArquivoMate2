using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArquivoMate2.Shared.Models.Sharing;

namespace ArquivoMate2.Shared.Serialization;

/// <summary>
/// Serializes DocumentPermissions flags as an array of strings and deserializes from array/string/number.
/// Backward compatible with previous numeric or comma separated forms.
/// </summary>
public sealed class DocumentPermissionsJsonConverter : JsonConverter<DocumentPermissions>
{
    private static readonly DocumentPermissions[] _baseFlags = new[]
    {
        DocumentPermissions.Read,
        DocumentPermissions.Edit,
        DocumentPermissions.Delete
    };

    public override DocumentPermissions Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            DocumentPermissions value = DocumentPermissions.None;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    return value;
                if (reader.TokenType != JsonTokenType.String)
                    throw new JsonException("Permission array must contain strings.");
                var str = reader.GetString();
                value |= ParseSingle(str);
            }
            throw new JsonException("Unexpected end of JSON while reading permissions array.");
        }
        if (reader.TokenType == JsonTokenType.String)
        {
            var raw = reader.GetString();
            if (string.IsNullOrWhiteSpace(raw)) return DocumentPermissions.None;
            // allow comma/semicolon separated or single token
            var parts = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0) return DocumentPermissions.None;
            DocumentPermissions value = DocumentPermissions.None;
            foreach (var p in parts)
                value |= ParseSingle(p);
            return value;
        }
        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt32(out int num))
            {
                return (DocumentPermissions)num;
            }
            throw new JsonException("Invalid numeric value for permissions.");
        }
        throw new JsonException($"Unsupported token {reader.TokenType} for DocumentPermissions.");
    }

    public override void Write(Utf8JsonWriter writer, DocumentPermissions value, JsonSerializerOptions options)
    {
        // Represent as array of flag names (excluding None). If no flags -> empty array.
        writer.WriteStartArray();
        if (value != DocumentPermissions.None)
        {
            foreach (var flag in _baseFlags)
            {
                if (flag != DocumentPermissions.None && value.HasFlag(flag))
                {
                    writer.WriteStringValue(flag.ToString());
                }
            }
        }
        writer.WriteEndArray();
    }

    private static DocumentPermissions ParseSingle(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return DocumentPermissions.None;
        token = token.Trim();
        if (token.Equals("All", StringComparison.OrdinalIgnoreCase))
            return DocumentPermissions.All;
        if (Enum.TryParse<DocumentPermissions>(token, ignoreCase: true, out var val))
            return val;
        throw new JsonException($"Unknown permission '{token}'.");
    }
}
