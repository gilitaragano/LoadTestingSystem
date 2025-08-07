using System.Text.Json.Serialization;

namespace LoadTestingSytem.Models
{
    public class ResponseForValidation<T>
    {
        [JsonPropertyName("requestIdentifier")]
        public string RequestIdentifier { get; set; } = string.Empty;

        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("resultSummary")]
        public T? ResultSummary { get; set; }

        [JsonPropertyName("error")]
        public ErrorResponse? Error { get; set; }
    }

    public class ValidationSummary
    {
        public int SuccessCallsCount { get; set; }

        public int FailureCallsCount { get; set; }
    }

}
