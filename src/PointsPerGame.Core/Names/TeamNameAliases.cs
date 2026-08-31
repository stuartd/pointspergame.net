namespace PointsPerGame.Core.Names;

public static class TeamNameAliases
{
	private static readonly IReadOnlyDictionary<string, string> displayNames =
		new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["AFC Bournemouth"] = "Bournemouth",
		};

	public static string GetTeamDisplayName(string teamName) =>
		displayNames.TryGetValue(teamName, out var shortName) ? shortName : teamName;
}
