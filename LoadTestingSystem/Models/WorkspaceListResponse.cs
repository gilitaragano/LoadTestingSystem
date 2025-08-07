using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LoadTestingSystem.Models
{
    public class WorkspaceListResponse
    {
        [JsonPropertyName("value")]
        public List<WorkspaceSummary> Value { get; set; }
    }

    public class WorkspaceSummary
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }
    }
}
