using System.Collections.Concurrent;
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
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"{(int)response.StatusCode} {response.ReasonPhrase} for commit statistics.{RateLimitHint(response)}");
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                break;
            return doc.RootElement.EnumerateArray().Select(w => w.GetProperty("total").GetInt32()).ToList();
        }
        return new List<int>();
    }

    /// <summary>
    /// Last successful answer per url with its ETag. A conditional request that GitHub answers with
    /// 304 Not Modified does not count against the rate limit, so repeated loads of the same
    /// repository cost nothing once they have been fetched once in this process.
    /// </summary>
    static readonly ConcurrentDictionary<string, (string ETag, string Body)> Cache = new();

    static async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (Cache.TryGetValue(url, out var cached) && !string.IsNullOrEmpty(cached.ETag))
            request.Headers.TryAddWithoutValidation("If-None-Match", cached.ETag);

        using var response = await Http.SendAsync(request, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotModified && cached.Body != null)
            return JsonDocument.Parse(cached.Body);

        if (!response.IsSuccessStatusCode)
        {
            if (cached.Body != null)
                return JsonDocument.Parse(cached.Body); // stale beats nothing, e.g. when the limit is reached

            throw new HttpRequestException($"{(int)response.StatusCode} {response.ReasonPhrase} for {url}.{RateLimitHint(response)}");
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        var etag = response.Headers.ETag?.Tag;
        if (!string.IsNullOrEmpty(etag))
            Cache[url] = (etag, body);
        return JsonDocument.Parse(body);
    }

    /// <summary>
    /// " GitHub rate limit reached, resets at HH:mm." when the answer says the hourly quota is used up, else empty.
    /// </summary>
    static string RateLimitHint(HttpResponseMessage response)
    {
        var remaining = response.Headers.TryGetValues("X-RateLimit-Remaining", out var v) ? v.FirstOrDefault() : null;
        if (remaining != "0")
            return "";
        var reset = response.Headers.TryGetValues("X-RateLimit-Reset", out var r) && long.TryParse(r.FirstOrDefault(), out var epoch)
            ? DateTimeOffset.FromUnixTimeSeconds(epoch).ToLocalTime().ToString("HH:mm")
            : "the next hour";
        return $" GitHub rate limit reached (60 unauthenticated requests per hour), resets at {reset}.";
    }
}
