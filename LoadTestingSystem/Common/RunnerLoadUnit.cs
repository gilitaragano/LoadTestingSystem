using LoadTestingSytem.Models;
using Scenarios;

public class RunnerLoadUnit<T, TUnit> : ILoadUnit
    where TUnit : class
{
    private readonly Func<Task<LiveExecutionSessionRunner<T, TUnit>>> _prepareFunc;

    public RunnerLoadUnit(Func<Task<LiveExecutionSessionRunner<T, TUnit>>> prepareFunc)
    {
        _prepareFunc = prepareFunc;
    }

    public async Task RunAsync(string testName)
    {
        var runner = await _prepareFunc();
        await runner.RunAsync(testName);
    }
}