using Actions;
using LoadTestingSytem.Common;
using LoadTestingSytem.Models;
using System.Reflection;

namespace Scenarios
{
    public class LiveExecutionSessionRunner<T, S, K>
    {
        private readonly object _resultsLock = new();
        private readonly List<ResponseForFile<T,S>> _results = new();
        private readonly CancellationToken _cancellationToken;
        private readonly LiveSessionConfiguration _liveSessionConfig;
        private readonly string _loadUnitName;

        private int _currentIndex = 0;
        private DateTime _sessionStart;
        private List<RequestForValidation<S>> _requests = null!;

        private ExecutionRateController<T, S, K> _controller;
        private readonly K _testInstance;
        private readonly int _loadUnitIndex;
        private readonly DateTime _sessionStartTime;

        private readonly MethodInfo _initMethod;
        private readonly MethodInfo _executeMethod;
        private readonly MethodInfo _validateMethod;
        private readonly MethodInfo _tokenExchangeMethod;

        public LiveExecutionSessionRunner(
            K testInstance, 
            int loadUnitIndex, 
            DateTime sessionStartTime, 
            LiveSessionConfiguration liveSessionConfig, 
            string loadUnitName, 
            CancellationToken cancellationToken)
        {
            _liveSessionConfig = liveSessionConfig;
            _loadUnitName = loadUnitName;
            _cancellationToken = cancellationToken;
            _testInstance = testInstance;
            _loadUnitIndex = loadUnitIndex;
            _sessionStartTime = sessionStartTime;

            var executor = new TestMethodExecution(_testInstance.GetType());
            _initMethod = executor.InitMethod;
            _executeMethod = executor.ExecuteMethod;
            _validateMethod = executor.ValidateMethod;
            _tokenExchangeMethod = executor.TokenExchangeMethod;

            Console.WriteLine("acquire all test method references via reflection.");
        }

        public async Task RunAsync(string testName)
        {
            Console.WriteLine("Initializing test data via TestPreparation...");
            var _requests = await InvokeMethodAsync(_initMethod);

            var cts = new CancellationTokenSource();
            var token = cts.Token;

            _ = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(30), token);

                        Console.WriteLine("Refreshing request list via init method...");
                        var clonedRequests = CloneRequestList(_requests);
                        var refreshedRequests = await InvokeMethodAsync(_tokenExchangeMethod, clonedRequests) as List<RequestForValidation<S>>;
                        if (refreshedRequests == null || refreshedRequests.Count == 0 || refreshedRequests.Count != _requests.Count)
                        {
                            Console.WriteLine("Warning: Init returned null or empty request list. Skipping update.");
                            continue;
                        }

                        _controller.SetRequests(refreshedRequests);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error while refreshing requests: {ex.Message}");
                    }
                }
            });

            if (_requests.Count == 0)
                    throw new Exception("No requests found during preparation.");

            _controller = new ExecutionRateController<T, S, K>(
                _testInstance,
                _loadUnitIndex,
                testName,
                _loadUnitName,
                sessionStartTime: _sessionStartTime,
                _requests,
                _liveSessionConfig,
                _executeMethod,
                _validateMethod,
                _cancellationToken);

            var runTask = _controller.RunAsync();

            Console.WriteLine("You can type a new rate during execution (e.g. '20') or 'exit' to stop.");

            while (!runTask.IsCompleted)
            {
                Console.WriteLine($"Current calls per second: {_controller.CurrentRate}. Enter new rate or 'exit': ");
                var input = Console.ReadLine();

                if (input?.Trim().ToLower() == "exit")
                {
                    cts.Cancel();
                    break;
                }
                else if (int.TryParse(input, out int newRate) && newRate > 0)
                {
                    _controller.SetRate(newRate);
                    Console.WriteLine($"Rate updated to {newRate} calls/sec");
                }
                else if (string.IsNullOrWhiteSpace(input))
                {
                    // Keep current rate
                }
                else
                {
                    Console.WriteLine("Invalid input.");
                }
            }

            await runTask;

            Console.WriteLine("Load test session complete.");
        }

        private async Task<dynamic> InvokeMethodAsync(MethodInfo method, params object[]? parameters)
        {
            var task = (Task)method.Invoke(_testInstance, parameters)!;
            await task.ConfigureAwait(false);
            return ((dynamic)task).Result;
        }

        public static List<RequestForValidation<S>> CloneRequestList(List<RequestForValidation<S>> originalList)
        {
            var cloneList = new List<RequestForValidation<S>>();

            foreach (var original in originalList)
            {
                var clonedRequest = new HttpRequestMessage(original.HttpRequestMessage.Method, original.HttpRequestMessage.RequestUri);

                // Clone headers
                foreach (var header in original.HttpRequestMessage.Headers)
                {
                    clonedRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                // Clone content
                if (original.HttpRequestMessage.Content != null)
                {
                    var originalContent = original.HttpRequestMessage.Content.ReadAsStringAsync().Result;
                    clonedRequest.Content = new StringContent(originalContent, System.Text.Encoding.UTF8);

                    // Copy content headers
                    foreach (var header in original.HttpRequestMessage.Content.Headers)
                    {
                        clonedRequest.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }

                var clone = new RequestForValidation<S>
                {
                    HttpRequestMessage = clonedRequest,
                    HttpRequestMessageIdentifier = original.HttpRequestMessageIdentifier,
                    ExpectedResultSummary = original.ExpectedResultSummary,
                    KustoQuery = original.KustoQuery,
                    ExpectedKustoQueryResult = original.ExpectedKustoQueryResult,
                };

                cloneList.Add(clone);
            }

            return cloneList;
        }
    }
}
