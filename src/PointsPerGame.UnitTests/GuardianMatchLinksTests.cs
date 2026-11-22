using NUnit.Framework;
using PointsPerGame.Core.Models;
using PointsPerGame.Core.Web;
using Shouldly;
using System.Net;

namespace PointsPerGame.UnitTests;

public class GuardianMatchLinksTests
{
    [Test]
    public async Task Links_Both_Teams_For_Todays_Upcoming_Live_And_Completed_Matches()
    {
        var clock = new TestClock(new(2026, 11, 22, 14, 0, 0, TimeSpan.Zero));
        var clientFactory = new StubHttpClientFactory(ScoresPage("""
            {"kind":"Fixture","homeTeam":{"name":"Getafe","id":"37459"},"awayTeam":{"name":"Atlético","id":"26305"},"dateTimeISOString":"2026-11-22T20:00:00.000Z","paId":"4561488"},
            {"kind":"Live","homeTeam":{"name":"Hull","id":"26"},"awayTeam":{"name":"Brighton","id":"6795"},"dateTimeISOString":"2026-11-22T14:00:00.000Z","paId":"4579461"},
            {"kind":"Result","homeTeam":{"name":"Juventus","id":"26359"},"awayTeam":{"name":"Venezia","id":"26472"},"dateTimeISOString":"2026-11-22T11:30:00.000Z","paId":"4553299"},
            {"kind":"Result","homeTeam":{"name":"Getafe","id":"37459"},"dateTimeISOString":"2026-11-21T11:30:00.000Z","paId":"111"},
            {"kind":"Fixture","homeTeam":{"name":"Brighton","id":"6795"},"dateTimeISOString":"2026-11-23T20:00:00.000Z","paId":"222"},
            {"kind":"Result","homeTeam":{"name":"Barcelona","id":"25553"},"dateTimeISOString":"2026-11-21T11:30:00.000Z","paId":"333"}
            """));
        var scraper = new GuardianScraper(clientFactory, new GuardianTableParser(), clock);
        TeamResults[] teams =
        [
            Team("Getafe", "getafe", "37459"),
            Team("Atlético Madrid", "atleticomadrid", "26305"),
            Team("Brighton", "brighton", "6795"),
            Team("Juventus", "juventus"),
            Team("Barcelona", "barcelona", "25553"),
            Team("Brighton", "other-brighton", "55555"),
        ];

        var linkedTeams = await scraper.WithTodaysMatchLinksAsync(teams);

        linkedTeams.Select(team => team.TeamUrl).ShouldBe(new[]
        {
            MatchUrl("4561488"), MatchUrl("4561488"), MatchUrl("4579461"), MatchUrl("4553299"),
            teams[4].TeamUrl, teams[5].TeamUrl,
        });
        teams[0].TeamUrl.ShouldBe("https://www.theguardian.com/football/getafe/fixtures");
        clientFactory.RequestUri.ShouldBe(new Uri("https://www.theguardian.com/football/live"));
    }

    [Test]
    public async Task Cached_Matches_Are_Refreshed_At_UK_Midnight_During_British_Summer_Time()
    {
        var clock = new TestClock(new(2026, 7, 15, 22, 59, 0, TimeSpan.Zero));
        var clientFactory = new StubHttpClientFactory(ScoresPage("""
            {"kind":"Result","homeTeam":{"name":"Getafe","id":"37459"},"dateTimeISOString":"2026-07-15T20:00:00.000Z","paId":"111"},
            {"kind":"Fixture","homeTeam":{"name":"Getafe","id":"37459"},"dateTimeISOString":"2026-07-15T23:30:00.000Z","paId":"222"}
            """));
        var scraper = new GuardianScraper(clientFactory, new GuardianTableParser(), clock);
        TeamResults[] teams = [Team("Getafe", "getafe", "37459")];

        (await scraper.WithTodaysMatchLinksAsync(teams))[0].TeamUrl.ShouldBe(MatchUrl("111"));
        (await scraper.WithTodaysMatchLinksAsync(teams))[0].TeamUrl.ShouldBe(MatchUrl("111"));
        clientFactory.RequestCount.ShouldBe(1);

        clock.UtcNow = clock.UtcNow.AddMinutes(2);

        (await scraper.WithTodaysMatchLinksAsync(teams))[0].TeamUrl.ShouldBe(MatchUrl("222"));
        clientFactory.RequestCount.ShouldBe(2);
    }

    [TestCase(HttpStatusCode.ServiceUnavailable, "")]
    [TestCase(HttpStatusCode.OK, "<html><body></body></html>")]
    [TestCase(HttpStatusCode.OK, "<gu-island name='FootballMatchesPageWrapper' props='invalid'></gu-island>")]
    public async Task Keeps_Fixtures_Links_When_Live_Scores_Are_Unavailable(HttpStatusCode status, string html)
    {
        var scraper = new GuardianScraper(new StubHttpClientFactory(html, status));
        TeamResults[] teams = [Team("Getafe", "getafe", "37459")];

        var linkedTeams = await scraper.WithTodaysMatchLinksAsync(teams);

        linkedTeams.ShouldBeSameAs(teams);
    }

    private static TeamResults Team(string name, string slug, string? id = null) => new()
    {
        TeamName = name,
        TeamUrl = $"https://www.theguardian.com/football/{slug}/fixtures",
        TeamCrest = id is null ? string.Empty : $"https://i.guim.co.uk/img/sport/football/crests/{id}.png?width=20",
    };

    private static string MatchUrl(string id) => $"https://www.theguardian.com/football/match-redirect/{id}";

    private static string ScoresPage(string matches)
    {
        var props = "{\"initialDays\":[{\"competitions\":[{\"matches\":[" + matches + "]}]}]}";
        return $"<gu-island name=\"FootballMatchesPageWrapper\" props=\"{WebUtility.HtmlEncode(props)}\"></gu-island>";
    }

    private sealed class TestClock(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class StubHttpClientFactory(string html, HttpStatusCode status = HttpStatusCode.OK) : IHttpClientFactory
    {
        public Uri? RequestUri { get; private set; }
        public int RequestCount { get; private set; }

        public HttpClient CreateClient(string name) => new(new StubHttpMessageHandler(this, html, status));

        private sealed class StubHttpMessageHandler(StubHttpClientFactory factory, string html, HttpStatusCode status) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                factory.RequestUri = request.RequestUri;
                factory.RequestCount++;
                return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(html) });
            }
        }
    }
}
