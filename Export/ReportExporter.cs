using DrawnUi.Infrastructure;
using RepoReport.Reports;
using RepoReport.ViewModels;

namespace RepoReport.Export;

/// <summary>
/// Renders a <see cref="ReportView"/> off-screen into a multi-page PDF or a single tall JPEG.
/// The caller creates the report and keeps it: once exported it can be shown on screen as the preview.
/// </summary>
public static class ReportExporter
{
    /// <summary>
    /// Pixels per inch of the report canvas. Report XAML sizes are in these units.
    /// </summary>
    public const int Dpi = 150;

    /// <summary>
    /// Page margins in inches.
    /// </summary>
    public const float MarginInches = 0.3f;

    /// <summary>
    /// Width of the printed content in report units, the width the report is laid out at.
    /// </summary>
    public static float PrintableWidth(PaperFormat format)
    {
        return Pdf.GetPaperSizePixels(format, Dpi).Width - MarginInches * Dpi * 2;
    }

    /// <summary>
    /// How long to wait for images before exporting whatever is there.
    /// </summary>
    static readonly TimeSpan ContentTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Renders the report to a PDF file and returns its full path.
    /// </summary>
    public static async Task<string> ExportPdfAsync(ReportView content, PaperFormat format, string folder)
    {
        var scale = 1f;
        var inches = Pdf.GetPaperSizeInInches(format);
        var paper = Pdf.GetPaperSizePixels(inches, Dpi);
        var margins = MarginInches * Dpi;
        var pageInner = new SKSize(paper.Width - margins * 2, paper.Height - margins * 2);
        var data = content.BindingContext as ReportViewModel;

        // Pass 1: lay the whole report out at page width with unlimited height, let images load,
        // and record it off the PDF so the render tree knows where every table row sits.
        await WarmUpAsync(content, pageInner.Width, float.PositiveInfinity, scale);

        var pages = Pdf.SplitStackToPages(content, isTemplated: true, pageInner, scale);
        var pdfClock = System.Diagnostics.Stopwatch.StartNew();

        // Pass 2: the printable tree. Padding gives the margins, the scroll shows one page at a time.
        SkiaScroll viewport = null;
        var wrapper = new SkiaLayout
        {
            BindingContext = data,
            Padding = new Thickness(margins),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = new List<SkiaControl>
            {
                new SkiaScroll
                {
                    BackgroundColor = Colors.White,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Content = content,
                }.Assign(out viewport)
            }
        };

        wrapper.Measure(paper.Width, paper.Height, scale);
        wrapper.Arrange(new SKRect(0, 0, paper.Width, paper.Height),
            wrapper.MeasuredSize.Pixels.Width, wrapper.MeasuredSize.Pixels.Height, scale);

        var pagePoints = new SKSize(inches.Width * 72, inches.Height * 72);
        var fullFilename = PrepareFile(folder, "pdf");

        using var ms = new MemoryStream();
        using (var stream = new SKManagedWStream(ms))
        using (var document = SKDocument.CreatePdf(stream, new SKDocumentPdfMetadata
        {
            RasterDpi = Dpi,
            EncodingQuality = 85,
            Author = "RepoReport sample",
            Producer = "DrawnUI",
            Subject = data?.Title,
            Title = $"{data?.Title} report",
        }))
        {
            foreach (var page in pages)
            {
                viewport.ViewportOffsetY = -page.Position.Y;

                // settle the scroll at the new offset off the PDF
                RenderOnce(wrapper, paper, scale);

                using (var canvas = document.BeginPage(pagePoints.Width, pagePoints.Height))
                {
                    // the page is in points (72 per inch), our units are Dpi pixels
                    canvas.Scale(72f / Dpi);
                    canvas.Clear(SKColors.White);

                    // the page ends where the split said, not at the paper edge: the viewport is a full
                    // paper tall, without the clip the top of the next row would show at the bottom
                    canvas.ClipRect(SKRect.Create(0, 0, paper.Width, margins + page.Height));

                    var ctx = new SkiaDrawingContext { Canvas = canvas, Width = paper.Width, Height = paper.Height };
                    wrapper.Render(new DrawingContext(ctx, new SKRect(0, 0, paper.Width, paper.Height), scale));
                }
                document.EndPage();
            }
            document.Close();
        }

        await File.WriteAllBytesAsync(fullFilename, ms.ToArray());
        Console.WriteLine($"[RepoReport] pdf {pages.Count} pages rendered+written {pdfClock.ElapsedMilliseconds} ms");

        // the report stays attached to the throwaway wrapper on purpose: detaching it (Content = null)
        // clears its BindingContext by design. The caller re-parents it directly to show it on screen.
        return fullFilename;
    }

    /// <summary>
    /// Renders the whole report as one tall JPEG at page width and returns its full path.
    /// </summary>
    public static async Task<string> ExportImageAsync(ReportView content, PaperFormat format, string folder)
    {
        var scale = 1f;
        var width = PrintableWidth(format);

        await WarmUpAsync(content, width, float.PositiveInfinity, scale);

        var w = (int)Math.Ceiling(content.MeasuredSize.Pixels.Width);
        var h = (int)Math.Ceiling(content.MeasuredSize.Pixels.Height);

        using var surface = SKSurface.Create(new SKImageInfo(w, h));
        surface.Canvas.Clear(SKColors.White);
        var ctx = new SkiaDrawingContext { Canvas = surface.Canvas, Surface = surface, Width = w, Height = h };
        content.Render(new DrawingContext(ctx, new SKRect(0, 0, w, h), scale));
        surface.Flush();

        var fullFilename = PrepareFile(folder, "jpg");
        using var image = surface.Snapshot();
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        await using (var file = File.Create(fullFilename))
        {
            encoded.SaveTo(file);
        }
        return fullFilename;
    }

    /// <summary>
    /// Measures and arranges the control, then renders it into a throwaway recording until its
    /// async content is loaded and nothing asks for another frame. Nothing of this lands in the output.
    /// </summary>
    static async Task WarmUpAsync(SkiaControl layout, float width, float height, float scale)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();


        layout.Measure(width, height, scale);
        layout.Arrange(new SKRect(0, 0, layout.MeasuredSize.Pixels.Width, layout.MeasuredSize.Pixels.Height),
            layout.MeasuredSize.Pixels.Width, layout.MeasuredSize.Pixels.Height, scale);

        var size = new SKSize(layout.MeasuredSize.Pixels.Width, layout.MeasuredSize.Pixels.Height);

        Console.WriteLine($"[RepoReport] warm-up measure+arrange {clock.ElapsedMilliseconds} ms");

        // first rendering starts the async image loads
        RenderOnce(layout, size, scale);
        Console.WriteLine($"[RepoReport] warm-up first render {clock.ElapsedMilliseconds} ms");

        var started = DateTime.UtcNow;
        if (layout is IContentReadyAware aware)
        {
            while (!aware.ContentIsLoaded && DateTime.UtcNow - started < ContentTimeout)
            {
                await Task.Delay(50);
            }
        }
        Console.WriteLine($"[RepoReport] warm-up images loaded {clock.ElapsedMilliseconds} ms");

        // second rendering reflects loaded images, then settle while controls still invalidate
        RenderOnce(layout, size, scale);
        var frames = 0;
        while (layout.NeedUpdate && frames++ < 60)
        {
            await Task.Delay(16);
            RenderOnce(layout, size, scale);
        }
        Console.WriteLine($"[RepoReport] warm-up settled after {frames} extra frames {clock.ElapsedMilliseconds} ms");

        // size can change after images and remeasure: re-arrange to the final measured size
        layout.Arrange(new SKRect(0, 0, layout.MeasuredSize.Pixels.Width, layout.MeasuredSize.Pixels.Height),
            layout.MeasuredSize.Pixels.Width, layout.MeasuredSize.Pixels.Height, scale);
    }

    /// <summary>
    /// One render pass into an SKPictureRecorder: no pixels allocated, the picture is discarded.
    /// </summary>
    static void RenderOnce(SkiaControl layout, SKSize size, float scale)
    {
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(new SKRect(0, 0, size.Width, size.Height));
        var ctx = new SkiaDrawingContext { Canvas = canvas, Width = size.Width, Height = size.Height };
        layout.Render(new DrawingContext(ctx, new SKRect(0, 0, size.Width, size.Height), scale));
        using var picture = recorder.EndRecording();
    }

    static string PrepareFile(string folder, string extension)
    {
        Directory.CreateDirectory(folder);
        var fullFilename = Path.Combine(folder, $"RepoReport_{DateTime.Now:yyyy-MM-dd_HHmmss}.{extension}");
        if (File.Exists(fullFilename))
            File.Delete(fullFilename);
        return fullFilename;
    }
}
