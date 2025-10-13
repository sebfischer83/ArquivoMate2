using ArquivoMate2.Domain.Document;
using ArquivoMate2.Domain.Import;
using ArquivoMate2.Domain.ReadModels;
using ArquivoMate2.Shared.Models;
using Marten.Events.Aggregation;
using System.Text.Json; // Required for dynamic event payload handling
using System.Globalization; // Required for culture-invariant conversions

namespace ArquivoMate2.Infrastructure.Persistance
{
    /// <summary>
    /// Projects document-related events into the <see cref="DocumentView"/> read model for querying.
    /// </summary>
    public class DocumentProjection : SingleStreamProjection<DocumentView, Guid>
    {
        /// <summary>
        /// Initializes the read model when a document is first uploaded.
        /// </summary>
        /// <param name="e">Uploaded event payload.</param>
        /// <param name="view">The read model to update.</param>
        public void Apply(DocumentUploaded e, DocumentView view)
        {
            view.Id = e.AggregateId;
            view.UserId = e.UserId;
            view.OccurredOn = e.OccurredOn;
            view.UploadedAt = e.OccurredOn; // Capture the initial upload timestamp
        }

        /// <summary>
        /// Marks the read model as encrypted when encryption is enabled for the document.
        /// </summary>
        /// <param name="e">Encryption event.</param>
        /// <param name="view">The read model to update.</param>
        public void Apply(DocumentEncryptionEnabled e, DocumentView view)
        {
            view.Encryption = DocumentEncryptionType.Custom;
            view.OccurredOn = e.OccurredOn;
        }

        /// <summary>
        /// Sets the initial title when the aggregate provides one.
        /// </summary>
        /// <param name="e">Title initialization event.</param>
        /// <param name="view">The read model to update.</param>
        public void Apply(DocumentTitleInitialized e, DocumentView view)
        {
            if (string.IsNullOrWhiteSpace(view.Title))
                view.Title = e.Title;
            view.OccurredOn = e.OccurredOn;
        }

        /// <summary>
        /// Replaces the title when a better suggestion is produced.
        /// </summary>
        /// <param name="e">Suggestion event.</param>
        /// <param name="view">The read model to update.</param>
        public void Apply(DocumentTitleSuggested e, DocumentView view)
        {
            view.Title = e.Title; // Always overwrite; aggregate already enforces business rules
            view.OccurredOn = e.OccurredOn;
        }

        /// <summary>
        /// Stores the extracted content when OCR or parsing finishes.
        /// </summary>
        /// <param name="e">Content extraction event.</param>
        /// <param name="view">The read model to update.</param>
        public void Apply(DocumentContentExtracted e, DocumentView view)
        {
            view.Content = e.Content;
            view.ContentLength = e.Content?.Length ?? 0;
            view.OccurredOn = e.OccurredOn;
        }

        /// <summary>
        /// Applies generic field updates produced by the aggregate.
        /// </summary>
        /// <param name="e">Update event containing field/value pairs.</param>
        /// <param name="view">The read model to update.</param>
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
                    // Handle List<string> explicitly
                    if (propType == typeof(List<string>))
                    {
                        prop.SetValue(view, ToStringList(raw));
                        continue;
                    }

                    // Direct assignment when the value is already compatible
                    if (raw == null)
                    {
                        prop.SetValue(view, null);
                        continue;
                    }

                    // Convert JsonElement instances before type checks
                    if (raw is JsonElement je)
                    {
                        raw = ConvertJsonElement(je, underlying);
                        if (raw == null)
                        {
                            prop.SetValue(view, null);
                            continue;
                        }
                    }

                    // Convert enums from either string or numeric representations
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

                    // Convert string values into Guid instances
                    if (underlying == typeof(Guid) && raw is string gs && Guid.TryParse(gs, out var guidVal))
                    {
                        prop.SetValue(view, guidVal);
                        continue;
                    }

                    // Parse ISO 8601 date strings
                    if (underlying == typeof(DateTime) && raw is string ds && DateTime.TryParse(ds, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dtVal))
                    {
                        prop.SetValue(view, dtVal);
                        continue;
                    }

                    // Parse decimal values using invariant culture
                    if (underlying == typeof(decimal) && raw is string decs && decimal.TryParse(decs, NumberStyles.Any, CultureInfo.InvariantCulture, out var decVal))
                    {
                        prop.SetValue(view, decVal);
                        continue;
                    }

                    // Assign value when it already matches the property type
                    if (propType.IsInstanceOfType(raw))
                    {
                        prop.SetValue(view, raw);
                        continue;
                    }

                    // Use IConvertible where available
                    var targetForConvert = underlying;
                    if (raw is IConvertible && typeof(IConvertible).IsAssignableFrom(targetForConvert))
                    {
                        var converted = Convert.ChangeType(raw, targetForConvert, CultureInfo.InvariantCulture);
                        prop.SetValue(view, converted);
                        continue;
                    }

                    // Last resort: roundtrip through JSON serialization to coerce into the target type
                    try
                    {
                        var json = JsonSerializer.Serialize(raw);
                        var deserialized = JsonSerializer.Deserialize(json, targetForConvert);
                        prop.SetValue(view, deserialized);
                    }
                    catch
                    {
                        // Ignore conversion failures and preserve the previous value
                    }
                }
                catch
                {
                    // Intentionally ignore single property conversion errors to keep projections resilient
                    // (Marten will continue applying other events). Optionally add logging here later.
                }
            }
            view.OccurredOn = e.OccurredOn;
        }

        /// <summary>
        /// Converts a <see cref="JsonElement"/> into the desired target type when possible.
        /// </summary>
        /// <param name="je">Source JSON element.</param>
        /// <param name="target">Desired target type.</param>
        /// <returns>The converted value or <c>null</c> when conversion fails.</returns>
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

        /// <summary>
        /// Determines whether the supplied value is a numeric primitive.
        /// </summary>
        /// <param name="value">Value to inspect.</param>
        /// <returns><c>true</c> if the value is numeric; otherwise, <c>false</c>.</returns>
        private static bool IsNumeric(object value) => value is byte or sbyte or short or ushort or int or uint or long or ulong;

        /// <summary>
        /// Normalizes various payload formats into a list of strings.
        /// </summary>
        /// <param name="value">Incoming value that may represent one or multiple strings.</param>
        /// <returns>A list of strings extracted from the value.</returns>
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

        /// <summary>
        /// Marks the document as processed once background processing finishes.
        /// </summary>
        /// <param name="e">Processing completion event.</param>
        /// <param name="view">The read model to update.</param>
        public void Apply(DocumentProcessed e, DocumentView view)
        {
            view.Processed = true;
            view.OccurredOn = e.OccurredOn;
            view.ProcessedAt = e.OccurredOn; // Record when processing finished
        }

        /// <summary>
        /// Stores AI-generated metadata such as keywords and summaries.
        /// </summary>
        /// <param name="e">Chat bot data event.</param>
        /// <param name="view">The read model to update.</param>
        public void Apply(DocumentChatBotDataReceived e, DocumentView view)
        {
            view.Keywords = e.Keywords;
            view.Summary = e.Summary;
            view.CustomerNumber = e.CustomerNumber;
            view.InvoiceNumber = e.InvoiceNumber;
            view.TotalPrice = e.TotalPrice;
            view.Type = e.Type;
            view.Date = e.Date;
            view.ChatBotModel = e.ModelName; // Persist the LLM model that generated the metadata
            view.ChatBotClass = e.ChatBotClass; // Persist the classification provided by the LLM
            view.OccurredOn = e.OccurredOn;

            // store party references for later resolution in DTO mapping
            view.SenderId = e.SenderId == Guid.Empty ? null : e.SenderId;
            view.RecipientId = e.RecipientId == Guid.Empty ? null : e.RecipientId;
        }

        /// <summary>
        /// Persists storage paths once file derivatives are ready.
        /// </summary>
        /// <param name="e">Files prepared event.</param>
        /// <param name="view">The read model to update.</param>
        public void Apply(DocumentFilesPrepared e, DocumentView view)
        {
            view.FilePath = e.FilePath;
            view.MetadataPath = e.MetadataPath;
            view.ThumbnailPath = e.ThumbnailPath;
            view.PreviewPath = e.PreviewPath;
            view.ArchivePath = e.ArchivePath; // Persist the optional archive bundle path
            view.OccurredOn = e.OccurredOn;
        }

        /// <summary>
        /// Increments the note counter when a new note is added.
        /// </summary>
        /// <param name="e">Note added event.</param>
        /// <param name="view">The read model to update.</param>
        public void Apply(DocumentNoteAdded e, DocumentView view)
        {
            view.NotesCount++;
            view.OccurredOn = e.OccurredOn;
        }

        /// <summary>
        /// Decrements the note counter when a note is removed.
        /// </summary>
        /// <param name="e">Note deleted event.</param>
        /// <param name="view">The read model to update.</param>
        public void Apply(DocumentNoteDeleted e, DocumentView view)
        {
            if (view.NotesCount > 0) view.NotesCount--;
            view.OccurredOn = e.OccurredOn;
        }

        /// <summary>
        /// Stores the detected language after analysis.
        /// </summary>
        /// <param name="e">Language detection event.</param>
        /// <param name="view">The read model to update.</param>
        public void Apply(DocumentLanguageDetected e, DocumentView view)
        {
            view.Language = e.IsoCode;
            view.OccurredOn = e.OccurredOn;
        }

        /// <summary>
        /// Marks the document as deleted within the read model.
        /// </summary>
        /// <param name="e">Deletion event.</param>
        /// <param name="view">The read model to update.</param>
        public void Apply(DocumentDeleted e, DocumentView view)
        {
            view.Deleted = true;
            view.OccurredOn = e.OccurredOn;
        }
    }
}
