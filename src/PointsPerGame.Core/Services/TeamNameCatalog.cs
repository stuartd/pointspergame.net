using PointsPerGame.Core.Names;

namespace PointsPerGame.Core.Services;

public sealed class TeamNameCatalog
{
    private readonly SemaphoreSlim cacheLock = new(1, 1);
    private volatile IReadOnlyList<string>? cachedNames;

    public async ValueTask<IReadOnlyList<string>> GetNamesAsync(IResultsDataSource dataSource)
    {
        if (cachedNames is { } names)
        {
            return names;
        }

        await cacheLock.WaitAsync();

        try
        {
            if (cachedNames is { } existingNames)
            {
                return existingNames;
            }

            var allNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (var league in LeagueLists.AllLeagues)
            {
                try
                {
                    var teams = await dataSource.GetResultsAsync(league);
                    foreach (var team in teams)
                    {
                        if (!string.IsNullOrWhiteSpace(team.TeamName))
                        {
                            allNames.Add(TeamNameAliases.GetTeamDisplayName(team.TeamName));
                        }
                    }
                }
                catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or OperationCanceledException)
                {
                    // Unavailable leagues should not prevent the other tables from being sized.
                }
            }

            IReadOnlyList<string> result = Array.AsReadOnly(allNames.Order(StringComparer.Ordinal).ToArray());
            if (result.Count > 0)
            {
                cachedNames = result;
            }

            return result;
        }
        finally
        {
            cacheLock.Release();
        }
    }
}
