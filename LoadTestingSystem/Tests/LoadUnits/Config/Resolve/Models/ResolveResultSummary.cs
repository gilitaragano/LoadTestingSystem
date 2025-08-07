using System.Text.Json.Serialization;

namespace LoadTestingSytem.Tests.Workloads.Config.Resolve.Models
{
    public class ResolveResultSummary
    {
        [JsonPropertyName("results")]
        public List<ResolvedVariable> Results { get; set; } = new();
    }

    public class ResolvedVariable
    {
        [JsonPropertyName("referenceString")]
        public string ReferenceString { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("variableLibraryObjectId")]
        public string VariableLibraryObjectId { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public object? Value { get; set; }
    }
}
