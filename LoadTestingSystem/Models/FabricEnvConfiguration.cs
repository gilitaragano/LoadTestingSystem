namespace LoadTestingSytem.Models
{
    public class FabricEnvConfiguration
    {
        public WorkspacesConfiguration WorkspacesConfiguration { get; set; } = new();
        public DeploymentPipelinesConfiguration DeploymentPipelinesConfiguration { get; set; } = new();
    }

    public class WorkspacesConfiguration
    {
        public string WorkspaceNamePrefix { get; set; } = string.Empty;
        public List<WorkspaceDetails> WorkspaceDetailsList { get; set; } = new();
        public string CapacityObjectId { get; set; } = string.Empty;
        public List<WorkspaceArtifactConfiguration> WorkspaceArtifactsByType { get; set; } = new();
    }

    public class WorkspaceDetails
    {
        public List<UserInfo> UserInfoList { get; set; } = new();
    }

    public class UserInfo
    {
        public int Index { get; set; }

        public string Role { get; set; } = string.Empty;
    }


    public class WorkspaceArtifactConfiguration
    {
        public string Type { get; set; } = string.Empty;

        public List<WorkspaceArtifactItemConfiguration> Items { get; set; } = new();
    }

    public class WorkspaceArtifactItemConfiguration
    {
        public int Count { get; set; }

        public string? Definition { get; set; }
    }

    public class DeploymentPipelinesConfiguration
    {
        public int PipelineCount { get; set; } = 0;

        public string PipelineNamePrefix { get; set; } = string.Empty;
    }
}
