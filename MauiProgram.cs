using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using NBNavApp.Common.Ble;
using NBNavApp.Common.Navigation;
using NBNavApp.Common.Services;
using NBNavApp.Pages;
using NBNavApp.ViewModels;
using Shiny;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace NBNavApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseShiny()
            .UseSkiaSharp()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });
        builder.Services.AddBluetoothLE();
        builder.Services.AddTransient<StartPage>();
        builder.Services.AddSingleton<StartPageViewModel>();
        builder.Services.AddTransient<RoutePage>();
        builder.Services.AddSingleton<RoutePageViewModel>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddSingleton<SettingsPageViewModel>();
        builder.Services.AddSingleton<BleConnectionState>();
        builder.Services.AddSingleton<BleStateMonitor>();
        builder.Services.AddSingleton<NavigationManager>();
        builder.Services.AddSingleton<BleSender>();
        builder.Services.AddSingleton<NavigationService>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}