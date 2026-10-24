using PointsPerGame.Core.Mappings;
using PointsPerGame.Core.Models;
using PointsPerGame.Core.Names;
using PointsPerGame.Core.Services;
using System.ComponentModel;
using System.Reflection;
using PointsPerGame.Core.Extensions;

namespace PointsPerGame.UI.Blazor.Services;

public readonly record struct LeagueLink(int Id, string Name, bool IsAvailable);

public sealed record LeagueTableData(
    IReadOnlyList<TeamResults> Rows,
    IReadOnlyList<string> UnavailableTableNames);

public class TablesService(
    ILeagueTableService leagueTableService,
    TableAvailabilityChecker tableAvailabilityChecker)
{
    public async ValueTask<IReadOnlyList<LeagueLink>> GetLeagueLinksAsync()
    {
        var availability = await tableAvailabilityChecker.CheckAllAsync();
        var unavailableTables = availability
            .GetUnavailableTablesFor(TableSelection.AllLeagues)
            .ToHashSet();

        return [
            .. Enum.GetValues<TableSelection>().Select(league => new LeagueLink(
                (int)league,
                GetLeagueDescription(league),
                league.IsMultiLeague() || unavailableTables.Contains(league) == false)),
        ];
    }

    public static string? GetLeagueName(int leagueId) => IsKnownLeague(leagueId) ? GetLeagueDescription((TableSelection)leagueId) : null;

    public static string? GetLeagueSourceUrl(int leagueId)
    {
        if (!IsKnownLeague(leagueId))
        {
            return null;
        }

        var league = (TableSelection)leagueId;

        return league.IsMultiLeague()
            ? null
            : GuardianLeagueMappings.GetUriForLeague(league);
    }

    public async Task<LeagueTableData> GetLeagueTableAsync(int leagueId)
    {
        if (!IsKnownLeague(leagueId))
        {
            return new([], []);
        }

        var league = (TableSelection)leagueId;
        IReadOnlyList<TableSelection> unavailableTables = league.IsMultiLeague()
            ? (await tableAvailabilityChecker.CheckAllAsync()).GetUnavailableTablesFor(league)
            : [];
        var rows = await leagueTableService.GetResultsAsync(league, unavailableTables);

        return new(rows, [.. unavailableTables.Select(GetLeagueDescription)]);
    }

    private static bool IsKnownLeague(int leagueId) => Enum.IsDefined(typeof(TableSelection), leagueId);

    private static string GetLeagueDescription(TableSelection tableSelection)
    {
        var member = typeof(TableSelection).GetMember(tableSelection.ToString()).Single();
        var attr = member.GetCustomAttribute<DescriptionAttribute>();
        return attr?.Description ?? tableSelection.ToString();
    }
}
