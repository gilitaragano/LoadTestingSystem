using LoadTestingSytem.Models;
using Scenarios;

public class RunnerLoadUnit<T, S, K> : ILoadUnit
    where K : class
{
    private readonly Func<Task<LiveExecutionSessionRunner<T, S, K>>> _prepareFunc;

    public RunnerLoadUnit(Func<Task<LiveExecutionSessionRunner<T, S, K>>> prepareFunc)
    {
        _prepareFunc = prepareFunc;
    }

    public async Task RunAsync(string testName)
    {
        var runner = await _prepareFunc();
        await runner.RunAsync(testName);
    }
}