using Microsoft.Extensions.DependencyInjection;

namespace SpawnDev.GameUI;

/// <summary>
/// DI registration extensions for SpawnDev.GameUI.
///
/// Usage in Program.cs:
///   builder.Services.AddBlazorJSRuntime();   // SpawnDev.BlazorJS (required)
///   builder.Services.AddGameUI();             // SpawnDev.GameUI
///
/// Then inject in any component:
///   @inject GameUIService UI
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Register SpawnDev.GameUI services for dependency injection.
    /// Registers GameUIService as a singleton.
    /// </summary>
    public static IServiceCollection AddGameUI(this IServiceCollection services)
    {
        services.AddSingleton<GameUIService>();
        return services;
    }

    /// <summary>
    /// Register SpawnDev.GameUI with a custom theme.
    /// </summary>
    public static IServiceCollection AddGameUI(this IServiceCollection services, UITheme theme)
    {
        UITheme.Current = theme;
        services.AddSingleton<GameUIService>();
        return services;
    }
}
