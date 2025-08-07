using LoadTestingSytem.Common;
using LoadTestingSytem.Models;
using LoadTestingSytem.Tests.LoadUnits.DeploymentPipelines.CalculateDiff.Models;
using PowerBITokenGenerator;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Scenarios
{
    public static class PrepareLoadUnitFabricEnv
    {
        public static async Task<List<UserCertWorkspace>> RunAsync(
            string loadUnitName, 
            int loadUnitIndex,
            string testEnvPreparationConfigFilePath, 
            string definitionsFilePath,
            List<UserCert> userCertList)
        {
            Console.WriteLine($"\nStarting fabric env creation for load unit {loadUnitName}, index: {loadUnitIndex}");

            var loadTestConfig = await Utils.LoadConfig<LoadTestConfig>($"Creation\\LoadTestConfiguration.json");

            List<UserCertWorkspace> userCertWorkspaceList;

            var tenantAdminAccessToken = await PowerBiCbaTokenProvider.GetTenantAdmin();

            // Run the creation
            Console.WriteLine("\nCreating Fabric Load Testing Environment...");
            var creator = new CreateFabricLoadUnitEnv(loadUnitIndex, loadTestConfig.BaseUrl, tenantAdminAccessToken, testEnvPreparationConfigFilePath, definitionsFilePath, userCertList);

            userCertWorkspaceList = await creator.RunAsync();

            Console.WriteLine("\nFabric Load Testing Environment setup complete.");

            return userCertWorkspaceList;
        }
    }

    public class CreateFabricLoadUnitEnv
    {
        private int _loadUnitIndex;
        private string _baseUrl;
        private string _publicApiBaseUrl;
        private string _tenantAdminAccessToken;
        private readonly FabricEnvConfiguration _fabricEnvConfiguration;
        private readonly ArtifactDefinitions? _definitions;
        private readonly List<UserCert> _userCertList;

        public CreateFabricLoadUnitEnv(
            int loadUnitIndex, 
            string baseUrl, 
            string tenantAdminAccessToken, 
            string testEnvPreparationConfigFilePath, 
            string definitionsFilePath,
            List<UserCert> userCertList)
        {
            _tenantAdminAccessToken = tenantAdminAccessToken;
            _baseUrl = baseUrl;
            _publicApiBaseUrl = $"{_baseUrl}/v1";
            _loadUnitIndex = loadUnitIndex;
            _userCertList = userCertList;

            var fabricEnvConfigurationFile = File.ReadAllText(testEnvPreparationConfigFilePath);
            _fabricEnvConfiguration = JsonSerializer.Deserialize<FabricEnvConfiguration>(fabricEnvConfigurationFile) ?? throw new InvalidOperationException("fabricEnvConfiguration file is invalid");

            if (definitionsFilePath != null)
            {
                var defFile = File.ReadAllText(definitionsFilePath);
                _definitions = JsonSerializer.Deserialize<ArtifactDefinitions>(defFile) ?? throw new InvalidOperationException("Definition file is invalid");
            }
        }

        public async Task<List<UserCertWorkspace>> RunAsync()
        {
            var userCertWorkspaceList = new List<UserCertWorkspace>();
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _tenantAdminAccessToken);

            for (int i = 1; i <= _fabricEnvConfiguration.WorkspacesConfiguration.WorkspaceCount; i++)
            {
                var workspaceName = $"{_fabricEnvConfiguration.WorkspacesConfiguration.WorkspaceNamePrefix}-{_loadUnitIndex}-{i:D3}-{Guid.NewGuid()}";
                Console.WriteLine($"\nCreating workspace: {workspaceName}");

                var workspaceBody = new
                {
                    DisplayName = workspaceName,
                    capacityId = _fabricEnvConfiguration.WorkspacesConfiguration.CapacityObjectId
                };

                var workspaceResponse = await PostWithRetryAsync(httpClient, $"{_publicApiBaseUrl}/workspaces", workspaceBody);
                var workspaceJson = JsonDocument.Parse(await workspaceResponse.Content.ReadAsStringAsync());
                var workspaceId = workspaceJson.RootElement.GetProperty("id").GetString();
                Console.WriteLine($"Workspace created with ID: {workspaceId}");

                var userCert = _userCertList[(i - 1) % _userCertList.Count];
                var username = userCert.UserName;
                Console.WriteLine($"Adding user '{username}' as admin to workspace '{workspaceName}'");

                var requestBody = new
                {
                    principal = new
                    {
                        id = userCert.UserId,
                        type = "User"
                    },
                    role = "Admin"
                };
                var addUserUrl = $"{_publicApiBaseUrl}/workspaces/{workspaceId}/roleAssignments";
                await PostWithRetryAsync(httpClient, addUserUrl, requestBody);
                Console.WriteLine($"User '{username}' added as admin.");

                foreach (var WorkspaceArtifacts in _fabricEnvConfiguration.WorkspacesConfiguration.WorkspaceArtifactsByType)
                {
                    for (int j = 1; j <= WorkspaceArtifacts.Items.Count; j++)
                    {
                        var itemType = WorkspaceArtifacts.Type;
                        var itemName = $"{WorkspaceArtifacts.Type}{j}";
                        Console.WriteLine($"Creating item '{itemName}' of type '{itemType}'...");

                        var definition = _definitions == null ? null : GetArtifactDefinition(itemType, j);

                        if (itemType == "ReportAndSemanticModel") //TODO make this part more generic
                        {
                            var itemBody = new
                            {
                                definitionParts = definition.DefinitionParts
                            };

                            var response = await PostWithRetryAsync(httpClient, $"{_publicApiBaseUrl}/workspaces/{workspaceId}/importItemDefinitions", itemBody);
                        }
                        else
                        {
                            var itemBody = new
                            {
                                displayName = itemName,
                                type = itemType,
                                definition
                            };

                            var response = await PostWithRetryAsync(httpClient, $"{_publicApiBaseUrl}/workspaces/{workspaceId}/items", itemBody);
                        }

                        Console.WriteLine("Item created successfully");
                    }
                }

                var userCertWorkspace = new UserCertWorkspace
                {
                    UserId = userCert.UserId,
                    UserName = userCert.UserName,
                    CertificateName = userCert.CertificateName,
                    WorkspaceId = workspaceId
                };

                userCertWorkspaceList.Add(userCertWorkspace);
            }

            var _workspaceIds = userCertWorkspaceList.Select(ucw => ucw.WorkspaceId).ToList();
            var deploymentPipelineConfiguration = _fabricEnvConfiguration.DeploymentPipelinesConfiguration;
            var pipelineCount = deploymentPipelineConfiguration == null ? 0 : deploymentPipelineConfiguration.PipelineCount;
            var stagesPerPipelineCount = deploymentPipelineConfiguration == null ? 0 : deploymentPipelineConfiguration.StageCount;

            if (_workspaceIds == null || _workspaceIds.Count < pipelineCount * stagesPerPipelineCount)
                throw new ArgumentException($"Provide exactly {pipelineCount * stagesPerPipelineCount} workspace ID.");

            if (pipelineCount > 0 && stagesPerPipelineCount > 0)
            { // TODO temp delay to avoid WorkspaceMigrationOperationInProgress failure
                var delayInSecForWorkspaceMigrationOperationComletion = 2 * 60;
                Console.WriteLine($"\nDelay of {delayInSecForWorkspaceMigrationOperationComletion} seconds to allow workspace capacity migration to be completed...");
                await Task.Delay(TimeSpan.FromSeconds(delayInSecForWorkspaceMigrationOperationComletion));
            }

            for (int p = 1; p <= pipelineCount; p++)
            {
                var pipelineName = $"{_fabricEnvConfiguration.DeploymentPipelinesConfiguration.PipelineNamePrefix}-{p:D3}";
                Console.WriteLine($"\nCreating deployment pipeline: {pipelineName}");

                var pipelineId = await CreatePipelineAsync(pipelineName, stagesPerPipelineCount);
                Console.WriteLine($"✅ Created pipeline '{pipelineName}', ID = {pipelineId}");

                var pipelineStages = await GetPipelineStagesAsync(pipelineId);

                for (int s = 0; s < stagesPerPipelineCount; s++)
                {
                    var stage = pipelineStages[s];
                    var stageId = stage.Id;
                    var workspaceId = _workspaceIds[(p - 1) * stagesPerPipelineCount + s];

                    await AssignWorkspaceToStageAsync(pipelineId, stageId, workspaceId);
                    Console.WriteLine($"   ➤ Assigned workspace {workspaceId} to stage {stage.DisplayName}");
                }
            }

            Console.WriteLine("\nDone!");
            
            return userCertWorkspaceList;
        }

        private async Task<string> CreatePipelineAsync(string pipelineName, int stagesCount)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _tenantAdminAccessToken);

            var stages = Enumerable.Range(1, stagesCount).Select(i => new
            {
                displayName = $"Stage{i}",
                description = $"Auto-generated stage {i}",
                isPublic = i > 1 // First stage is private, others are public
            }).ToArray();

            var body = new
            {
                displayName = pipelineName,
                description = $"Auto-generated deployment pipeline",
                stages
            };

            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"{_publicApiBaseUrl}/deploymentPipelines", content);
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("id").GetString()!;
        }


        private async Task<PipelineStage[]> GetPipelineStagesAsync(string pipelineId)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _tenantAdminAccessToken);

            var response = await httpClient.GetAsync($"{_publicApiBaseUrl}/deploymentPipelines/{pipelineId}/stages");

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();

            var parsed = JsonSerializer.Deserialize<PipelineStagesResponse>(json);
            return parsed?.Value?.ToArray() ?? Array.Empty<PipelineStage>();
        }

        private async Task AssignWorkspaceToStageAsync(string pipelineId, string stageId, string workspaceId)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _tenantAdminAccessToken);

            var payload = new { workspaceId };

            var response = await PostWithRetryAsync(httpClient, $"{_publicApiBaseUrl}/deploymentPipelines/{pipelineId}/stages/{stageId}/assignWorkspace", payload);

            response.EnsureSuccessStatusCode();
        }

        private ArtifactDefinition? GetArtifactDefinition(string itemType, int itemIndex)
        {
            var artifactsGroup = _definitions.ArtifactEntries.Find(artifact => artifact.Type == itemType);
            return artifactsGroup == null ? null : artifactsGroup.ArtifactDefinitions[itemIndex % artifactsGroup.ArtifactDefinitions.Count];
        }

        private async Task<HttpResponseMessage> PostWithRetryAsync(HttpClient client, string url, object body)
        {
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            while (true)
            {
                // Update the Authorization header before each request with the current token
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _tenantAdminAccessToken);

                var response = await client.PostAsync(url, content);

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
                    _tenantAdminAccessToken = await PowerBiCbaTokenProvider.GetTenantAdmin();
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
