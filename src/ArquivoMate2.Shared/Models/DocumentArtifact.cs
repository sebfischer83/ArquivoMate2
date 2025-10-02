using System.Text.Json.Serialization;

namespace ArquivoMate2.Shared.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter<DocumentArtifact>))] // .NET 9 generic converter
    public enum DocumentArtifact
    {
        File = 0,
        Preview = 1,
        Thumb = 2,
        Metadata = 3,
        Archive = 4
    }

    public static class DocumentArtifactExtensions
    {
        public static string ToWireValue(this DocumentArtifact artifact) => artifact switch
        {
            DocumentArtifact.File => "file",
            DocumentArtifact.Preview => "preview",
            DocumentArtifact.Thumb => "thumb",
            DocumentArtifact.Metadata => "metadata",
            DocumentArtifact.Archive => "archive",
            _ => "file"
        };

        public static bool TryParse(string? value, out DocumentArtifact artifact)
        {
            artifact = DocumentArtifact.File;
            if (string.IsNullOrWhiteSpace(value)) return true; // treat null as default
            return value.ToLowerInvariant() switch
            {
                "file" => (artifact = DocumentArtifact.File) == DocumentArtifact.File,
                "preview" => (artifact = DocumentArtifact.Preview) == DocumentArtifact.Preview,
                "thumb" => (artifact = DocumentArtifact.Thumb) == DocumentArtifact.Thumb,
                "metadata" => (artifact = DocumentArtifact.Metadata) == DocumentArtifact.Metadata,
                "archive" => (artifact = DocumentArtifact.Archive) == DocumentArtifact.Archive,
                _ => false
            };
        }
    }
}
