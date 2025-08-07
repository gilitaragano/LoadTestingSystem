namespace LoadTestingSytem.Models
{
    public class RequestForValidation
    {
        public HttpRequestMessage HttpRequestMessage { get; set; }

        public string HttpRequestMessageIdentifier { get; set; } = string.Empty;
    }
}
