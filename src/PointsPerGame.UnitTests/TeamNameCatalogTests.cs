using NUnit.Framework;
using PointsPerGame.Core.Models;
using PointsPerGame.Core.Names;
using PointsPerGame.Core.Services;
using Shouldly;

namespace PointsPerGame.UnitTests;

public class TeamNameCatalogTests
{
    [Test]
    public async Task Collects_Distinct_Display_Names_From_Every_League()
    {
        var source = new StubDataSource(league => ValueTask.FromResult<IReadOnlyList<TeamResults>>(
            league == TableSelection.EnglishPremierLeague
                ? [Team("AFC Bournemouth"), Team("Liverpool"), Team("")]
                : [Team("Liverpool"), Team(league.ToString())]));
        var catalog = new TeamNameCatalog();

        var names = await catalog.GetNamesAsync(source);

        source.RequestedLeagues.ShouldBe(LeagueLists.AllLeagues);
        names.ShouldContain("Bournemouth");
        names.ShouldNotContain("AFC Bournemouth");
        names.ShouldNotContain("");
        names.Count(name => name == "Liverpool").ShouldBe(1);
        foreach (var league in LeagueLists.AllLeagues.Where(league => league != TableSelection.EnglishPremierLeague))
        {
            names.ShouldContain(league.ToString());
        }
    }

    [Test]
    public async Task Reuses_The_First_Snapshot_For_Subsequent_Callers()
    {
        var firstSource = new StubDataSource(_ => Results("Original team"));
        var secondSource = new StubDataSource(_ => Results("Changed team"));
        var catalog = new TeamNameCatalog();

        var firstNames = await catalog.GetNamesAsync(firstSource);
        var secondNames = await catalog.GetNamesAsync(secondSource);

        secondNames.ShouldBe(firstNames);
        secondSource.RequestedLeagues.ShouldBeEmpty();
        firstSource.RequestedLeagues.ShouldBe(LeagueLists.AllLeagues);
    }

    [Test]
    public async Task Concurrent_First_Requests_Share_One_Collection()
    {
        var firstLeague = new TaskCompletionSource<IReadOnlyList<TeamResults>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstSource = new StubDataSource(league => league == LeagueLists.AllLeagues[0]
            ? new(firstLeague.Task)
            : Results("Other team"));
        var secondSource = new StubDataSource(_ => Results("Unexpected team"));
        var catalog = new TeamNameCatalog();

        var firstRequest = catalog.GetNamesAsync(firstSource).AsTask();
        var secondRequest = catalog.GetNamesAsync(secondSource).AsTask();
        firstLeague.SetResult([Team("First team")]);
        var results = await Task.WhenAll(firstRequest, secondRequest);

        results[0].ShouldBe(results[1]);
        firstSource.RequestedLeagues.ShouldBe(LeagueLists.AllLeagues);
        secondSource.RequestedLeagues.ShouldBeEmpty();
    }

    [TestCase(typeof(HttpRequestException))]
    [TestCase(typeof(InvalidOperationException))]
    [TestCase(typeof(OperationCanceledException))]
    public async Task Unavailable_Leagues_Do_Not_Prevent_Collecting_Other_Names(Type exceptionType)
    {
        var source = new StubDataSource(league => league == LeagueLists.AllLeagues[0]
            ? ValueTask.FromException<IReadOnlyList<TeamResults>>((Exception)Activator.CreateInstance(exceptionType)!)
            : Results("Available team"));

        var names = await new TeamNameCatalog().GetNamesAsync(source);

        names.ShouldBe(["Available team"]);
        source.RequestedLeagues.ShouldBe(LeagueLists.AllLeagues);
    }

    [Test]
    public async Task An_Empty_First_Attempt_Can_Be_Retried()
    {
        var unavailableSource = new StubDataSource(_ => ValueTask.FromException<IReadOnlyList<TeamResults>>(new HttpRequestException()));
        var availableSource = new StubDataSource(_ => Results("Available team"));
        var catalog = new TeamNameCatalog();

        (await catalog.GetNamesAsync(unavailableSource)).ShouldBeEmpty();
        var names = await catalog.GetNamesAsync(availableSource);

        names.ShouldBe(["Available team"]);
        availableSource.RequestedLeagues.ShouldBe(LeagueLists.AllLeagues);
    }

    private static TeamResults Team(string name) => new() { TeamName = name };

    private static ValueTask<IReadOnlyList<TeamResults>> Results(string name) =>
        ValueTask.FromResult<IReadOnlyList<TeamResults>>([Team(name)]);

    private sealed class StubDataSource(Func<TableSelection, ValueTask<IReadOnlyList<TeamResults>>> getResults) : IResultsDataSource
    {
        public List<TableSelection> RequestedLeagues { get; } = [];

        public ValueTask<IReadOnlyList<TeamResults>> GetResultsAsync(TableSelection tableSelection)
        {
            RequestedLeagues.Add(tableSelection);
            return getResults(tableSelection);
        }
    }
}
