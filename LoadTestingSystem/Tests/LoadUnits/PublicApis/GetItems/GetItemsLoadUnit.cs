using LoadTestingSystem.Scenarios;
using LoadTestingSytem.Common;
using LoadTestingSytem.Models;
using LoadTestingSytem.Tests.LoadUnits.PublicApis.GetItems.Actions;
using LoadTestingSytem.Tests.Workloads.Config.Resolve.Models;
using PowerBITokenGenerator;
using Scenarios;
using System.Linq;
using System.Net.Http.Headers;
using static LoadTestingSystem.Tests.LoadUnits.CommonUtils;

namespace LoadTestingSytem.Tests.LoadUnits.PublicApis.GetItems
{
    public class GetItemsLoadUnit
    {
        private LoadTestConfig _loadTestConfig = null!;
        private FabricEnvConfiguration _fabricEnvConfiguration = null!;
        private string _tenantAdminAccessToken = null!;
        private static List<UserCertWorkspace> _userCertWorkspaceList = null!;

        private readonly HttpClient _client = new HttpClient();
        private List<RequestForValidation<NoPayload>> _requestForValidationList = null!;

        private static bool _prepareFabricEnv;
        private static DateTime _sessionStartTime;

        private static readonly string _dirBase = ".\\Tests\\LoadUnits\\PublicApis\\GetItems\\";
        private static readonly string _loadUnitName = "GetItemsLoadUnit";

        private List<string> _workspaceIds = null!;
        private int _loadUnitIndex;
        private Guid _loadUnitObjectId;

        public GetItemsLoadUnit(bool prepareFabricEnv, DateTime sessionStartTime, Guid? loadUnitObjectId, int loadUnitIndex = 0)
        {
            _prepareFabricEnv = prepareFabricEnv;
            _sessionStartTime = sessionStartTime;
            _loadUnitIndex = loadUnitIndex;
            _loadUnitObjectId = loadUnitObjectId ?? Guid.NewGuid();
        }

        public async Task<LiveExecutionSessionRunner<NoPayload, NoPayload, GetItemsLoadUnit>> PrepareLoadUnit(string sessionConfigFile)
        {
            var liveSessionConfig = await Utils.LoadConfig<LiveSessionConfiguration>($"{_dirBase}{sessionConfigFile}");
            var userCertList = (await Utils.LoadConfig<List<UserCert>>("Creation/UserCerts.json")).Skip(1).ToList();

            _fabricEnvConfiguration = await Utils.LoadConfig<FabricEnvConfiguration>($"{_dirBase}Creation\\GetItemsLoadUnitPreparationConfiguration.json");

            if (_prepareFabricEnv)
            {
                _userCertWorkspaceList = await PrepareLoadUnitFabricEnv.RunAsync(
                    _loadUnitName,
                    _loadUnitIndex,
                    _loadUnitObjectId,
                    $"{_dirBase}Creation/GetItemsLoadUnitPreparationConfiguration.json",
                    $"{_dirBase}Creation/ReportDefinitions.json",
                    userCertList);
            }
            else
            {
                _userCertWorkspaceList = await ConstructUserCertWs.RunAsync(
                    baseUrl: _loadTestConfig.BaseUrl,
                    _tenantAdminAccessToken,
                    workspaceNamePrefix: _fabricEnvConfiguration.WorkspacesConfiguration.WorkspaceNamePrefix,
                    _loadUnitIndex,
                    _loadUnitObjectId,
                    userCertList);
            }

            using var cts = new CancellationTokenSource();
            return new LiveExecutionSessionRunner<NoPayload, NoPayload, GetItemsLoadUnit>(
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

            return await BuildGetItemsRequestsAsync();
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

        [TestTokenExchange]
        public async Task<List<RequestForValidation<NoPayload>>> ExchangeAccessToken(List<RequestForValidation<NoPayload>> responsesForValidation)
        {
            Console.WriteLine("\nExchangeAccessToken prepare new set of GetItmes calls for execution phase...");
            return await PrepareCallInputList.RunAsync(
                baseUrl: _loadTestConfig.BaseUrl,
                userCertWorkspaceList: _userCertWorkspaceList);
        }

        private async Task<List<RequestForValidation<NoPayload>>> BuildGetItemsRequestsAsync()
        {
            _loadTestConfig = await Utils.LoadConfig<LoadTestConfig>($".\\Creation\\LoadTestConfiguration.json");

            var tenantAdminAccessToken = await PowerBiCbaTokenProvider.GetTenantAdmin();

            Console.WriteLine("\nStep 1: Preparing workspace ID list...");
            _workspaceIds = await PrepareWorkspaceIdList.RunAsync(
                baseUrl: _loadTestConfig.BaseUrl,
                tenantAdminAccessToken,
                loadUnitObjectId: _loadUnitObjectId
            );

            if (_workspaceIds == null || _workspaceIds.Count == 0)
            {
                throw new Exception("No workspace IDs found, aborting.");
            }

            Console.WriteLine("\nStep 2: Prepare GetItmes calls for execution phase...");
            _requestForValidationList = await PrepareCallInputList.RunAsync(
                baseUrl: _loadTestConfig.BaseUrl,
                userCertWorkspaceList: _userCertWorkspaceList
            );

            return _requestForValidationList;
        }
    }
}
