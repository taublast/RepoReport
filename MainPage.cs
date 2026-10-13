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
                // toolbar: static chrome, cached as one image
                new SkiaStack
                {
                    UseCache = SkiaCacheType.Image,
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

                        new SkiaLabel(_model.Status)
                        {
                            FontFamily = "FontText",
                            FontSize = 12,
                            TextColor = Color.Parse("#A9B4C6"),
                            MaxLines = 2,
                        }
                        .WithAccessibilityText()
                        .ObserveProperty(_model, nameof(ReportViewModel.Status), me => me.Text = _model.Status)
                        .Assign(out _status),
                    }
                },

                // the live report: same XAML as the PDF, laid out at screen width
                new SkiaScroll
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Content = new ReportView
                    {
                        BindingContext = _model,
                        UseCache = SkiaCacheType.Image,
                        HorizontalOptions = LayoutOptions.Fill,
                    }
                }
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
        if (!_model.HasData)
        {
            await LoadAsync();
            if (!_model.HasData)
                return;
        }

        _busy = true;
        try
        {
            _status.Text = asImage ? "Rendering image..." : "Rendering PDF...";
            var folder = Path.Combine(FileSystem.CacheDirectory, "Reports");

            var file = asImage
                ? await ReportExporter.ExportImageAsync(_model, PaperFormat.A4, folder)
                : await ReportExporter.ExportPdfAsync(_model, PaperFormat.A4, folder);

            _status.Text = $"Saved {Path.GetFileName(file)} ({new FileInfo(file).Length / 1024} KB)";

#if WINDOWS
            await Launcher.Default.OpenAsync(new OpenFileRequest("Report", new ReadOnlyFile(file)));
#else
            // Mac Catalyst too: Launcher.OpenAsync(OpenFileRequest) returns without opening anything there
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
