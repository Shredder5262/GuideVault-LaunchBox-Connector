using System.Drawing;
using System.Windows.Forms;

namespace GuideVault.LaunchBoxConnector;

internal sealed class GuideVaultRelationshipWindow : Form
{
    private static readonly Color Shell = Color.FromArgb(5, 10, 18);
    private static readonly Color Card = Color.FromArgb(10, 23, 38);
    private static readonly Color PanelAlt = Color.FromArgb(16, 39, 64);
    private static readonly Color Border = Color.FromArgb(50, 96, 146);
    private static readonly Color TextMain = Color.FromArgb(231, 238, 250);
    private static readonly Color TextSoft = Color.FromArgb(152, 178, 211);
    private static readonly Color Accent = Color.FromArgb(0, 142, 255);

    private readonly string _matchType;
    private readonly Label _summaryLabel = new();
    private readonly TextBox _searchBox = new();
    private readonly DataGridView _grid = new();
    private readonly HashSet<string> _collapsed = new(StringComparer.OrdinalIgnoreCase);
    private List<GuideVaultDocumentRelationshipItem> _items = new();
    private int _lastServerItemCount;
    private int _lastServerConnectionCount;
    private DateTimeOffset? _lastGeneratedAt;

    private GuideVaultRelationshipWindow(string matchType)
    {
        _matchType = string.IsNullOrWhiteSpace(matchType) ? "Strategy Guide" : matchType.Trim();
        Text = $"GuideVault {_matchType} Links";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(840, 520);
        Size = new Size(1040, 660);
        BackColor = Shell;
        ForeColor = TextMain;
        Font = new Font("Segoe UI", 9.25f);
        Icon = GuideVaultAssets.WindowIcon;
        BuildUi();
        Shown += async (_, _) => await RefreshAsync().ConfigureAwait(true);
    }

    public static void ShowWindow(IWin32Window? owner, string matchType)
    {
        var window = new GuideVaultRelationshipWindow(matchType);
        if (owner is null)
        {
            window.Show();
            window.Activate();
            return;
        }

        window.ShowInTaskbar = false;
        window.ShowDialog(owner);
    }

    private void BuildUi()
    {
        var outer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Shell,
            Padding = new Padding(12)
        };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        Controls.Add(outer);

        var title = new Label
        {
            Dock = DockStyle.Fill,
            Text = _matchType.Equals("Magazine", StringComparison.OrdinalIgnoreCase)
                ? "Magazine links by title"
                : "Strategy guide links by title",
            ForeColor = TextMain,
            Font = new Font("Segoe UI", 14.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        outer.Controls.Add(title, 0, 0);

        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            BackColor = Shell
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        outer.Controls.Add(top, 0, 1);

        top.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Search",
            ForeColor = TextSoft,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        _searchBox.Dock = DockStyle.Fill;
        _searchBox.BackColor = Color.FromArgb(3, 13, 25);
        _searchBox.ForeColor = TextMain;
        _searchBox.BorderStyle = BorderStyle.FixedSingle;
        _searchBox.TextChanged += (_, _) => RenderRows();
        top.Controls.Add(_searchBox, 1, 0);

        var refresh = NewButton("Refresh");
        refresh.Click += async (_, _) => await RefreshAsync().ConfigureAwait(true);
        top.Controls.Add(refresh, 2, 0);

        var expand = NewButton("Expand All");
        expand.Click += (_, _) => { _collapsed.Clear(); RenderRows(); };
        top.Controls.Add(expand, 3, 0);

        var collapse = NewButton("Collapse All");
        collapse.Click += (_, _) =>
        {
            _collapsed.Clear();
            foreach (var item in _items) _collapsed.Add(item.ItemId);
            RenderRows();
        };
        top.Controls.Add(collapse, 4, 0);

        var gridPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Card,
            Padding = new Padding(1)
        };
        outer.Controls.Add(gridPanel, 0, 2);

        _grid.Dock = DockStyle.Fill;
        _grid.BackgroundColor = Card;
        _grid.BorderStyle = BorderStyle.None;
        _grid.GridColor = Border;
        _grid.ForeColor = TextMain;
        _grid.EnableHeadersVisualStyles = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.ReadOnly = true;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = PanelAlt;
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = TextMain;
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.25f, FontStyle.Bold);
        _grid.DefaultCellStyle.BackColor = Card;
        _grid.DefaultCellStyle.ForeColor = TextMain;
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 82, 150);
        _grid.DefaultCellStyle.SelectionForeColor = Color.White;
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(7, 18, 31);
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) ToggleRow(e.RowIndex); };
        _grid.CellContentClick += (_, e) => { if (e.RowIndex >= 0 && e.ColumnIndex == 0) ToggleRow(e.RowIndex); };

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Document", HeaderText = _matchType.Equals("Magazine", StringComparison.OrdinalIgnoreCase) ? "Magazine Title" : "Strategy Guide Title", FillWeight = 230 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Game", HeaderText = "Matched Game", FillWeight = 190 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Platform", HeaderText = "Platform", FillWeight = 120 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", FillWeight = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Confidence", HeaderText = "Confidence", FillWeight = 80 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Reason", HeaderText = "Reason", FillWeight = 170 });
        gridPanel.Controls.Add(_grid);

        _summaryLabel.Dock = DockStyle.Fill;
        _summaryLabel.ForeColor = TextSoft;
        _summaryLabel.BackColor = Shell;
        _summaryLabel.TextAlign = ContentAlignment.MiddleLeft;
        outer.Controls.Add(_summaryLabel, 0, 3);
    }

    private async Task RefreshAsync()
    {
        SetBusy(true);
        try
        {
            var settings = SettingsStore.Load();
            var result = await new GuideVaultClient(settings).GetDocumentRelationshipsAsync(_matchType).ConfigureAwait(true);
            _items = (result.Items ?? new List<GuideVaultDocumentRelationshipItem>())
                .OrderBy(i => i.ItemTitle, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _lastServerItemCount = result.TotalItems > 0 ? result.TotalItems : _items.Count;
            _lastServerConnectionCount = result.TotalConnections > 0 ? result.TotalConnections : _items.Sum(i => i.TotalConnections);
            _lastGeneratedAt = result.GeneratedAt == DateTimeOffset.MinValue ? null : result.GeneratedAt;
            _collapsed.Clear();
            RenderRows();
        }
        catch (Exception ex)
        {
            _items.Clear();
            _grid.Rows.Clear();
            _summaryLabel.Text = "Unable to load links: " + ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void RenderRows()
    {
        var query = (_searchBox.Text ?? string.Empty).Trim();
        var rows = string.IsNullOrWhiteSpace(query)
            ? _items
            : _items.Where(item => Matches(item, query)).ToList();

        var shownConnections = rows.Sum(item => item.TotalConnections);
        var lowConfidence = rows
            .SelectMany(item => item.Connections ?? new List<GuideVaultDocumentRelationshipConnection>())
            .Count(connection => connection.ConfidenceScore > 0 && connection.ConfidenceScore < 80);
        var generated = _lastGeneratedAt is null ? string.Empty : $" Refreshed {_lastGeneratedAt.Value.LocalDateTime:g}.";

        _grid.SuspendLayout();
        _grid.Rows.Clear();

        if (rows.Count == 0)
        {
            var message = string.IsNullOrWhiteSpace(query)
                ? $"No matched {PluralLabel().ToLowerInvariant()} found yet. Run {SyncHint()} first, then reopen this review."
                : $"No matched {PluralLabel().ToLowerInvariant()} match the current search.";
            var emptyIndex = _grid.Rows.Add(message, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
            var empty = _grid.Rows[emptyIndex];
            empty.DefaultCellStyle.BackColor = Card;
            empty.DefaultCellStyle.ForeColor = TextSoft;
            empty.DefaultCellStyle.Font = new Font("Segoe UI", 9.25f, FontStyle.Italic);
            _summaryLabel.Text = string.IsNullOrWhiteSpace(query)
                ? $"No matched {PluralLabel().ToLowerInvariant()} are available yet.{generated}"
                : $"Search returned 0 of {_lastServerItemCount:N0} {SingularLabel().ToLowerInvariant()} title(s). Clear search to show all loaded relationships.";
            _grid.ResumeLayout();
            return;
        }

        foreach (var item in rows)
        {
            var collapsed = _collapsed.Contains(item.ItemId);
            var icon = collapsed ? "▸" : "▾";
            var headerIndex = _grid.Rows.Add($"{icon} {item.ItemTitle}", $"{item.TotalConnections:N0} matched game(s)", item.ItemKind, string.Empty, string.Empty, string.Empty);
            var header = _grid.Rows[headerIndex];
            header.Tag = new RelationshipHeaderTag(item.ItemId);
            header.DefaultCellStyle.BackColor = PanelAlt;
            header.DefaultCellStyle.ForeColor = TextMain;
            header.DefaultCellStyle.Font = new Font("Segoe UI", 9.25f, FontStyle.Bold);

            if (collapsed) continue;

            foreach (var connection in item.Connections.OrderBy(c => c.LaunchBoxPlatform, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.LaunchBoxGameTitle, StringComparer.OrdinalIgnoreCase))
            {
                var game = connection.LaunchBoxGameTitle;
                if (!string.IsNullOrWhiteSpace(connection.LaunchBoxReleaseYear)) game += $" ({connection.LaunchBoxReleaseYear})";
                var confidence = connection.ConfidenceScore > 0 ? $"{connection.ConfidenceScore:0.##}%" : string.Empty;
                var rowIndex = _grid.Rows.Add(string.Empty, game, connection.LaunchBoxPlatform, connection.MatchStatus, confidence, connection.MatchReason);
                _grid.Rows[rowIndex].Tag = connection;
            }
        }

        _summaryLabel.Text = string.IsNullOrWhiteSpace(query)
            ? $"Showing {rows.Count:N0} {SingularLabel().ToLowerInvariant()} title(s), {shownConnections:N0} matched game connection(s), {lowConfidence:N0} low-confidence connection(s).{generated}"
            : $"Search showing {rows.Count:N0}/{_lastServerItemCount:N0} title(s), {shownConnections:N0}/{_lastServerConnectionCount:N0} connection(s), {lowConfidence:N0} low-confidence connection(s).";
        _grid.ResumeLayout();
    }

    private string SingularLabel() => _matchType.Equals("Magazine", StringComparison.OrdinalIgnoreCase) ? "Magazine" : "Strategy guide";

    private string PluralLabel() => _matchType.Equals("Magazine", StringComparison.OrdinalIgnoreCase) ? "Magazines" : "Strategy guides";

    private string SyncHint() => _matchType.Equals("Magazine", StringComparison.OrdinalIgnoreCase) ? "Sync All Magazines" : "Sync All Guides";

    private static bool Matches(GuideVaultDocumentRelationshipItem item, string query)
    {
        if (item.ItemTitle.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
        return item.Connections.Any(c =>
            c.LaunchBoxGameTitle.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            c.LaunchBoxPlatform.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            c.MatchStatus.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            c.MatchReason.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private void ToggleRow(int rowIndex)
    {
        if (_grid.Rows[rowIndex].Tag is not RelationshipHeaderTag header) return;
        if (!_collapsed.Add(header.ItemId)) _collapsed.Remove(header.ItemId);
        RenderRows();
    }

    private void SetBusy(bool busy)
    {
        Cursor = Cursors.Default;
        UseWaitCursor = false;
        _grid.Enabled = !busy;
        _searchBox.Enabled = !busy;
        if (busy) _summaryLabel.Text = "Loading GuideVault relationships...";
    }

    private static Button NewButton(string text) => new()
    {
        Text = text,
        Width = 104,
        Height = 30,
        FlatStyle = FlatStyle.Flat,
        BackColor = Accent,
        ForeColor = Color.White,
        Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
        Margin = new Padding(4, 2, 4, 2)
    };

    private sealed record RelationshipHeaderTag(string ItemId);
}
