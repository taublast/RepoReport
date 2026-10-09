using System.Text.Json;
using RepoReport.Models;

namespace RepoReport.Services;

/// <summary>
/// Thin GitHub REST client, unauthenticated: no setup, 60 requests per hour per address.
/// </summary>
public class GitHubService
{
    static readonly HttpClient Http = CreateClient();

    static HttpClient CreateClient()
    {
        var http = new HttpClient { BaseAddress = new Uri("https://api.github.com/") };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("RepoReport-DrawnUI-Sample");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return http;
    }

    public async Task<RepoInfo> GetRepoAsync(string fullName, CancellationToken ct)
    {
        using var doc = await GetJsonAsync($"repos/{fullName}", ct);
        var r = doc.RootElement;
        return new RepoInfo
        {
            FullName = r.GetProperty("full_name").GetString() ?? fullName,
            Description = r.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : "",
            HtmlUrl = r.GetProperty("html_url").GetString() ?? "",
            OwnerAvatarUrl = r.GetProperty("owner").GetProperty("avatar_url").GetString() ?? "",
            Language = r.TryGetProperty("language", out var l) && l.ValueKind == JsonValueKind.String ? l.GetString() : "",
            License = r.TryGetProperty("license", out var lic) && lic.ValueKind == JsonValueKind.Object
                ? lic.GetProperty("spdx_id").GetString() ?? "" : "",
            Stars = r.GetProperty("stargazers_count").GetInt32(),
            Forks = r.GetProperty("forks_count").GetInt32(),
            OpenIssues = r.GetProperty("open_issues_count").GetInt32(),
            Watchers = r.GetProperty("subscribers_count").GetInt32(),
            CreatedAt = r.GetProperty("created_at").GetDateTime(),
            PushedAt = r.GetProperty("pushed_at").GetDateTime(),
        };
    }

    public async Task<List<Contributor>> GetContributorsAsync(string fullName, int max, CancellationToken ct)
    {
        using var doc = await GetJsonAsync($"repos/{fullName}/contributors?per_page={max}", ct);
        var list = new List<Contributor>();
        foreach (var c in doc.RootElement.EnumerateArray())
        {
            if (c.TryGetProperty("type", out var type) && type.GetString() == "Anonymous")
                continue;
            list.Add(new Contributor
            {
                Login = c.GetProperty("login").GetString() ?? "",
                AvatarUrl = (c.GetProperty("avatar_url").GetString() ?? "") + "&s=96",
                Contributions = c.GetProperty("contributions").GetInt32(),
            });
        }
        return list;
    }

    public async Task<List<LanguageShare>> GetLanguagesAsync(string fullName, CancellationToken ct)
    {
        using var doc = await GetJsonAsync($"repos/{fullName}/languages", ct);
        var list = new List<LanguageShare>();
        foreach (var p in doc.RootElement.EnumerateObject())
            list.Add(new LanguageShare { Name = p.Name, Bytes = p.Value.GetInt64() });
        return list;
    }

    /// <summary>
    /// 52 weekly commit totals, oldest first. GitHub computes these lazily and answers 202
    /// until ready, so we retry a few times.
    /// </summary>
    public async Task<List<int>> GetCommitActivityAsync(string fullName, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            using var response = await Http.GetAsync($"repos/{fullName}/stats/commit_activity", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
            {
                await Task.Delay(2000, ct);
                continue;
            }
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                break;
            return doc.RootElement.EnumerateArray().Select(w => w.GetProperty("total").GetInt32()).ToList();
        }
        return new List<int>();
    }

    static async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct)
    {
        using var response = await Http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            var remaining = response.Headers.TryGetValues("X-RateLimit-Remaining", out var v) ? v.FirstOrDefault() : null;
            var hint = remaining == "0" ? " GitHub rate limit reached, try again in an hour." : "";
            throw new HttpRequestException($"{(int)response.StatusCode} {response.ReasonPhrase} for {url}.{hint}");
        }
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }
}
