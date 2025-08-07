using LoadTestingSytem.Common;
using LoadTestingSytem.Models;
using LoadTestingSytem.Tests.LoadUnits.Config.Resolve.ValidateResolveResponse;
using LoadTestingSytem.Tests.Workloads.Config.Resolve.Models;

public static class ValidateResolveResponse
{
    public static async Task<ValidationSummary> RunAsync(string dirBase, List<ResolveResultSummary> responsesForValidation)
    {
        var expectedResolvedVariablesMap =
            await Utils.LoadConfig<Dictionary<string, ValidationEntry>>(
                $"{dirBase}Validations\\ResolvedVariablesForValidation.json");

        int SuccessCallsCount = 0;
        int FailureCallsCount = 0;

        foreach (var responseForValidation in responsesForValidation)
        {
            foreach (var actual in responseForValidation.Results)
            {
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

                SuccessCallsCount++;
            }
        }

        var validationSummary = new ValidationSummary()
        {
            SuccessCallsCount = SuccessCallsCount,
            FailureCallsCount = FailureCallsCount
        };

        return validationSummary;
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
