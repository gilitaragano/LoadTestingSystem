using LoadTestingSystem.Scenarios;
using LoadTestingSytem.Common;
using LoadTestingSytem.Models;
using LoadTestingSytem.Tests.LoadUnits.Config.Resolve.Actions;
using LoadTestingSytem.Tests.Workloads.Config.Resolve.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerBITokenGenerator;
using Scenarios;
using System.Text.Json;

namespace LoadTestingSytem.Tests.Workloads.Config.Resolve
{
    public class ResolveLoadUnit
    {
        private static List<UserCertWorkspace> _userCertWorkspaceList = null!;

        private LoadTestConfig _loadTestConfig = null!;
        private FabricEnvConfiguration _fabricEnvConfiguration = null!;
        private ResolveCallsConfig _resolveCallsConfiguration = null!;
        private readonly HttpClient _client = new HttpClient();

        private List<string> _workspaceIds = null!;
        private Dictionary<string, WorkspaceArtifact> _workspaceArtifacts = null!;
        private List<RequestForValidation<ResolveResultSummaryPredefined>> _requestForValidationList = null!;
        private string _tenantAdminAccessToken;

        private static bool _prepareFabricEnv;
        private static DateTime _sessionStartTime;

        private static readonly string _dirBase = ".\\Tests\\LoadUnits\\Config\\Resolve\\";
        private static readonly string _loadUnitName = "ResolveLoadUnit";

        private int _loadUnitIndex;
        private Guid _loadUnitObjectId;

        public ResolveLoadUnit(bool prepareFabricEnv, DateTime sessionStartTime, Guid? loadUnitObjectId, int loadUnitIndex = 0)
        {
            Assert.IsTrue((prepareFabricEnv && loadUnitObjectId == null) || (!prepareFabricEnv && loadUnitObjectId != null));
            _prepareFabricEnv = prepareFabricEnv;
            _sessionStartTime = sessionStartTime;
            _loadUnitIndex = loadUnitIndex;
            _loadUnitObjectId = loadUnitObjectId ?? Guid.NewGuid();
        }

        public async Task<LiveExecutionSessionRunner<ResolveResultSummary, ResolveResultSummaryPredefined, ResolveLoadUnit>> PrepareLoadUnit(string sessionConfigFile, string preparationConfigFile, string resolveCallsConfigFile)
        {
            var liveSessionConfig = await Utils.LoadConfig<LiveSessionConfiguration>($"{_dirBase}{sessionConfigFile}");
            var userCertList = (await Utils.LoadConfig<List<UserCert>>("Creation/UserCerts.json")).Skip(1).ToList();

            _fabricEnvConfiguration = await Utils.LoadConfig<FabricEnvConfiguration>($"{_dirBase}Creation\\{preparationConfigFile}");
            _resolveCallsConfiguration = await Utils.LoadConfig<ResolveCallsConfig>($"{_dirBase}{resolveCallsConfigFile}");
            _loadTestConfig = await Utils.LoadConfig<LoadTestConfig>($".\\Creation\\LoadTestConfiguration.json");
            _tenantAdminAccessToken = await PowerBiCbaTokenProvider.GetTenantAdmin();

            if (_prepareFabricEnv)
            {
                _userCertWorkspaceList = await PrepareLoadUnitFabricEnv.RunAsync(
                    _loadUnitName,  
                    _loadUnitIndex,
                    _loadUnitObjectId,
                    $"{_dirBase}Creation/{preparationConfigFile}",
                    $"{_dirBase}Creation/VariableLibraryDefinitions.json",
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

            var cts = new CancellationTokenSource();
            return new LiveExecutionSessionRunner<ResolveResultSummary, ResolveResultSummaryPredefined, ResolveLoadUnit>(
                this,
                _loadUnitIndex,
                _sessionStartTime,
                liveSessionConfig,
                _loadUnitName,
                cts.Token);
        }

        [TestPreparation]
        public async Task<List<RequestForValidation<ResolveResultSummaryPredefined>>> PrepareLoadUnitCalls()
        {
            Console.WriteLine("\nStep 1: Preparing workspace ID list...");
            _workspaceIds = await PrepareWorkspaceIdList.RunAsync(
                baseUrl: _loadTestConfig.BaseUrl,
                _tenantAdminAccessToken,
                _loadUnitIndex,
                _loadUnitObjectId,
                workspaceNamePrefix: _fabricEnvConfiguration.WorkspacesConfiguration.WorkspaceNamePrefix
            );

            if (_workspaceIds == null || _workspaceIds.Count == 0)
            {
                throw new Exception("No workspace IDs found, aborting.");
            }

            Console.WriteLine("\nStep 2: Preparing workspace items details...");
            _workspaceArtifacts = await PrepareWorkspaceItemsDetailsList.RunAsync(
                baseUrl: _loadTestConfig.BaseUrl,
                _tenantAdminAccessToken,
                loadUnitIndex: _loadUnitIndex,
                loadUnitObjectId: _loadUnitObjectId,
                workspaceNamePrefix: _fabricEnvConfiguration.WorkspacesConfiguration.WorkspaceNamePrefix,
                workspaceIds: _workspaceIds
            );

            if (_workspaceArtifacts == null || _workspaceArtifacts.Count == 0)
            {
                throw new Exception("No workspace artifacts found, aborting.");
            }

            Console.WriteLine("\nStep 3: Prepare resolve call input listWorkspace Items Details List...");
            _requestForValidationList = await PrepareCallInputList.RunAsync(
                baseUrl: _loadTestConfig.BaseUrl,
                capacityObjectId: _fabricEnvConfiguration.WorkspacesConfiguration.CapacityObjectId,
                workspaceArtifacts: _workspaceArtifacts,
                userCertWorkspaceList: _userCertWorkspaceList,
                resolveCallsInfo: _resolveCallsConfiguration.ResolveCallsInfo
            );

            return _requestForValidationList;
        }

        [TestExecute]
        public async Task<ResponseForValidation<ResolveResultSummary>> ExecuteResolveCall(RequestForValidation<ResolveResultSummaryPredefined> requestForValidation)
        {
            HttpResponseMessage response;

            try
            {
                response = await _client.SendAsync(requestForValidation.HttpRequestMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR IN SendAsync: " + ex);
                throw;
            }

            string rootActivityId = response.Headers.TryGetValues("x-ms-root-activity-id", out var headerValues)
                ? headerValues.FirstOrDefault() ?? string.Empty
                : string.Empty;

            var responseBody = await response.Content.ReadAsStringAsync();

            var res = new ResponseForValidation<ResolveResultSummary>
            {
                RequestId = rootActivityId,
                Status = (int)response.StatusCode,
                ResultSummary = JsonSerializer.Deserialize<ResolveResultSummary>(responseBody),
                Error = JsonSerializer.Deserialize<ErrorResponse>(responseBody),
            };

            return res;
        }

        [TestResultValidatation]
        public async Task ValidateCallResult(List<ResponseForFile<ResolveResultSummary, ResolveResultSummaryPredefined>> responsesForFile)
        {
            var failedCallsCount = responsesForFile.Where(response => !Utils.c_validStatusCodes.Contains(response.Status)).Count();

            int failedResolveSummaryValidationCount = await ResolveCallResultsSummaryValidator.RunAsync(
                _resolveCallsConfiguration.ResolveCallsInfo,
                _dirBase,
                responsesForFile);

            var resolveCallKustoQueryValidatorFailureCount = await ResolveCallKustoQueryValidator.RunAsync(
                responsesForFile);

            var successCalls = responsesForFile.Where(response => Utils.c_validStatusCodes.Contains(response.Status));
            var successAvgDuration = successCalls.Any()
                    ? successCalls.Average(call => call.DurationMs)
                    : 0;

            Console.WriteLine("\n============ Validation Summary ============");
            Console.WriteLine($"Successes: {responsesForFile.Count() - failedCallsCount}, Avg duration: {successAvgDuration}");
            Console.WriteLine($"FailedResolveCallsCount: {failedCallsCount}");
            Console.WriteLine($"FailedResolveCallResultSummaryValidationCount: {failedResolveSummaryValidationCount}");
            Console.WriteLine($"FailedResolveCallKustoQueryValidationCount: {resolveCallKustoQueryValidatorFailureCount}");
            Console.WriteLine("\n============================================");
        }

        [TestTokenExchange]
        public async Task<List<RequestForValidation<ResolveResultSummaryPredefined>>> ExchangeAccessToken(List<RequestForValidation<ResolveResultSummaryPredefined>> responsesForValidation)
        {
            return await PrepareCallInputList.RunAsync(
                baseUrl: _loadTestConfig.BaseUrl,
                capacityObjectId: _fabricEnvConfiguration.WorkspacesConfiguration.CapacityObjectId,
                workspaceArtifacts: _workspaceArtifacts,
                userCertWorkspaceList: _userCertWorkspaceList,
                resolveCallsInfo: _resolveCallsConfiguration.ResolveCallsInfo);
        }
    }
}
