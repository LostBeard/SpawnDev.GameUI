using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

using SpawnDev.BlazorJS;
using SpawnDev.GameUI.Demo;
using SpawnDev.GameUI.Demo.Shared.UnitTests;

// Print build timestamp so we can verify we're running the right build via browser console
Console.WriteLine($"[SpawnDev.GameUI.Demo] Build: {BuildTimestamp.Value}");

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Services.AddBlazorJSRuntime();

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Register PMT-discoverable test harnesses
builder.Services.AddSingleton<GameUITestsHarness>();

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

await builder.Build().BlazorJSRunAsync();
