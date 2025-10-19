using LoadTestingSystem.Scenarios;
using LoadTestingSytem.Common;
using LoadTestingSytem.Models;
using LoadTestingSytem.Tests.LoadUnits.DeploymentPipelines.CalculateDiff.Models;
using LoadTestingSytem.Tests.Workloads.Config.Resolve.Models;
using PowerBITokenGenerator;
using Scenarios;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using static LoadTestingSystem.Tests.LoadUnits.CommonUtils;

namespace LoadTestingSytem.Tests.LoadUnits.DeploymentPipelines.CalculateDiff
{
    public class CalculateDiffLoadUnit
    {
        private static List<UserCertWorkspace> _userCertWorkspaceList = null!;

        private LoadTestConfig _loadTestConfig = null!;
        private string _tenantAdminAccessToken = null!;

        private readonly HttpClient _client;

        private static bool _prepareFabricEnv;
        private static DateTime _sessionStartTime;

        private static readonly string _dirBase = ".\\Tests\\LoadUnits\\DeploymentPipelines\\CalculateDiff\\";
        private static readonly string _loadUnitName = "CalculateDiffLoadUnit";

        private int _loadUnitIndex;
        private Guid _loadUnitObjectId;

        public CalculateDiffLoadUnit(bool prepareFabricEnv, DateTime sessionStartTime, Guid? loadUnitObjectId, int loadUnitIndex = 0)
        {
            _prepareFabricEnv = prepareFabricEnv;
            _sessionStartTime = sessionStartTime;
            _loadUnitIndex = loadUnitIndex;
            _loadUnitObjectId = loadUnitObjectId ?? Guid.NewGuid();
        }

        public async Task<LiveExecutionSessionRunner<NoPayload, NoPayload, CalculateDiffLoadUnit>> PrepareLoadUnit(string sessionConfigFile)
        {
            var liveSessionConfig = await Utils.LoadConfig<LiveSessionConfiguration>($"{_dirBase}{sessionConfigFile}");
            var userCertList = (await Utils.LoadConfig<List<UserCert>>("Creation/UserCerts.json")).Skip(1).ToList();
            var fabricEnvConfiguration = await Utils.LoadConfig<FabricEnvConfiguration>($"{_dirBase}Creation\\CalculateDiffLoadUnitLiveSessionConfiguration.json");

            if (_prepareFabricEnv)
            {
                _userCertWorkspaceList = await PrepareLoadUnitFabricEnv.RunAsync(
                    _loadUnitName,
                    _loadUnitIndex,
                    _loadUnitObjectId,
                    $"{_dirBase}Creation/CalculateDiffLoadUnitPreparationConfiguration.json",
                    $"{_dirBase}Creation/ReportDefinitions.json",
                    userCertList);
            }
            else
            {
                _userCertWorkspaceList = await ConstructUserCertWs.RunAsync(
                    baseUrl: _loadTestConfig.BaseUrl,
                    _tenantAdminAccessToken,
                    workspaceNamePrefix: fabricEnvConfiguration.WorkspacesConfiguration.WorkspaceNamePrefix,
                    _loadUnitIndex,
                    _loadUnitObjectId,
                    userCertList);
            }

            using var cts = new CancellationTokenSource();
            return new LiveExecutionSessionRunner<NoPayload, NoPayload, CalculateDiffLoadUnit>(
                this,
                _loadUnitIndex,
                _sessionStartTime,
                liveSessionConfig,
                _loadUnitName, 
                cts.Token);
        }

        [TestPreparation]
        public async Task<List<RequestForValidation<NoPayload>>> PrepareLoadUnitCalls()
        {
            _loadTestConfig = await Utils.LoadConfig<LoadTestConfig>($".\\Creation\\LoadTestConfiguration.json");

            _tenantAdminAccessToken = await PowerBiCbaTokenProvider.GetTenantAdmin();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _tenantAdminAccessToken);
            _client.BaseAddress = new Uri(_loadTestConfig.BaseUrl);

            return await BuildDiffCalculationRequestsAsync();
        }

        [TestExecute]
        public async Task<ResponseForValidation<string>> ExecuteCall(RequestForValidation<NoPayload> requestForValidation)
        {

            using var httpClient = new HttpClient();

            var response = await httpClient.SendAsync(requestForValidation.HttpRequestMessage);

            string rootActivityId = response.Headers.TryGetValues("RequestId", out var headerValues)
                ? headerValues.FirstOrDefault() ?? string.Empty
                : string.Empty;

            var responseBody = await response.Content.ReadAsStringAsync();

            var res = new ResponseForValidation<string>
            {
                RequestIdentifier = requestForValidation.HttpRequestMessageIdentifier,
                RequestId = rootActivityId,
                Status = (int)response.StatusCode,
                ResultSummary = "NA",
            };

            return res;
        }

        [TestResultValidatation]
        public async Task ValidateCallResult(List<ResponseForFile<NoPayload, NoPayload>> responseForFileList)
        {
            int failedCallsCount = responseForFileList.Count(response => !Utils.c_validStatusCodes.Contains(response.Status));

            var successCalls = responseForFileList.Where(response => Utils.c_validStatusCodes.Contains(response.Status));
            var successAvgDuration = successCalls.Any()
                    ? successCalls.Average(call => call.DurationMs)
                    : 0;

            Console.WriteLine("\n============ Validation Summary ============");
            Console.WriteLine($"Successes: {responseForFileList.Count() - failedCallsCount}, Avg duration: {successAvgDuration}");
            Console.WriteLine($"FailedResolveCallsCount: {failedCallsCount}");
            Console.WriteLine("\n============================================");
        }

        // TODO it should not be only for tenant admins
        // TODO it should not be only access token
        // TODO it should not be only for admin tenant
        [TestTokenExchange]
        public async Task<List<RequestForValidation<NoPayload>>> ExchangeAccessToken(List<RequestForValidation<NoPayload>> responsesForValidation)
        {
            var newAccessToken = await PowerBiCbaTokenProvider.GetTenantAdmin();

            foreach (var request in responsesForValidation)
            {
                var headers = request.HttpRequestMessage.Headers;

                // Remove the old Authorization header
                if (headers.Contains("Authorization"))
                {
                    headers.Remove("Authorization");
                }

                // Add the new Authorization header
                headers.Authorization = new AuthenticationHeaderValue("Bearer", newAccessToken);
            }

            return responsesForValidation;
        }

        private async Task<List<RequestForValidation<NoPayload>>> BuildDiffCalculationRequestsAsync()
        {
            var workspaceArtifacts = new Dictionary<int, List<int>>();
            var calculateDiffRequestForValidationList = new List<RequestForValidation<NoPayload>>();

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _tenantAdminAccessToken);

            // Step 1: Get all pipelines
            var pipelineResponse = await _client.GetAsync($"{_loadTestConfig.BaseUrl}/metadata/almpipelines");
            pipelineResponse.EnsureSuccessStatusCode();
            var pipelineJson = await pipelineResponse.Content.ReadAsStringAsync();
            var pipelineData = JsonSerializer.Deserialize<List<AlmPipeline>>(pipelineJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (pipelineData == null) return calculateDiffRequestForValidationList;

            // Step 2: For each pipeline, get its stages
            foreach (var pipeline in pipelineData)
            {
                var stagesResponse = await _client.GetAsync($"{_loadTestConfig.BaseUrl}/metadata/almpipelines/{pipeline.Id}");
                stagesResponse.EnsureSuccessStatusCode();
                var stagesJson = await stagesResponse.Content.ReadAsStringAsync();
                var stagesData = JsonSerializer.Deserialize<AlmPipelineStagesResponse>(stagesJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (stagesData == null) continue;

                workspaceArtifacts[pipeline.Id] = stagesData.Stages.Select(s => s.Id).ToList();
            }

            // Step 3: Build diff calculation requests
            foreach (var (pipelineId, stageIds) in workspaceArtifacts)
            {
                foreach (var stageId in stageIds.Skip(1)) // skipping the first stage since you can't diff it
                {
                    var url = $"{_loadTestConfig.BaseUrl}/metadata/almpipelines/{pipelineId}/stages/{stageId}/diffCalculation";

                    var requestBody = new DiffCalculationRequestBody();
                    var requestJson = JsonSerializer.Serialize(requestBody);
                    var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
                    };

                    requestMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tenantAdminAccessToken);

                    calculateDiffRequestForValidationList.Add(new RequestForValidation<NoPayload>
                    {
                        HttpRequestMessage = requestMessage,
                        HttpRequestMessageIdentifier = $"{pipelineId}::{stageId}"
                    });
                }
            }

            return calculateDiffRequestForValidationList;
        }

        //private async Task<HttpResponseMessage> PostWithRetryAsync(HttpClient client, string url, object body)
        //{
        //    var json = JsonSerializer.Serialize(body);
        //    var content = new StringContent(json, Encoding.UTF8, "application/json");

        //    while (true)
        //    {
        //        // Update the Authorization header before each request with the current token
        //        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _tenantAdminAccessToken);

        //        var response = await client.PostAsync(url, content);

        //        if (response.IsSuccessStatusCode)
        //            return response;

        //        if ((int)response.StatusCode == 429 && response.Headers.TryGetValues("Retry-After", out var retryAfterValues))
        //        {
        //            var retryAfter = int.TryParse(retryAfterValues.FirstOrDefault(), out var delay) ? delay : 5;
        //            Console.WriteLine($"429 received. Retrying after {retryAfter} seconds...");
        //            await Task.Delay(TimeSpan.FromSeconds(retryAfter));
        //        }
        //        else if ((int)response.StatusCode == 401 &&
        //                 response.Headers.TryGetValues("x-ms-public-api-error-code", out var errorCodeValues) &&
        //                 errorCodeValues.FirstOrDefault()?.Equals("TokenExpired", StringComparison.OrdinalIgnoreCase) == true)
        //        {
        //            Console.WriteLine($"401 Unauthorized with TokenExpired. Creating new access token for tenant admin user...");
        //            _tenantAdminAccessToken = await PowerBiCbaTokenProvider.GetTenantAdmin();
        //            // Authorization header will be updated on next iteration
        //        }
        //        else
        //        {
        //            // TODO fix the name conflicts that happens over and over
        //            var error = await response.Content.ReadAsStringAsync();
        //            throw new HttpRequestException($"Request failed: {response.StatusCode} {error}");
        //        }
        //    }
        //}
    }
}
