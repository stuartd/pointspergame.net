using Microsoft.JSInterop;

namespace PointsPerGame.UI.Blazor.Services;

internal static class MatchPagePreference
{
    private const string StorageKey = "points-per-game-match-page";

    public static async ValueTask<bool> ResolveAsync(IJSRuntime jsRuntime, bool? requested)
    {
        try
        {
            switch (requested)
            {
                case true:
                    await jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, "true");
                    break;
                case false:
                    await jsRuntime.InvokeVoidAsync("localStorage.removeItem", StorageKey);
                    break;
                case null:
                    return await jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey) == "true";
            }
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException)
        {
            // The URL still controls the preference when browser storage is unavailable.
        }

        return requested ?? false;
    }
}
