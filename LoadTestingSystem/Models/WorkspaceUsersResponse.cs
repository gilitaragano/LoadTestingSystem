using System.Text.Json.Serialization;

public class UserCert
{
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("userName")]
    public string UserName { get; set; } = string.Empty;

    [JsonPropertyName("certificateName")]
    public string CertificateName { get; set; } = string.Empty;
}

public class UserCertWorkspace : UserCert
{
    [JsonPropertyName("workspaceIds")]
    public List<string> WorkspaceIds { get; set; } = new();
}

public class WorkspaceRoleAssignmentsResponse
{
    [JsonPropertyName("value")]
    public List<WorkspaceRoleAssignment> Value { get; set; } = new();
}

public class WorkspaceRoleAssignment
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("principal")]
    public Principal Principal { get; set; } = new();

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;
}

public class Principal
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("userDetails")]
    public UserDetails? UserDetails { get; set; }
}

public class UserDetails
{
    [JsonPropertyName("userPrincipalName")]
    public string UserPrincipalName { get; set; } = string.Empty;
}