using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

using SpawnDev.SpawnJS;
using SpawnDev.GameUI.Demo;
using SpawnDev.GameUI.Demo.Shared.UnitTests;

// Print build timestamp so we can verify we're running the right build via browser console
Console.WriteLine($"[SpawnDev.GameUI.Demo] Build: {BuildTimestamp.Value}");

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Services.AddSpawnJSRuntime();
// Slot lifetime is manual in SpawnJS; watcher names leaks from owned wrappers/callbacks.
SpawnJSRuntime.EnableIDisposableWatcher = true;

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Register PMT-discoverable test harnesses
builder.Services.AddSingleton<GameUITestsHarness>();

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

await builder.Build().SpawnJSRunAsync();
