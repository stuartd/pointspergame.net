using PointsPerGame.Core.Mappings;
using PointsPerGame.Core.Models;
using PointsPerGame.Core.Names;
using System.Net;
using System.Runtime.Caching;
using System.Text.Json;

namespace PointsPerGame.Core.Web;

public class GuardianScraper(IHttpClientFactory httpClientFactory, GuardianTableParser tableParser, TimeProvider? timeProvider = null)
    : BaseScraper(httpClientFactory) {
    
    private static readonly MemoryCache cache = new(nameof(GuardianScraper));
    private readonly GuardianTableParser tableParser = tableParser ?? throw new ArgumentNullException(nameof(tableParser));
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;
    private readonly GuardianMatchParser matchParser = new();
    private readonly SemaphoreSlim liveScoresLock = new(1, 1);
    private LiveScoresCacheEntry? liveScores;

    public GuardianScraper(IHttpClientFactory httpClientFactory) : this(httpClientFactory, new GuardianTableParser())
    {
    }

    public override async ValueTask<IReadOnlyList<TeamResults>> GetResultsAsync(TableSelection tableSelection)
    {
        if (TryGetCachedResults(tableSelection, out var cachedResults))
        {
            return cachedResults;
        }

        var leagueUri = GuardianLeagueMappings.GetUriForLeague(tableSelection);
        var (html, finalUri) = await GetPageHtmlAsync(leagueUri);

        if (IsGenericTablesPage(finalUri))
        {
            throw new HttpRequestException(
                $"The Guardian league page redirected to the generic tables page: {finalUri}",
                inner: null,
                HttpStatusCode.NotFound);
        }

        var teamData = tableParser.Parse(html);

        CacheResults(tableSelection, teamData);

        return teamData;
    }

    public async Task<IReadOnlyList<TeamResults>> WithTodaysMatchLinksAsync(IReadOnlyList<TeamResults> teams)
    {
        var matches = await GetTodaysMatchesAsync();
        if (matches.Count == 0)
        {
            return teams;
        }

        return [.. teams.Select(team => GuardianMatchParser.Match.FindUrlForTeam(matches, team) is { } matchUrl
            ? team with { TeamUrl = matchUrl }
            : team)];
    }

    private async Task<IReadOnlyList<GuardianMatchParser.Match>> GetTodaysMatchesAsync()
    {
        await liveScoresLock.WaitAsync();
        try
        {
            var now = timeProvider.GetUtcNow();
            var today = GuardianMatchParser.GetUkDate(now);
            if (liveScores is { } cached && cached.Date == today && cached.ExpiresAt > now)
            {
                return cached.Matches;
            }

            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var (html, _) = await GetPageHtmlAsync("https://www.theguardian.com/football/live", timeout.Token);
                now = timeProvider.GetUtcNow();
                today = GuardianMatchParser.GetUkDate(now);
                liveScores = new(today, now.AddMinutes(5), matchParser.Parse(html, today));
            }
            catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException)
            {
                liveScores = new(today, now.AddSeconds(30), []);
            }

            return liveScores.Matches;
        }
        finally
        {
            liveScoresLock.Release();
        }
    }

    private sealed record LiveScoresCacheEntry(DateOnly Date, DateTimeOffset ExpiresAt, IReadOnlyList<GuardianMatchParser.Match> Matches);

    private static bool TryGetCachedResults(TableSelection tableSelection, out IReadOnlyList<TeamResults> results)
    {
        if (cache.Get(GetCacheKey(tableSelection)) is IReadOnlyList<TeamResults> cachedResults)
        {
            results = cachedResults;
            return true;
        }

        results = [];
        return false;
    }

    private static void CacheResults(TableSelection tableSelection, IReadOnlyList<TeamResults> results)
        => cache.Set(GetCacheKey(tableSelection), results, DateTimeOffset.Now.AddMinutes(5));

    private static string GetCacheKey(TableSelection tableSelection) => tableSelection.ToString();

    private static bool IsGenericTablesPage(Uri uri) =>
        uri.Host.Equals("www.theguardian.com", StringComparison.OrdinalIgnoreCase) &&
        uri.AbsolutePath.TrimEnd('/').Equals("/football/tables", StringComparison.OrdinalIgnoreCase);
}
