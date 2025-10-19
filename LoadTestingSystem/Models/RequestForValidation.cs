namespace LoadTestingSytem.Models
{
    public class RequestForValidation<S>
    {
        public HttpRequestMessage HttpRequestMessage { get; set; }

        public string HttpRequestMessageIdentifier { get; set; } = string.Empty;

        public S? ExpectedResultSummary { get; set; }

        public string? KustoQuery { get; set; } = string.Empty;

        public string? ExpectedKustoQueryResult { get; set; } = string.Empty;
    }
}
