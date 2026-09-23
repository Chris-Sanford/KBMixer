using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KBMixer
{
    /// <summary>
    /// Checks the latest GitHub release and prompts the user to download it when a newer version exists.
    /// </summary>
    public static class UpdateChecker
    {
        private const string LatestReleaseUrl = "https://api.github.com/repos/Chris-Sanford/KBMixer/releases/latest";
        private const string ReleasesPageUrl = "https://github.com/Chris-Sanford/KBMixer/releases/latest";

        private static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("KBMixer", GetCurrentVersion().ToString(3)));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            return client;
        }

        /// <summary>Returns the running Major.Minor.Build version from the assembly name.</summary>
        private static Version GetCurrentVersion()
        {
            var v = typeof(UpdateChecker).Assembly.GetName().Version ?? new Version(0, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
        }

        /// <summary>Parses "v2.0.0-r123" -> 2.0.0. Returns null if the tag is not a version.</summary>
        internal static Version? ParseTagVersion(string? tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName)) return null;
            var s = tagName.Trim().TrimStart('v', 'V');
            var dash = s.IndexOf('-');
            if (dash >= 0) s = s[..dash];
            if (!Version.TryParse(s, out var parsed)) return null;
            return new Version(parsed.Major, Math.Max(parsed.Minor, 0), Math.Max(parsed.Build, 0));
        }

        public static async Task CheckAndPromptAsync(XamlRoot xamlRoot, bool silentIfUpToDate = true)
        {
            try
            {
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KBMIXER_NO_UPDATE_CHECK"))) return;

                var current = GetCurrentVersion();
                if (current == new Version(0, 0, 0)) return;

                using var response = await Http.GetAsync(LatestReleaseUrl);
                response.EnsureSuccessStatusCode();
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var root = doc.RootElement;

                var tag = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() : null;
                var htmlUrl = root.TryGetProperty("html_url", out var htmlEl) ? htmlEl.GetString() : null;
                string? downloadUrl = null;
                if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                {
                    string? fallbackExeUrl = null;
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var name = asset.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                        var url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                        if (url == null)
                            continue;
                        if (string.Equals(name, "KBMixer-self-contained.zip", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = url;
                            break;
                        }
                        if (fallbackExeUrl == null && url.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            fallbackExeUrl = url;
                    }
                    downloadUrl ??= fallbackExeUrl;
                }

                var latest = ParseTagVersion(tag) ?? throw new FormatException($"Unrecognized release tag '{tag}'.");

                if (latest > current)
                {
                    var dialog = new ContentDialog
                    {
                        XamlRoot = xamlRoot,
                        Title = "Update available",
                        Content = $"KBMixer {latest} is available (you have {current}).",
                        PrimaryButtonText = "Download",
                        CloseButtonText = "Later",
                        DefaultButton = ContentDialogButton.Primary,
                    };
                    if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        var target = downloadUrl ?? htmlUrl ?? ReleasesPageUrl;
                        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
                    }
                }
                else if (!silentIfUpToDate)
                {
                    await ShowMessageAsync(xamlRoot, "You're up to date", $"KBMixer {current} is the latest version.");
                }
            }
            catch (Exception ex)
            {
                if (!silentIfUpToDate)
                {
                    try { await ShowMessageAsync(xamlRoot, "Update check failed", $"Could not check for updates: {ex.Message}"); }
                    catch { /* nothing more we can do */ }
                }
            }
        }

        private static async Task ShowMessageAsync(XamlRoot xamlRoot, string title, string message)
        {
            var dialog = new ContentDialog { XamlRoot = xamlRoot, Title = title, Content = message, CloseButtonText = "OK" };
            await dialog.ShowAsync();
        }
    }
}
