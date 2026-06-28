using System.Net.Http.Json;
using System.Text.Json;

namespace GuideVaultReaderLauncher;

internal static class GuideVaultLoginBridgeClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    public static async Task<string> BuildAuthenticatedLaunchUrlAsync(
        GuideVaultLauncherSettings settings,
        string requestedUrl,
        string targetUrl,
        string reason)
    {
        if (settings is null)
        {
            LauncherLogger.Log("Login bridge skipped: settings object was null.");
            return requestedUrl;
        }

        if (!settings.UseBrowserLoginBridge || !settings.HasBrowserLoginProfile)
        {
            LauncherLogger.Log(
                "Login bridge skipped. useBridge=" + settings.UseBrowserLoginBridge +
                " hasProfile=" + settings.HasBrowserLoginProfile +
                " usernameSet=" + !string.IsNullOrWhiteSpace(settings.GuideVaultUsername) +
                " emailSet=" + !string.IsNullOrWhiteSpace(settings.GuideVaultEmail) +
                " passwordSet=" + !string.IsNullOrWhiteSpace(settings.GuideVaultPassword));
            return requestedUrl;
        }

        var finalTargetUrl = !string.IsNullOrWhiteSpace(targetUrl)
            ? targetUrl
            : ExtractTargetUrl(requestedUrl) ?? requestedUrl;
        if (string.IsNullOrWhiteSpace(finalTargetUrl)) return requestedUrl;

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 10, 600)) };
            var request = new GuideVaultBrowserLoginLinkRequest
            {
                TargetUrl = finalTargetUrl,
                Username = settings.GuideVaultUsername,
                Email = settings.GuideVaultEmail,
                Password = settings.GuideVaultPassword
            };

            var endpoint = settings.GuideVaultUrl.TrimEnd('/') + "/api/integrations/launchbox/browser-login-link";
            LauncherLogger.Log("Requesting browser login bridge. reason=" + reason + " target=" + finalTargetUrl);
            var response = await http.PostAsJsonAsync(endpoint, request, JsonOptions).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                LauncherLogger.Log("Browser login bridge HTTP " + (int)response.StatusCode + ": " + Truncate(body, 600));
                return requestedUrl;
            }

            var result = await response.Content.ReadFromJsonAsync<GuideVaultBrowserLoginLinkResult>(JsonOptions).ConfigureAwait(false)
                ?? new GuideVaultBrowserLoginLinkResult { Success = false, Message = "GuideVault returned an empty browser-login response." };

            if (result.Success && !string.IsNullOrWhiteSpace(result.Url))
            {
                var bridgeUrl = NormalizeBridgeUrl(settings.GuideVaultUrl, result.Url);
                LauncherLogger.Log("Browser login bridge returned launch URL. expires=" + (result.ExpiresAt?.ToString("O") ?? "unknown"));
                return bridgeUrl;
            }

            LauncherLogger.Log("Browser login bridge did not return a usable URL: " + (result.Message ?? string.Empty));
        }
        catch (Exception ex)
        {
            LauncherLogger.Log("Browser login bridge failed: " + ex.Message);
        }

        return requestedUrl;
    }

    public static string? ExtractTargetUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        if (!uri.AbsolutePath.Contains("/launchbox/sign-in", StringComparison.OrdinalIgnoreCase)) return null;

        var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in query)
        {
            var parts = pair.Split('=', 2);
            if (parts.Length != 2) continue;
            if (!string.Equals(Uri.UnescapeDataString(parts[0]), "target", StringComparison.OrdinalIgnoreCase)) continue;

            var decoded = Uri.UnescapeDataString(parts[1].Replace("+", " "));
            return string.IsNullOrWhiteSpace(decoded) ? null : decoded;
        }

        return null;
    }

    private static string NormalizeBridgeUrl(string baseUrl, string bridgeUrl)
    {
        var trimmed = (bridgeUrl ?? string.Empty).Trim();
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        if (trimmed.StartsWith("/", StringComparison.Ordinal))
            return baseUrl.TrimEnd('/') + trimmed;

        return baseUrl.TrimEnd('/') + "/" + trimmed.TrimStart('/');
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }

    private sealed class GuideVaultBrowserLoginLinkRequest
    {
        public string TargetUrl { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    private sealed class GuideVaultBrowserLoginLinkResult
    {
        public bool Success { get; set; }
        public string Url { get; set; } = string.Empty;
        public DateTimeOffset? ExpiresAt { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
