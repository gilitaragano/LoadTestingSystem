using System.Text.Json.Serialization;

namespace LoadTestingSytem.Tests.LoadUnits.Config.Resolve.ValidateResolveResponse
{
    public class ResolveValidationSummary
    {
        public int FailedResolvedValueValidationCallsCount { get; set; }
        public int FailedResolvedStatusValidationCallsCount { get; set; }
    }
}
