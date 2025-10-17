using ArquivoMate2.Domain.Import;
using ArquivoMate2.Shared.Models;
using System.Text.Json; // NEW

namespace ArquivoMate2.Domain.Document
{
    public class Document
    {
        public Guid Id { get; private set; }
        public string FilePath { get; private set; } = string.Empty;
        public string ThumbnailPath { get; private set; } = string.Empty;
        public string MetadataPath { get; private set; } = string.Empty;
        public string PreviewPath { get; private set; } = string.Empty;
        public string ArchivePath { get; private set; } = string.Empty;
        public string UserId { get; private set; } = string.Empty;
        public bool Accepted { get; private set; }
        public bool Processed { get; private set; }
        public bool Deleted { get; private set; }
        public string Content { get; private set; } = string.Empty;
        public string Hash { get; private set; } = string.Empty;
        public DateTime? Date { get; private set; } = null;
        public Guid Sender { get; private set; } = Guid.Empty;
        public Guid Recipient { get; private set; } = Guid.Empty;
        public string Type { get; set; } = string.Empty;
        public string CustomerNumber { get; private set; } = string.Empty;
        public string InvoiceNumber { get; private set; } = string.Empty;
        public decimal? TotalPrice { get; private set; }
        public List<string> Keywords { get; private set; } = new List<string>();
        public string Summary { get; private set; } = string.Empty;
        public string Title { get; private set; } = string.Empty;
        public string ChatBotModel { get; private set; } = string.Empty;
        public string ChatBotClass { get; private set; } = string.Empty;
        public int NotesCount { get; private set; } = 0;
        public string Language { get; private set; } = string.Empty;
        public bool Encrypted { get; private set; } // NEW
        public DocumentEncryptionType EncryptionType { get; private set; } = DocumentEncryptionType.None; // NEW
        public DateTime? OccurredOn { get; private set; }

        // store original file name for DTOs and UI
        public string OriginalFileName { get; private set; } = string.Empty;

        private string? _initialTitle;

        public Document() { }

        public void Apply(DocumentUploaded e)
        {
            Id = e.AggregateId;
            UserId = e.UserId;
            OccurredOn = e.OccurredOn;
            Hash = e.Hash;
        }

        public void Apply(DocumentEncryptionEnabled e)
        {
            Encrypted = true;
            OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentEncryptionTypeSet e)
        {
            EncryptionType = (DocumentEncryptionType)e.EncryptionType;
            if (EncryptionType != DocumentEncryptionType.None)
            {
                Encrypted = true;
            }
            OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentEncryptionKeysAdded e)
        {
            // keys selbst nicht im Aggregate speichern (Security / Minimierung)
            OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentTitleInitialized e)
        {
            if (string.IsNullOrWhiteSpace(Title))
            {
                Title = e.Title;
                _initialTitle = e.Title;
            }
            OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentTitleSuggested e)
        {
            if (string.IsNullOrWhiteSpace(Title) || (!string.IsNullOrWhiteSpace(_initialTitle) && Title == _initialTitle))
            {
                Title = e.Title;
            }
            OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentContentExtracted e)
        {
            Content = e.Content;
            OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentFilesPrepared e)
        {
            FilePath = e.FilePath;
            MetadataPath = e.MetadataPath;
            ThumbnailPath = e.ThumbnailPath;
            PreviewPath = e.PreviewPath;
            ArchivePath = e.ArchivePath;
            // record original filename if present
            if (!string.IsNullOrWhiteSpace(e.OriginalFileName)) OriginalFileName = e.OriginalFileName;
            OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentProcessed e)
        {
            Processed = true;
            OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentChatBotDataReceived e)
        {
            Sender = e.SenderId;
            Recipient = e.RecipientId;
            Date = e.Date;
            Type = e.Type;
            CustomerNumber = e.CustomerNumber;
            InvoiceNumber = e.InvoiceNumber;
            TotalPrice = e.TotalPrice;
            Keywords = e.Keywords;
            Summary = e.Summary;
            ChatBotModel = e.ModelName;
            ChatBotClass = e.ChatBotClass;
            OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentUpdated e)
        {
            var type = GetType();
            foreach (var kvp in e.Values)
            {
                var prop = type.GetProperty(kvp.Key);
                if (prop != null && prop.CanWrite)
                {
                    if (prop.PropertyType == typeof(List<string>))
                    {
                        // Robust conversion (string, JsonElement, IEnumerable<object>, etc.)
                        var list = ToStringList(kvp.Value);
                        prop.SetValue(this, list);
                        continue;
                    }

                    if (prop.PropertyType.IsEnum && kvp.Value is string enumString)
                    {
                        var enumValue = Enum.Parse(prop.PropertyType, enumString);
                        prop.SetValue(this, enumValue);
                    }
                    else if (kvp.Value == null || prop.PropertyType.IsInstanceOfType(kvp.Value))
                    {
                        prop.SetValue(this, kvp.Value);
                    }
                    else
                    {
                        var converted = Convert.ChangeType(kvp.Value, prop.PropertyType);
                        prop.SetValue(this, converted);
                    }
                }
            }
            if (!string.IsNullOrWhiteSpace(Title) && Title != _initialTitle)
                _initialTitle = null;
            OccurredOn = e.OccurredOn;
        }

        private static List<string> ToStringList(object? value)
        {
            if (value == null) return new List<string>();
            switch (value)
            {
                case List<string> ls: return ls;
                case IEnumerable<string> enumStr: return enumStr.ToList();
                case IEnumerable<object> enumObj:
                    return enumObj.Select(o => o?.ToString() ?? string.Empty).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
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

        public void Apply(DocumentDeleted e)
        {
            Deleted = true;
            OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentNoteAdded e)
        {
            NotesCount++;
            OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentNoteDeleted e)
        {
            if (NotesCount > 0) NotesCount--;
            OccurredOn = e.OccurredOn;
        }

        public void Apply(DocumentLanguageDetected e)
        {
            Language = e.IsoCode;
            OccurredOn = e.OccurredOn;
        }
    }
}
