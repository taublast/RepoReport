using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using RepoReport.Models;
using RepoReport.Services;

namespace RepoReport.ViewModels;

/// <summary>
/// Everything the report binds to. Loaded asynchronously from the GitHub API.
/// </summary>
public class ReportViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    readonly GitHubService _github = new();

    public const int MaxContributors = 100;

    string _repoName = "mono/SkiaSharp";
    public string RepoName
    {
        get => _repoName;
        set { if (_repoName != value) { _repoName = value; OnPropertyChanged(); } }
    }

    RepoInfo _repo = new();
    public RepoInfo Repo
    {
        get => _repo;
        private set { _repo = value; OnPropertyChanged(); }
    }

    public ObservableCollection<Contributor> Contributors { get; } = new();

    List<LanguageShare> _languages = new();
    public List<LanguageShare> Languages
    {
        get => _languages;
        private set { _languages = value; OnPropertyChanged(); }
    }

    List<int> _weeklyCommits = new();
    public List<int> WeeklyCommits
    {
        get => _weeklyCommits;
        private set { _weeklyCommits = value; OnPropertyChanged(); OnPropertyChanged(nameof(CommitsSummary)); }
    }

    bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set { _isLoading = value; OnPropertyChanged(); }
    }

    bool _hasData;
    public bool HasData
    {
        get => _hasData;
        private set { _hasData = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// The repository the current data belongs to; null until a load succeeded.
    /// </summary>
    public string LoadedRepoName { get; private set; }

    /// <summary>
    /// True when the typed repository has not been loaded yet (nothing loaded, or the name changed since).
    /// </summary>
    public bool NeedsLoad => !HasData || !string.Equals(LoadedRepoName, RepoName?.Trim().Trim('/'), StringComparison.OrdinalIgnoreCase);

    string _status = "Enter owner/repo and tap Load";
    public string Status
    {
        get => _status;
        private set { _status = value; OnPropertyChanged(); }
    }

    // Formatted strings the report binds to

    public string Title => Repo.FullName;
    public string Description => string.IsNullOrEmpty(Repo.Description) ? "No description" : Repo.Description;
    public string StarsText => Format(Repo.Stars);
    public string ForksText => Format(Repo.Forks);
    public string IssuesText => Format(Repo.OpenIssues);
    public string WatchersText => Format(Repo.Watchers);
    public string OwnerAvatarUrl => Repo.OwnerAvatarUrl;
    public string Generated => $"Generated {DateTime.Now:d MMMM yyyy} from {Repo.HtmlUrl}";
    public string Facts =>
        $"{(string.IsNullOrEmpty(Repo.Language) ? "" : Repo.Language + "  ·  ")}" +
        $"{(string.IsNullOrEmpty(Repo.License) || Repo.License == "NOASSERTION" ? "" : Repo.License + "  ·  ")}" +
        $"Created {Repo.CreatedAt:MMMM yyyy}  ·  Last push {Repo.PushedAt:d MMMM yyyy}";
    public string ContributorsTitle => $"Top {Contributors.Count} contributors";
    public string CommitsSummary => WeeklyCommits.Count == 0
        ? "No commit statistics available yet"
        : $"{WeeklyCommits.Sum()} commits in the last {WeeklyCommits.Count} weeks, best week {WeeklyCommits.Max()}";

    /// <summary>
    /// Images the report will load asynchronously: the owner avatar plus one per contributor.
    /// The exporter waits until this many images reported Success or Error.
    /// </summary>
    public int ExpectedImages => (string.IsNullOrEmpty(Repo.OwnerAvatarUrl) ? 0 : 1) + Contributors.Count;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var name = RepoName?.Trim().Trim('/');
        if (string.IsNullOrEmpty(name) || !name.Contains('/'))
        {
            Status = "Repository must be owner/repo";
            return;
        }

        IsLoading = true;
        HasData = false;
        Status = $"Loading {name}...";
        try
        {
            var repoTask = _github.GetRepoAsync(name, ct);
            var contributorsTask = _github.GetContributorsAsync(name, MaxContributors, ct);
            var languagesTask = _github.GetLanguagesAsync(name, ct);
            var activityTask = _github.GetCommitActivityAsync(name, ct);

            Repo = await repoTask;

            var contributors = await contributorsTask;
            var top = contributors.Count > 0 ? contributors[0].Contributions : 1;
            Contributors.Clear();
            var rank = 0;
            foreach (var c in contributors)
            {
                c.Rank = ++rank;
                c.Share = Math.Max(0.02, (double)c.Contributions / top);
                c.RowBackground = rank % 2 == 0 ? Color.Parse("#F4F6FA") : Colors.Transparent;
                Contributors.Add(c);
            }

            var languages = await languagesTask;
            var total = Math.Max(1, languages.Sum(l => l.Bytes));
            foreach (var l in languages)
            {
                l.Percent = 100.0 * l.Bytes / total;
                l.Color = LanguageColors.For(l.Name);
            }
            Languages = languages.OrderByDescending(l => l.Bytes).ToList();

            WeeklyCommits = await activityTask;

            LoadedRepoName = name;
            HasData = true;
            Status = $"{Repo.FullName}: {Contributors.Count} contributors, {Languages.Count} languages";
            RaiseAllFormatted();
        }
        catch (Exception e)
        {
            Status = e.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    void RaiseAllFormatted()
    {
        foreach (var name in new[]
                 {
                     nameof(Title), nameof(Description), nameof(StarsText), nameof(ForksText), nameof(IssuesText),
                     nameof(WatchersText), nameof(OwnerAvatarUrl), nameof(Generated), nameof(Facts),
                     nameof(ContributorsTitle), nameof(CommitsSummary), nameof(ExpectedImages)
                 })
            OnPropertyChanged(name);
    }

    static string Format(int n) => n switch
    {
        >= 1_000_000 => $"{n / 1_000_000.0:0.#}M",
        >= 10_000 => $"{n / 1000.0:0}k",
        >= 1_000 => $"{n / 1000.0:0.#}k",
        _ => n.ToString()
    };

    void OnPropertyChanged([CallerMemberName] string name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// GitHub-like language colors for the common ones, a stable hash color for the rest.
/// </summary>
public static class LanguageColors
{
    static readonly Dictionary<string, string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["C#"] = "#178600", ["JavaScript"] = "#F1E05A", ["TypeScript"] = "#3178C6", ["Python"] = "#3572A5",
        ["Rust"] = "#DEA584", ["Java"] = "#B07219", ["Kotlin"] = "#A97BFF", ["Swift"] = "#F05138",
        ["HTML"] = "#E34C26", ["CSS"] = "#663399", ["SCSS"] = "#C6538C", ["Shell"] = "#89E051",
        ["PowerShell"] = "#012456", ["C"] = "#555555", ["C++"] = "#F34B7D", ["Go"] = "#00ADD8",
        ["Objective-C"] = "#438EFF", ["Dart"] = "#00B4AB", ["Ruby"] = "#701516", ["PHP"] = "#4F5D95",
        ["Batchfile"] = "#C1F12E", ["Dockerfile"] = "#384D54", ["GLSL"] = "#5686A5", ["Makefile"] = "#427819",
    };

    public static Color For(string name)
    {
        if (Known.TryGetValue(name, out var hex))
            return Color.Parse(hex);
        var h = (uint)name.GetHashCode();
        return Color.FromHsla((h % 360) / 360.0, 0.55, 0.5);
    }
}
