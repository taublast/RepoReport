namespace RepoReport.Models;

/// <summary>
/// Repository facts from GET /repos/{owner}/{repo}.
/// </summary>
public class RepoInfo
{
    public string FullName { get; set; } = "";
    public string Description { get; set; } = "";
    public string HtmlUrl { get; set; } = "";
    public string OwnerAvatarUrl { get; set; } = "";
    public string Language { get; set; } = "";
    public string License { get; set; } = "";
    public int Stars { get; set; }
    public int Forks { get; set; }
    public int OpenIssues { get; set; }
    public int Watchers { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime PushedAt { get; set; }
}

/// <summary>
/// One row of the contributors table. Share is 0..1 relative to the top contributor.
/// </summary>
public class Contributor
{
    public int Rank { get; set; }
    public string Login { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public int Contributions { get; set; }
    public double Share { get; set; }
    public Color RowBackground { get; set; } = Colors.Transparent;
}

/// <summary>
/// One segment of the languages bar.
/// </summary>
public class LanguageShare
{
    public string Name { get; set; } = "";
    public long Bytes { get; set; }
    public double Percent { get; set; }
    public Color Color { get; set; } = Colors.Gray;
}
