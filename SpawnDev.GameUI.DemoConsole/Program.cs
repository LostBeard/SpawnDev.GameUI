// SpawnDev.GameUI Desktop Test Runner
// Uses SpawnDev.UnitTesting's ConsoleRunner to enumerate + run [TestMethod]s
// from the shared test project. Compatible with PlaywrightMultiTest's
// no-args-enumerate / one-arg-runs-it console contract.
using Microsoft.Extensions.DependencyInjection;
using SpawnDev.GameUI.Demo.Shared.UnitTests;
using SpawnDev.UnitTesting;

var services = new ServiceCollection();
services.AddSingleton<GameUITestsHarness>();
var sp = services.BuildServiceProvider();
var runner = new UnitTestRunner(sp, true);
await ConsoleRunner.Run(args, runner);
