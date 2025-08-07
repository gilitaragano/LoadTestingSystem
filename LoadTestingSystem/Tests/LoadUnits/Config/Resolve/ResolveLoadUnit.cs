using LoadTestingSystem.Scenarios;
using LoadTestingSytem.Common;
using LoadTestingSytem.Models;
using LoadTestingSytem.Tests.LoadUnits.Config.Resolve.Actions;
using LoadTestingSytem.Tests.Workloads.Config.Resolve.Models;
using PowerBITokenGenerator;
using Scenarios;
using System.Linq;
using System.Text.Json;

namespace LoadTestingSytem.Tests.Workloads.Config.Resolve
{
    public class ResolveLoadUnit
    {
        private static List<UserCertWorkspace> _userCertWorkspaceList = null!;

        private LoadTestConfig _loadTestConfig = null!;
        private FabricEnvConfiguration _fabricEnvConfiguration = null!;

        private List<string> _workspaceIds = null!;
        private Dictionary<string, WorkspaceArtifact> _workspaceArtifacts = null!;
        private List<RequestForValidation> _requestForValidationList = null!;
        private string _tenantAdminAccessToken;

        private static bool _prepareFabricEnv;
        private static DateTime _sessionStartTime;

        private static readonly string _dirBase = ".\\Tests\\LoadUnits\\Config\\Resolve\\";
        private static readonly string _loadUnitName = "ResolveLoadUnit";

        private int _loadUnitIndex;

        public ResolveLoadUnit(bool prepareFabricEnv, DateTime sessionStartTime, int loadUnitIndex = 0)
        {
            _prepareFabricEnv = prepareFabricEnv;
            _sessionStartTime = sessionStartTime;
            _loadUnitIndex = loadUnitIndex;
        }

        public async Task<LiveExecutionSessionRunner<ResolveResultSummary, ResolveLoadUnit>> PrepareLoadUnit(string sessionConfigFile)
        {
            var liveSessionConfig = await Utils.LoadConfig<LiveSessionConfiguration>($"{_dirBase}{sessionConfigFile}");
            var userCertList = (await Utils.LoadConfig<List<UserCert>>("Creation/UserCerts.json")).Skip(1).ToList();

            _fabricEnvConfiguration = await Utils.LoadConfig<FabricEnvConfiguration>($"{_dirBase}Creation\\ResolveLoadUnitPreparationConfiguration.json");
            _loadTestConfig = await Utils.LoadConfig<LoadTestConfig>($".\\Creation\\LoadTestConfiguration.json");
            _tenantAdminAccessToken = await PowerBiCbaTokenProvider.GetTenantAdmin();

            if (_prepareFabricEnv)
            {
                _userCertWorkspaceList = await PrepareLoadUnitFabricEnv.RunAsync(
                    _loadUnitName,
                    _loadUnitIndex,
                    $"{_dirBase}Creation/ResolveLoadUnitPreparationConfiguration.json",
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
                    userCertList);
            }

            var cts = new CancellationTokenSource();
            return new LiveExecutionSessionRunner<ResolveResultSummary, ResolveLoadUnit>(
                this,
                _loadUnitIndex,
                _sessionStartTime,
                liveSessionConfig,
                _loadUnitName,
                cts.Token);
        }

        [TestPreparation]
        public async Task<List<RequestForValidation>> PrepareLoadUnitCalls()
        {
            Console.WriteLine("\nStep 1: Preparing workspace ID list...");
            _workspaceIds = await PrepareWorkspaceIdList.RunAsync(
                baseUrl: _loadTestConfig.BaseUrl,
                _tenantAdminAccessToken,
                _loadUnitIndex,
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
                userCertWorkspaceList: _userCertWorkspaceList
            );

            return _requestForValidationList;
        }

        [TestExecute]
        public async Task<ResponseForValidation<ResolveResultSummary>> ExecuteResolveCall(RequestForValidation requestForValidation)
        {
            using var httpClient = new HttpClient();

            var response = await httpClient.SendAsync(requestForValidation.HttpRequestMessage);

            string rootActivityId = response.Headers.TryGetValues("x-ms-root-activity-id", out var headerValues)
                ? headerValues.FirstOrDefault() ?? string.Empty
                : string.Empty;

            var responseBody = await response.Content.ReadAsStringAsync();

            var res = new ResponseForValidation<ResolveResultSummary>
            {
                RequestIdentifier = requestForValidation.HttpRequestMessageIdentifier,
                RequestId = rootActivityId,
                Status = (int)response.StatusCode,
                ResultSummary = JsonSerializer.Deserialize<ResolveResultSummary>(responseBody),
                Error = JsonSerializer.Deserialize<ErrorResponse>(responseBody)
            };

            return res;
        }

        [TestResultValidatation]
        public async Task<ValidationSummary> ValidateCallResult(List<ResponseForValidation<ResolveResultSummary>> responsesForValidation)
        {
            // Return false if any response does not have a valid status code
            int SuccessCallsCount = responsesForValidation.Count(response => Utils.c_validStatusCodes.Contains(response.Status));

            if (SuccessCallsCount != responsesForValidation.Count)
            {
                var validationSummary = new ValidationSummary()
                {
                    SuccessCallsCount = SuccessCallsCount,
                    FailureCallsCount = responsesForValidation.Count - SuccessCallsCount
                };

                return validationSummary;
            }

            var validationRes = await ValidateResolveResponse.RunAsync(_dirBase, responsesForValidation.Select(res => res.ResultSummary).ToList());

            return validationRes;
        }

        [TestTokenExchange]
        public async Task<List<RequestForValidation>> ExchangeAccessToken(List<RequestForValidation> responsesForValidation)
        {
            return await PrepareCallInputList.RunAsync(
                baseUrl: _loadTestConfig.BaseUrl,
                capacityObjectId: _fabricEnvConfiguration.WorkspacesConfiguration.CapacityObjectId,
                workspaceArtifacts: _workspaceArtifacts,
                userCertWorkspaceList: _userCertWorkspaceList);
        }
    }
}
