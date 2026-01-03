using Microsoft.Extensions.Logging;
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
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });
        builder.Services.AddTransient<BleScanViewModel>();
        builder.Services.AddTransient<StartPage>();
        builder.Services.AddBluetoothLE();
        builder.Services.AddSingleton<BleConnectionState>();
        builder.Services.AddSingleton<NavigationManager>();
        builder.Services.AddSingleton<BleSender>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}