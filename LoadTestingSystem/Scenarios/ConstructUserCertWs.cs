using LoadTestingSystem.Models;
using LoadTestingSytem.Models;
using PowerBITokenGenerator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LoadTestingSystem.Scenarios
{
    internal class ConstructUserCertWs
    {
        public static async Task<List<UserCertWorkspace>> RunAsync(
            string baseUrl,
            string tenantAdminAccessToken,
            string workspaceNamePrefix,
            int loadUnitIndex,
            Guid loadUnitObjectId,
            List<UserCert> userCertList)
        {
            var workspaceSummaries = await GetFilteredWorkspaceSummariesAsync(baseUrl, tenantAdminAccessToken, workspaceNamePrefix, loadUnitIndex, loadUnitObjectId);
            if (workspaceSummaries == null)
            {
                Console.WriteLine("Failed to fetch workspace summaries.");
                return new List<UserCertWorkspace>();
            }

            var userCertWorkspaceList = await GetWorkspaceRoleAssignmentsAsync(
                baseUrl,
                tenantAdminAccessToken,
                workspaceSummaries.Select(workspaceSummary => workspaceSummary.Id).ToList(),
                userCertList);

            return userCertWorkspaceList;
        }

        private static async Task<List<WorkspaceSummary>> GetFilteredWorkspaceSummariesAsync(
            string baseUrl,
            string tenantAdminAccessToken,
            string workspaceNamePrefix,
            int loadUnitIndex,
            Guid loadUnitObjectId)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantAdminAccessToken);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var publicApiWorkspaceUrl = $"{baseUrl}/v1/workspaces";
            var response = await httpClient.GetAsync(publicApiWorkspaceUrl);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            var workspaceListResponse = JsonSerializer.Deserialize<WorkspaceListResponse>(json);

            return workspaceListResponse?.Value?
                .Where(ws =>
                    !string.IsNullOrEmpty(ws.DisplayName) &&
                    ws.DisplayName.EndsWith($"{loadUnitObjectId}"))
                .ToList();
        }

        public static async Task<List<UserCertWorkspace>> GetWorkspaceRoleAssignmentsAsync(
            string baseUrl,
            string tenantAdminAccessToken,
            List<string> workspaceIds,
            List<UserCert> userCertList)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantAdminAccessToken);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var result = new List<UserCertWorkspace>();

            foreach (var wsId in workspaceIds)
            {
                var response = await GetWithRetryAsync(httpClient, $"{baseUrl}/v1/workspaces/{wsId}/roleAssignments", tenantAdminAccessToken);

                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var assignmentsResponse = JsonSerializer.Deserialize<WorkspaceRoleAssignmentsResponse>(json);

                if (assignmentsResponse?.Value == null) continue;

                foreach (var assignment in assignmentsResponse.Value)
                {
                    if (assignment.Principal.Type != "User") continue; // only users, skip service principals/groups

                    var userId = assignment.Principal.Id;

                    var userCert = userCertList.FirstOrDefault(u => u.UserId == userId);
                    if (userCert == null) continue; // skip if user not in provided list

                    var existing = result.FirstOrDefault(u => u.UserId == userId);
                    if (existing != null)
                    {
                        if (!existing.WorkspaceIds.Contains(wsId))
                        {
                            existing.WorkspaceIds.Add(wsId);
                        }
                    }
                    else
                    {
                        var userCertWorkspace = new UserCertWorkspace
                        {
                            UserId = userCert.UserId,
                            UserName = userCert.UserName,
                            CertificateName = userCert.CertificateName,
                            WorkspaceIds = new List<string> { wsId }
                        };

                        result.Add(userCertWorkspace);
                    }
                }
            }

            return result;
        }

        private static async Task<HttpResponseMessage> GetWithRetryAsync(HttpClient client, string url, string tenantAdminAccessToken)
        {
            while (true)
            {
                // Update the Authorization header before each request with the current token
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantAdminAccessToken);

                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                    return response;

                if ((int)response.StatusCode == 429 && response.Headers.TryGetValues("Retry-After", out var retryAfterValues))
                {
                    var retryAfter = int.TryParse(retryAfterValues.FirstOrDefault(), out var delay) ? delay : 5;
                    Console.WriteLine($"429 received. Retrying after {retryAfter} seconds...");
                    await Task.Delay(TimeSpan.FromSeconds(retryAfter));
                }
                else if ((int)response.StatusCode == 401 &&
                         response.Headers.TryGetValues("x-ms-public-api-error-code", out var errorCodeValues) &&
                         errorCodeValues.FirstOrDefault()?.Equals("TokenExpired", StringComparison.OrdinalIgnoreCase) == true)
                {
                    Console.WriteLine($"401 Unauthorized with TokenExpired. Creating new access token for tenant admin user...");
                    tenantAdminAccessToken = await PowerBiCbaTokenProvider.GetTenantAdmin();
                    // Authorization header will be updated on next iteration
                }
                else
                {
                    // TODO fix the name conflicts that happens over and over
                    var error = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Request failed: {response.StatusCode} {error}");
                }
            }
        }
    }
}
