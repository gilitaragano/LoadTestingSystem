using Actions;
using LoadTestingSytem.Models;
using LoadTestingSytem.Tests.LoadUnits.PublicApis.GetItems;
using LoadTestingSytem.Tests.Workloads.Config.Resolve;
using LoadTestingSytem.Tests.Workloads.Config.Resolve.Models;

//class Program
//{
//    static async Task Main(string[] args)
//    {
//        if (args.Length == 0)
//        {
//            Console.WriteLine("Usage: LoadTestingSystem.exe ResolveLoadUnitLiveSession");
//            return;
//        }

//        string mode = args[0];
//        bool prepareFabricEnv = false;
//        var testStartTime = DateTime.UtcNow;

//        ILoadUnit loadUnit = mode switch
//        {
//            "resolve" => new RunnerLoadUnit<ResolveResultSummary, ResolveLoadUnit>(
//                () => new ResolveLoadUnit(prepareFabricEnv, testStartTime)
//                    .PrepareLoadUnit("ResolveLoadUnitLiveSessionConfiguration.json")),

//            _ => throw new ArgumentException($"Unknown mode: {args[0]}")
//        };

//        await loadUnit.RunAsync("Test_Resolve");
//    }
//}


//class Program
//{
//    static async Task Main(string[] args)
//    {
//        if (args.Length == 0)
//        {
//            Console.WriteLine("Usage: LoadTestingSystem.exe [getItems|getItemsBaseLine]");
//            return;
//        }

//        string mode = args[0];
//        bool prepareFabricEnv = true;
//        var testStartTime = DateTime.UtcNow;

//        ILoadUnit loadUnit = mode switch
//        {
//            "getitems" => new RunnerLoadUnit<string, GetItemsLoadUnit>(
//                () => new GetItemsLoadUnit(prepareFabricEnv, testStartTime)
//                    .PrepareLoadUnit("GetItemsLoadUnitLiveSessionConfiguration.json")),

//            "getitemsbaseline" => new RunnerLoadUnit<string, GetItemsLoadUnit>(
//                () => new GetItemsLoadUnit(prepareFabricEnv, testStartTime)
//                    .PrepareLoadUnit("GetItemsLoadUnitLiveSessionBaselineConfiguration.json")),

//            _ => throw new ArgumentException($"Unknown mode: {args[0]}")
//        };

//        await loadUnit.RunAsync("Test_GetItemsUponBaseline");
//    }
//}

//using LoadTestingSytem.Tests.LoadUnits.PublicApis.GetItems;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Choose Load Test Mode:");
        Console.WriteLine("0 - Load test - Resolve");
        Console.WriteLine("1 - Load test - GetItems");
        Console.WriteLine("2 - Load test - GetItemsBaseline");
        Console.WriteLine("3 - Load test - Run Both in Parallel");
        Console.WriteLine("4 - Generate userCerts file");
        Console.WriteLine("5 - Caching test - 2 resolve call: [{WS0, User5 [{$VL1/Var1}]}, {WS0, User5 [{$VL2/Var2}]}");
        Console.WriteLine("6 - Caching test - 2 resolve call: [{WS0, User5 [{$VL1/Var3}, {$VL2/Var4}]}, {WS0, User5 [{$VL1/Var3}, {$VL2/Var4}]}");
        Console.WriteLine("7 - Caching test - 2 resolve call: [{WS0, User5 [{$VL_NotExists/Var1}]}, {WS0, User5 [{$VL_NotExists/Var2}]}");
        Console.Write("Enter your choice:");

        var choice = Console.ReadLine()?.Trim();

        string testName = "Test_GetItemsUponBaseline";
        var testStartTime = DateTime.UtcNow;

        switch (choice)
        {
            case "0":
                {
                    var loadUnit = CreateResolveLoadUnit(
                        testStartTime,
                        "ResolveLoadUnitLiveSessionConfiguration.json",
                        "ResolveLoadUnitPreparationConfiguration.json",
                        "ResolveLoadUnitResolveCallsConfiguration.json");

                    Console.WriteLine($"Running Resolve with test '{testName}'...");
                    await loadUnit.RunAsync(testName);
                    break;
                }
            case "1":
                {
                    var loadUnit = new RunnerLoadUnit<string, GetItemsLoadUnit>(
                        () => new GetItemsLoadUnit(prepareFabricEnv: true, testStartTime, loadUnitObjectId: null)
                            .PrepareLoadUnit("GetItemsLoadUnitLiveSessionConfiguration.json"));

                    Console.WriteLine($"Running getitems with test '{testName}'...");
                    await loadUnit.RunAsync(testName);
                    break;
                }

            case "2":
                {
                    var loadUnit = new RunnerLoadUnit<string, GetItemsLoadUnit>(
                        () => new GetItemsLoadUnit(prepareFabricEnv: true, testStartTime, loadUnitObjectId: null)
                            .PrepareLoadUnit("GetItemsLoadUnitLiveSessionBaselineConfiguration.json"));

                    Console.WriteLine($"Running getitemsBaseline with test '{testName}'...");
                    await loadUnit.RunAsync(testName);
                    break;
                }

            case "3":
                {
                    var loadUnit1 = new RunnerLoadUnit<string, GetItemsLoadUnit>(
                        () => new GetItemsLoadUnit(prepareFabricEnv: true, testStartTime, loadUnitObjectId: null)
                            .PrepareLoadUnit("GetItemsLoadUnitLiveSessionConfiguration.json"));

                    var loadUnit2 = new RunnerLoadUnit<string, GetItemsLoadUnit>(
                        () => new GetItemsLoadUnit(prepareFabricEnv: true, testStartTime, loadUnitObjectId: null)
                            .PrepareLoadUnit("GetItemsLoadUnitLiveSessionBaselineConfiguration.json"));

                    Console.WriteLine("Running both getitems and getitemsBaseline in parallel...");

                    var task1 = Task.Run(() => loadUnit1.RunAsync(testName + "_GetItems"));
                    var task2 = Task.Run(() => loadUnit2.RunAsync(testName + "_GetItemsBaseline"));

                    await Task.WhenAll(task1, task2);
                    break;
                }

            case "4":
                {
                    await UserCertsFileGenerator.RunAsync();
                    break;
                }
            case "5":
                {
                    var loadUnit = new RunnerLoadUnit<ResolveResultSummary, ResolveLoadUnit>(
                        () => new ResolveLoadUnit(prepareFabricEnv: true, testStartTime, loadUnitObjectId: null)
                            .PrepareLoadUnit(
                                "ResolveLoadUnitLiveSessionConfiguration_CacheTest1.json",
                                "ResolveLoadUnitPreparationConfiguration_1WS_2VL_1CI.json",
                                "ResolveLoadUnitResolveCallsConfiguration_CacheTest1.json"));

                    Console.WriteLine($"Running Resolve with test '{testName}'...");
                    await loadUnit.RunAsync(testName);
                    break;
                }
            case "6":
                {
                    var loadUnit = CreateResolveLoadUnit(
                        testStartTime,
                        "ResolveLoadUnitLiveSessionConfiguration_CacheTest1.json",
                        "ResolveLoadUnitPreparationConfiguration_1WS_2VL_1CI.json",
                        "ResolveLoadUnitResolveCallsConfiguration_CacheTest2.json");

                    Console.WriteLine($"Running Resolve with test '{testName}'...");
                    await loadUnit.RunAsync(testName);
                    break;
                }

            case "7":
                {
                    var loadUnit = CreateResolveLoadUnit(
                        testStartTime,
                        "ResolveLoadUnitLiveSessionConfiguration_CacheTest1.json",
                        "ResolveLoadUnitPreparationConfiguration_1WS_2VL_1CI.json",
                        "ResolveLoadUnitResolveCallsConfiguration_CacheTest3.json");

                    Console.WriteLine($"Running Resolve with test '{testName}'...");
                    await loadUnit.RunAsync(testName);
                    break;
                }

            default:
                throw new ArgumentException("Invalid choice. Please enter 1, 2, or 3.");
        }

        Console.WriteLine("Execution completed. Press any key to exit.");
        Console.ReadKey();
    }
    private static RunnerLoadUnit<ResolveResultSummary, ResolveLoadUnit> CreateResolveLoadUnit(
    DateTime testStartTime,
    string liveSessionConfigFile,
    string preparationConfigFile,
    string resolveCallsConfigFile)
    {
        return new RunnerLoadUnit<ResolveResultSummary, ResolveLoadUnit>(
            () => new ResolveLoadUnit(prepareFabricEnv: true, testStartTime, loadUnitObjectId: null)
                .PrepareLoadUnit(
                    liveSessionConfigFile,
                    preparationConfigFile,
                    resolveCallsConfigFile));
    }
}