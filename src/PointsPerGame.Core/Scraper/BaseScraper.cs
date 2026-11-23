using PointsPerGame.Core.Models;
using PointsPerGame.Core.Names;

namespace PointsPerGame.Core.Web;

public abstract class BaseScraper(IHttpClientFactory httpClientFactory) : IResultsDataSource
{
    private readonly IHttpClientFactory httpClientFactory = httpClientFactory;

    protected async Task<(string Html, Uri FinalUri)> GetPageHtmlAsync(string url, CancellationToken cancellationToken = default)
    {
        var httpClient = httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new("text/html"));
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PointsPerGame");

        using var response = await httpClient.GetAsync(new Uri(url), cancellationToken);
        response.EnsureSuccessStatusCode();

        var finalUri = response.RequestMessage?.RequestUri ?? new Uri(url);
        return (await response.Content.ReadAsStringAsync(cancellationToken), finalUri);
    }

    public abstract ValueTask<IReadOnlyList<TeamResults>> GetResultsAsync(TableSelection tableSelection);
}
