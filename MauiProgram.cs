using Microsoft.Extensions.Logging;

namespace RepoReport;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseDrawnUi(new DrawnUiStartupSettings
            {
                // Listen to hardware keys on desktop (Windows / Mac Catalyst).
                UseDesktopKeyboard = true,

                // Desktop window wide enough for the report at A4 width (1240 px at 150 dpi).
                DesktopWindow = new()
                {
                    Width = 1320,
                    Height = 900,
                    //IsFixedSize = true
                }
            })
            .ConfigureFonts(fonts =>
            {
                // Same aliases the DrawnUI Fiddle registers, so exported snippets
                // find the fonts they were written against.
                fonts.AddFont("OpenSans-Regular.ttf", "FontText");
                fonts.AddFont("OpenSans-Semibold.ttf", "FontTextTitle");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
