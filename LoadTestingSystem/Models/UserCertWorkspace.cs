using System.Text.Json.Serialization;

namespace LoadTestingSytem.Models
{
    public class UserCertWorkspace : UserCert
    {
        [JsonPropertyName("workspaceIds")]
        public List<string> WorkspaceIds { get; set; } = new();
    }
}
