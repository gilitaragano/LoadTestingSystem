using LoadTestingSystem.Models;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoadTestingSytem.Tests.LoadUnits.Config.Resolve.Actions
{
    public static class PrepareWorkspaceIdList
    {
        public static async Task<List<string>> RunAsync(string baseUrl, string tenantAdminAccessToken, int loadUnitIndex, Guid loadUnitObjectId, string workspaceNamePrefix)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                Console.WriteLine("base url is required.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(tenantAdminAccessToken))
            {
                Console.WriteLine("Access token is required.");
                return null;
            }

            Console.WriteLine("Calling Fabric API to list workspaces...");

            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantAdminAccessToken);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var publicApiWorkspaceUrl = $"{baseUrl}/v1/workspaces";
                var response = await httpClient.GetAsync(publicApiWorkspaceUrl);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();

                var workspaceListResponse = JsonSerializer.Deserialize<WorkspaceListResponse>(json);

                var allWorkspaces = workspaceListResponse?.Value ?? new List<WorkspaceSummary>();

                var filtered = allWorkspaces
                    .Where(ws => !string.IsNullOrEmpty(ws.DisplayName) && ws.DisplayName.EndsWith($"{loadUnitObjectId}"))
                    .ToList();

                if (filtered.Count == 0)
                {
                    Console.WriteLine($"No workspaces found with name ends with '{loadUnitObjectId}'.");
                    return new List<string>();
                }

                return filtered.Select(ws => ws.Id).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while calling API: {ex.Message}");
                return null;
            }
        }
    }
}
