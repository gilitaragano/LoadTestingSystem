using LoadTestingSytem.Models;
using LoadTestingSytem.Tests.Workloads.Config.Resolve.Models;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoadTestingSytem.Tests.LoadUnits.Config.Resolve.Actions
{
    public static class PrepareWorkspaceItemsDetailsList
    {
        public static async Task<Dictionary<string, WorkspaceArtifact>> RunAsync(
            string baseUrl,
            string tenantAdminAccessToken,
            int loadUnitIndex,
            string workspaceNamePrefix,
            List<string> workspaceIds)
        {
            if (string.IsNullOrWhiteSpace(tenantAdminAccessToken))
            {
                Console.WriteLine("Access token is required.");
                return null!;
            }

            if (workspaceIds == null || workspaceIds.Count == 0)
            {
                Console.WriteLine("No workspace IDs provided.");
                return null!;
            }

            var publicApiWorkspaceUrl = $"{baseUrl}/v1/workspaces";
            var results = new Dictionary<string, WorkspaceArtifact>();

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantAdminAccessToken);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            foreach (var wsId in workspaceIds)
            {
                Console.WriteLine($"\nProcessing workspace ID: {wsId}");

                try
                {
                    var wsDetailsResponse = await GetWithRetriesAsync(httpClient, $"{publicApiWorkspaceUrl}/{wsId}");
                    var wsDetailsJson = await wsDetailsResponse.Content.ReadAsStringAsync();
                    var wsDetails = JsonSerializer.Deserialize<WorkspaceDetails>(wsDetailsJson);

                    var wsName = wsDetails?.DisplayName ?? string.Empty;
                    var wsPrefix = $"{workspaceNamePrefix}-{loadUnitIndex}-";

                    if (!wsName.StartsWith(wsPrefix))
                    {
                        Console.WriteLine($"Skipping workspace '{wsName}' (name does not start with {wsPrefix})");
                        continue;
                    }

                    var itemsResponse = await GetWithRetriesAsync(httpClient, $"{publicApiWorkspaceUrl}/{wsId}/items");
                    var itemsJson = await itemsResponse.Content.ReadAsStringAsync();
                    var itemsList = JsonSerializer.Deserialize<WorkspaceItemsResponse>(itemsJson);

                    string? consumingItemId = null;
                    var variableLibraries = new List<VariableLibrary>();

                    foreach (var item in itemsList?.Value ?? Enumerable.Empty<WorkspaceItem>())
                    {
                        string name = item.DisplayName ?? string.Empty;

                        if (name.StartsWith("VariableLibrary"))
                        {
                            variableLibraries.Add(new VariableLibrary
                            {
                                Id = item.Id,
                                DisplayName = item.DisplayName
                            });
                        }
                        else if (name.StartsWith("Lakehouse"))
                        {
                            consumingItemId = item.Id;
                        }
                    }

                    results[wsId] = new WorkspaceArtifact
                    {
                        ConsumingItemId = consumingItemId ?? string.Empty,
                        VariableLibraries = variableLibraries
                    };

                    Console.WriteLine($"Workspace '{wsName}' has {variableLibraries.Count} variable libraries and consuming item ID: {consumingItemId}");

                    // Optional: gentle delay to reduce risk of rate limiting
                    await Task.Delay(150);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching data for workspace {wsId}: {ex.Message}");
                }
            }

            return results;
        }

        private static async Task<HttpResponseMessage> GetWithRetriesAsync(HttpClient httpClient, string url, int maxRetries = 5)
        {
            int delayMs = 1000; // Start delay 1 second
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                var response = await httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    return response;
                }
                else if ((int)response.StatusCode == 429)
                {
                    // Try to get Retry-After header if present
                    if (response.Headers.TryGetValues("Retry-After", out var values))
                    {
                        if (int.TryParse(values.FirstOrDefault(), out int retryAfterSeconds))
                        {
                            delayMs = retryAfterSeconds * 1000;
                        }
                    }

                    Console.WriteLine($"Received 429 Too Many Requests. Retrying in {delayMs}ms...");
                    await Task.Delay(delayMs);
                    delayMs *= 2; // Exponential backoff
                    continue;
                }
                else
                {
                    // For other errors, throw
                    response.EnsureSuccessStatusCode();
                }
            }

            throw new Exception($"Failed to get a successful response after {maxRetries} retries for URL: {url}");
        }

        private class WorkspaceDetails
        {
            [JsonPropertyName("displayName")]
            public string? DisplayName { get; set; }
        }

        private class WorkspaceItemsResponse
        {
            [JsonPropertyName("value")]
            public List<WorkspaceItem>? Value { get; set; }
        }

        private class WorkspaceItem
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = null!;

            [JsonPropertyName("displayName")]
            public string? DisplayName { get; set; }
        }
    }
}
