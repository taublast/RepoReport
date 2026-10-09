using RepoReport.ViewModels;

namespace RepoReport.Reports;

/// <summary>
/// Tells the exporter when every asynchronous part of the report has arrived.
/// </summary>
public interface IContentReadyAware
{
    bool ContentIsLoaded { get; }
}

/// <summary>
/// The report template. The same XAML is shown on screen and rendered into the PDF.
/// </summary>
public partial class ReportView : SkiaLayout, IContentReadyAware
{
    public ReportView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// True once every image of the report exists and has settled: a bitmap arrived, the load failed,
    /// or nothing is loading anymore. State, not events, so a missed event can never hold the export.
    /// </summary>
    public bool ContentIsLoaded
    {
        get
        {
            if (BindingContext is not ReportViewModel vm)
                return false;

            var images = Descendants(this).OfType<SkiaImage>().ToList();
            return images.Count >= vm.ExpectedImages
                   && images.All(image => image.LoadedSource != null || image.HasError || !image.IsLoading);
        }
    }

    static IEnumerable<SkiaControl> Descendants(SkiaControl control)
    {
        // a templated layout keeps its live cells in the adapter, not in Views
        var children = control is SkiaLayout { IsTemplated: true } layout
            ? layout.ChildrenFactory.GetCellsInUse()
            : control.Views;

        foreach (var child in children)
        {
            yield return child;
            foreach (var deeper in Descendants(child))
                yield return deeper;
        }
    }
}
