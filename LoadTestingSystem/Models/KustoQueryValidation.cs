using System.Text.Json.Serialization;

namespace LoadTestingSytem.Models
{
    public class KustoQueryValidation
    {
        [JsonPropertyName("query")]
        public string Query { get; set; }

        [JsonPropertyName("params")]
        public List<string> Params { get; set; }
    }
}
