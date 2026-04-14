// SpawnDev.GameUI Desktop Test Runner
using SpawnDev.GameUI.Demo.Shared.UnitTests;

Console.WriteLine("[SpawnDev.GameUI.DemoConsole] Running unit tests...");
Console.WriteLine();

var (passed, failed, errors) = GameUITests.RunAll();

Console.WriteLine($"Results: {passed} passed, {failed} failed");
if (errors.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("FAILURES:");
    foreach (var err in errors)
        Console.WriteLine($"  FAIL: {err}");
}

Console.WriteLine();
Console.WriteLine(failed == 0 ? "ALL TESTS PASSED" : "SOME TESTS FAILED");
return failed > 0 ? 1 : 0;
