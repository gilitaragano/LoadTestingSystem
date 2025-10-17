using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LoadTestingSytem.Tests.Workloads.Config.Resolve.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ResolveCallsPreparationMode
    {
        Predefined,
        Cartesian,
    }

    public class ResolveCallsConfig
    {
        public ResolveCallsInfo ResolveCallsInfo { get; set; } = new();
    }

    public class ResolveCallsInfo
    {
        public ResolveCallsPreparationMode ResolveCallsPreparationMode { get; set; }

        public List<PredefinedResolveCall> PredefinedResolveCalls { get; set; } = new();
    }

    public class PredefinedResolveCall
    {
        public int UserIndex { get; set; }
        public int WorkspaceIndex { get; set; }

        public List<PredefinedResolveReference> PredefinedResolveReferences { get; set; } = new();
    }

    public class PredefinedResolveReference
    {
        public string VariableLibraryName { get; set; } = string.Empty;
        public string VariableName { get; set; } = string.Empty;
    }
}
