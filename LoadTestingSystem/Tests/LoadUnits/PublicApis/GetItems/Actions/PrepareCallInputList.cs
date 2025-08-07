using LoadTestingSytem.Common;
using LoadTestingSytem.Models;
using PowerBITokenGenerator;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace LoadTestingSytem.Tests.LoadUnits.PublicApis.GetItems.Actions
{
    public static class PrepareCallInputList
    {
        public static async Task<List<RequestForValidation>> RunAsync(
            string baseUrl,
            List<UserCertWorkspace> userCertWorkspaceList)
        {
            Console.WriteLine("Generating PowerBI Access tokens...");
            var userCertWorkspaceTokens = await PowerBiCbaTokenProvider.RunAsync(userCertWorkspaceList);

            var requestForValidationList = new List<RequestForValidation>();

            foreach (var userWorkspace in userCertWorkspaceTokens)
            {
                string workspaceId = userWorkspace.WorkspaceId;
                string token = userWorkspace.AccessToken;

                string url = $"{baseUrl}/v1/workspaces/{workspaceId}/items";

                var getRequest = new HttpRequestMessage(HttpMethod.Get, url);
                getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                requestForValidationList.Add(new RequestForValidation
                {
                    HttpRequestMessage = getRequest,
                    HttpRequestMessageIdentifier = workspaceId,
                });
            }

            return requestForValidationList;
        }
    }

}
