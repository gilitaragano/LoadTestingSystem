using System.Text.Json.Serialization;

namespace LoadTestingSytem.Models
{
    public class ResponseForFile<T> : ResponseForValidation<T>
    {
        [JsonPropertyName("startedAt")]
        public DateTime StartedAt { get; set; }

        [JsonPropertyName("finishedAt")]
        public DateTime FinishedAt { get; set; }

        [JsonPropertyName("durationMs")]
        public double DurationMs { get; set; }
    }
}
