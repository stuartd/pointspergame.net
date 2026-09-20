using System.Reflection;

namespace PointsPerGame.Core.Services
{
    public static class GetLastCommitService
    {
        public static string LastShortCommitId
        {
            get
            {
                var version = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion;

                var commitId = version?.Split('+', 2).ElementAtOrDefault(1);
                return commitId?[..7] ?? "Unavailable";
            }
        }
    }
}