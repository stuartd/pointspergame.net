using HtmlAgilityPack;
using PointsPerGame.Core.Models;
using PointsPerGame.Core.Names;
using System.Net;
using System.Text.Json;

namespace PointsPerGame.Core.Web;

internal sealed class GuardianMatchParser
{
    private static readonly TimeZoneInfo guardianTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    public IReadOnlyList<Match> Parse(string html, DateOnly matchDate)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        var scores = document.DocumentNode.SelectSingleNode("//gu-island[@name='FootballMatchesPageWrapper']");
        var props = scores?.GetAttributeValue("props", string.Empty);
        if (string.IsNullOrWhiteSpace(props))
        {
            throw new InvalidOperationException("The Guardian live scores page is missing its match data.");
        }

        var page = JsonSerializer.Deserialize<ScoresPage>(WebUtility.HtmlDecode(props), JsonSerializerOptions.Web);
        return [.. (page?.InitialDays ?? [])
            .SelectMany(day => day.Competitions ?? [])
            .SelectMany(competition => competition.Matches ?? [])
            .Where(match => match.Kind is "Fixture" or "Live" or "Result" &&
                match.DateTimeISOString is { } kickoff && GetUkDate(kickoff) == matchDate &&
                match.PaId is { Length: > 0 } id && id.All(char.IsAsciiDigit))];
    }

    public static DateOnly GetUkDate(DateTimeOffset timestamp) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timestamp, guardianTimeZone).DateTime);

    private sealed record ScoresPage(Day[]? InitialDays);
    private sealed record Day(Competition[]? Competitions);
    private sealed record Competition(Match[]? Matches);

    internal sealed record Match(string? PaId, string? Kind, DateTimeOffset? DateTimeISOString, Team? HomeTeam, Team? AwayTeam)
    {
        // The Guardian's www endpoint resolves the match ID to its current coverage URL.
        public string Url => $"https://www.theguardian.com/football/match-redirect/{PaId}";

        public static string? FindUrlForTeam(IReadOnlyList<Match> matches, TeamResults team)
        {
            const string crestPath = "/img/sport/football/crests/";
            if (Uri.TryCreate(team.TeamCrest, UriKind.Absolute, out var crest) &&
                crest.AbsolutePath.StartsWith(crestPath, StringComparison.Ordinal))
            {
                var id = Path.GetFileNameWithoutExtension(crest.AbsolutePath);
                if (id.Length > 0 && id.All(char.IsAsciiDigit))
                {
                    return matches.FirstOrDefault(match => match.HomeTeam?.Id == id || match.AwayTeam?.Id == id)?.Url;
                }
            }

            var urls = matches.Where(match => MatchesName(match.HomeTeam) || MatchesName(match.AwayTeam))
                .Select(match => match.Url)
                .Distinct()
                .Take(2)
                .ToArray();
            return urls.Length == 1 ? urls[0] : null;

            bool MatchesName(Team? side) => side?.Name is { } name &&
                string.Equals(TeamNameAliases.GetTeamDisplayName(name), team.TeamName, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal sealed record Team(string? Id, string? Name);
}
