using LoadTestingSytem.Common;
using LoadTestingSytem.Models;
using PowerBITokenGenerator;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using static LoadTestingSystem.Tests.LoadUnits.CommonUtils;

namespace LoadTestingSytem.Tests.LoadUnits.PublicApis.GetItems.Actions
{
    public static class PrepareCallInputList
    {
        public static async Task<List<RequestForValidation<NoPayload>>> RunAsync(
            string baseUrl,
            List<UserCertWorkspace> userCertWorkspaceList)
        {
            Console.WriteLine("Generating PowerBI Access tokens...");
            var userCertWorkspaceTokens = await PowerBiCbaTokenProvider.RunAsync(userCertWorkspaceList);

            var requestForValidationList = new List<RequestForValidation<NoPayload>>();

            // For randomization - randomly select here how many calls you want to have at the end
            // For randomization - randomly select which of the uses of userCertWorkspaceTokens you want to take each time
            foreach (var userCertWorkspaceToken in userCertWorkspaceTokens)
            {
                // For randomization - randomly select which WS among the WS this user belong to to take, for now take the first
                string workspaceId = userCertWorkspaceToken.WorkspaceIds[0];
                string token = userCertWorkspaceToken.AccessToken;

                string url = $"{baseUrl}/v1/workspaces/{workspaceId}/items";

                var getRequest = new HttpRequestMessage(HttpMethod.Get, url);
                getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                requestForValidationList.Add(new RequestForValidation<NoPayload>
                {
                    HttpRequestMessage = getRequest,
                    HttpRequestMessageIdentifier = workspaceId,
                });
            }

            return requestForValidationList;
        }
    }

}
