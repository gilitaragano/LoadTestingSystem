using System.Text.Json.Serialization;

namespace LoadTestingSytem.Models
{
    public class ResponseForFile<T, S> : ResponseForValidation<T>
    {
        [JsonPropertyName("startedAt")]
        public DateTime StartedAt { get; set; }

        [JsonPropertyName("finishedAt")]
        public DateTime FinishedAt { get; set; }

        [JsonPropertyName("durationMs")]
        public double DurationMs { get; set; }
        [JsonPropertyName("expectedResultSummary")]
        public S? ExpectedResultSummary { get; set; }

        [JsonPropertyName("kustoQueryValidation")]
        public ExpectedKustoQueryValidation? ExpectedKustoQueryValidation { get; set; } = new();
    }

    public class ExpectedKustoQueryValidation
    {
        public string Query { get; set; } = string.Empty;

        public string ExpectedResult { get; set; } = string.Empty;
    }
}
