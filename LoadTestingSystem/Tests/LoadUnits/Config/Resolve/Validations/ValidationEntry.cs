using System.Text.Json.Serialization;

namespace LoadTestingSytem.Tests.LoadUnits.Config.Resolve.ValidateResolveResponse
{
    public class ValidationEntry
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
        [JsonPropertyName("value")]
        public object? Value { get; set; }
    }
}
