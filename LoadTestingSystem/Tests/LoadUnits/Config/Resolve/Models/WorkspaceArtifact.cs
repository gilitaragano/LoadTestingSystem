namespace LoadTestingSytem.Tests.Workloads.Config.Resolve.Models
{
    public class WorkspaceArtifact
    {
        public string ConsumingItemId { get; set; } = null!;
        public List<VariableLibrary> VariableLibraries { get; set; } = new List<VariableLibrary>();
    }
}
