# RepoReport

A .NET MAUI sample that renders a DrawnUI XAML layout into a multi-page PDF.

It asks the GitHub API for a repository (stars, forks, issues, languages, weekly commits, top contributors with avatars) and exports a XAML report as:

- a multi-page A4 PDF, pages split between table rows (`Pdf.SplitStackToPages`)
- one tall JPEG

The exported report is then shown on screen: the same instance that went into the file, cached as one image and scaled down to fit a narrow screen, so there is one data load and one render. Nothing inside the template is cached, so text and shapes stay vectors inside the PDF.

## Run

Windows:

```
dotnet build -f net10.0-windows10.0.19041.0
```

Mac Catalyst:

```
dotnet build -f net10.0-maccatalyst
```

Then run from the IDE or `dotnet run`. Type `owner/repo`, tap **Load**, then **Export PDF** or **Export JPG**: the report is rendered into the file and appears on screen. On Windows the file opens in the default viewer, on Mac a save dialog appears (`Launcher.OpenAsync` does nothing on Mac Catalyst and `Share` crashes on completion there), on Android and iOS the share sheet appears.

On Windows a **Preview window** button opens the report at A4 width in a second window: edit `ReportView.xaml`, save, and XAML hot reload shows the change next to the app. Not on Mac yet: the second window needs `UIApplicationSupportsMultipleScenes` in `Info.plist`, and with that manifest the app comes up with an empty main window on MAUI 10.0.80 (open question).

Verified on Windows and Mac Catalyst (load, PDF, JPG). Android and iOS build but were not run yet; reports welcome.

No setup and no account needed: the GitHub API answers unauthenticated requests, 60 per hour per address, enough for a demo. Optionally set a `GITHUB_TOKEN` environment variable for 5000 per hour.

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
