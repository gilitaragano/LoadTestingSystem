using System.Text.Json.Serialization;

namespace LoadTestingSytem.Models
{
    public class UserCert
    {
        [JsonPropertyName("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("userName")]
        public string UserName { get; set; } = string.Empty;

        [JsonPropertyName("certificateName")]
        public string CertificateName { get; set; } = string.Empty;
    }
}
