using System.Drawing;
using System.Windows.Forms;
using Unbroken.LaunchBox.Plugins;

namespace GuideVault.LaunchBoxConnector;

public sealed class SyncStatusSystemMenuItem : ISystemMenuItemPlugin
{
    public string Caption => "GuideVault: Sync Status";
    public Image? IconImage => null;
    public bool ShowInLaunchBox => true;
    public bool ShowInBigBox => false;
    public bool AllowInBigBoxWhenLocked => false;

    public async void OnSelected()
    {
        try
        {
            var settings = SettingsStore.Load();
            var client = new GuideVaultClient(settings);
            var status = await client.GetStatusAsync().ConfigureAwait(true);
            var job = await client.GetCurrentSyncJobAsync().ConfigureAwait(true);

            MessageBox.Show(
                BuildMessage(status, job),
                "GuideVault LaunchBox Connector",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not read GuideVault sync status.\n\nCheck that GuideVault is running and that GuideVaultUrl is correct in:\n{SettingsStore.SettingsPath}\n\n{ex.Message}",
                "GuideVault LaunchBox Connector",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static string BuildMessage(GuideVaultStatusResult status, GuideVaultSyncJobStatus job)
    {
        var percent = job.TotalGames > 0
            ? Math.Round((double)job.ProcessedGames / job.TotalGames * 100, 1)
            : 0;

        var lines = new List<string>
        {
            "GuideVault LaunchBox sync status",
            string.Empty,
            $"GuideVault version: {status.Version}",
            $"Synced games: {status.GameCount}",
            $"Active matches: {status.MatchCount}",
            string.Empty,
            $"Job: {(string.IsNullOrWhiteSpace(job.JobId) ? "None" : job.JobId)}",
            $"Status: {job.Status}",
            $"Progress: {job.ProcessedGames} / {job.TotalGames} ({percent}%)",
            $"Imported: {job.ImportedGames}",
            $"Matched games: {job.MatchedGames}",
            $"Manual matches: {job.ManualMatchedGames}",
            $"Strategy guide matches: {job.StrategyGuideMatchedGames}",
            $"Ambiguous: {job.AmbiguousMatches}",
            $"Missing: {job.MissingGames}"
        };

        if (!string.IsNullOrWhiteSpace(job.Message))
        {
            lines.Add(string.Empty);
            lines.Add(job.Message);
        }

        if (job.Errors is { Count: > 0 })
        {
            lines.Add(string.Empty);
            lines.Add("Errors:");
            lines.AddRange(job.Errors.Take(5).Select(error => $"- {error}"));
        }

        return string.Join(Environment.NewLine, lines);
    }
}
