using Actions;
using LoadTestingSytem.Common;
using LoadTestingSytem.Models;
using LoadTestingSytem.Tests.Workloads.Config.Resolve.Models;
using Newtonsoft.Json.Linq;
using PowerBITokenGenerator;
using System;
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
            List<UserCertWorkspace> userCertWorkspaceList,
            ResolveCallsInfo resolveCallsInfo)
        {
            var consumingItemsByWorkspace = workspaceArtifacts
                .Where(kv => !string.IsNullOrEmpty(kv.Value.ConsumingItemId))
                .ToDictionary(kv => kv.Key, kv => kv.Value.ConsumingItemId);

            Console.WriteLine("Generating PowerBI Access tokens...");
            var userCertWorkspaceTokens = await PowerBiCbaTokenProvider.RunAsync(userCertWorkspaceList);

            Console.WriteLine("Generating MWC tokens...");
            var mwcTokens = await MwcTokenProvider.GenerateTokensAsync(baseUrl, userCertWorkspaceTokens, capacityObjectId, consumingItemsByWorkspace);

            if (resolveCallsInfo.ResolveCallsPreparationMode == ResolveCallsPreparationMode.Predefined)
            {
                return GeneratePredefinedResolveCalls(
                    resolveCallsInfo.PredefinedResolveCalls,
                    userCertWorkspaceList,
                    workspaceArtifacts,
                    mwcTokens,
                    capacityObjectId);
            }
            else if (resolveCallsInfo.ResolveCallsPreparationMode == ResolveCallsPreparationMode.Cartesian)
            {
                return GenerateCartesianResolveCalls(
                    userCertWorkspaceList,
                    workspaceArtifacts,
                    mwcTokens,
                    capacityObjectId);
            }
            else
            {
                throw new ArgumentException($"Unsupported ResolveCallsPreparationMode : {resolveCallsInfo.ResolveCallsPreparationMode}");
            }
        }

        private static List<RequestForValidation> GeneratePredefinedResolveCalls(
            List<PredefinedResolveCall> predefinedResolveCalls,
            List<UserCertWorkspace> userCertWorkspaceList,
            Dictionary<string, WorkspaceArtifact> workspaceArtifacts,
            Dictionary<(string workspaceId, string userId), string> mwcTokens,
            string capacityObjectId)
        {
            var resolveRequestForValidationList = new List<RequestForValidation>();

            foreach (var predefinedResolveCall in predefinedResolveCalls)
            {

                var variableReferences = predefinedResolveCall.PredefinedResolveReferences.Select(
                    predefinedResolveReference => $"$(/**/{predefinedResolveReference.VariableLibraryName}/{predefinedResolveReference.VariableName})");

                var body = new
                {
                    variableReferences
                };

                var requestJson = JsonSerializer.Serialize(body);

                var userIndex = predefinedResolveCall.UserIndex;

                var userCertWorkspace = userCertWorkspaceList.Find(userCertWorkspace => userCertWorkspace.UserName.Contains($"User{userIndex}"));


                if (userCertWorkspace == null)
                {
                    throw new ArgumentException($"User with index {userIndex} was not found");
                }

                var wsId = userCertWorkspace.WorkspaceIds[predefinedResolveCall.WorkspaceIndex];
                var artifacts = workspaceArtifacts[wsId];

                if (!mwcTokens.TryGetValue((wsId, userCertWorkspace.UserId), out var token))
                {
                    continue;
                }

                var normalizedcapacityObjectId = Utils.NormalizeGuid(capacityObjectId);
                var clusterBase = $"https://{normalizedcapacityObjectId}.pbidedicated.windows-int.net";
                var url = $"{clusterBase}/webapi/capacities/{capacityObjectId}/workloads/Config/ConfigService/automatic/public/workspaces/{wsId}/items/{artifacts.ConsumingItemId}/ResolveVariableReferencesV2";

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
                    HttpRequestMessageIdentifier = string.Join(",", variableReferences)
                });
            }

            return resolveRequestForValidationList;
        }

        private static List<RequestForValidation> GenerateCartesianResolveCalls(
            List<UserCertWorkspace> userCertWorkspaceList,
            Dictionary<string, WorkspaceArtifact> workspaceArtifacts,
            Dictionary<(string workspaceId, string userId), string> mwcTokens,
            string capacityObjectId)
        {
            //For randomization -variableNames should be dynamically set by discover call
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
