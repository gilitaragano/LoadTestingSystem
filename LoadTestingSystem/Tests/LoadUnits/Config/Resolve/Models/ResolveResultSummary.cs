using System.Text.Json.Serialization;

namespace LoadTestingSytem.Tests.Workloads.Config.Resolve.Models
{
    public class ResolveResultSummary
    {
        [JsonPropertyName("results")]
        public List<ResolvedVariable> Results { get; set; } = new();
    }

    public class ResolvedVariable : ResolvedVariablePredefined
    {
        [JsonPropertyName("variableLibraryObjectId")]
        public string VariableLibraryObjectId { get; set; } = string.Empty;
    }

    public class ResolveResultSummaryPredefined
    {
        [JsonPropertyName("results")]
        public List<ResolvedVariablePredefined> Results { get; set; } = new();
    }

    public class ResolvedVariablePredefined
    {
        [JsonPropertyName("referenceString")]
        public string ReferenceString { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public object? Value { get; set; }
    }
}
