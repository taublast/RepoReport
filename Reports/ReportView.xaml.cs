using DrawnUi.Infrastructure.Models;
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
    int _imagesDone;

    public ReportView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// True once every avatar reported Success or Error. The count of expected images comes from the view model.
    /// </summary>
    public bool ContentIsLoaded =>
        BindingContext is ReportViewModel vm && _imagesDone >= vm.ExpectedImages;

    void Image_OnSuccess(object sender, ContentLoadedEventArgs e)
    {
        _imagesDone++;
    }

    void Image_OnError(object sender, ContentLoadedEventArgs e)
    {
        _imagesDone++;
    }
}
