# RepoReport

A .NET MAUI sample that renders a DrawnUI XAML layout into a multi-page PDF.

It asks the GitHub API for a repository (stars, forks, issues, languages, weekly commits, top contributors with avatars), shows the report on screen, and exports the same XAML as:

- a multi-page A4 PDF, pages split between table rows (`Pdf.SplitStackToPages`)
- one tall JPEG

The report on screen and the report in the PDF are one `ReportView.xaml` with one view model. Nothing in the template is cached, so text and shapes stay vectors inside the PDF.

## Run

```
dotnet build -f net10.0-windows10.0.19041.0
```

Then run from the IDE or `dotnet run`. Type `owner/repo`, tap **Load**, then **Export PDF** or **Export JPG**. On Windows and Mac the file opens in the default viewer, on Android and iOS the share sheet appears.

Unauthenticated GitHub requests are limited to 60 per hour. Set a `GITHUB_TOKEN` environment variable to raise the limit.

## Where to look

| File | What |
|---|---|
| `Reports/ReportView.xaml` | the report template, bindings only |
| `Reports/StatTile.cs`, `BarChart.cs`, `LanguageBar.cs` | small custom drawn controls used by the template |
| `Export/ReportExporter.cs` | measure, warm-up passes, page split, `SKDocument` output |
| `Preview/PreviewDrawnLayoutPage.cs` | desktop preview window for designing with XAML hot reload |
| `ViewModels/ReportViewModel.cs` | async loading from the GitHub API |

## How the export works

1. Create `ReportView`, set its `BindingContext`.
2. Measure it at page width with unlimited height and render it into a throwaway `SKPictureRecorder` until every avatar has loaded and nothing asks for a redraw.
3. `Pdf.SplitStackToPages` reads the rendered tree of the contributors stack and returns page offsets that never cut a row.
4. Wrap the report in a layout with padding (the margins) and a `SkiaScroll`; for each page set the scroll offset and render once into `SKDocument.BeginPage`. The page is opened in points (72 per inch) and the canvas scaled by `72 / 150`, so the PDF is a real A4.
