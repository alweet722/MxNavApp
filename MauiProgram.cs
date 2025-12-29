using Microsoft.Extensions.Logging;
using Shiny;
using SkiaSharp.Views.Maui.Controls.Hosting;
using System.Diagnostics;

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
        builder.Services.AddBluetoothLE();
        builder.Services.AddSingleton<BleConnectionState>();
        builder.Services.AddSingleton<NavigationManager>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}