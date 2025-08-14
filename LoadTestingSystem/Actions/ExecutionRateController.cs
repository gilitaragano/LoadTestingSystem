using LoadTestingSytem.Common;
using LoadTestingSytem.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace Actions
{
    public class ExecutionRateController<T, S>
    {
        private readonly List<RequestForValidation> _requests;
        private readonly ConcurrentBag<ResponseForFile<T>> _results = new();

        private readonly int _maxCallCount;
        private int _currentRate;
        private readonly LiveSessionConfiguration _liveSessionConfig;
        private DateTime _lastRateIncreaseTime;

        private readonly CancellationToken _cancellationToken;
        private readonly DateTime _sessionStartTime;
        private readonly string _sessionFolderPath;
        private int _currentIndex = 0;
        private int _totalCallCounter = 0;
        private readonly List<Task> _runningTasks = new();
        private readonly object _rateLock = new();
        private readonly object _flushLock = new();
        private readonly object _requestsLock = new();

        private MethodInfo _validateMethod;
        private MethodInfo _executeMethod;
        private readonly S _testInstance;
        private readonly int _loadUnitIndex;

        public ExecutionRateController(
            S testInstance,
            int loadUnitIndex,
            string testName,
            string loadUnitName,
            DateTime sessionStartTime,
            List<RequestForValidation> requests,
            LiveSessionConfiguration liveSessionConfig,
            MethodInfo executeMethod,
            MethodInfo validateMethod,
            CancellationToken cancellationToken
            )
        {
            _requests = requests;
            _testInstance = testInstance;
            _loadUnitIndex = loadUnitIndex;
            _validateMethod = validateMethod;
            _executeMethod = executeMethod;
            _sessionStartTime = sessionStartTime;
            _currentRate = liveSessionConfig.CallsRateInfo.InitialCallsRate;
            _liveSessionConfig = liveSessionConfig;
            _lastRateIncreaseTime = sessionStartTime;
            _cancellationToken = cancellationToken;
            _sessionFolderPath = Path.Combine(
                "Results", 
                $"{testName}_{_sessionStartTime.ToString("yyyyMMdd_HHmmss")}", 
                $"{loadUnitName}_{_loadUnitIndex}");
            Directory.CreateDirectory(_sessionFolderPath);
        }

        public int CurrentRate
        {
            get
            {
                lock (_rateLock)
                {
                    return _currentRate;
                }
            }
        }

        public void SetRate(int newRate)
        {
            lock (_rateLock)
            {
                _currentRate = newRate;
            }
        }

        public void SetRequests(List<RequestForValidation> newRequests)
        {
            if (newRequests == null || newRequests.Count == 0)
                throw new ArgumentException("New request list is null or empty.");

            lock (_requestsLock)
            {
                _requests.Clear();
                _requests.AddRange(newRequests);
            }

            Console.WriteLine($"Request list updated with {newRequests.Count} new requests.");
        }

        private string GetOutputFilePath()
        {
            string now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss.fffZ");
            return Path.Combine(_sessionFolderPath, $"ExecutionResults_{now}.json");
        }

        private async Task ExecuteAndTrackAsync(RequestForValidation request)
        {
            var startedAt = DateTime.UtcNow;
            var stopwatch = Stopwatch.StartNew();

            var response = await InvokeMethodAsync(_executeMethod, request);

            stopwatch.Stop();
            var finishedAt = DateTime.UtcNow;

            var result = new ResponseForFile<T>
            {
                StartedAt = startedAt,
                FinishedAt = finishedAt,
                DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                RequestIdentifier = request.HttpRequestMessageIdentifier,
                Status = (int)response.Status,
                RequestId = (string)response.RequestId,
                ResultSummary = (T)response.ResultSummary,
                Error = (ErrorResponse)response.Error
            };

            _results.Add(result);
        }

        private async Task FlushResultsToFileAsync()
        {
            List<ResponseForFile<T>> responseForFileList;

            lock (_flushLock)
            {
                if (_results.Count == 0)
                {
                    Console.WriteLine("No results to flush at this time.");
                    return;
                }

                responseForFileList = _results
                    .OrderByDescending(r => r.FinishedAt)
                    .ToList();

                _results.Clear();
            }

            var fileName = GetOutputFilePath();
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(responseForFileList, options);
            await File.WriteAllTextAsync(fileName, json);
            Console.WriteLine($"File written with {responseForFileList.Count} results: {fileName}");

            try
            {
                var responseForValidationList = responseForFileList.Cast<ResponseForValidation<T>>().ToList();
                ValidationSummary validationSummary = await InvokeMethodAsync(_validateMethod, responseForValidationList.ToList());

                var successCalls = responseForFileList.Where(response => Utils.c_validStatusCodes.Contains(response.Status));
                var failedCalls = responseForFileList.Where(response => !Utils.c_validStatusCodes.Contains(response.Status));

                var successAvgDuration = successCalls.Any()
                        ? successCalls.Average(call => call.DurationMs)
                        : 0;
                var failureAvgDuration = failedCalls.Any()
                        ? failedCalls.Average(call => call.DurationMs)
                        : 0;
                Console.WriteLine("\n============ Validation Summary ============");
                Console.WriteLine($"Successes: {successCalls.Count()}, Avg duration: {successAvgDuration}");
                Console.WriteLine($"Failures: {failedCalls.Count()}, Avg duration: {failureAvgDuration}");
                Console.WriteLine($"FailedValidation: {validationSummary.FailureCallsCount}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Validation failed for {fileName}: {ex.Message}");
            }
        }

        private async Task<dynamic> InvokeMethodAsync(MethodInfo method, params object[]? parameters)
        {
            var task = (Task)method.Invoke(_testInstance, parameters)!;
            await task.ConfigureAwait(false);
            return ((dynamic)task).Result;
        }

        public async Task RunAsync()
        {
            Console.WriteLine($"Starting live execution session at {_currentRate} calls/sec");

            var lastFlushTime = DateTime.UtcNow;
            var flushInterval = TimeSpan.FromSeconds(20);
            int secondCounter = 0;
            CallsRateInfo callsRateInfo = _liveSessionConfig.CallsRateInfo;
            int maxCallCount = _liveSessionConfig.CallsLimit;

            while (!_cancellationToken.IsCancellationRequested && (maxCallCount <= 0 || _totalCallCounter < maxCallCount))
            {
                // Must be at start
                var currentSecondStart = DateTime.UtcNow;

                if (callsRateInfo.CallsRateUpdateMode == CallsRateUpdateMode.LinearRampUp &&
                    callsRateInfo.LinearRampUpConfig != null)
                {
                    var rampConfig = callsRateInfo.LinearRampUpConfig;
                    var timeSinceLastIncrease = DateTime.UtcNow - _lastRateIncreaseTime;

                    if (timeSinceLastIncrease.TotalSeconds >= rampConfig.SecondsBetweenIncreases)
                    {
                        lock (_rateLock)
                        {
                            _currentRate += rampConfig.CallsRateIncreasePerStep;
                        }

                        _lastRateIncreaseTime = DateTime.UtcNow;
                        Console.WriteLine($"[Rate Increased] New rate: {_currentRate} calls/sec");
                    }
                }
                else if (callsRateInfo.CallsRateUpdateMode == CallsRateUpdateMode.SecondBySecond &&
                         callsRateInfo.SecondBySecondConfig != null &&
                         callsRateInfo.SecondBySecondConfig.Count() > 0)
                {
                    var secondBySecondRates = callsRateInfo.SecondBySecondConfig;

                    int rateIndex = secondCounter % secondBySecondRates.Count();

                    lock (_rateLock)
                    {
                        _currentRate = secondBySecondRates[rateIndex];
                    }
                }

                int rateSnapshot;
                lock (_rateLock)
                {
                    rateSnapshot = _currentRate;
                }

                Console.WriteLine($"Second {secondCounter}: Dispatching {rateSnapshot} calls");

                for (int i = 0; i < rateSnapshot && (_maxCallCount <= 0 || _totalCallCounter < _maxCallCount); i++)
                {
                    lock (_requestsLock)
                    {
                        var originalRequest = _requests[_currentIndex % _requests.Count];
                        _currentIndex++;
                        _totalCallCounter++;

                        var clonedRequest = CloneRequestForValidation(originalRequest);
                        var task = ExecuteAndTrackAsync(clonedRequest);
                        _runningTasks.Add(task);
                    }
                }

                if (DateTime.UtcNow - lastFlushTime >= flushInterval)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await FlushResultsToFileAsync();
                        }
                        catch (Exception ex)
                        {
                            // Log or handle exceptions if needed
                            Console.WriteLine($"Flush failed: {ex.Message}");
                        }
                    });

                    lastFlushTime = DateTime.UtcNow;
                }


                // Must be at the end
                var timeUntilNextSecond = currentSecondStart.AddSeconds(1) - DateTime.UtcNow;
                    if (timeUntilNextSecond.TotalMilliseconds > 0)
                {
                    try
                    {
                        await Task.Delay(timeUntilNextSecond, _cancellationToken);
                    }
                    catch (TaskCanceledException) { break; }
                }
                else
                {
                    Console.WriteLine($"[LoadTestingSystem] - ERROR - Load Testing System on second: {secondCounter} could not dispatch {rateSnapshot} calls");
                }

                secondCounter++;
            }

            Console.WriteLine("Waiting for all remaining calls to finish...");
            await Task.WhenAll(_runningTasks);

            if (_results.Count > 0)
            {
                await FlushResultsToFileAsync();
            }

            Console.WriteLine("Execution session complete.");
        }


        private static RequestForValidation CloneRequestForValidation(RequestForValidation original)
        {
            var originalRequest = original.HttpRequestMessage;

            var clonedRequest = new HttpRequestMessage(originalRequest.Method, originalRequest.RequestUri)
            {
                Version = originalRequest.Version
            };

            // Clone the content stream if present
            if (originalRequest.Content != null)
            {
                var memoryStream = new MemoryStream();
                originalRequest.Content.CopyToAsync(memoryStream).Wait();
                memoryStream.Position = 0;
                clonedRequest.Content = new StreamContent(memoryStream);

                foreach (var header in originalRequest.Content.Headers)
                {
                    clonedRequest.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            // Clone the headers
            foreach (var header in originalRequest.Headers)
            {
                clonedRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return new RequestForValidation
            {
                HttpRequestMessage = clonedRequest,
                HttpRequestMessageIdentifier = original.HttpRequestMessageIdentifier
            };
        }
    }
}
