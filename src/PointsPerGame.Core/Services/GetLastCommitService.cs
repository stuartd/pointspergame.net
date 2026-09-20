using System.Reflection;

namespace PointsPerGame.Core.Services;

public static class GetLastCommitService
{
    public static string? GetLastCommitId()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        return version?.Split('+', 2).ElementAtOrDefault(1);
    }
}