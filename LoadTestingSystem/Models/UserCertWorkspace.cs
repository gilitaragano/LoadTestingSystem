using System.Text.Json.Serialization;

namespace LoadTestingSytem.Models
{
    public class UserCertWorkspace : UserCert
    {
        [JsonPropertyName("workspaceId")]
        public string WorkspaceId { get; set; } = string.Empty;
    }
}
