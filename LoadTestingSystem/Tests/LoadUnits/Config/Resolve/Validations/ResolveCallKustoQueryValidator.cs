using LoadTestingSytem.Models;
using LoadTestingSytem.Tests.Workloads.Config.Resolve.Models;
using System.Linq;

public static class ResolveCallKustoQueryValidator
{
    private const int NumMinutesToLookBack = 20;

    public static async Task<int> RunAsync(
        List<ResponseForFile<ResolveResultSummary, ResolveResultSummaryPredefined>> responsesForFile)
    {
        var responsesWithQustoQueryValidation = responsesForFile.Where(responseForFile => !string.IsNullOrEmpty(responseForFile.ExpectedKustoQueryValidation.Query)).ToList();

        if (!responsesWithQustoQueryValidation.Any())
        {
            // No kusto validation - no failures
            return 0;
        }

        List<KustoQueryValidation> kustoQueryValidations = responsesWithQustoQueryValidation
                .Select(r => new KustoQueryValidation
                {
                    Query = r.ExpectedKustoQueryValidation.Query,
                    Params = new List<string>
                    {
                        NumMinutesToLookBack.ToString(),
                        r.RequestId
                    }
                })
                .ToList();

        var actualResults = await KustoQueryValidator.RunAsync(kustoQueryValidations);

        var failureCount = 0;

        for (int i = 0; i < kustoQueryValidations.Count; i++)
        {
            var expected = responsesWithQustoQueryValidation[i].ExpectedKustoQueryValidation.ExpectedResult;
            var actual = actualResults[i];

            bool ok =
                expected is not null &&
                actual is not null &&
                actual.Contains(expected, StringComparison.OrdinalIgnoreCase);

            if (!ok)
            {
                failureCount++;
            }
        }

        return failureCount;
    }
}
