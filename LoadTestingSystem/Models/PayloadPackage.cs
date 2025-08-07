using System.Text.Json.Serialization;

namespace LoadTestingSytem.Models
{
    public class ArtifactDefinitions
    {
        [JsonPropertyName("artifactEntries")]
        public List<ArtifactEntry> ArtifactEntries { get; set; } = new();
    }

    public class ArtifactEntry
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("artifactDefinitions")]
        public List<ArtifactDefinition> ArtifactDefinitions { get; set; } = new();
    }

    public class ArtifactDefinition
    {
        [JsonPropertyName("format")]
        public string Format { get; set; } = string.Empty;

        [JsonPropertyName("parts")]
        public List<ArtifactPart> Parts { get; set; } = new();

        [JsonPropertyName("definitionParts")]
        public List<ArtifactPart> DefinitionParts { get; set; } = new();
    }

    public class ArtifactPart
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("payload")]
        public string Payload { get; set; } = string.Empty;

        [JsonPropertyName("payloadType")]
        public string PayloadType { get; set; } = string.Empty;
    }
}
