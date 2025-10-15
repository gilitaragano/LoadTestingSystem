using LoadTestingSystem.Models;
using LoadTestingSytem.Models;
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
                var url = $"{baseUrl}/v1/workspaces/{wsId}/roleAssignments";
                var response = await httpClient.GetAsync(url);
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
    }
}
