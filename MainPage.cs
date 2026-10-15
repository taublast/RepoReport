using DrawnUi.Infrastructure;
using RepoReport.Export;
using RepoReport.Preview;
using RepoReport.Reports;
using RepoReport.ViewModels;

namespace RepoReport;

/// <summary>
/// One page: a toolbar to pick a repository and export, and the live report below it.
/// The report on screen and the report in the PDF are the same XAML with the same view model.
/// </summary>
public partial class MainPage : BasePageReloadable, IDisposable
{
    readonly ReportViewModel _model = new();
    Canvas _canvas;
    SkiaLabel _status;
    SkiaScroll _preview;
    bool _busy;

    static readonly Color Bg = Color.Parse("#0B0E14");
    static readonly Color Accent = Color.Parse("#4C8DFF");

    public MainPage()
    {
        Title = "RepoReport";
        BackgroundColor = Bg;
    }

    public override void Build()
    {
        _canvas?.Dispose();

        _canvas = new Canvas
        {
            Gestures = GesturesMode.Enabled,
            RenderingMode = RenderingModeType.Accelerated,
            BackgroundColor = Bg,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = CreateContent()
        };

        Content = new Grid { Children = { _canvas } };
    }

    SkiaControl CreateContent()
    {
        return new SkiaStack
        {
            VerticalOptions = LayoutOptions.Fill,
            Spacing = 0,
            Children =
            {
                // toolbar: buttons cache themselves; no cache on the whole bar, the spinner animates inside it
                new SkiaStack
                {
                    Padding = new Thickness(16, 12),
                    Spacing = 10,
                    BackgroundColor = Color.Parse("#141925"),
                    Children =
                    {
                        new SkiaGrid
                        {
                            ColumnSpacing = 10,
                            HorizontalOptions = LayoutOptions.Fill,
                            Children =
                            {
                                new SkiaEditor
                                {
                                    Text = _model.RepoName,
                                    PlaceholderText = "owner/repo",
                                    FontFamily = "FontText",
                                    FontSize = 15,
                                    TextColor = Colors.White,
                                    BackgroundColor = Color.Parse("#1D2434"),
                                    StrokeWidth = 0,
                                    CornerRadius = 8,
                                    Padding = new Thickness(12, 0),
                                    HeightRequest = 40,
                                    MaxLines = 1,
                                    ReturnType = ReturnType.Send,
                                    CommandOnSubmit = new Command(async () => await LoadAsync()),
                                    HorizontalOptions = LayoutOptions.Fill,
                                }
                                .OnTextChanged(text => _model.RepoName = text)
                                .SetGrid(0, 0),

                                ToolbarButton("Load", Accent).OnTapped(async me => await LoadAsync()).SetGrid(1, 0),
                            }
                        }.WithColumnDefinitions("*,Auto"),

                        new SkiaWrap
                        {
                            Spacing = 8,
                            Children =
                            {
                                ToolbarButton("Export PDF", Color.Parse("#30A46C"))
                                    .OnTapped(async me => await ExportAsync(asImage: false)),
                                ToolbarButton("Export JPG", Color.Parse("#30A46C"))
                                    .OnTapped(async me => await ExportAsync(asImage: true)),
#if WINDOWS
                                ToolbarButton("Preview window", Color.Parse("#5B6475"))
                                    .OnTapped(me => OpenPreview()),
#endif
                            }
                        },

                        new SkiaRow
                        {
                            Spacing = 8,
                            Children =
                            {
                                // spinning arc while the GitHub data loads
                                new SkiaShape
                                {
                                    Type = ShapeType.Arc,
                                    Value1 = 0,
                                    Value2 = 270,
                                    StrokeColor = Accent,
                                    StrokeWidth = 2,
                                    BackgroundColor = Colors.Transparent,
                                    WidthRequest = 14,
                                    HeightRequest = 14,
                                    IsVisible = _model.IsLoading,
                                    VerticalOptions = LayoutOptions.Center,
                                }
                                .AnimateRotation(0, 360, seconds: 1.0, repeat: -1)
                                .ObserveProperty(_model, nameof(ReportViewModel.IsLoading), me => me.IsVisible = _model.IsLoading),

                                new SkiaLabel(_model.Status)
                                {
                                    FontFamily = "FontText",
                                    FontSize = 12,
                                    TextColor = Color.Parse("#A9B4C6"),
                                    MaxLines = 2,
                                    VerticalOptions = LayoutOptions.Center,
                                }
                                .WithAccessibilityText()
                                .ObserveProperty(_model, nameof(ReportViewModel.Status), me => me.Text = _model.Status)
                                .Assign(out _status),
                            }
                        },
                    }
                },

                // the exported report is shown here after an export, cached as one image; pans both ways on a phone
                new SkiaScroll
                {
                    Orientation = ScrollOrientation.Both,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                }.Assign(out _preview)
            }
        };
    }

    static SkiaButton ToolbarButton(string text, Color color)
    {
        return new SkiaButton(text)
        {
            UseCache = SkiaCacheType.Image,
            BackgroundColor = color,
            TextColor = Colors.White,
            FontFamily = "FontTextTitle",
            FontSize = 13,
            CornerRadius = 8,
            HeightRequest = 40,
            Padding = new Thickness(14, 0),
        }.WithAccessibilityButton();
    }

    async Task LoadAsync()
    {
        if (_busy)
            return;
        _busy = true;
        try
        {
            await _model.LoadAsync();
        }
        finally
        {
            _busy = false;
        }
    }

    async Task ExportAsync(bool asImage)
    {
        if (_busy)
            return;
        // the typed repository was not loaded yet, or changed since: load it first
        if (_model.NeedsLoad)
        {
            await LoadAsync();
            if (_model.NeedsLoad)
                return;
        }

        _busy = true;
        try
        {
            _status.Text = "Rendering report...";
            var folder = Path.Combine(FileSystem.CacheDirectory, "Reports");

            // one instance: rendered into the file, then shown on screen with its images already loaded
            var report = new ReportView { BindingContext = _model };

            var file = asImage
                ? await ReportExporter.ExportImageAsync(report, PaperFormat.A4, folder)
                : await ReportExporter.ExportPdfAsync(report, PaperFormat.A4, folder);

            ShowPreview(report);
            _status.Text = $"Saved {Path.GetFileName(file)} ({new FileInfo(file).Length / 1024} KB)";

#if WINDOWS
            await Launcher.Default.OpenAsync(new OpenFileRequest("Report", new ReadOnlyFile(file)));
#elif MACCATALYST
            // Launcher.OpenAsync(OpenFileRequest) does nothing on Mac Catalyst and Share crashes in the
            // completion callback (dotnet/macios binding), so offer a native save dialog instead
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var picker = new UIKit.UIDocumentPickerViewController(new[] { Foundation.NSUrl.FromFilename(file) }, true);
                Platform.GetCurrentUIViewController()?.PresentViewController(picker, true, null);
            });
#else
            await Share.Default.RequestAsync(new ShareFileRequest("Report", new ShareFile(file)));
#endif
        }
        catch (Exception e)
        {
            Super.Log(e);
            _status.Text = e.Message;
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>
    /// Shows the exported report on screen at the printed width, cached as one image so scrolling is a blit.
    /// </summary>
    void ShowPreview(ReportView report)
    {
        var previous = _preview.Content;
        var width = ReportExporter.PrintableWidth(PaperFormat.A4);
        report.WidthRequest = width;
        report.UseCache = SkiaCacheType.Image;

        // narrow screens: the scroll renders its content at a reduced scale so the full page width fits
        var viewportWidth = _preview.DrawingRect.Width / _preview.RenderingScale;
        _preview.ViewportZoom = viewportWidth > 0 ? Math.Min(1.0, viewportWidth / width) : 1.0;

        _preview.Content = report;
        previous?.Dispose();
    }

    /// <summary>
    /// Windows only: the report at A4 width in a second window. Edit ReportView.xaml, save, watch it reload.
    /// </summary>
    void OpenPreview()
    {
        var inches = Pdf.GetPaperSizeInInches(PaperFormat.A4);
        var scale = 0.75f; // a 150 dpi page shown at 112 window pixels per inch
        var size = new SKSize(ReportExporter.Dpi * inches.Width * scale + 32, ReportExporter.Dpi * inches.Height * scale);

        var page = new PreviewDrawnLayoutPage(() => new ReportView
        {
            BindingContext = _model,
            WidthRequest = ReportExporter.Dpi * inches.Width * scale,
            HorizontalOptions = LayoutOptions.Center,
        });

        PreviewDrawnLayoutPage.OpenPageInNewWindow(page, "Report preview", size);
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
