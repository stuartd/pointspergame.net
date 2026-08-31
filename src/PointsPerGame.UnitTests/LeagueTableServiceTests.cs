using NUnit.Framework;
using PointsPerGame.Core.Models;
using PointsPerGame.Core.Names;
using PointsPerGame.Core.Services;
using Shouldly;
using System.Net;

namespace PointsPerGame.UnitTests;

public class LeagueTableServiceTests
{
	[Test]
	public async Task GetResultsAsync_For_Single_League_Requests_That_League()
	{
		var dataSource = new StubDataSource();
		dataSource.SetResults(TableSelection.EnglishChampionship, Team("Championship", points: 30, played: 10));
		var service = new LeagueTableService(dataSource);

		var results = await service.GetResultsAsync(TableSelection.EnglishChampionship);

		dataSource.RequestedLeagues.ShouldBe([TableSelection.EnglishChampionship]);
		results.Count(r => r.TeamName == "Championship").ShouldBe(1);
	}

	[Test]
	public async Task GetResultsAsync_For_All_Requests_Every_Source_League()
	{
		var dataSource = new StubDataSource();
		foreach (var league in LeagueLists.AllLeagues)
		{
			dataSource.SetResults(league, Team(league.ToString(), points: 30, played: 10));
		}
		var service = new LeagueTableService(dataSource);

		var results = await service.GetResultsAsync(TableSelection.AllLeagues);

		dataSource.RequestedLeagues.ShouldBe(LeagueLists.AllLeagues);
		results.Count.ShouldBe(LeagueLists.AllLeagues.Length);
	}

	[Test]
	public async Task GetResultsAsync_For_All_Fails_When_A_Source_League_Is_Not_Found()
	{
		var service = new LeagueTableService(new NotFoundDataSource());

		var exception = await Should.ThrowAsync<HttpRequestException>(
			async () => await service.GetResultsAsync(TableSelection.AllLeagues));

		exception.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Test]
	public async Task GetResultsAsync_For_AllTopDivisions_Requests_Top_Division_Leagues()
	{
		var dataSource = new StubDataSource();
		foreach (var league in LeagueLists.AllTopDivisions)
		{
			dataSource.SetResults(league, Team(league.ToString(), points: 30, played: 10));
		}
		var service = new LeagueTableService(dataSource);

		var results = await service.GetResultsAsync(TableSelection.AllTopDivisions);

		dataSource.RequestedLeagues.ShouldBe(LeagueLists.AllTopDivisions);
		results.Count.ShouldBe(LeagueLists.AllTopDivisions.Length);
	}

	[Test]
	public async Task GetResultsAsync_For_A_Composite_Table_Excludes_Unavailable_Source_Leagues()
	{
		var dataSource = new StubDataSource();
		foreach (var league in LeagueLists.AllTopDivisions)
		{
			dataSource.SetResults(league, Team(league.ToString(), points: 30, played: 10));
		}
		var service = new LeagueTableService(dataSource);

		var results = await service.GetResultsAsync(
			TableSelection.AllTopDivisions,
			[TableSelection.Ligue1, TableSelection.SerieA]);

		dataSource.RequestedLeagues.ShouldBe([
			.. LeagueLists.AllTopDivisions.Where(league =>
				league is not TableSelection.Ligue1 and not TableSelection.SerieA),
		]);
		results.Select(result => result.TeamName).ShouldNotContain(TableSelection.Ligue1.ToString());
		results.Select(result => result.TeamName).ShouldNotContain(TableSelection.SerieA.ToString());
	}

	[Test]
	public async Task GetResultsAsync_Sorts_Combined_Results()
	{
		var dataSource = new StubDataSource();
		dataSource.SetResults(TableSelection.EnglishPremierLeague,
			Team("lower", points: 25, played: 10),
			Team("higher", points: 26, played: 10));
		var service = new LeagueTableService(dataSource);

		var results = await service.GetResultsAsync(TableSelection.EnglishPremierLeague);

		results.Select(r => r.TeamName).ShouldBe(["higher", "lower"]);
	}

	[Test]
	public async Task GetResultsAsync_Uses_Short_Team_Name_Alias()
	{
		var dataSource = new StubDataSource();
		dataSource.SetResults(
			TableSelection.EnglishPremierLeague,
			Team("AFC Bournemouth", points: 3, played: 1));
		var service = new LeagueTableService(dataSource);

		var results = await service.GetResultsAsync(TableSelection.EnglishPremierLeague);

		results.Single().TeamName.ShouldBe("Bournemouth");
	}

	[Test]
	public async Task GetResultsAsync_Records_All_Point_Deductions_And_Sorts_By_Points_Before_Deduction()
	{
		var dataSource = new StubDataSource();
		dataSource.SetResults(TableSelection.EnglishChampionship,
			Team("Southampton", points: -4, played: 0, won: 0, drawn: 0),
			Team("Five point deduction", points: 2, played: 3, won: 2, drawn: 1),
			Team("No deduction", points: 6, played: 3, won: 2, drawn: 0));
		var service = new LeagueTableService(dataSource);

		var results = await service.GetResultsAsync(TableSelection.EnglishChampionship);

		results.Select(r => r.TeamName).ShouldBe(["Five point deduction", "No deduction", "Southampton"]);
		results.Where(r => r.PointsDeducted > 0)
			.Select(r => (r.TeamName, r.PointsDeducted))
			.ShouldBe([
				("Five point deduction", 5),
				("Southampton", 4),
			]);
		results.Single(r => r.TeamName == "Five point deduction").PointsBeforeDeduction.ShouldBe(7);
		results.Single(r => r.TeamName == "Five point deduction").PointsPerGame.ShouldBe(7d / 3);
		results.Single(r => r.TeamName == "No deduction").PointsDeducted.ShouldBe(0);
		results.Single(r => r.TeamName == "Southampton").PointsPerGame.ShouldBe(0);
	}

	private static TeamResults Team(
		string name,
		int points,
		int played,
		int? won = null,
		int? drawn = null) => new()
	{
		TeamName = name,
		TeamUrl = string.Empty,
		TeamCrest = string.Empty,
		Played = played,
		Won = won ?? points / 3,
		Drawn = drawn ?? points % 3,
		Points = points,
	};

	private sealed class StubDataSource : IResultsDataSource
	{
		private readonly Dictionary<TableSelection, IReadOnlyList<TeamResults>> resultsByLeague = [];

		public List<TableSelection> RequestedLeagues { get; } = [];

		public void SetResults(TableSelection tableSelection, params TeamResults[] results) => resultsByLeague[tableSelection] = [.. results];

		public ValueTask<IReadOnlyList<TeamResults>> GetResultsAsync(TableSelection tableSelection)
		{
			RequestedLeagues.Add(tableSelection);

			if (resultsByLeague.TryGetValue(tableSelection, out var leagueResults))
			{
				return ValueTask.FromResult<IReadOnlyList<TeamResults>>([.. leagueResults]);
			}

			return ValueTask.FromResult<IReadOnlyList<TeamResults>>([]);
		}
    }

	private sealed class NotFoundDataSource : IResultsDataSource
	{
		public ValueTask<IReadOnlyList<TeamResults>> GetResultsAsync(TableSelection tableSelection) =>
			ValueTask.FromException<IReadOnlyList<TeamResults>>(
				new HttpRequestException("Not found.", inner: null, HttpStatusCode.NotFound));
	}
}
