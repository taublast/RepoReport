namespace RepoReport.Preview;

/// <summary>
/// Desktop helper: shows a drawn layout in its own window at a given size,
/// so a report can be designed with XAML hot reload instead of exporting every time.
/// </summary>
public class PreviewDrawnLayoutPage : BasePageReloadable
{
    readonly Func<SkiaControl> _factory;
    Canvas _canvas;

    public PreviewDrawnLayoutPage(Func<SkiaControl> factory)
    {
        _factory = factory;
        BackgroundColor = Color.Parse("#0B0E14");
    }

    public override void Build()
    {
        _canvas?.Dispose();

        _canvas = new Canvas
        {
            Gestures = GesturesMode.Enabled,
            RenderingMode = RenderingModeType.Accelerated,
            BackgroundColor = Color.Parse("#0B0E14"),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new SkiaScroll
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Content = _factory(),
            }
        };

        Content = _canvas;
    }

    /// <summary>
    /// Opens a page in a second window next to the main one. Windows and Mac Catalyst only.
    /// </summary>
    public static void OpenPageInNewWindow(ContentPage page, string title, SKSize size)
    {
#if WINDOWS || MACCATALYST
        var window = new Window(page)
        {
            Title = title,
            MinimumWidth = 200,
            MinimumHeight = 100,
            Width = size.Width,
            Height = size.Height,
        };

        var mainWindow = Application.Current?.Windows?.FirstOrDefault();
        if (mainWindow != null)
        {
            window.X = mainWindow.X + mainWindow.Width + 20;
            window.Y = mainWindow.Y;
        }

        Application.Current.OpenWindow(window);
#endif
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            Content = null;
            _canvas?.Dispose();
            _canvas = null;
        }
        base.Dispose(isDisposing);
    }
}
