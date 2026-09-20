using System.Reflection;



public static class GetLastCommitService
{
    public static string? GetLastCommitId()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        var commitId = version?.Split('+', 2).ElementAtOrDefault(1);
        return commitId?[..7] ?? "Unavailable";
    }
}