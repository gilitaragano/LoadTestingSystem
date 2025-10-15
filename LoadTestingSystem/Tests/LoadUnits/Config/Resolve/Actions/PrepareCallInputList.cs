using Actions;
using LoadTestingSytem.Common;
using LoadTestingSytem.Models;
using LoadTestingSytem.Tests.Workloads.Config.Resolve.Models;
using PowerBITokenGenerator;
using System.Net.Http.Headers;
using System.Text.Json;

namespace LoadTestingSytem.Tests.LoadUnits.Config.Resolve.Actions
{
    public static class PrepareCallInputList
    {
        public static async Task<List<RequestForValidation>> RunAsync(
            string baseUrl,
            string capacityObjectId,
            Dictionary<string, WorkspaceArtifact> workspaceArtifacts,
            List<UserCertWorkspace> userCertWorkspaceList)
        {
            var consumingItemsByWorkspace = workspaceArtifacts
                .Where(kv => !string.IsNullOrEmpty(kv.Value.ConsumingItemId))
                .ToDictionary(kv => kv.Key, kv => kv.Value.ConsumingItemId);

            Console.WriteLine("Generating PowerBI Access tokens...");
            var userCertWorkspaceTokens = await PowerBiCbaTokenProvider.RunAsync(userCertWorkspaceList);

            Console.WriteLine("Generating MWC tokens...");
            var mwcTokens = await MwcTokenProvider.GenerateTokensAsync(baseUrl, userCertWorkspaceTokens, capacityObjectId, consumingItemsByWorkspace);


            // For randomization - variableNames should be dynamically set
            var variableNames = new[] { "test", "aa", "bb", "cc", "dd", "ee" };
            var resolveRequestForValidationList = new List<RequestForValidation>();

            // For randomization - randomly select which of the WS users you want to take each time
            foreach (var userCertWorkspace in userCertWorkspaceList)
            {
                // For randomization - randomly select which single WS of the workspaceArtifacts you want to take each time
                foreach (var (wsId, artifacts) in workspaceArtifacts)
                {
                    if (!mwcTokens.TryGetValue((wsId, userCertWorkspace.UserId), out var token))
                    {
                        continue;
                    }

                    var normalizedcapacityObjectId = Utils.NormalizeGuid(capacityObjectId);
                    var clusterBase = $"https://{normalizedcapacityObjectId}.pbidedicated.windows-int.net";

                    // For randomization - randomly select the subset of artifacts.VariableLibraries you want to take each time
                    foreach (var varLib in artifacts.VariableLibraries)
                    {
                        var url = $"{clusterBase}/webapi/capacities/{capacityObjectId}/workloads/Config/ConfigService/automatic/public/workspaces/{wsId}/items/{artifacts.ConsumingItemId}/ResolveVariableReferencesV2";

                        // For randomization - should be the variableNames of the subset you choosed randmly before
                        foreach (var varName in variableNames)
                        {
                            var variableReference = $"$(/**/{varLib.DisplayName}/{varName})";

                            var body = new
                            {
                                variableReferences = new[] { variableReference }
                            };

                            var requestJson = JsonSerializer.Serialize(body);

                            var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
                            {
                                Content = new StringContent(requestJson)
                            };
                            requestMessage.Headers.Authorization =
                                new AuthenticationHeaderValue("MwcToken", token);
                            requestMessage.Content.Headers.ContentType =
                                new MediaTypeHeaderValue("application/json");

                            resolveRequestForValidationList.Add(new RequestForValidation
                            {
                                HttpRequestMessage = requestMessage,
                                HttpRequestMessageIdentifier = variableReference,
                            });
                        }
                    }
                }
            }

            return resolveRequestForValidationList;
        }
    }

}
