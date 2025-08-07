using System.Text.Json.Serialization;

namespace LoadTestingSytem.Models
{
    public class ErrorResponse
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("subCode")]
        public int SubCode { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("timeStamp")]
        public DateTime TimeStamp { get; set; }

        [JsonPropertyName("httpStatusCode")]
        public int HttpStatusCode { get; set; }

        [JsonPropertyName("hresult")]
        public int HResult { get; set; }

        [JsonPropertyName("details")]
        public List<ErrorDetail> Details { get; set; } = new();
    }

    public class ErrorDetail
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }
}
