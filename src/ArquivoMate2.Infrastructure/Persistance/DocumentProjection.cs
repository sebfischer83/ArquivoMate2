using ArquivoMate2.Domain.Document;
using ArquivoMate2.Domain.Import;
using Marten.Events.Aggregation;
using System.Text.Json; // NEW
using System.Globalization; // NEW

namespace ArquivoMate2.Infrastructure.Persistance
{
    public class DocumentProjection : SingleStreamProjection<DocumentView, Guid>
    {
        public void Apply(DocumentUploaded e, DocumentView view)
        {
            view.Id = e.AggregateId;
            view.UserId = e.UserId;
            view.OccurredOn = e.OccurredOn;
            view.UploadedAt = e.OccurredOn; // neu gesetzt
        }

        public void Apply(DocumentTitleInitialized e, DocumentView view)
        {
            if (string.IsNullOrWhiteSpace(view.Title))
                view.Title = e.Title;
            view.OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentTitleSuggested e, DocumentView view)
        {
            view.Title = e.Title; // immer überschreiben (Logik bereits im Aggregate abgesichert)
            view.OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentContentExtracted e, DocumentView view)
        {
            view.Content = e.Content;
            view.ContentLength = e.Content?.Length ?? 0;
            view.OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentUpdated e, DocumentView view)
        {
            var type = view.GetType();
            foreach (var kvp in e.Values)
            {
                var prop = type.GetProperty(kvp.Key);
                if (prop == null || !prop.CanWrite) continue;

                var propType = prop.PropertyType;
                var underlying = Nullable.GetUnderlyingType(propType) ?? propType;
                object? raw = kvp.Value;

                try
                {
                    // Special case List<string>
                    if (propType == typeof(List<string>))
                    {
                        prop.SetValue(view, ToStringList(raw));
                        continue;
                    }

                    // If value already assignable
                    if (raw == null)
                    {
                        prop.SetValue(view, null);
                        continue;
                    }

                    // Unwrap JsonElement early
                    if (raw is JsonElement je)
                    {
                        raw = ConvertJsonElement(je, underlying);
                        if (raw == null)
                        {
                            prop.SetValue(view, null);
                            continue;
                        }
                    }

                    // Enum parsing (string or numeric)
                    if (underlying.IsEnum)
                    {
                        if (raw is string es)
                        {
                            var enumVal = Enum.Parse(underlying, es, ignoreCase: true);
                            prop.SetValue(view, enumVal);
                            continue;
                        }
                        if (IsNumeric(raw))
                        {
                            var enumVal = Enum.ToObject(underlying, raw);
                            prop.SetValue(view, enumVal);
                            continue;
                        }
                    }

                    // Guid parsing
                    if (underlying == typeof(Guid) && raw is string gs && Guid.TryParse(gs, out var guidVal))
                    {
                        prop.SetValue(view, guidVal);
                        continue;
                    }

                    // DateTime from string
                    if (underlying == typeof(DateTime) && raw is string ds && DateTime.TryParse(ds, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dtVal))
                    {
                        prop.SetValue(view, dtVal);
                        continue;
                    }

                    // Decimal from string (culture invariant)
                    if (underlying == typeof(decimal) && raw is string decs && decimal.TryParse(decs, NumberStyles.Any, CultureInfo.InvariantCulture, out var decVal))
                    {
                        prop.SetValue(view, decVal);
                        continue;
                    }

                    // Direct assign if already compatible
                    if (propType.IsInstanceOfType(raw))
                    {
                        prop.SetValue(view, raw);
                        continue;
                    }

                    // Attempt convertible change type
                    var targetForConvert = underlying;
                    if (raw is IConvertible && typeof(IConvertible).IsAssignableFrom(targetForConvert))
                    {
                        var converted = Convert.ChangeType(raw, targetForConvert, CultureInfo.InvariantCulture);
                        prop.SetValue(view, converted);
                        continue;
                    }

                    // Last resort: try JSON serialize/deserialize roundtrip to target type
                    try
                    {
                        var json = JsonSerializer.Serialize(raw);
                        var deserialized = JsonSerializer.Deserialize(json, targetForConvert);
                        prop.SetValue(view, deserialized);
                    }
                    catch
                    {
                        // swallow, keep old value
                    }
                }
                catch
                {
                    // Intentionally ignore single property conversion errors to avoid projection crash
                    // (Marten will continue applying other events). Optionally add logging here later.
                }
            }
            view.OccurredOn = e.OccurredOn;
        }

        private static object? ConvertJsonElement(JsonElement je, Type target)
        {
            if (target == typeof(string))
            {
                return je.ValueKind switch
                {
                    JsonValueKind.String => je.GetString(),
                    JsonValueKind.Number => je.ToString(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => null,
                    _ => je.ToString()
                };
            }

            if (je.ValueKind == JsonValueKind.Null) return null;

            try
            {
                switch (je.ValueKind)
                {
                    case JsonValueKind.String:
                        if (target == typeof(Guid) && Guid.TryParse(je.GetString(), out var g)) return g;
                        if (target == typeof(DateTime) && DateTime.TryParse(je.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)) return dt;
                        return je.GetString();
                    case JsonValueKind.Number:
                        if (target == typeof(int) && je.TryGetInt32(out var i)) return i;
                        if (target == typeof(long) && je.TryGetInt64(out var l)) return l;
                        if (target == typeof(decimal) && je.TryGetDecimal(out var d)) return d;
                        if (target == typeof(double) && je.TryGetDouble(out var dbl)) return dbl;
                        if (target == typeof(float) && je.TryGetDouble(out var f)) return (float)f;
                        return je.GetRawText();
                    case JsonValueKind.True:
                        if (target == typeof(bool)) return true;
                        return true;
                    case JsonValueKind.False:
                        if (target == typeof(bool)) return false;
                        return false;
                    case JsonValueKind.Array:
                        if (target == typeof(List<string>))
                        {
                            var list = new List<string>();
                            foreach (var item in je.EnumerateArray())
                            {
                                if (item.ValueKind == JsonValueKind.String) list.Add(item.GetString()!);
                                else list.Add(item.ToString());
                            }
                            return list;
                        }
                        return JsonSerializer.Deserialize(je.GetRawText(), target);
                    case JsonValueKind.Object:
                        return JsonSerializer.Deserialize(je.GetRawText(), target);
                    default:
                        return null;
                }
            }
            catch
            {
                return null;
            }
        }

        private static bool IsNumeric(object value) => value is byte or sbyte or short or ushort or int or uint or long or ulong;

        private static List<string> ToStringList(object? value)
        {
            if (value == null) return new List<string>();
            switch (value)
            {
                case List<string> ls:
                    return ls;
                case string[] sa:
                    return sa.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                case IEnumerable<string> enumStr:
                    return enumStr.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                case IEnumerable<object> enumObj:
                    return enumObj.Select(o => o?.ToString() ?? string.Empty)
                                   .Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                case string s:
                    if (string.IsNullOrWhiteSpace(s)) return new List<string>();
                    var trimmed = s.Trim();
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        try
                        {
                            var arr = JsonSerializer.Deserialize<List<string>>(trimmed);
                            if (arr != null) return arr.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                        }
                        catch { }
                    }
                    return s.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                case JsonElement je:
                    if (je.ValueKind == JsonValueKind.Array)
                    {
                        var list = new List<string>();
                        foreach (var item in je.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String)
                            {
                                var str = item.GetString();
                                if (!string.IsNullOrWhiteSpace(str)) list.Add(str);
                            }
                            else
                            {
                                var raw = item.ToString();
                                if (!string.IsNullOrWhiteSpace(raw)) list.Add(raw);
                            }
                        }
                        return list;
                    }
                    if (je.ValueKind == JsonValueKind.String)
                    {
                        var single = je.GetString();
                        return string.IsNullOrWhiteSpace(single) ? new List<string>() : new List<string> { single };
                    }
                    break;
            }
            var fallback = value.ToString();
            if (string.IsNullOrWhiteSpace(fallback)) return new List<string>();
            return fallback.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                           .Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        }

        public void Apply(DocumentProcessed e, DocumentView view)
        {
            view.Processed = true;
            view.OccurredOn = e.OccurredOn;
            view.ProcessedAt = e.OccurredOn; // neu gesetzt
        }

        public void Apply(DocumentChatBotDataReceived e, DocumentView view)
        {
            view.Keywords = e.Keywords;
            view.Summary = e.Summary;
            view.CustomerNumber = e.CustomerNumber;
            view.InvoiceNumber = e.InvoiceNumber;
            view.TotalPrice = e.TotalPrice;
            view.Type = e.Type;
            view.Date = e.Date;
            view.ChatBotModel = e.ModelName; // NEW
            view.ChatBotClass = e.ChatBotClass; // NEW
            view.OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentFilesPrepared e, DocumentView view)
        {
            view.FilePath = e.FilePath;
            view.MetadataPath = e.MetadataPath;
            view.ThumbnailPath = e.ThumbnailPath;
            view.PreviewPath = e.PreviewPath;
            view.ArchivePath = e.ArchivePath; // NEW
            view.OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentNoteAdded e, DocumentView view)
        {
            view.NotesCount++;
            view.OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentNoteDeleted e, DocumentView view)
        {
            if (view.NotesCount > 0) view.NotesCount--;
            view.OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentLanguageDetected e, DocumentView view)
        {
            view.Language = e.IsoCode;
            view.OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentDeleted e, DocumentView view)
        {
            view.Deleted = true;
            view.OccurredOn = e.OccurredOn;
        }
    }
}
