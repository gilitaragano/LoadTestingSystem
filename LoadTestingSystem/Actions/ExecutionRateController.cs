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
        private DateTime _lastFlushTime;

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

            var _lastFlushTime = DateTime.UtcNow;
            var flushInterval = TimeSpan.FromSeconds(20);
            int secondCounter = 0;
            CallsRateInfo callsRateInfo = _liveSessionConfig.CallsRateInfo;
            int maxCallCount = _liveSessionConfig.CallsLimit;

            while (!_cancellationToken.IsCancellationRequested && (maxCallCount <= 0 || _totalCallCounter < maxCallCount))
            {
                var currentSecondStart = DateTime.UtcNow;

                // Determine _currentRate
                int rateSnapshot = GetCurrentRateForThisSecond(secondCounter);

                Console.WriteLine($"Second {secondCounter}: Dispatching {rateSnapshot} calls");

                // Execute calls according to mode
                if (callsRateInfo.CallsRateUpdateMode == CallsRateUpdateMode.SecondBySecond &&
                    callsRateInfo.SecondBySecondConfig != null &&
                    callsRateInfo.SecondBySecondConfig.Count() > 0)
                {
                    var config = callsRateInfo.SecondBySecondConfig[secondCounter % callsRateInfo.SecondBySecondConfig.Count()];
                    if (config.CallOffsetsMs != null && config.CallOffsetsMs.Count > 0 && config.HasValidOffsets)
                    {
                        await ExecuteCallsWithOffsets(config);
                    }
                    else
                    {
                        await ExecuteCallsAsFastAsYouCan(rateSnapshot);
                    }
                }
                else
                {
                    await ExecuteCallsAsFastAsYouCan(rateSnapshot);
                }

                // Flush if needed
                await CheckAndFlushAsync();

                // Wait until next second
                await WaitUntilNextSecondAsync(currentSecondStart);

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

        private async Task CheckAndFlushAsync()
        {
            TimeSpan flushInterval = TimeSpan.FromSeconds(20);

            if (DateTime.UtcNow - _lastFlushTime >= flushInterval)
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

                _lastFlushTime = DateTime.UtcNow;
            }
        }

        private async Task WaitUntilNextSecondAsync(DateTime currentSecondStart)
        {
            var timeUntilNextSecond = currentSecondStart.AddSeconds(1) - DateTime.UtcNow;
            if (timeUntilNextSecond.TotalMilliseconds > 0)
            {
                try
                {
                    await Task.Delay(timeUntilNextSecond, _cancellationToken);
                }
                catch (TaskCanceledException) { }
            }
            else
            {
                Console.WriteLine("[LoadTestingSystem] - ERROR - Could not dispatch all calls in time.");
            }
        }


        private int GetCurrentRateForThisSecond(int secondCounter)
        {
            int rateSnapshot;

            switch (_liveSessionConfig.CallsRateInfo.CallsRateUpdateMode)
            {
                case CallsRateUpdateMode.Static:
                    lock (_rateLock)
                    {
                        rateSnapshot = _currentRate;
                    }
                    break;

                case CallsRateUpdateMode.LinearRampUp:
                    var rampConfig = _liveSessionConfig.CallsRateInfo.LinearRampUpConfig;
                    if (rampConfig != null)
                    {
                        var timeSinceLastIncrease = DateTime.UtcNow - _lastRateIncreaseTime;
                        if (timeSinceLastIncrease.TotalSeconds >= rampConfig.SecondsBetweenIncreases)
                        {
                            lock (_rateLock)
                            {
                                _currentRate += rampConfig.CallsRateIncreasePerStep;
                                if (_currentRate < 0) _currentRate = 0; // ensure non-negative
                            }
                            _lastRateIncreaseTime = DateTime.UtcNow;
                            Console.WriteLine($"[Rate Increased] New rate: {_currentRate} calls/sec");
                        }
                    }

                    lock (_rateLock)
                    {
                        rateSnapshot = _currentRate;
                    }
                    break;

                case CallsRateUpdateMode.SecondBySecond:
                    var secondBySecondRates = _liveSessionConfig.CallsRateInfo.SecondBySecondConfig;
                    if (secondBySecondRates != null && secondBySecondRates.Count() > 0)
                    {
                        int index = secondCounter % secondBySecondRates.Count();
                        var config = secondBySecondRates[index];

                        lock (_rateLock)
                        {
                            _currentRate = config.CallsCount;
                            rateSnapshot = _currentRate;
                        }
                    }
                    else
                    {
                        lock (_rateLock)
                        {
                            rateSnapshot = _currentRate;
                        }
                    }
                    break;

                default:
                    lock (_rateLock)
                    {
                        rateSnapshot = _currentRate;
                    }
                    break;
            }

            return rateSnapshot;
        }

        private async Task ExecuteCallsAsFastAsYouCan(int callsToExecute)
        {
            for (int i = 0; i < callsToExecute && (_maxCallCount <= 0 || _totalCallCounter < _maxCallCount); i++)
            {
                RequestForValidation request;
                lock (_requestsLock)
                {
                    request = CloneRequestForValidation(_requests[_currentIndex % _requests.Count]);
                    _currentIndex++;
                    _totalCallCounter++;
                }

                var task = ExecuteAndTrackAsync(request);
                _runningTasks.Add(task);
            }
        }

        private async Task ExecuteCallsWithOffsets(SecondConfig config)
        {
            DateTime secondStart = DateTime.UtcNow;
            secondStart = secondStart.AddMilliseconds(-secondStart.Millisecond).AddSeconds(1);

            for (int i = 0; i < config.CallsCount && (_maxCallCount <= 0 || _totalCallCounter < _maxCallCount); i++)
            {
                int offsetMs = config.CallOffsetsMs![i];
                DateTime targetTime = secondStart.AddMilliseconds(offsetMs);

                RequestForValidation request;
                lock (_requestsLock)
                {
                    request = CloneRequestForValidation(_requests[_currentIndex % _requests.Count]);
                    _currentIndex++;
                    _totalCallCounter++;
                }

                var delay = targetTime - DateTime.UtcNow;
                if (delay.TotalMilliseconds > 0)
                {
                    try
                    {
                        await Task.Delay(delay, _cancellationToken);
                    }
                    catch (TaskCanceledException) { break; }
                }

                var task = ExecuteAndTrackAsync(request);
                _runningTasks.Add(task);
            }
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
