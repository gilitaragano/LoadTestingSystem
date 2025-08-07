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
        public int WorkspaceCount { get; set; }
        public string CapacityObjectId { get; set; } = string.Empty;
        public List<WorkspaceArtifactConfiguration> WorkspaceArtifactsByType { get; set; } = new();
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
        public string PipelineNamePrefix { get; set; } = string.Empty;
        public int PipelineCount { get; set; }
        public int StageCount { get; set; }
    }
}
