using System.Text.Json.Serialization;

namespace LoadTestingSytem.Models
{
    public class UserCertWorkspaceToken : UserCertWorkspace
    {
        [JsonPropertyName("accessToken")]
        public string AccessToken { get; set; } = string.Empty;
    }
}
