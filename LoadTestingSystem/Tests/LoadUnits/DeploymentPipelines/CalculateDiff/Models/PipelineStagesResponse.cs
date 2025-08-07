using System.Text.Json.Serialization;

namespace LoadTestingSytem.Tests.LoadUnits.DeploymentPipelines.CalculateDiff.Models
{

    public class PipelineStagesResponse
    {
        [JsonPropertyName("value")]
        public List<PipelineStage> Value { get; set; } = new();
    }

    public class PipelineStage
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = default!;

        [JsonPropertyName("order")]
        public int Order { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = default!;

        [JsonPropertyName("description")]
        public string Description { get; set; } = default!;

        [JsonPropertyName("workspaceId")]
        public string? WorkspaceId { get; set; }

        [JsonPropertyName("workspaceName")]
        public string? WorkspaceName { get; set; }

        [JsonPropertyName("isPublic")]
        public bool IsPublic { get; set; }
    }

    public class AlmPipeline
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("tenantId")]
        public int TenantId { get; set; }

        [JsonPropertyName("objectId")]
        public string ObjectId { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("lastUpdatedTime")]
        public DateTime LastUpdatedTime { get; set; }
    }

    public class AlmPipelineStagesResponse
    {
        [JsonPropertyName("stages")]
        public List<AlmPipelineStage> Stages { get; set; } = new();
    }

    public class AlmPipelineStage
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
    }

    public class DiffCalculationRequestBody
    {
        public int RetryCount { get; set; } = 5;

        [JsonPropertyName("__isGeneratedOptions")]
        public bool IsGeneratedOptions { get; set; } = true;

        public string TelemetryDescription { get; set; } = "scheduleDiff";
    }
}
