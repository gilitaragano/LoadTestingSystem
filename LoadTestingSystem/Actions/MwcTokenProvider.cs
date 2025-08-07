using LoadTestingSytem.Models;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Actions
{
    public class MwcTokenProvider
    {
        private static readonly HttpClient HttpClient = new();

        private class MwcTokenResponse
        {
            [JsonPropertyName("Token")]
            public string Token { get; set; } = string.Empty;
        }

        public static async Task<Dictionary<string, string>> GenerateTokensAsync(
            string baseUrl,
            List<UserCertWorkspaceToken> userCertWorkspaceTokenList,
            string capacityObjectId,
            Dictionary<string, string> consumingItemsByWorkspace)
        {
            var result = new Dictionary<string, string>();
            var tokenEndpoint = $"{baseUrl}/metadata/v201606/generatemwctokenv2";

            foreach (var (workspaceId, itemId) in consumingItemsByWorkspace)
            {
                var userCertWorkspaceToken = userCertWorkspaceTokenList.Find(ucwt => ucwt.WorkspaceId == workspaceId);

                if (userCertWorkspaceToken == null)
                {
                    Console.WriteLine($"No userCertWorkspaceToken found for this workspaceId: {workspaceId}");
                    return null;
                }

                var requestBody = new
                {
                    type = "[Start] GetMWCToken",
                    workloadType = "Config",
                    workspaceObjectId = workspaceId,
                    artifactObjectIds = new[] { itemId },
                    capacityObjectId,
                    asyncId = Guid.NewGuid().ToString(),
                    iframeId = Guid.NewGuid().ToString()
                };

                var requestJson = JsonSerializer.Serialize(requestBody);
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
                {
                    Content = new StringContent(requestJson)
                };

                requestMessage.Content.Headers.ContentType =
                    new MediaTypeHeaderValue("application/json");
                requestMessage.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", userCertWorkspaceToken.AccessToken);

                Console.WriteLine($"Requesting MWC token for user: {userCertWorkspaceToken.UserName}, workspace {workspaceId}, item {itemId}");

                try
                {
                    var response = await HttpClient.SendAsync(requestMessage);
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync();
                    var token = JsonSerializer.Deserialize<MwcTokenResponse>(json)?.Token;


                    if (!string.IsNullOrEmpty(token))
                    {
                        result[workspaceId] = token;
                    }
                    else
                    {
                        Console.WriteLine($"No token returned for workspace {workspaceId}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching MWC token for workspace {workspaceId}: {ex.Message}");
                }
            }

            return result;
        }
    }
}
