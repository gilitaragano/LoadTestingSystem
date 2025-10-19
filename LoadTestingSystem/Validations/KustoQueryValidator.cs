using LoadTestingSytem.Models;
using Microsoft.PowerBI.Test.E2E.Common.NotebookRunners.Utils.KustoClient;

public static class KustoQueryValidator
{
    private const int PollAttemptsCount = 15;
    private const int PollDelayInMs = 60 * 1000;
    private const string KustoEndpoint = "https://pbipkustppe.kusto.windows.net";
    private const string KustoDbPbip = "pbipppe";

    public static async Task<List<string?>> RunAsync(List<KustoQueryValidation> kustoQueryValidations)
    {
        var queryResultsPerCall = new List<string?>();

        foreach (var kustoQueryValidation in kustoQueryValidations)
        {
            string query = string.Format(kustoQueryValidation.Query, kustoQueryValidation.Params.ToArray());

            object[]? queryResult = null;
            int attempts = 0;

            while (attempts < PollAttemptsCount)
            {
                Console.WriteLine($"Attempt {attempts + 1}: Querying Kusto — {query}");

                queryResult = await KustoUtils
                    .GetKustoQueryResultAsync(KustoEndpoint, KustoDbPbip, query)
                    .ConfigureAwait(false);

                if (queryResult?.FirstOrDefault() is not null)
                    break;

                await Task.Delay(PollDelayInMs);
                attempts++;
            }

            queryResultsPerCall.Add(queryResult?.FirstOrDefault()?.ToString());
        }

        return queryResultsPerCall;
    }
}
