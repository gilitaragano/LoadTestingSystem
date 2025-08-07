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
            List<UserCert> userCertList)
        {
            var userCertWorkspaceList = new List<UserCertWorkspace>();

            var workspaceSummaries = await GetFilteredWorkspaceSummariesAsync(baseUrl, tenantAdminAccessToken, workspaceNamePrefix, loadUnitIndex);
            if (workspaceSummaries == null)
            {
                Console.WriteLine("Failed to fetch workspace summaries.");
                return userCertWorkspaceList;
            }

            foreach (var ws in workspaceSummaries)
            {
                // Extract index from name like: "AutoWS-002-ENVX"
                var match = Regex.Match(ws.DisplayName, $@"^{workspaceNamePrefix}-{loadUnitIndex}-(\d{{3}})-[0-9a-fA-F]{{8}}-[0-9a-fA-F]{{4}}-[0-9a-fA-F]{{4}}-[0-9a-fA-F]{{4}}-[0-9a-fA-F]{{12}}$");
                if (!match.Success)
                {
                    Console.WriteLine($"Skipping workspace with unexpected name format: {ws.DisplayName}");
                    continue;
                }

                int userIndex = int.Parse(match.Groups[1].Value) - 1;
                if (userIndex < 0 || userIndex >= userCertList.Count)
                {
                    Console.WriteLine($"No matching user for index {userIndex + 1} (workspace: {ws.DisplayName})");
                    continue;
                }

                var userCert = userCertList[userIndex];

                var userCertWorkspace = new UserCertWorkspace
                {
                    UserId = userCert.UserId,
                    UserName = userCert.UserName,
                    CertificateName = userCert.CertificateName,
                    WorkspaceId = ws.Id
                };

                userCertWorkspaceList.Add(userCertWorkspace);
                Console.WriteLine($"Mapped user '{userCert.UserName}' to workspace '{ws.DisplayName}' (ID: {ws.Id})");
            }

            return userCertWorkspaceList;
        }

        private static async Task<List<WorkspaceSummary>> GetFilteredWorkspaceSummariesAsync(
            string baseUrl,
            string tenantAdminAccessToken,
            string workspaceNamePrefix,
            int loadUnitIndex)
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
                    ws.DisplayName.StartsWith($"{workspaceNamePrefix}-{loadUnitIndex}-"))
                .ToList();
        }
    }
}
