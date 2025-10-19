using LoadTestingSytem.Common;
using LoadTestingSytem.Models;
using LoadTestingSytem.Tests.LoadUnits.Config.Resolve.ValidateResolveResponse;
using LoadTestingSytem.Tests.Workloads.Config.Resolve.Models;
using System.Text.Json.Serialization;

public class ValidationEntry
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    [JsonPropertyName("value")]
    public object? Value { get; set; }
}

public static class ResolveCallResultsSummaryValidator
{
    public static async Task<int> RunAsync(ResolveCallsInfo resolveCallsInfo, string dirBase, List<ResponseForFile<ResolveResultSummary, ResolveResultSummaryPredefined>> responsesForFile)
    {
        var mode = resolveCallsInfo.ResolveCallsPreparationMode;

        if (mode == ResolveCallsPreparationMode.Cartesian)
        {
            return await ValidateResolveCallResultsOnCartesianMode(
                dirBase,
                responsesForFile);
        }
        else if (mode == ResolveCallsPreparationMode.Predefined)
        {
            return await validateResolveCallResultsOnPredefinedMode(
                responsesForFile);
        }
        else
        {
            throw new ArgumentException($"unsupported ResolveCallsPreparationMode ({mode})");
        }
    }

    private static async Task<int> ValidateResolveCallResultsOnCartesianMode(string dirBase, List<ResponseForFile<ResolveResultSummary, ResolveResultSummaryPredefined>> responsesForFile)
    {
        var expectedResolvedVariablesMap =
                await Utils.LoadConfig<Dictionary<string, ValidationEntry>>(
                    $"{dirBase}Validations\\ResolvedVariablesForValidation.json");

        int FailureCallsCount = 0;

        foreach (var responseForFile in responsesForFile)
        {
            foreach (var actual in responseForFile.ResultSummary.Results)
            {
                var expectedResultStatus = "Ok";

                if (actual.Status != expectedResultStatus)
                {
                    Console.WriteLine($"resolved variable status is not {expectedResultStatus}, it is: {actual.Status}");
                    FailureCallsCount++;
                    continue;
                }

                if (!expectedResolvedVariablesMap.TryGetValue(actual.ReferenceString, out var expected))
                {
                    Console.WriteLine($"Expected result for variable '{actual.ReferenceString}' not found.");
                    FailureCallsCount++;
                    continue;
                }

                if (!Compare(expected, actual, out string diff))
                {
                    Console.WriteLine($"Mismatch for variable '{actual.ReferenceString}': {diff}");
                    FailureCallsCount++;
                    continue;
                }
            }
        }

        return FailureCallsCount;
    }

    private static async Task<int> validateResolveCallResultsOnPredefinedMode(
        List<ResponseForFile<ResolveResultSummary, ResolveResultSummaryPredefined>> responsesForFile)
    {
        int failureCallsCount = 0;

        foreach (var responseForFile in responsesForFile)
        {
            var actual = responseForFile.ResultSummary;
            var expected = responseForFile.ExpectedResultSummary;

            if (!CompareResolveResultSummary(actual, expected))
                failureCallsCount++;
        }

        return await Task.FromResult(failureCallsCount);
    }

    private static bool CompareResolveResultSummary(ResolveResultSummary? actualSummary, ResolveResultSummaryPredefined? expectedSummary)
    {
        if (actualSummary is null && expectedSummary is null) return true;
        if (actualSummary is null || expectedSummary is null) return false;

        // index the expected list by referenceString for lookup
        var expectedMap = expectedSummary.Results
            .ToDictionary(r => r.ReferenceString, StringComparer.Ordinal);

        // track matched actuals
        var matchedKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var actual in actualSummary.Results)
        {
            if (!expectedMap.TryGetValue(actual.ReferenceString, out var expected))
                return false; // actual result with no expected

            if (!CompareResolvedVariable(actual, expected))
                return false;

            matchedKeys.Add(actual.ReferenceString);
        }

        // if expected has items that were never matched by actual → fail
        if (matchedKeys.Count != expectedMap.Count)
            return false;

        return true;
    }

    private static bool CompareResolvedVariable(ResolvedVariable actual, ResolvedVariablePredefined expected)
    {
        if (!string.Equals(actual.ReferenceString, expected.ReferenceString, StringComparison.Ordinal)) {
            return false;
        }
        if (!string.Equals(actual.Status, expected.Status, StringComparison.Ordinal))
        {
            return false;
        }
        if (!string.Equals(actual.Type, expected.Type, StringComparison.Ordinal))
        {
            return false;
        }
        return CompareResolvedVariableValue(actual.Value, expected.Value);
    }

    private static bool CompareResolvedVariableValue(object? v1, object? v2)
    {
        if (ReferenceEquals(v1, v2)) return true;
        if (v1 is null || v2 is null) return false;

        return string.Equals(
            System.Text.Json.JsonSerializer.Serialize(v1),
            System.Text.Json.JsonSerializer.Serialize(v2),
            StringComparison.Ordinal);
    }

    private static bool Compare(ValidationEntry expected, ResolvedVariable actual, out string diff)
    {
        if (expected.Status != actual.Status)
        {
            diff = $"Status mismatch: expected '{expected.Status}', got '{actual.Status}'";
            return false;
        }

        if (expected.Type != actual.Type)
        {
            diff = $"Type mismatch: expected '{expected.Type}', got '{actual.Type}'";
            return false;
        }

        if (!Equals(expected.Value?.ToString(), actual.Value?.ToString()))
        {
            diff = $"Value mismatch: expected '{expected.Value}', got '{actual.Value}'";
            return false;
        }

        diff = null;
        return true;
    }
}
