using PointsPerGame.Core.Models;
using PointsPerGame.Core.Names;
using PointsPerGame.Core.Extensions;

namespace PointsPerGame.Core.Services;

public interface ILeagueTableService
{
	ValueTask<IReadOnlyList<TeamResults>> GetResultsAsync(
		TableSelection tableSelection,
		IReadOnlyCollection<TableSelection>? excludedTables = null);
}

public sealed class LeagueTableService(IResultsDataSource dataSource) : ILeagueTableService
{
	private const int pointsForWin = 3;

	public async ValueTask<IReadOnlyList<TeamResults>> GetResultsAsync(
		TableSelection tableSelection,
		IReadOnlyCollection<TableSelection>? excludedTables = null)
	{
		var leagues = tableSelection.GetConcreteTables()
			.Where(table => excludedTables?.Contains(table) != true);

		var results = new List<TeamResults>();

		foreach (var sourceLeague in leagues)
		{
			var leagueResults = await dataSource.GetResultsAsync(sourceLeague);
			results.AddRange(leagueResults.Select(GetTeamResultsWithDisplayName).Select(RecordPointsDeduction));
		}

		return [.. results.SortTeams(pointsForWin)];
	}

	private static TeamResults GetTeamResultsWithDisplayName(TeamResults team) =>
		team with { 
			TeamName = TeamNameAliases.GetTeamDisplayName(team.TeamName)
		};

	private static TeamResults RecordPointsDeduction(TeamResults team)
	{
		var pointsBeforeDeduction = (team.Won * pointsForWin) + team.Drawn;
		var pointsDeducted = pointsBeforeDeduction - team.Points;

		if (pointsDeducted < 0)
		{
			throw new InvalidOperationException(
				$"{team.TeamName} has {team.Points} points, but its wins and draws account for only {pointsBeforeDeduction}.");
		}

		return team with { 
			PointsDeducted = pointsDeducted
		};
	}
}
