using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace GuideVault.LaunchBoxConnector;

internal sealed class GuideVaultClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = false };
    private static readonly HttpClient SharedHttpClient = new() { Timeout = Timeout.InfiniteTimeSpan };
    private readonly GuideVaultConnectorSettings _settings;

    public GuideVaultClient(GuideVaultConnectorSettings settings)
    {
        _settings = settings;
    }

    public async Task<GuideVaultStatusResult> GetStatusAsync()
    {
        using var response = await GetAsync(ApiUrl("/api/integrations/launchbox/status"), RequestTimeout()).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GuideVaultStatusResult>(JsonOptions).ConfigureAwait(false)
            ?? new GuideVaultStatusResult();
    }

    public async Task<GuideVaultCoverageResult> GetCoverageAsync()
    {
        using var response = await GetAsync(ApiUrl("/api/integrations/launchbox/coverage"), RequestTimeout()).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GuideVaultCoverageResult>(JsonOptions).ConfigureAwait(false)
            ?? new GuideVaultCoverageResult();
    }

    public async Task<GuideVaultBadgeMapResult> GetBadgeMapAsync()
    {
        using var response = await GetAsync(ApiUrl("/api/integrations/launchbox/badges/matched-items"), RequestTimeout()).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GuideVaultBadgeMapResult>(JsonOptions).ConfigureAwait(false)
            ?? new GuideVaultBadgeMapResult();
    }

    public async Task<GuideVaultSyncResult> SyncAsync(LaunchBoxSyncRequest request)
    {
        var url = ApiUrl("/api/integrations/launchbox/sync");
        using var response = await PostAsJsonAsync(url, request, RequestTimeout()).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GuideVaultSyncResult>(JsonOptions).ConfigureAwait(false)
            ?? new GuideVaultSyncResult();
    }

    public async Task<GuideVaultSyncJobStatus> GetCurrentSyncJobAsync()
    {
        using var response = await GetAsync(ApiUrl("/api/integrations/launchbox/sync/jobs/current"), RequestTimeout()).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GuideVaultSyncJobStatus>(JsonOptions).ConfigureAwait(false)
            ?? new GuideVaultSyncJobStatus();
    }

    public async Task<GuideVaultSyncJobStatus> CancelCurrentSyncJobAsync()
    {
        using var response = await PostAsync(ApiUrl("/api/integrations/launchbox/sync/cancel"), null, RequestTimeout()).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GuideVaultSyncJobStatus>(JsonOptions).ConfigureAwait(false)
            ?? new GuideVaultSyncJobStatus();
    }

    public async Task<GuideVaultOpenResult> OpenAsync(GuideVaultOpenRequest request)
    {
        var url = ApiUrl("/api/integrations/launchbox/open");
        using var response = await PostAsJsonAsync(url, request, RequestTimeout()).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GuideVaultOpenResult>(JsonOptions).ConfigureAwait(false)
            ?? new GuideVaultOpenResult();
    }


    public async Task<GuideVaultDocumentRelationshipResult> GetDocumentRelationshipsAsync(string matchType)
    {
        var type = Uri.EscapeDataString(matchType ?? string.Empty);
        using var response = await GetAsync(ApiUrl($"/api/integrations/launchbox/relationships?matchType={type}"), RequestTimeout()).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GuideVaultDocumentRelationshipResult>(JsonOptions).ConfigureAwait(false)
            ?? new GuideVaultDocumentRelationshipResult { MatchType = matchType ?? string.Empty };
    }

    public async Task<GuideVaultGameMatchDetailsResult> GetGameMatchesAsync(string launchBoxGameId, TimeSpan? timeout = null)
    {
        var id = Uri.EscapeDataString(launchBoxGameId ?? string.Empty);
        using var response = await GetAsync(ApiUrl($"/api/integrations/launchbox/game/{id}/matches"), timeout ?? RequestTimeout()).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GuideVaultGameMatchDetailsResult>(JsonOptions).ConfigureAwait(false)
            ?? new GuideVaultGameMatchDetailsResult();
    }

    public async Task<GuideVaultBrowserLoginLinkResult> CreateBrowserLoginLinkAsync(string targetUrl)
    {
        var request = new GuideVaultBrowserLoginLinkRequest
        {
            TargetUrl = targetUrl,
            Username = _settings.GuideVaultUsername,
            Email = _settings.GuideVaultEmail,
            Password = _settings.GuideVaultPassword
        };
        using var response = await PostAsJsonAsync(ApiUrl("/api/integrations/launchbox/browser-login-link"), request, RequestTimeout()).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GuideVaultBrowserLoginLinkResult>(JsonOptions).ConfigureAwait(false)
            ?? new GuideVaultBrowserLoginLinkResult { Success = false, Message = "GuideVault returned an empty browser-login response." };
    }

    public void OpenInBrowser(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private string ApiUrl(string path) => $"{_settings.GuideVaultUrl.TrimEnd('/')}{path}";

    private TimeSpan RequestTimeout() => TimeSpan.FromSeconds(Math.Clamp(_settings.TimeoutSeconds, 10, 600));

    private static async Task<HttpResponseMessage> GetAsync(string url, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        return await SharedHttpClient.GetAsync(url, cts.Token).ConfigureAwait(false);
    }

    private static async Task<HttpResponseMessage> PostAsync(string url, HttpContent? content, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        return await SharedHttpClient.PostAsync(url, content, cts.Token).ConfigureAwait(false);
    }

    private static async Task<HttpResponseMessage> PostAsJsonAsync<T>(string url, T request, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        return await SharedHttpClient.PostAsJsonAsync(url, request, JsonOptions, cts.Token).ConfigureAwait(false);
    }
}
