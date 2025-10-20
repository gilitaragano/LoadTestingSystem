using Actions;
using LoadTestingSytem.Models;
using LoadTestingSytem.Tests.LoadUnits.PublicApis.GetItems;
using LoadTestingSytem.Tests.Workloads.Config.Resolve;
using LoadTestingSytem.Tests.Workloads.Config.Resolve.Models;
using System.Reflection.PortableExecutable;
using static LoadTestingSystem.Tests.LoadUnits.CommonUtils;
using static System.Net.Mime.MediaTypeNames;

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
        Console.WriteLine("0 - Load test - Resolve static rate, initial 50 RPS");
        Console.WriteLine("1 - Load test - Resolve linear, initial 10 RPS, increase by 5 each 10 seconds");
        Console.WriteLine("2 - Load test - Resolve second by second, 5 calls per seconds [ 100, 300, 400, 800, 900 ]");
        Console.WriteLine("3 - Load test - GetItems");
        Console.WriteLine("4 - Load test - GetItemsBaseline");
        Console.WriteLine("5 - Load test - Run Both in Parallel");
        Console.WriteLine("6 - Generate userCerts file");
        Console.WriteLine("7 - Caching test - 2 resolve call: [{WS0, User5 [{$VL1/Var1}]}, {WS0, User5 [{$VL2/Var2}]}");
        Console.WriteLine("8 - Caching test - 2 resolve call: [{WS0, User5 [{$VL1/Var3}, {$VL2/Var4}]}, {WS0, User5 [{$VL1/Var3}, {$VL2/Var4}]}");
        Console.WriteLine("9 - Caching test - 2 resolve call: [{WS0, User5 [{$VL_NotExists/Var1}]}, {WS0, User5 [{$VL_NotExists/Var2}]}");
        Console.WriteLine("10 - Caching test - 2 resolve call: [{WS0, User5 [{$VL1/Var1}]}, {WS0, User5 [{$VL1/Var1}]}");
        Console.WriteLine("11 - Caching test - 2 resolve call with 4 min delay between each call: [{WS0, User5 [{$VL1/Var1}]}, {WS0, User5 [{$VL1/Var1}]}");
        Console.Write("Enter your choice:");

        var choice = Console.ReadLine()?.Trim();

        var testStartTime = DateTime.UtcNow;

        switch (choice)
        {
            case "0":
                {
                    string testName = "LoadTest_ResolveCalls_SaticRate";

                    var loadUnit = new RunnerLoadUnit<ResolveResultSummary, ResolveResultSummaryPredefined, ResolveLoadUnit>(
                        () => new ResolveLoadUnit(prepareFabricEnv: true, testStartTime, loadUnitObjectId: null) // loadUnitObjectId: new Guid("24adfab5-6aae-4793-8761-9290f294361b")
                            .PrepareLoadUnit(
                                "ResolveLoadUnitLiveSessionConfiguration_StaticRate.json",
                                "ResolveLoadUnitPreparationConfiguration_10Ws.json",
                                "ResolveLoadUnitResolveCallsConfiguration_CartesianPreparation.json"));

                    Console.WriteLine($"Running Resolve with test '{testName}'...");
                    await loadUnit.RunAsync(testName);
                    break;
                }
            case "1":
                {
                    string testName = "LoadTest_ResolveCalls_LinearIncreaseRate";

                    var loadUnit = CreateResolveLoadUnit(
                        testStartTime,
                        "ResolveLoadUnitLiveSessionConfiguration_LinearRateIncrease.json",
                        "ResolveLoadUnitPreparationConfiguration_10Ws.json",
                        "ResolveLoadUnitResolveCallsConfiguration_CartesianPreparation.json");

                    Console.WriteLine($"Running Resolve with test '{testName}'...");
                    await loadUnit.RunAsync(testName);
                    break;
                }
            case "2":
                {
                    string testName = "LoadTest_ResolveCalls_SecondBySecondRate";

                    var loadUnit = CreateResolveLoadUnit(
                        testStartTime,
                        "ResolveLoadUnitLiveSessionConfiguration_SecondBySecondCallsRate.json",
                        "ResolveLoadUnitPreparationConfiguration_2Ws.json",
                        "ResolveLoadUnitResolveCallsConfiguration_CartesianPreparation.json");

                    Console.WriteLine($"Running Resolve with test '{testName}'...");
                    await loadUnit.RunAsync(testName);
                    break;
                }
            case "3":
                {
                    string testName = "Load test - GetItems";
                    var loadUnit = new RunnerLoadUnit<NoPayload, NoPayload, GetItemsLoadUnit>(
                        () => new GetItemsLoadUnit(prepareFabricEnv: true, testStartTime, loadUnitObjectId: null)
                            .PrepareLoadUnit("GetItemsLoadUnitLiveSessionConfiguration.json"));

                    Console.WriteLine($"Running getitems with test '{testName}'...");
                    await loadUnit.RunAsync(testName);
                    break;
                }

            case "4":
                {

                    string testName = "Load test - GetItemsBaseline";
                    var loadUnit = new RunnerLoadUnit<NoPayload, NoPayload, GetItemsLoadUnit>(
                        () => new GetItemsLoadUnit(prepareFabricEnv: true, testStartTime, loadUnitObjectId: null)
                            .PrepareLoadUnit("GetItemsLoadUnitLiveSessionBaselineConfiguration.json"));

                    Console.WriteLine($"Running getitemsBaseline with test '{testName}'...");
                    await loadUnit.RunAsync(testName);
                    break;
                }

            case "5":
                {
                    string testName = "Load test - Run Both in Parallel";

                    var loadUnit1 = new RunnerLoadUnit<NoPayload, NoPayload, GetItemsLoadUnit>(
                        () => new GetItemsLoadUnit(prepareFabricEnv: true, testStartTime, loadUnitObjectId: null)
                            .PrepareLoadUnit("GetItemsLoadUnitLiveSessionConfiguration.json"));

                    var loadUnit2 = new RunnerLoadUnit<NoPayload, NoPayload, GetItemsLoadUnit>(
                        () => new GetItemsLoadUnit(prepareFabricEnv: true, testStartTime, loadUnitObjectId: null)
                            .PrepareLoadUnit("GetItemsLoadUnitLiveSessionBaselineConfiguration.json"));

                    Console.WriteLine("Running both getitems and getitemsBaseline in parallel...");

                    var task1 = Task.Run(() => loadUnit1.RunAsync(testName + "_GetItems"));
                    var task2 = Task.Run(() => loadUnit2.RunAsync(testName + "_GetItemsBaseline"));

                    await Task.WhenAll(task1, task2);
                    break;
                }

            case "6":
                {
                    await UserCertsFileGenerator.RunAsync();
                    break;
                }
            case "7":
                {
                    string testName = "CachingTest_2ResolveCalls_1VarRef";

                    var loadUnit = new RunnerLoadUnit<ResolveResultSummary, ResolveResultSummaryPredefined, ResolveLoadUnit>(
                        () => new ResolveLoadUnit(prepareFabricEnv: true, testStartTime, loadUnitObjectId: null)
                            .PrepareLoadUnit(
                                "ResolveLoadUnitLiveSessionConfiguration_2C_1Sec.json",
                                "ResolveLoadUnitPreparationConfiguration_1WS_2VL_1CI.json",
                                "ResolveLoadUnitResolveCallsConfiguration_Predefined_CacheTest_2C_2VL_1VR.json"));

                    Console.WriteLine($"Running Resolve with test '{testName}'...");
                    await loadUnit.RunAsync(testName);
                    break;
                }
            case "8":
                {
                    string testName = "CachingTest_2ResolveCalls_2VarRef";

                    var loadUnit = CreateResolveLoadUnit(
                        testStartTime,
                        "ResolveLoadUnitLiveSessionConfiguration_2C_1Sec.json",
                        "ResolveLoadUnitPreparationConfiguration_1WS_2VL_1CI.json",
                        "ResolveLoadUnitResolveCallsConfiguration_Predefined_CacheTest_2C_2VL_2VR.json");

                    Console.WriteLine($"Running Resolve with test '{testName}'...");
                    await loadUnit.RunAsync(testName);
                    break;
                }

            case "9":
                {
                    string testName = "CachingTest_2ResolveCalls_VarlibNotExists";

                    var loadUnit = CreateResolveLoadUnit(
                        testStartTime,
                        "ResolveLoadUnitLiveSessionConfiguration_2C_1Sec.json",
                        "ResolveLoadUnitPreparationConfiguration_1WS_2VL_1CI.json",
                        "ResolveLoadUnitResolveCallsConfiguration_Predefined_CacheTest_2C_VL_Not_Exists.json");

                    Console.WriteLine($"Running Resolve with test '{testName}'...");
                    await loadUnit.RunAsync(testName);
                    break;
                }

            case "10":
                {
                    string testName = "CachingTest_2ResolveCalls_IncludeKustoQueryValidation";

                    var loadUnit = CreateResolveLoadUnit(
                        testStartTime,
                        "ResolveLoadUnitLiveSessionConfiguration_1C_1Sec.json",
                        "ResolveLoadUnitPreparationConfiguration_1WS_2VL_1CI.json",
                        "ResolveLoadUnitResolveCallsConfiguration_Predefined_CacheTest_1C_1VL_1VR_KustoValidation.json");

                    Console.WriteLine($"Running Resolve with test '{testName}'...");
                    await loadUnit.RunAsync(testName);
                    break;
                }

            case "11":
                {
                    string testName = "CachingTest_2ResolveCalls_LongDelayBetween";

                    var loadUnit = CreateResolveLoadUnit(
                        testStartTime,
                        "ResolveLoadUnitLiveSessionConfiguration_2C_1Sec_3MinDelay_2C_1Sec.json",
                        "ResolveLoadUnitPreparationConfiguration_1WS_2VL_1CI.json",
                        "ResolveLoadUnitResolveCallsConfiguration_Predefined_CacheTest_1C_1VL_1VR.json");

                    Console.WriteLine($"Running Resolve with test '{testName}'...");
                    await loadUnit.RunAsync(testName);
                    break;

                    break;
                }

            default:
                throw new ArgumentException("Invalid choice. Please enter 1, 2, or 3.");
        }

        Console.WriteLine("Execution completed. Press any key to exit.");
        Console.ReadKey();
    }
    private static RunnerLoadUnit<ResolveResultSummary, ResolveResultSummaryPredefined, ResolveLoadUnit> CreateResolveLoadUnit(
    DateTime testStartTime,
    string liveSessionConfigFile,
    string preparationConfigFile,
    string resolveCallsConfigFile)
    {
        return new RunnerLoadUnit<ResolveResultSummary, ResolveResultSummaryPredefined, ResolveLoadUnit>(
            () => new ResolveLoadUnit(prepareFabricEnv: true, testStartTime, loadUnitObjectId: null)
                .PrepareLoadUnit(
                    liveSessionConfigFile,
                    preparationConfigFile,
                    resolveCallsConfigFile));
    }
}