using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;

namespace GuideVault.LaunchBoxConnector;

internal enum GuideVaultWindowStartupAction
{
    None,
    OpenManual,
    OpenStrategyGuide,
    OpenMagazine,
    SyncLibrary,
    SyncStatus,
    TestConnection,
    CancelSync,
    Settings
}

internal sealed class GuideVaultConnectorWindow : Form
{
    private static readonly Color Shell = Color.FromArgb(5, 10, 18);
    private static readonly Color TitleShell = Color.FromArgb(3, 8, 15);
    private static readonly Color Panel = Color.FromArgb(11, 26, 43);
    private static readonly Color PanelAlt = Color.FromArgb(16, 39, 64);
    private static readonly Color Card = Color.FromArgb(10, 23, 38);
    private static readonly Color Border = Color.FromArgb(50, 96, 146);
    private static readonly Color BorderSoft = Color.FromArgb(28, 62, 98);
    private static readonly Color TextMain = Color.FromArgb(231, 238, 250);
    private static readonly Color TextSoft = Color.FromArgb(152, 178, 211);
    private static readonly Color Accent = Color.FromArgb(0, 142, 255);
    private static readonly Color Accent2 = Color.FromArgb(15, 95, 224);
    private static readonly Color AccentGlow = Color.FromArgb(71, 207, 255);
    private static readonly Color AccentStripLeft = Color.FromArgb(3, 32, 60);
    private static readonly Color AccentStripRight = Color.FromArgb(0, 128, 238);

    private const int WmNclButtonDown = 0xA1;
    private const int WmNcHitTest = 0x84;
    private const int HtCaption = 0x2;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    private static GuideVaultConnectorWindow? _current;

    private IGame? _selectedGame;
    private readonly Label _selectedGameLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Label _jobLabel = new();
    private readonly ProgressBar _progressBar = new();
    private readonly RichTextBox _log = new();
    private readonly RichTextBox _syncOutput = new();
    private readonly TextBox _urlText = NewTextBox();
    private readonly ComboBox _openModeCombo = NewComboBox();
    private readonly CheckBox _readerMaximizedCheck = NewCheckBox("Open reader window maximized");
    private readonly CheckBox _readerFullscreenCheck = NewCheckBox("Open reader in borderless fullscreen by default");
    private readonly CheckBox _browserBridgeCheck = NewCheckBox("Use local-login bridge for WebView2");
    private readonly TextBox _usernameText = NewTextBox();
    private readonly TextBox _emailText = NewTextBox();
    private readonly TextBox _passwordText = NewTextBox();
    private readonly CheckBox _alternateNamesCheck = NewCheckBox("Use LaunchBox alternate titles for matching");
    private readonly CheckBox _customFieldsCheck = NewCheckBox("Send user-defined LaunchBox custom fields");
    private readonly NumericUpDown _timeoutBox = NewNumberBox(10, 600);
    private readonly Button _manualButton = NewButton("Open Manual", 180);
    private readonly Button _strategyButton = NewButton("Open Strategy Guide", 180);
    private readonly Button _magazineButton = NewButton("Open Magazine", 180);
    private readonly Button _syncButton = NewButton("Sync Library", 160);
    private readonly Button _statusButton = NewButton("Refresh Status", 160);
    private readonly Button _cancelButton = NewButton("Cancel Sync", 160);
    private readonly Button _testButton = NewButton("Test Connection", 160);
    private readonly DataGridView _manualPlatformGrid = NewGrid();
    private readonly Label _manualPlatformSummaryLabel = new();
    private readonly Button _refreshManualPlatformsButton = NewButton("Refresh Platforms", 170);
    private readonly Button _saveManualPlatformsButton = NewButton("Save Selection", 150);
    private readonly Button _syncManualPlatformsButton = NewButton("Sync Selected", 150);
    private readonly Button _syncAllPlatformsButton = NewButton("Sync All", 120);
    private readonly DataGridView _strategyGuideGrid = NewGrid();
    private readonly Label _strategyGuideSummaryLabel = new();
    private readonly Button _refreshStrategyGuidesButton = NewButton("Refresh Coverage", 170);
    private readonly Button _syncSelectedStrategyGuidesButton = NewButton("Sync Selected Guides", 190);
    private readonly Button _syncAllStrategyGuidesButton = NewButton("Sync All Guides", 150);
    private readonly Button _openStrategyGuideReviewButton = NewButton("View Matched Guides", 190);
    private readonly DataGridView _magazineGrid = NewGrid();
    private readonly Label _magazineSummaryLabel = new();
    private readonly Button _refreshMagazinesButton = NewButton("Refresh Coverage", 170);
    private readonly Button _syncSelectedMagazinesButton = NewButton("Sync Selected Magazines", 210);
    private readonly Button _syncAllMagazinesButton = NewButton("Sync All Magazines", 170);
    private readonly Button _openMagazineReviewButton = NewButton("View Matched Magazines", 220);
    private TabControl? _tabs;
    private CancellationTokenSource? _syncPollCts;
    private string _lastSyncOutputLine = string.Empty;
    private bool _isBusy;
    private bool _syncJobRunning;

    private GuideVaultConnectorWindow(IGame? selectedGame)
    {
        _selectedGame = selectedGame;
        Text = "GuideVault LaunchBox Connector";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1060, 680);
        Size = new Size(1160, 740);
        BackColor = Shell;
        ForeColor = TextMain;
        Font = new Font("Segoe UI", 9.25f);
        FormBorderStyle = FormBorderStyle.None;
        Padding = new Padding(1);
        var windowIcon = GuideVaultAssets.WindowIcon;
        if (windowIcon is not null) Icon = windowIcon;

        BuildUi();
        LoadSettingsIntoControls();
        UpdateSelectedGame(selectedGame);
        AppendLog($"GuideVault LaunchBox Connector {ConnectorConstants.PluginVersion} ready.");
    }

    public static void ShowWindow(IGame? selectedGame = null, GuideVaultWindowStartupAction action = GuideVaultWindowStartupAction.None)
    {
        if (_current is null || _current.IsDisposed)
        {
            _current = new GuideVaultConnectorWindow(selectedGame);
            _current.FormClosed += (_, _) => _current = null;
            _current.Show();
        }
        else
        {
            if (selectedGame is not null) _current.UpdateSelectedGame(selectedGame);
            if (_current.WindowState == FormWindowState.Minimized) _current.WindowState = FormWindowState.Normal;
            _current.Show();
            _current.Activate();
        }

        if (action != GuideVaultWindowStartupAction.None)
            _current.BeginInvoke(new Action(() => _current.RunStartupAction(action)));
    }

    public static void AppendExternalLog(string message)
    {
        if (_current is null || _current.IsDisposed) return;
        _current.AppendLog(message);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Border, 1);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (m.Msg != WmNcHitTest || WindowState == FormWindowState.Maximized) return;
        if ((int)m.Result != 1) return;

        const int grip = 8;
        var point = PointToClient(Cursor.Position);
        var left = point.X <= grip;
        var right = point.X >= ClientSize.Width - grip;
        var top = point.Y <= grip;
        var bottom = point.Y >= ClientSize.Height - grip;

        if (left && top) m.Result = HtTopLeft;
        else if (right && top) m.Result = HtTopRight;
        else if (left && bottom) m.Result = HtBottomLeft;
        else if (right && bottom) m.Result = HtBottomRight;
        else if (left) m.Result = HtLeft;
        else if (right) m.Result = HtRight;
        else if (top) m.Result = HtTop;
        else if (bottom) m.Result = HtBottom;
    }


    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _syncPollCts?.Cancel();
        _syncPollCts?.Dispose();
        base.OnFormClosed(e);
    }

    private void BuildUi()
    {
        var outer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Shell,
            Padding = new Padding(0)
        };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(outer);

        outer.Controls.Add(BuildTitleBar(), 0, 0);
        outer.Controls.Add(BuildHeader(), 0, 1);

        var tabs = new GuideVaultTabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.25f, FontStyle.Bold),
            BackColor = Shell,
            ForeColor = TextMain,
            DrawMode = TabDrawMode.OwnerDrawFixed,
            ItemSize = new Size(146, 34),
            SizeMode = TabSizeMode.Fixed,
            Padding = new Point(14, 6)
        };
        tabs.DrawItem += DrawGuideVaultTab;
        _tabs = tabs;
        outer.Controls.Add(tabs, 0, 2);

        tabs.TabPages.Add(BuildDashboardTab());
        tabs.TabPages.Add(BuildManualPlatformsTab());
        tabs.TabPages.Add(BuildStrategyGuidesTab());
        tabs.TabPages.Add(BuildMagazinesTab());
        tabs.TabPages.Add(BuildSettingsTab());
        tabs.TabPages.Add(BuildLogTab());
        tabs.TabPages.Add(BuildAboutTab());
    }

    private Control BuildTitleBar()
    {
        var titleBar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = TitleShell,
            Padding = new Padding(10, 0, 6, 0)
        };
        titleBar.MouseDown += DragWindow;

        var icon = new PictureBox
        {
            Image = GuideVaultAssets.Favicon,
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(24, 24),
            Location = new Point(10, 7),
            BackColor = Color.Transparent
        };
        icon.MouseDown += DragWindow;
        titleBar.Controls.Add(icon);

        var title = new Label
        {
            Text = "GuideVault LaunchBox Connector",
            ForeColor = TextMain,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            AutoSize = false,
            Location = new Point(42, 0),
            Size = new Size(420, 38),
            TextAlign = ContentAlignment.MiddleLeft
        };
        title.MouseDown += DragWindow;
        titleBar.Controls.Add(title);

        var close = NewTitleButton("×");
        close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        close.Location = new Point(Width - 46, 4);
        close.Click += (_, _) => Close();
        titleBar.Controls.Add(close);

        var minimize = NewTitleButton("—");
        minimize.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        minimize.Location = new Point(Width - 86, 4);
        minimize.Click += (_, _) => WindowState = FormWindowState.Minimized;
        titleBar.Controls.Add(minimize);

        titleBar.Resize += (_, _) =>
        {
            close.Location = new Point(titleBar.Width - 42, 4);
            minimize.Location = new Point(titleBar.Width - 82, 4);
        };

        return titleBar;
    }

    private Control BuildHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Shell,
            Padding = new Padding(18, 12, 18, 10)
        };

        header.Paint += (_, e) =>
        {
            var rect = new Rectangle(8, 6, header.Width - 16, header.Height - 12);
            using var brush = new LinearGradientBrush(rect, Color.FromArgb(13, 30, 50), Color.FromArgb(4, 13, 24), LinearGradientMode.Horizontal);
            using var pen = new Pen(BorderSoft);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillRectangle(brush, rect);
            e.Graphics.DrawRectangle(pen, rect);
        };

        var wordmark = new PictureBox
        {
            Image = GuideVaultAssets.Wordmark,
            BackColor = Color.Transparent,
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(24, 17),
            Size = new Size(410, 58)
        };
        header.Controls.Add(wordmark);

        _selectedGameLabel.AutoSize = false;
        _selectedGameLabel.Location = new Point(456, 22);
        _selectedGameLabel.Size = new Size(660, 48);
        _selectedGameLabel.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        _selectedGameLabel.ForeColor = Color.FromArgb(125, 211, 252);
        _selectedGameLabel.BackColor = Color.Transparent;
        _selectedGameLabel.TextAlign = ContentAlignment.MiddleRight;
        header.Controls.Add(_selectedGameLabel);

        header.Resize += (_, _) =>
        {
            _selectedGameLabel.Width = Math.Max(200, header.Width - 486);
        };

        return header;
    }

    private TabPage BuildDashboardTab()
    {
        var page = NewTab("Status");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Shell,
            Padding = new Padding(10)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.Controls.Add(layout);

        var actions = NewCard("Quick actions");
        actions.Dock = DockStyle.Fill;
        layout.Controls.Add(actions, 0, 0);

        var actionFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = false,
            BackColor = Card,
            Padding = new Padding(4, 2, 4, 4)
        };
        actions.Controls.Add(actionFlow);

        _testButton.Click += async (_, _) => await RunAndLogAsync("Testing connection", () => GuideVaultActions.TestConnectionAsync()).ConfigureAwait(true);
        _statusButton.Click += async (_, _) => await RefreshStatusAsync().ConfigureAwait(true);
        _syncButton.Click += async (_, _) => await StartSyncLibraryAsync().ConfigureAwait(true);
        _cancelButton.Click += async (_, _) => await RunAndLogAsync("Canceling sync", () => GuideVaultActions.CancelSyncAsync()).ConfigureAwait(true);

        var gvButton = NewButton("Open GuideVault", 160);
        gvButton.Click += async (_, _) => await RunAndLogAsync("Opening GuideVault LaunchBox page", () => GuideVaultActions.OpenGuideVaultLaunchBoxPageAsync()).ConfigureAwait(true);

        actionFlow.Controls.Add(_testButton);
        actionFlow.Controls.Add(_statusButton);
        actionFlow.Controls.Add(_syncButton);
        actionFlow.Controls.Add(_cancelButton);
        actionFlow.Controls.Add(gvButton);

        var statusCard = NewCard("GuideVault sync status");
        statusCard.Dock = DockStyle.Fill;
        layout.Controls.Add(statusCard, 0, 1);

        var statusLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Card,
            Padding = new Padding(10)
        };
        statusLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        statusLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
        statusLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        statusCard.Controls.Add(statusLayout);

        _progressBar.Dock = DockStyle.Fill;
        _progressBar.Minimum = 0;
        _progressBar.Maximum = 100;
        statusLayout.Controls.Add(_progressBar, 0, 0);

        var summaryPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Card,
            Margin = new Padding(0, 8, 0, 8)
        };
        summaryPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        summaryPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        statusLayout.Controls.Add(summaryPanel, 0, 1);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.ForeColor = TextMain;
        _statusLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        _statusLabel.Text = "Refresh status to load GuideVault sync data.";
        summaryPanel.Controls.Add(_statusLabel, 0, 0);

        _jobLabel.Dock = DockStyle.Fill;
        _jobLabel.ForeColor = TextSoft;
        _jobLabel.Font = new Font("Segoe UI", 9.25f);
        _jobLabel.Text = string.Empty;
        summaryPanel.Controls.Add(_jobLabel, 1, 0);

        _syncOutput.Dock = DockStyle.Fill;
        _syncOutput.ReadOnly = true;
        _syncOutput.BackColor = Color.FromArgb(4, 11, 20);
        _syncOutput.ForeColor = TextMain;
        _syncOutput.Font = new Font("Consolas", 9.25f);
        _syncOutput.BorderStyle = BorderStyle.FixedSingle;
        _syncOutput.Text = "Sync output will appear here when a library sync is running." + Environment.NewLine;
        statusLayout.Controls.Add(_syncOutput, 0, 2);

        return page;
    }

    private TabPage BuildGameTab()
    {
        var page = NewTab("Selected Game");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1,
            BackColor = Shell,
            Padding = new Padding(10)
        };
        page.Controls.Add(layout);

        var gameGroup = NewCard("Open matched GuideVault documents");
        layout.Controls.Add(gameGroup, 0, 0);

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 84,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = Card,
            Padding = new Padding(8, 10, 8, 8)
        };
        gameGroup.Controls.Add(flow);

        _manualButton.Click += async (_, _) => await OpenSelectedDocumentAsync("Manual").ConfigureAwait(true);
        _strategyButton.Click += async (_, _) => await OpenSelectedDocumentAsync("Strategy Guide").ConfigureAwait(true);
        _magazineButton.Click += async (_, _) => await OpenSelectedDocumentAsync("Magazine").ConfigureAwait(true);

        var detailsButton = NewButton("Coverage / Review", 180);
        detailsButton.Click += async (_, _) => await RunAndLogAsync("Opening GuideVault coverage page", () => GuideVaultActions.OpenGuideVaultLaunchBoxPageAsync()).ConfigureAwait(true);

        flow.Controls.Add(_manualButton);
        flow.Controls.Add(_strategyButton);
        flow.Controls.Add(_magazineButton);
        flow.Controls.Add(detailsButton);

        var info = NewInfoLabel("Use this tab to test the selected LaunchBox game. Right-click actions use the same open path.", 52);
        info.Dock = DockStyle.Top;
        gameGroup.Controls.Add(info);
        info.BringToFront();

        return page;
    }

    private TabPage BuildManualPlatformsTab()
    {
        var page = NewTab("Manual Platforms");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Shell,
            Padding = new Padding(10)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 184));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.Controls.Add(layout);

        var controlsCard = NewCard("Manual platform selection");
        layout.Controls.Add(controlsCard, 0, 0);

        var controlsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Card,
            Padding = new Padding(10)
        };
        controlsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        controlsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        controlsCard.Controls.Add(controlsLayout);

        var buttonFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = Card,
            Padding = new Padding(0, 4, 0, 0)
        };
        controlsLayout.Controls.Add(buttonFlow, 0, 0);

        _refreshManualPlatformsButton.Click += async (_, _) => await RefreshManualPlatformsAsync().ConfigureAwait(true);
        _saveManualPlatformsButton.Click += (_, _) => SaveManualPlatformSelection();
        _syncManualPlatformsButton.Click += async (_, _) => await SyncSelectedManualPlatformsAsync().ConfigureAwait(true);
        _syncAllPlatformsButton.Click += async (_, _) => await SyncAllLaunchBoxPlatformsAsync().ConfigureAwait(true);

        buttonFlow.Controls.Add(_refreshManualPlatformsButton);
        buttonFlow.Controls.Add(_saveManualPlatformsButton);
        buttonFlow.Controls.Add(_syncManualPlatformsButton);
        buttonFlow.Controls.Add(_syncAllPlatformsButton);

        _manualPlatformSummaryLabel.Dock = DockStyle.Fill;
        _manualPlatformSummaryLabel.ForeColor = TextSoft;
        _manualPlatformSummaryLabel.Font = new Font("Segoe UI", 9.25f);
        _manualPlatformSummaryLabel.Text = "Select LaunchBox platforms for manual-focused syncing. Use Sync Selected to send only checked platform games, or Sync All to clear the filter.";
        controlsLayout.Controls.Add(_manualPlatformSummaryLabel, 0, 1);

        var gridCard = NewCard("LaunchBox platforms and GuideVault coverage");
        layout.Controls.Add(gridCard, 0, 1);

        ConfigureManualPlatformGrid();
        gridCard.Controls.Add(_manualPlatformGrid);

        page.Enter += async (_, _) =>
        {
            if (_manualPlatformGrid.Rows.Count == 0)
                await RefreshManualPlatformsAsync().ConfigureAwait(true);
        };

        return page;
    }

    private TabPage BuildStrategyGuidesTab()
    {
        var page = NewTab("Strategy Guides");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Shell,
            Padding = new Padding(10)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 184));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.Controls.Add(layout);

        var actionsCard = NewCard("Strategy guide matching");
        layout.Controls.Add(actionsCard, 0, 0);

        var actionsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Card,
            Padding = new Padding(10)
        };
        actionsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        actionsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        actionsCard.Controls.Add(actionsLayout);

        var buttonFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = Card,
            Padding = new Padding(0, 4, 0, 0)
        };
        actionsLayout.Controls.Add(buttonFlow, 0, 0);

        _refreshStrategyGuidesButton.Click += async (_, _) => await RefreshStrategyGuidesAsync().ConfigureAwait(true);
        _syncSelectedStrategyGuidesButton.Click += async (_, _) => await SyncSelectedStrategyGuidePlatformsAsync().ConfigureAwait(true);
        _syncAllStrategyGuidesButton.Click += async (_, _) => await SyncAllStrategyGuidePlatformsAsync().ConfigureAwait(true);
        _openStrategyGuideReviewButton.Click += (_, _) => GuideVaultRelationshipWindow.ShowWindow(this, "Strategy Guide");

        buttonFlow.Controls.Add(_refreshStrategyGuidesButton);
        buttonFlow.Controls.Add(_syncSelectedStrategyGuidesButton);
        buttonFlow.Controls.Add(_syncAllStrategyGuidesButton);
        buttonFlow.Controls.Add(_openStrategyGuideReviewButton);

        _strategyGuideSummaryLabel.Dock = DockStyle.Fill;
        _strategyGuideSummaryLabel.ForeColor = TextSoft;
        _strategyGuideSummaryLabel.Font = new Font("Segoe UI", 9.25f);
        _strategyGuideSummaryLabel.Text = "Check platforms to run a selected strategy-guide-only sync, or use Sync All Guides to rematch strategy guides across all LaunchBox games.";
        actionsLayout.Controls.Add(_strategyGuideSummaryLabel, 0, 1);

        var gridCard = NewCard("Strategy guide coverage by platform");
        layout.Controls.Add(gridCard, 0, 1);

        ConfigureStrategyGuideGrid();
        gridCard.Controls.Add(_strategyGuideGrid);

        page.Enter += async (_, _) =>
        {
            if (_strategyGuideGrid.Rows.Count == 0)
                await RefreshStrategyGuidesAsync().ConfigureAwait(true);
        };

        return page;
    }

    private void ConfigureStrategyGuideGrid()
    {
        if (_strategyGuideGrid.Columns.Count > 0) return;

        _strategyGuideGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Sync", HeaderText = "Sync", Width = 56, FillWeight = 36 });
        _strategyGuideGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Platform", HeaderText = "LaunchBox Platform", FillWeight = 160, ReadOnly = true });
        _strategyGuideGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Games", HeaderText = "LB Games", FillWeight = 70, ReadOnly = true });
        _strategyGuideGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "StrategyGuideMatches", HeaderText = "Guide Matches", FillWeight = 95, ReadOnly = true });
        _strategyGuideGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "AnyMatches", HeaderText = "Any GV Match", FillWeight = 95, ReadOnly = true });
        _strategyGuideGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Missing", HeaderText = "Missing", FillWeight = 70, ReadOnly = true });
        _strategyGuideGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Coverage", HeaderText = "Guide Coverage", FillWeight = 92, ReadOnly = true });

        foreach (DataGridViewColumn column in _strategyGuideGrid.Columns)
            column.SortMode = DataGridViewColumnSortMode.NotSortable;

        _strategyGuideGrid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_strategyGuideGrid.IsCurrentCellDirty)
                _strategyGuideGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
    }

    private async Task RefreshStrategyGuidesAsync()
    {
        SetBusy(true);
        AppendLog("Refreshing strategy guide coverage...");
        try
        {
            var localPlatforms = LaunchBoxGameMapper.GetPlatformSummaries();

            GuideVaultCoverageResult? coverage = null;
            try
            {
                coverage = await GuideVaultActions.GetCoverageAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                AppendLog("GuideVault strategy guide coverage unavailable: " + ex.Message);
            }

            var coverageByPlatform = (coverage?.ByPlatform ?? new List<GuideVaultPlatformCoverage>())
                .GroupBy(row => CleanPlatform(row.Platform), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var settings = SettingsStore.Load();
            var selected = settings.StrategyGuideSyncPlatforms.ToHashSet(StringComparer.OrdinalIgnoreCase);

            _strategyGuideGrid.Rows.Clear();
            foreach (var platform in localPlatforms)
            {
                coverageByPlatform.TryGetValue(CleanPlatform(platform.Platform), out var match);
                var guideMatches = match?.StrategyGuideMatchedGames ?? 0;
                var coverageGameCount = match?.GameCount ?? 0;
                var gameCount = coverageGameCount > 0 ? coverageGameCount : platform.GameCount;
                var coveragePercent = gameCount <= 0 ? 0 : guideMatches * 100.0 / gameCount;

                var index = _strategyGuideGrid.Rows.Add(
                    settings.LimitStrategyGuideSyncToSelectedPlatforms && selected.Contains(CleanPlatform(platform.Platform)),
                    platform.Platform,
                    platform.GameCount.ToString("N0"),
                    guideMatches.ToString("N0"),
                    (match?.AnyMatchedGames ?? 0).ToString("N0"),
                    (match?.MissingGames ?? platform.GameCount).ToString("N0"),
                    match is null ? "Not synced" : $"{coveragePercent:0.##}%");
                _strategyGuideGrid.Rows[index].Tag = platform.Platform;
            }

            var totalGames = coverage?.TotalLaunchBoxGames ?? localPlatforms.Sum(row => row.GameCount);
            var guideMatched = coverage?.StrategyGuideMatchedGames ?? coverageByPlatform.Values.Sum(row => row.StrategyGuideMatchedGames);
            var percent = totalGames <= 0 ? 0 : guideMatched * 100.0 / totalGames;
            var selectedCount = settings.LimitStrategyGuideSyncToSelectedPlatforms ? selected.Count : 0;
            _strategyGuideSummaryLabel.Text = $"Strategy guide coverage: {guideMatched:N0}/{totalGames:N0} LaunchBox games ({percent:0.##}%). Platforms shown: {localPlatforms.Count:N0}. Strategy-guide sync filter: {(selectedCount > 0 ? selectedCount.ToString("N0") + " selected" : "off / sync all")}.";
            AppendLog($"Loaded strategy guide coverage for {localPlatforms.Count:N0} LaunchBox platform(s). Guide-matched games: {guideMatched:N0}.");
        }
        catch (Exception ex)
        {
            AppendLog("ERROR refreshing strategy guide coverage: " + ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private TabPage BuildMagazinesTab()
    {
        var page = NewTab("Magazines");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Shell,
            Padding = new Padding(10)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 184));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.Controls.Add(layout);

        var actionsCard = NewCard("Magazine featured-game matching");
        layout.Controls.Add(actionsCard, 0, 0);
        var actionsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Card,
            Padding = new Padding(10)
        };
        actionsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        actionsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        actionsCard.Controls.Add(actionsLayout);

        var buttonFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = Card,
            Padding = new Padding(0, 4, 0, 0)
        };
        _refreshMagazinesButton.Click += async (_, _) => await RefreshMagazinesAsync().ConfigureAwait(true);
        _syncSelectedMagazinesButton.Click += async (_, _) => await SyncSelectedMagazinePlatformsAsync().ConfigureAwait(true);
        _syncAllMagazinesButton.Click += async (_, _) => await SyncAllMagazinePlatformsAsync().ConfigureAwait(true);
        _openMagazineReviewButton.Click += (_, _) => GuideVaultRelationshipWindow.ShowWindow(this, "Magazine");
        buttonFlow.Controls.Add(_refreshMagazinesButton);
        buttonFlow.Controls.Add(_syncSelectedMagazinesButton);
        buttonFlow.Controls.Add(_syncAllMagazinesButton);
        buttonFlow.Controls.Add(_openMagazineReviewButton);
        actionsLayout.Controls.Add(buttonFlow, 0, 0);
        _magazineSummaryLabel.Dock = DockStyle.Fill;
        _magazineSummaryLabel.ForeColor = TextSoft;
        _magazineSummaryLabel.Font = new Font("Segoe UI", 9.25f);
        _magazineSummaryLabel.Text = "Check platforms to run a selected magazine-only sync, or use Sync All Magazines to rematch featured-game magazine coverage across all LaunchBox games.";
        actionsLayout.Controls.Add(_magazineSummaryLabel, 0, 1);

        var gridCard = NewCard("Magazine featured-game coverage by platform");
        layout.Controls.Add(gridCard, 0, 1);
        ConfigureMagazineGrid();
        gridCard.Controls.Add(_magazineGrid);

        page.Enter += async (_, _) =>
        {
            if (_magazineGrid.Rows.Count == 0)
                await RefreshMagazinesAsync().ConfigureAwait(true);
        };

        return page;
    }

    private void ConfigureMagazineGrid()
    {
        if (_magazineGrid.Columns.Count > 0) return;

        _magazineGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Sync", HeaderText = "Sync", Width = 56, FillWeight = 36 });
        _magazineGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Platform", HeaderText = "LaunchBox Platform", FillWeight = 160, ReadOnly = true });
        _magazineGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Games", HeaderText = "LB Games", FillWeight = 70, ReadOnly = true });
        _magazineGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "MagazineMatches", HeaderText = "Magazine Matches", FillWeight = 110, ReadOnly = true });
        _magazineGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "AnyMatches", HeaderText = "Any GV Match", FillWeight = 95, ReadOnly = true });
        _magazineGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Missing", HeaderText = "Missing", FillWeight = 70, ReadOnly = true });
        _magazineGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Coverage", HeaderText = "Magazine Coverage", FillWeight = 110, ReadOnly = true });

        foreach (DataGridViewColumn column in _magazineGrid.Columns)
            column.SortMode = DataGridViewColumnSortMode.NotSortable;

        _magazineGrid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_magazineGrid.IsCurrentCellDirty)
                _magazineGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
    }

    private async Task RefreshMagazinesAsync()
    {
        SetBusy(true);
        AppendLog("Refreshing magazine featured-game coverage...");
        try
        {
            var localPlatforms = LaunchBoxGameMapper.GetPlatformSummaries()
                .OrderBy(p => p.Platform, StringComparer.OrdinalIgnoreCase)
                .ToList();

            GuideVaultCoverageResult? coverage = null;
            try
            {
                coverage = await GuideVaultActions.GetCoverageAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                AppendLog("GuideVault magazine coverage unavailable: " + ex.Message);
            }

            var coverageByPlatform = (coverage?.ByPlatform ?? new List<GuideVaultPlatformCoverage>())
                .GroupBy(row => CleanPlatform(row.Platform), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var settings = SettingsStore.Load();
            var selected = settings.MagazineSyncPlatforms.ToHashSet(StringComparer.OrdinalIgnoreCase);

            _magazineGrid.Rows.Clear();
            foreach (var platform in localPlatforms)
            {
                coverageByPlatform.TryGetValue(CleanPlatform(platform.Platform), out var match);
                var magazineMatches = match?.MagazineMatchedGames ?? 0;
                var gameCount = match?.GameCount > 0 ? match.GameCount : platform.GameCount;
                var missing = match?.MissingGames ?? Math.Max(0, gameCount - magazineMatches);
                var coveragePercent = gameCount <= 0 ? 0 : magazineMatches * 100.0 / gameCount;
                var index = _magazineGrid.Rows.Add(
                    settings.LimitMagazineSyncToSelectedPlatforms && selected.Contains(CleanPlatform(platform.Platform)),
                    platform.Platform,
                    platform.GameCount.ToString("N0"),
                    magazineMatches.ToString("N0"),
                    (match?.AnyMatchedGames ?? 0).ToString("N0"),
                    missing.ToString("N0"),
                    $"{coveragePercent:0.##}%");
                _magazineGrid.Rows[index].Tag = platform.Platform;
            }

            var totalGames = localPlatforms.Sum(p => p.GameCount);
            var magazineMatched = coverage?.MagazineMatchedGames ?? coverageByPlatform.Values.Sum(row => row.MagazineMatchedGames);
            var percent = totalGames <= 0 ? 0 : magazineMatched * 100.0 / totalGames;
            var selectedCount = settings.LimitMagazineSyncToSelectedPlatforms ? selected.Count : 0;
            _magazineSummaryLabel.Text = $"Magazine featured-game coverage: {magazineMatched:N0}/{totalGames:N0} LaunchBox games ({percent:0.##}%). Magazine sync filter: {(selectedCount > 0 ? selectedCount.ToString("N0") + " selected" : "off / sync all")}.";
            AppendLog($"Loaded magazine coverage for {localPlatforms.Count:N0} LaunchBox platform(s). Magazine-matched games: {magazineMatched:N0}.");
        }
        catch (Exception ex)
        {
            AppendLog("ERROR refreshing magazine coverage: " + ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ConfigureManualPlatformGrid()
    {
        if (_manualPlatformGrid.Columns.Count > 0) return;

        _manualPlatformGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Sync", HeaderText = "Sync", Width = 56, FillWeight = 36 });
        _manualPlatformGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Platform", HeaderText = "LaunchBox Platform", FillWeight = 150, ReadOnly = true });
        _manualPlatformGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Games", HeaderText = "LB Games", FillWeight = 70, ReadOnly = true });
        _manualPlatformGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "GuideVaultPlatform", HeaderText = "GuideVault Platform", FillWeight = 150, ReadOnly = true });
        _manualPlatformGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ManualMatches", HeaderText = "Manual Matches", FillWeight = 90, ReadOnly = true });
        _manualPlatformGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Missing", HeaderText = "Missing", FillWeight = 70, ReadOnly = true });
        _manualPlatformGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Coverage", HeaderText = "Coverage", FillWeight = 78, ReadOnly = true });

        foreach (DataGridViewColumn column in _manualPlatformGrid.Columns)
            column.SortMode = DataGridViewColumnSortMode.NotSortable;

        _manualPlatformGrid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_manualPlatformGrid.IsCurrentCellDirty)
                _manualPlatformGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
    }

    private async Task RefreshManualPlatformsAsync()
    {
        SetBusy(true);
        AppendLog("Refreshing LaunchBox platform list...");
        try
        {
            var localPlatforms = LaunchBoxGameMapper.GetPlatformSummaries();

            GuideVaultCoverageResult? coverage = null;
            try
            {
                coverage = await GuideVaultActions.GetCoverageAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                AppendLog("GuideVault platform coverage unavailable: " + ex.Message);
            }

            var coverageByPlatform = (coverage?.ByPlatform ?? new List<GuideVaultPlatformCoverage>())
                .GroupBy(row => CleanPlatform(row.Platform), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var settings = SettingsStore.Load();
            var selected = settings.ManualSyncPlatforms.ToHashSet(StringComparer.OrdinalIgnoreCase);

            _manualPlatformGrid.Rows.Clear();
            foreach (var platform in localPlatforms)
            {
                coverageByPlatform.TryGetValue(CleanPlatform(platform.Platform), out var match);
                var index = _manualPlatformGrid.Rows.Add(
                    settings.LimitManualSyncToSelectedPlatforms && selected.Contains(CleanPlatform(platform.Platform)),
                    platform.Platform,
                    platform.GameCount.ToString("N0"),
                    match?.Platform ?? "—",
                    (match?.ManualMatchedGames ?? 0).ToString("N0"),
                    (match?.MissingGames ?? platform.GameCount).ToString("N0"),
                    match is null ? "Not synced" : $"{match.CoveragePercent:0.##}%");
                _manualPlatformGrid.Rows[index].Tag = platform.Platform;
            }

            var selectedCount = settings.LimitManualSyncToSelectedPlatforms ? selected.Count : 0;
            _manualPlatformSummaryLabel.Text = $"LaunchBox platforms: {localPlatforms.Count:N0}. Last GuideVault coverage platforms: {coverageByPlatform.Count:N0}. Manual platform filter: {(selectedCount > 0 ? selectedCount.ToString("N0") + " selected" : "off / sync all")}.";
            AppendLog($"Loaded {localPlatforms.Count:N0} LaunchBox platform(s). GuideVault coverage returned {coverageByPlatform.Count:N0} platform row(s).");
        }
        catch (Exception ex)
        {
            AppendLog("ERROR refreshing platforms: " + ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SaveManualPlatformSelection()
    {
        CommitManualPlatformGridEdits();
        var selected = GetSelectedManualPlatforms();
        var settings = SettingsStore.Load();
        settings.LimitManualSyncToSelectedPlatforms = selected.Count > 0;
        settings.ManualSyncPlatforms = selected;
        SettingsStore.Save(settings);
        _manualPlatformSummaryLabel.Text = selected.Count > 0
            ? $"Manual platform selection saved: {selected.Count:N0} selected. Use Sync Selected to send only those LaunchBox platform games."
            : "Manual platform selection cleared. Use Sync All to send all LaunchBox games.";
        AppendLog(_manualPlatformSummaryLabel.Text);
    }

    private async Task SyncSelectedManualPlatformsAsync()
    {
        CommitManualPlatformGridEdits();
        var selected = GetSelectedManualPlatforms();
        if (selected.Count == 0)
        {
            AppendLog("No manual platforms selected. Use Sync All, or select at least one platform before syncing selected platforms.");
            return;
        }

        var settings = SettingsStore.Load();
        settings.LimitManualSyncToSelectedPlatforms = true;
        settings.ManualSyncPlatforms = selected;
        SettingsStore.Save(settings);

        _manualPlatformSummaryLabel.Text = $"Manual platform filter saved: {selected.Count:N0} selected. Sync Selected will send only checked platform games.";
        AppendLog($"Sync Selected requested for {selected.Count:N0} platform(s): {string.Join(", ", selected)}");
        SelectTab("Status");
        await StartSyncLibraryAsync(selected).ConfigureAwait(true);
    }

    private async Task SyncAllLaunchBoxPlatformsAsync()
    {
        var settings = SettingsStore.Load();
        settings.LimitManualSyncToSelectedPlatforms = false;
        settings.ManualSyncPlatforms.Clear();
        SettingsStore.Save(settings);
        foreach (DataGridViewRow row in _manualPlatformGrid.Rows)
            row.Cells["Sync"].Value = false;
        AppendLog("Manual platform filter cleared. Syncing all LaunchBox games.");
        SelectTab("Status");
        await StartSyncLibraryAsync().ConfigureAwait(true);
    }

    private void CommitManualPlatformGridEdits()
    {
        if (_manualPlatformGrid.IsCurrentCellDirty)
            _manualPlatformGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        _manualPlatformGrid.EndEdit();
    }

    private List<string> GetSelectedManualPlatforms()
    {
        var selected = new List<string>();
        foreach (DataGridViewRow row in _manualPlatformGrid.Rows)
        {
            if (row.IsNewRow) continue;
            var include = row.Cells["Sync"].Value is bool value && value;
            if (!include) continue;
            var platform = row.Tag?.ToString() ?? row.Cells["Platform"].Value?.ToString() ?? string.Empty;
            platform = CleanPlatform(platform);
            if (!string.IsNullOrWhiteSpace(platform)) selected.Add(platform);
        }
        return selected.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(platform => platform, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task SyncSelectedStrategyGuidePlatformsAsync()
    {
        CommitStrategyGuideGridEdits();
        var selected = GetSelectedStrategyGuidePlatforms();
        if (selected.Count == 0)
        {
            AppendLog("No strategy guide platforms selected. Use Sync All Guides, or check at least one platform before syncing selected strategy guides.");
            return;
        }

        var settings = SettingsStore.Load();
        settings.LimitStrategyGuideSyncToSelectedPlatforms = true;
        settings.StrategyGuideSyncPlatforms = selected;
        SettingsStore.Save(settings);

        _strategyGuideSummaryLabel.Text = $"Strategy-guide platform filter saved: {selected.Count:N0} selected. Sync Selected Guides will rematch only strategy guides for checked platform games.";
        AppendLog($"Sync Selected Guides requested for {selected.Count:N0} platform(s): {string.Join(", ", selected)}");
        SelectTab("Status");
        await StartSyncLibraryAsync(selected, new[] { "Strategy Guide" }).ConfigureAwait(true);
    }

    private async Task SyncAllStrategyGuidePlatformsAsync()
    {
        var settings = SettingsStore.Load();
        settings.LimitStrategyGuideSyncToSelectedPlatforms = false;
        settings.StrategyGuideSyncPlatforms.Clear();
        SettingsStore.Save(settings);
        foreach (DataGridViewRow row in _strategyGuideGrid.Rows)
            row.Cells["Sync"].Value = false;
        AppendLog("Strategy-guide platform filter cleared. Syncing all LaunchBox games for strategy-guide matching only.");
        SelectTab("Status");
        await StartSyncLibraryAsync(null, new[] { "Strategy Guide" }).ConfigureAwait(true);
    }

    private void CommitStrategyGuideGridEdits()
    {
        if (_strategyGuideGrid.IsCurrentCellDirty)
            _strategyGuideGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        _strategyGuideGrid.EndEdit();
    }

    private List<string> GetSelectedStrategyGuidePlatforms()
    {
        var selected = new List<string>();
        foreach (DataGridViewRow row in _strategyGuideGrid.Rows)
        {
            if (row.IsNewRow) continue;
            var include = row.Cells["Sync"].Value is bool value && value;
            if (!include) continue;
            var platform = row.Tag?.ToString() ?? row.Cells["Platform"].Value?.ToString() ?? string.Empty;
            platform = CleanPlatform(platform);
            if (!string.IsNullOrWhiteSpace(platform)) selected.Add(platform);
        }
        return selected.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(platform => platform, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task SyncSelectedMagazinePlatformsAsync()
    {
        CommitMagazineGridEdits();
        var selected = GetSelectedMagazinePlatforms();
        if (selected.Count == 0)
        {
            AppendLog("No magazine platforms selected. Use Sync All Magazines, or check at least one platform before syncing selected magazines.");
            return;
        }

        var settings = SettingsStore.Load();
        settings.LimitMagazineSyncToSelectedPlatforms = true;
        settings.MagazineSyncPlatforms = selected;
        SettingsStore.Save(settings);

        _magazineSummaryLabel.Text = $"Magazine platform filter saved: {selected.Count:N0} selected. Sync Selected Magazines will rematch only magazines for checked platform games.";
        AppendLog($"Sync Selected Magazines requested for {selected.Count:N0} platform(s): {string.Join(", ", selected)}");
        SelectTab("Status");
        await StartSyncLibraryAsync(selected, new[] { "Magazine" }).ConfigureAwait(true);
    }

    private async Task SyncAllMagazinePlatformsAsync()
    {
        var settings = SettingsStore.Load();
        settings.LimitMagazineSyncToSelectedPlatforms = false;
        settings.MagazineSyncPlatforms.Clear();
        SettingsStore.Save(settings);
        foreach (DataGridViewRow row in _magazineGrid.Rows)
            row.Cells["Sync"].Value = false;
        AppendLog("Magazine platform filter cleared. Syncing all LaunchBox games for magazine matching only.");
        SelectTab("Status");
        await StartSyncLibraryAsync(null, new[] { "Magazine" }).ConfigureAwait(true);
    }

    private void CommitMagazineGridEdits()
    {
        if (_magazineGrid.IsCurrentCellDirty)
            _magazineGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        _magazineGrid.EndEdit();
    }

    private List<string> GetSelectedMagazinePlatforms()
    {
        var selected = new List<string>();
        foreach (DataGridViewRow row in _magazineGrid.Rows)
        {
            if (row.IsNewRow) continue;
            var include = row.Cells["Sync"].Value is bool value && value;
            if (!include) continue;
            var platform = row.Tag?.ToString() ?? row.Cells["Platform"].Value?.ToString() ?? string.Empty;
            platform = CleanPlatform(platform);
            if (!string.IsNullOrWhiteSpace(platform)) selected.Add(platform);
        }
        return selected.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(platform => platform, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private TabPage BuildAboutTab()
    {
        var page = NewTab("About");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Shell,
            Padding = new Padding(10)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.Controls.Add(layout);

        var versionCard = NewCard("Plugin version");
        var historyCard = NewCard("Plugin update history");
        layout.Controls.Add(versionCard, 0, 0);
        layout.Controls.Add(historyCard, 0, 1);

        var versionPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Card,
            Padding = new Padding(18, 12, 18, 12)
        };
        versionCard.Controls.Add(versionPanel);

        var logo = new PictureBox
        {
            Image = GuideVaultAssets.Wordmark,
            SizeMode = PictureBoxSizeMode.Zoom,
            Width = 360,
            Height = 66,
            BackColor = Card,
            Location = new Point(12, 10)
        };
        versionPanel.Controls.Add(logo);

        var versionTitle = new Label
        {
            AutoSize = false,
            Text = $"GuideVault LaunchBox Connector {ConnectorConstants.PluginVersion}",
            ForeColor = Color.White,
            BackColor = Card,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            Location = new Point(18, 86),
            Size = new Size(880, 24),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        versionPanel.Controls.Add(versionTitle);

        var settingsPath = new Label
        {
            AutoSize = false,
            Text = $"Settings file: {SettingsStore.SettingsPath}",
            ForeColor = TextSoft,
            BackColor = Card,
            Font = new Font("Segoe UI", 8.75f),
            Location = new Point(18, 120),
            Size = new Size(980, 24),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        versionPanel.Controls.Add(settingsPath);

        versionPanel.Resize += (_, _) =>
        {
            var available = Math.Max(240, versionPanel.Width - 42);
            versionTitle.Width = available;
            settingsPath.Width = available;
        };

        var history = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(4, 11, 20),
            ForeColor = TextMain,
            Font = new Font("Consolas", 9.25f),
            Text = PluginUpdateHistoryText(),
            DetectUrls = false
        };
        historyCard.Controls.Add(history);
        return page;
    }

    private static string PluginUpdateHistoryText()
    {
        return string.Join(Environment.NewLine + Environment.NewLine, new[]
        {
            "0.4.22 - Release hardening\n- Shows plugin version, server version, last synced plugin version, last sync time, and last match time on the Status tab.\n- Keeps sync buttons disabled while a GuideVault match job is active to prevent accidental stacked sync jobs.\n- Adds clearer match-review counts and empty states for Strategy Guides and Magazines.",
            "0.4.21 - Type-scoped guide and magazine sync\n- Added Sync Selected Guides / Sync All Guides to the Strategy Guides tab.\n- Added Sync Selected Magazines / Sync All Magazines to the Magazines tab.\n- Sends a match-type scope to GuideVault so strategy guide and magazine sync actions do not unnecessarily rematch every content type.",
            "0.4.20 - Popup rebuild and lighter background work\n- Rebuilt the package source around the relationship popup actions for Strategy Guides and Magazines.\n- Reduced sync polling and badge refresh churn so LaunchBox should spend less time showing a busy/loading cursor.\n- Reused one HTTP client for connector calls instead of creating a new connection pipeline per request.",
            "0.4.19 - Guide and magazine link popups\n- Strategy Guides and Magazines tabs now open collapsible relationship grids showing each GuideVault title and its matched LaunchBox games.\n- Removed selected-game open buttons from Strategy Guides and Magazines tab actions.",
            "0.4.18 - Matched-title right-click menu\n- Right-click Strategy Guide and Magazine actions now list each active matched title when a game has multiple matched items.\n- Selecting a listed title opens that exact GuideVault item.",
            "0.4.17 - Guide/magazine compile fix\n- Added shared platform summary helpers for Strategy Guides and Magazines coverage views.",
            "0.4.16 - Field-aware guide and magazine matching UI\n- Strategy Guides use Covered Games. Magazines use Featured Games.\n- Added a magazine coverage workspace.",
            "0.4.15 - Strategy Guides tab setup\n- Replaced the placeholder Strategy Guides page with coverage actions and a platform-level strategy guide coverage grid.\n- Added early quick actions to refresh guide coverage and review GuideVault matching.",
            "0.4.14 - About tab copy cleanup\n- Removed the About tab feature-summary text so the tab focuses on versioning and update history.\n- Kept the stacked About layout and GuideVault matched-items badge artwork.",
            "0.4.13 - About tab compile fix\n- Fixed the About tab muted-text color reference that caused a CS0103 build failure.\n- Kept the stacked About layout and GuideVault matched-items badge artwork.",
            "0.4.12 - Favicon resource cleanup\n- Removed stale favicon embedded-resource references from the project/build path.\n- Build script prints active embedded-resource lines before compiling.",
            "0.4.7 - Menu cleanup, About tab, and visual polish\n- Removed Sync / Matching and Connector groups from the right-click game menu.\n- Removed badge cache/test debug buttons from Manual Platforms.\n- Added plugin-specific About and update history.\n- Cleaned the Settings tab and manual platform help text.",
            "0.4.6 - Badge image install fix\n- Installed a real GuideVault matched-item badge image into LaunchBox badge image folders so LaunchBox can render the badge reliably.",
            "0.4.5 - Badge rendering debug tools\n- Added temporary badge test/force tools to isolate LaunchBox badge rendering from GuideVault match-cache logic.",
            "0.4.4 - Badge visibility and repaint fix\n- Improved matched-item badge cache diagnostics and LaunchBox view refresh behavior.",
            "0.4.3 - Badge refresh after matching\n- Refreshes badge cache after GuideVault matching jobs complete.\n- Added stronger title/platform fallback matching.",
            "0.4.2 - GuideVault matched-item badge\n- Added the GuideVault Matched Item badge for games with a matched manual, strategy guide, or magazine.",
            "0.4.1 - Manual platform sync scope fix\n- Sync Selected now sends only checked platforms and uses selected-platform matching scope on the server.",
            "0.4.0 - Platform sync tabs\n- Added Manual Platforms tab and scaffold tabs for Strategy Guides and Magazines.",
            "0.3.x - Embedded reader and UI polish\n- Added WebView2 helper window, fullscreen/focus behavior, reader auth fixes, and compact themed plugin settings."
        });
    }

    private TabPage BuildSettingsTab()
    {
        var page = NewTab("Settings");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Shell,
            Padding = new Padding(10)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        page.Controls.Add(layout);

        var connector = NewCard("Connection and reader launch");
        var auth = NewCard("Authentication and sync details");
        layout.Controls.Add(connector, 0, 0);
        layout.Controls.Add(auth, 1, 0);

        var connectorFlow = NewVerticalFlow();
        connector.Controls.Add(connectorFlow);
        connectorFlow.Controls.Add(FieldRow("GuideVault URL", _urlText));
        connectorFlow.Controls.Add(FieldRow("Open behavior", _openModeCombo));
        connectorFlow.Controls.Add(CheckRow(_readerMaximizedCheck));
        connectorFlow.Controls.Add(CheckRow(_readerFullscreenCheck));
        connectorFlow.Controls.Add(FieldRow("Request timeout", _timeoutBox));
        connectorFlow.Controls.Add(NewInfoLabel("Fullscreen can also be toggled in the WebView2 reader with the Fullscreen toolbar button or F11. If fullscreen is enabled, it overrides maximized window mode.", 62));

        var connectorButtons = new FlowLayoutPanel
        {
            Width = 500,
            Height = 48,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Card,
            Padding = new Padding(0, 6, 0, 0)
        };
        var saveButton = NewButton("Save Settings", 150);
        saveButton.Click += (_, _) => SaveSettingsFromControls();
        connectorButtons.Controls.Add(saveButton);
        connectorFlow.Controls.Add(connectorButtons);

        var authFlow = NewVerticalFlow();
        auth.Controls.Add(authFlow);
        authFlow.Controls.Add(CheckRow(_browserBridgeCheck));
        authFlow.Controls.Add(FieldRow("Username", _usernameText));
        authFlow.Controls.Add(FieldRow("Email", _emailText));
        _passwordText.UseSystemPasswordChar = true;
        authFlow.Controls.Add(FieldRow("Password", _passwordText));
        authFlow.Controls.Add(CheckRow(_alternateNamesCheck));
        authFlow.Controls.Add(CheckRow(_customFieldsCheck));
        authFlow.Controls.Add(NewInfoLabel("Alternate titles are LaunchBox aliases that can improve matching when a game has regional or renamed releases. Custom fields are extra user-defined LaunchBox metadata; leave them off unless you use those fields for matching or filtering.", 90));

        return page;
    }

    private TabPage BuildLogTab()
    {
        var page = NewTab("Log");
        _log.Dock = DockStyle.Fill;
        _log.BackColor = Color.FromArgb(4, 11, 20);
        _log.ForeColor = TextMain;
        _log.Font = new Font("Consolas", 9.5f);
        _log.BorderStyle = BorderStyle.FixedSingle;
        page.Controls.Add(_log);
        return page;
    }

    private async void RunStartupAction(GuideVaultWindowStartupAction action)
    {
        switch (action)
        {
            case GuideVaultWindowStartupAction.OpenManual:
                await OpenSelectedDocumentAsync("Manual").ConfigureAwait(true);
                break;
            case GuideVaultWindowStartupAction.OpenStrategyGuide:
                await OpenSelectedDocumentAsync("Strategy Guide").ConfigureAwait(true);
                break;
            case GuideVaultWindowStartupAction.OpenMagazine:
                await OpenSelectedDocumentAsync("Magazine").ConfigureAwait(true);
                break;
            case GuideVaultWindowStartupAction.SyncLibrary:
                SelectTab("Status");
                await StartSyncLibraryAsync().ConfigureAwait(true);
                break;
            case GuideVaultWindowStartupAction.SyncStatus:
                SelectTab("Status");
                await RefreshStatusAsync().ConfigureAwait(true);
                break;
            case GuideVaultWindowStartupAction.TestConnection:
                SelectTab("Status");
                await RunAndLogAsync("Testing connection", () => GuideVaultActions.TestConnectionAsync()).ConfigureAwait(true);
                break;
            case GuideVaultWindowStartupAction.CancelSync:
                SelectTab("Status");
                await RunAndLogAsync("Canceling sync", () => GuideVaultActions.CancelSyncAsync()).ConfigureAwait(true);
                break;
            case GuideVaultWindowStartupAction.Settings:
                SelectTab("Settings");
                break;
        }
    }

    private async Task StartSyncLibraryAsync(IReadOnlyList<string>? selectedPlatforms = null, IReadOnlyList<string>? matchTypes = null)
    {
        _syncPollCts?.Cancel();
        _lastSyncOutputLine = string.Empty;
        ClearSyncOutput();
        SetBusy(true);
        var scopeLabel = matchTypes is { Count: > 0 } ? $" ({string.Join(", ", matchTypes)} only)" : string.Empty;
        AppendSyncOutput(selectedPlatforms is { Count: > 0 }
            ? $"Starting selected platform sync{scopeLabel}: {string.Join(", ", selectedPlatforms)}"
            : $"Starting LaunchBox library sync{scopeLabel}...");

        try
        {
            var message = await GuideVaultActions.SyncLibraryAsync(selectedPlatforms, matchTypes).ConfigureAwait(true);
            AppendSyncOutput(message);
            _syncJobRunning = true;
            ApplyButtonState();
            await RefreshStatusAsync().ConfigureAwait(true);
            StartSyncPolling();
        }
        catch (Exception ex)
        {
            _syncJobRunning = false;
            AppendSyncOutput("ERROR: " + ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void StartSyncPolling()
    {
        _syncPollCts?.Cancel();
        _syncPollCts = new CancellationTokenSource();
        _ = PollSyncStatusAsync(_syncPollCts.Token);
    }

    private async Task PollSyncStatusAsync(CancellationToken token)
    {
        var deadline = DateTime.UtcNow.AddMinutes(30);
        while (!token.IsCancellationRequested && !IsDisposed && DateTime.UtcNow < deadline)
        {
            try
            {
                var (status, job) = await GuideVaultActions.GetSyncStatusAsync().ConfigureAwait(true);
                UpdateStatus(status, job);
                AppendSyncJobLine(job);

                if (IsTerminalSyncStatus(job.Status))
                {
                    _syncJobRunning = false;
                    ApplyButtonState();
                    AppendSyncOutput($"Sync job finished: {job.Status}.");
                    return;
                }
            }
            catch (Exception ex)
            {
                _syncJobRunning = false;
                ApplyButtonState();
                AppendSyncOutput("Status polling error: " + ex.Message);
                return;
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), token).ConfigureAwait(true);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }

        if (!token.IsCancellationRequested && !IsDisposed)
        {
            _syncJobRunning = false;
            ApplyButtonState();
            AppendSyncOutput("Stopped sync status polling after 30 minutes. Use Refresh Status to check the final state.");
        }
    }

    private void AppendSyncJobLine(GuideVaultSyncJobStatus job)
    {
        var percent = job.TotalGames > 0 ? Math.Round((double)job.ProcessedGames / job.TotalGames * 100, 1) : 0;
        var line = $"{job.Status}: {job.ProcessedGames:N0}/{job.TotalGames:N0} ({percent}%)  Imported {job.ImportedGames:N0}, Matched {job.MatchedGames:N0}, Manuals {job.ManualMatchedGames:N0}, Guides {job.StrategyGuideMatchedGames:N0}, Magazines {job.MagazineMatchedGames:N0}, Ambiguous {job.AmbiguousMatches:N0}, Missing {job.MissingGames:N0}";
        if (!string.IsNullOrWhiteSpace(job.Message)) line += "  - " + job.Message.Trim();
        if (string.Equals(line, _lastSyncOutputLine, StringComparison.Ordinal)) return;
        _lastSyncOutputLine = line;
        AppendSyncOutput(line);
    }

    private static bool IsTerminalSyncStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        return status.Equals("Completed", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Complete", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Succeeded", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Failed", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Canceled", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsActiveSyncStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        if (IsTerminalSyncStatus(status)) return false;
        return !status.Equals("Idle", StringComparison.OrdinalIgnoreCase)
            && !status.Equals("None", StringComparison.OrdinalIgnoreCase)
            && !status.Equals("Unknown", StringComparison.OrdinalIgnoreCase);
    }

    private void ClearSyncOutput()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(ClearSyncOutput));
            return;
        }
        _syncOutput.Clear();
    }

    private void AppendSyncOutput(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(AppendSyncOutput), message);
            return;
        }

        var line = $"[{DateTime.Now:T}] {message}{Environment.NewLine}";
        _syncOutput.AppendText(line);
        _syncOutput.ScrollToCaret();
        _log.AppendText(line);
        _log.ScrollToCaret();
    }

    private async Task OpenSelectedDocumentAsync(string matchType)
    {
        await RunAndLogAsync($"Opening {matchType}", () => GuideVaultActions.OpenDocumentAsync(_selectedGame, matchType)).ConfigureAwait(true);
    }

    private async Task RefreshStatusAsync()
    {
        await RunUiAsync("Refreshing sync status", async () =>
        {
            var (status, job) = await GuideVaultActions.GetSyncStatusAsync().ConfigureAwait(false);
            UpdateStatus(status, job);
            return BuildStatusText(status, job);
        }).ConfigureAwait(true);
    }

    private async Task RunAndLogAsync(string caption, Func<Task<string>> action) => await RunUiAsync(caption, action).ConfigureAwait(true);

    private async Task RunUiAsync(string caption, Func<Task<string>> action)
    {
        SetBusy(true);
        AppendLog(caption + "...");
        try
        {
            var message = await action().ConfigureAwait(true);
            AppendLog(message);
        }
        catch (Exception ex)
        {
            AppendLog("ERROR: " + ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void UpdateStatus(GuideVaultStatusResult status, GuideVaultSyncJobStatus job)
    {
        var percent = job.TotalGames > 0 ? Math.Round((double)job.ProcessedGames / job.TotalGames * 100, 1) : 0;
        _syncJobRunning = IsActiveSyncStatus(job.Status);
        _statusLabel.Text = string.Join(Environment.NewLine, new[]
        {
            $"Plugin {ConnectorConstants.PluginVersion}",
            $"GuideVault server: {Blank(status.Version, "Unknown")}",
            $"Last plugin sync: {Blank(status.PluginVersion, "Unknown")}",
            $"Synced games: {status.GameCount:N0}",
            $"Active matches: {status.MatchCount:N0}",
            $"Last sync: {FormatLocalTime(status.LastSyncedAt)}",
            $"Last match: {FormatLocalTime(status.LastMatchedAt)}"
        });
        _jobLabel.Text = string.Join(Environment.NewLine, new[]
        {
            $"Job: {Blank(job.JobId, "None")}",
            $"Status: {job.Status}",
            $"Progress: {job.ProcessedGames:N0} / {job.TotalGames:N0} ({percent}%)",
            $"Imported: {job.ImportedGames:N0}",
            $"Matched: {job.MatchedGames:N0}",
            $"Manuals: {job.ManualMatchedGames:N0}",
            $"Guides: {job.StrategyGuideMatchedGames:N0}",
            $"Magazines: {job.MagazineMatchedGames:N0}",
            $"Ambiguous: {job.AmbiguousMatches:N0}",
            $"Missing: {job.MissingGames:N0}",
            job.Message ?? string.Empty
        });
        _progressBar.Value = job.TotalGames > 0 ? Math.Min(100, Math.Max(0, (int)Math.Round(percent))) : 0;
        ApplyButtonState();
    }

    private static string BuildStatusText(GuideVaultStatusResult status, GuideVaultSyncJobStatus job)
    {
        var percent = job.TotalGames > 0 ? Math.Round((double)job.ProcessedGames / job.TotalGames * 100, 1) : 0;
        return $"Status loaded. Plugin {ConnectorConstants.PluginVersion}; GuideVault server {Blank(status.Version, "Unknown")}; last plugin sync {Blank(status.PluginVersion, "Unknown")}; synced games {status.GameCount:N0}; active matches {status.MatchCount:N0}; last sync {FormatLocalTime(status.LastSyncedAt)}; job {Blank(job.JobId, "None")} {job.Status}; progress {job.ProcessedGames:N0}/{job.TotalGames:N0} ({percent}%).";
    }

    private static string FormatLocalTime(DateTimeOffset? value)
    {
        if (value is null || value.Value == DateTimeOffset.MinValue) return "Never";
        return value.Value.LocalDateTime.ToString("g");
    }

    private void LoadSettingsIntoControls()
    {
        var settings = SettingsStore.Load();
        _urlText.Text = settings.GuideVaultUrl;
        EnsureOpenModeItems();
        _openModeCombo.SelectedIndex = settings.OpenInEmbeddedWindow ? 0 : settings.OpenInDefaultBrowser ? 1 : 2;
        _readerMaximizedCheck.Checked = settings.OpenReaderMaximized;
        _readerFullscreenCheck.Checked = settings.OpenReaderFullscreen;
        _browserBridgeCheck.Checked = settings.UseBrowserLoginBridge;
        _usernameText.Text = settings.GuideVaultUsername;
        _emailText.Text = settings.GuideVaultEmail;
        _passwordText.Text = settings.GuideVaultPassword;
        _alternateNamesCheck.Checked = settings.IncludeAlternateNames;
        _customFieldsCheck.Checked = settings.IncludeCustomFields;
        _timeoutBox.Value = Math.Clamp(settings.TimeoutSeconds, 10, 600);
    }

    private void SaveSettingsFromControls()
    {
        EnsureOpenModeItems();
        var openMode = _openModeCombo.SelectedIndex < 0 ? 0 : _openModeCombo.SelectedIndex;
        var currentSettings = SettingsStore.Load();
        var settings = new GuideVaultConnectorSettings
        {
            GuideVaultUrl = _urlText.Text,
            OpenInEmbeddedWindow = openMode == 0,
            OpenInDefaultBrowser = openMode == 1,
            UseBrowserLoginBridge = _browserBridgeCheck.Checked,
            OpenReaderMaximized = _readerMaximizedCheck.Checked,
            OpenReaderFullscreen = _readerFullscreenCheck.Checked,
            GuideVaultUsername = _usernameText.Text,
            GuideVaultEmail = _emailText.Text,
            GuideVaultPassword = _passwordText.Text,
            IncludeAlternateNames = _alternateNamesCheck.Checked,
            IncludeCustomFields = _customFieldsCheck.Checked,
            TimeoutSeconds = (int)_timeoutBox.Value,
            MaxGamesToSync = 0,
            LimitManualSyncToSelectedPlatforms = currentSettings.LimitManualSyncToSelectedPlatforms,
            ManualSyncPlatforms = currentSettings.ManualSyncPlatforms,
            LimitStrategyGuideSyncToSelectedPlatforms = currentSettings.LimitStrategyGuideSyncToSelectedPlatforms,
            StrategyGuideSyncPlatforms = currentSettings.StrategyGuideSyncPlatforms,
            LimitMagazineSyncToSelectedPlatforms = currentSettings.LimitMagazineSyncToSelectedPlatforms,
            MagazineSyncPlatforms = currentSettings.MagazineSyncPlatforms,
            ForceGuideVaultBadgeOnAllGames = false
        };
        SettingsStore.Save(settings);
        LoadSettingsIntoControls();
        AppendLog($"Settings saved: {SettingsStore.SettingsPath}");
    }

    private void UpdateSelectedGame(IGame? game)
    {
        _selectedGame = game ?? _selectedGame;
        if (_selectedGame is null)
        {
            _selectedGameLabel.Text = string.Empty;
            ApplyButtonState();
            return;
        }

        _selectedGameLabel.Text = $"Selected: {_selectedGame.Title}  •  {_selectedGame.Platform}";
        ApplyButtonState();
    }


    private void EnsureOpenModeItems()
    {
        if (_openModeCombo.Items.Count > 0) return;
        _openModeCombo.Items.Add("Embedded WebView2 window");
        _openModeCombo.Items.Add("Default browser");
        _openModeCombo.Items.Add("Active GuideVault browser tab");
        _openModeCombo.SelectedIndex = 0;
    }

    private void SelectTab(string title)
    {
        if (_tabs is null) return;
        foreach (TabPage page in _tabs.TabPages)
        {
            if (string.Equals(page.Text, title, StringComparison.OrdinalIgnoreCase))
            {
                _tabs.SelectedTab = page;
                return;
            }
        }
    }

    private void AppendLog(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(AppendLog), message);
            return;
        }
        _log.AppendText($"[{DateTime.Now:T}] {message}{Environment.NewLine}");
        _log.ScrollToCaret();
    }

    private static string CleanPlatform(object? value) => value?.ToString()?.Trim() ?? string.Empty;

    private void SetBusy(bool busy)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<bool>(SetBusy), busy);
            return;
        }

        _isBusy = busy;
        ApplyButtonState();
    }

    private void ApplyButtonState()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(ApplyButtonState));
            return;
        }

        // Avoid setting a form-wide wait cursor. LaunchBox can ask badge/menu plugins for state while the
        // connector is open, and a global wait cursor makes the whole app feel like it is constantly busy.
        Cursor = Cursors.Default;
        UseWaitCursor = false;

        var idle = !_isBusy;
        var syncCanStart = idle && !_syncJobRunning;
        _syncButton.Enabled = syncCanStart;
        _statusButton.Enabled = idle;
        _cancelButton.Enabled = idle && _syncJobRunning;
        _testButton.Enabled = idle;
        _manualButton.Enabled = idle && _selectedGame is not null;
        _strategyButton.Enabled = idle && _selectedGame is not null;
        _magazineButton.Enabled = idle && _selectedGame is not null;
        _refreshManualPlatformsButton.Enabled = idle;
        _saveManualPlatformsButton.Enabled = idle;
        _syncManualPlatformsButton.Enabled = syncCanStart;
        _syncAllPlatformsButton.Enabled = syncCanStart;
        _refreshStrategyGuidesButton.Enabled = idle;
        _syncSelectedStrategyGuidesButton.Enabled = syncCanStart;
        _syncAllStrategyGuidesButton.Enabled = syncCanStart;
        _openStrategyGuideReviewButton.Enabled = idle;
        _refreshMagazinesButton.Enabled = idle;
        _syncSelectedMagazinesButton.Enabled = syncCanStart;
        _syncAllMagazinesButton.Enabled = syncCanStart;
        _openMagazineReviewButton.Enabled = idle;
    }

    private void DragWindow(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, WmNclButtonDown, HtCaption, 0);
    }

    private sealed class GuideVaultTabControl : TabControl
    {
        private const int WmPaint = 0x000F;

        public GuideVaultTabControl()
        {
            DoubleBuffered = true;
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg == WmPaint && !IsDisposed && TabCount > 0)
                PaintGuideVaultTabStrip();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        private void PaintGuideVaultTabStrip()
        {
            using var g = CreateGraphics();
            var stripHeight = Math.Max(ItemSize.Height + 6, 40);
            var fullStrip = new Rectangle(0, 0, Width, stripHeight);
            var lastTab = GetTabRect(TabCount - 1);
            var fillLeft = Math.Min(Width, Math.Max(0, lastTab.Right + 2));

            if (fillLeft < Width)
            {
                using var brush = new LinearGradientBrush(fullStrip, AccentStripLeft, AccentStripRight, LinearGradientMode.Horizontal);
                g.FillRectangle(brush, new Rectangle(fillLeft, 0, Width - fillLeft, stripHeight));
            }

            using var topPen = new Pen(BorderSoft);
            using var glowPen = new Pen(Color.FromArgb(56, 173, 255));
            g.DrawLine(topPen, 0, 0, Width, 0);
            g.DrawLine(glowPen, 0, stripHeight - 1, Width, stripHeight - 1);
        }
    }

    private static void DrawGuideVaultTab(object? sender, DrawItemEventArgs e)
    {
        if (sender is not TabControl tabs) return;
        var selected = e.Index == tabs.SelectedIndex;
        var rect = tabs.GetTabRect(e.Index);
        rect.Inflate(-2, -2);

        using var background = new LinearGradientBrush(
            rect,
            selected ? Color.FromArgb(0, 145, 255) : Color.FromArgb(6, 31, 56),
            selected ? Color.FromArgb(20, 77, 190) : Color.FromArgb(2, 18, 34),
            LinearGradientMode.Vertical);
        using var border = new Pen(selected ? AccentGlow : BorderSoft);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillRectangle(background, rect);
        e.Graphics.DrawRectangle(border, rect);

        TextRenderer.DrawText(
            e.Graphics,
            tabs.TabPages[e.Index].Text,
            tabs.Font,
            rect,
            selected ? Color.White : TextSoft,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private static TabPage NewTab(string title) => new(title)
    {
        BackColor = Shell,
        ForeColor = TextMain,
        Padding = new Padding(6)
    };

    private static Panel NewCard(string title)
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(1, 36, 1, 1),
            Margin = new Padding(8),
            BackColor = BorderSoft
        };

        var titleLabel = new Label
        {
            Text = title,
            AutoSize = false,
            Height = 34,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0),
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = PanelAlt,
            Location = new Point(1, 1),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        titleLabel.Width = Math.Max(1, outer.Width - 2);
        outer.Controls.Add(titleLabel);
        titleLabel.BringToFront();

        outer.Resize += (_, _) => titleLabel.Width = Math.Max(1, outer.Width - 2);
        outer.Paint += (_, e) =>
        {
            using var pen = new Pen(BorderSoft);
            e.Graphics.DrawRectangle(pen, 0, 0, outer.Width - 1, outer.Height - 1);
        };

        return outer;
    }

    private static FlowLayoutPanel NewVerticalFlow() => new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoScroll = true,
        Padding = new Padding(14),
        BackColor = Card
    };

    private static FlowLayoutPanel NewHorizontalFlow() => new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = true,
        AutoScroll = false,
        Padding = new Padding(0, 4, 0, 0),
        BackColor = Card
    };

    private static Button NewTitleButton(string text)
    {
        var button = new Button
        {
            Text = text,
            Width = 36,
            Height = 28,
            FlatStyle = FlatStyle.Flat,
            BackColor = TitleShell,
            ForeColor = TextMain,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = BorderSoft;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(21, 50, 80);
        button.FlatAppearance.MouseDownBackColor = Accent2;
        return button;
    }

    private static DataGridView NewGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.FromArgb(4, 11, 20),
            ForeColor = TextMain,
            GridColor = BorderSoft,
            BorderStyle = BorderStyle.FixedSingle,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = true,
            EnableHeadersVisualStyles = false
        };
        grid.ColumnHeadersDefaultCellStyle.BackColor = PanelAlt;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        grid.DefaultCellStyle.BackColor = Color.FromArgb(6, 17, 30);
        grid.DefaultCellStyle.ForeColor = TextMain;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(14, 82, 137);
        grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(9, 24, 40);
        return grid;
    }

    private static Button NewButton(string text, int width = 180)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 34,
            Margin = new Padding(4, 4, 8, 4),
            FlatStyle = FlatStyle.Flat,
            BackColor = Accent,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.25f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = AccentGlow;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(38, 165, 255);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(14, 82, 205);
        return button;
    }

    private static Control FieldRow(string label, Control editor)
    {
        var panel = new TableLayoutPanel
        {
            Width = 500,
            Height = 42,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Card,
            Margin = new Padding(0, 2, 0, 4)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
            ForeColor = TextMain
        }, 0, 0);
        editor.Dock = DockStyle.Fill;
        panel.Controls.Add(editor, 1, 0);
        return panel;
    }

    private static Control CheckRow(CheckBox checkbox)
    {
        var panel = new Panel { Width = 500, Height = 30, BackColor = Card, Margin = new Padding(0, 0, 0, 4) };
        checkbox.Location = new Point(0, 5);
        checkbox.Width = 480;
        checkbox.BackColor = Card;
        panel.Controls.Add(checkbox);
        return panel;
    }


    private static ComboBox NewComboBox() => new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        BackColor = Color.FromArgb(4, 15, 26),
        ForeColor = TextMain,
        FlatStyle = FlatStyle.Flat,
        Font = new Font("Segoe UI", 9.25f)
    };

    private static TextBox NewTextBox() => new()
    {
        BackColor = Color.FromArgb(4, 15, 26),
        ForeColor = TextMain,
        BorderStyle = BorderStyle.FixedSingle,
        Font = new Font("Segoe UI", 9.25f)
    };

    private static CheckBox NewCheckBox(string text) => new()
    {
        Text = text,
        ForeColor = TextMain,
        BackColor = Card,
        Font = new Font("Segoe UI", 9.0f)
    };

    private static NumericUpDown NewNumberBox(int min, int max) => new()
    {
        Minimum = min,
        Maximum = max,
        DecimalPlaces = 0,
        BackColor = Color.FromArgb(4, 15, 26),
        ForeColor = TextMain,
        BorderStyle = BorderStyle.FixedSingle,
        Font = new Font("Segoe UI", 9.25f)
    };

    private static Label NewInfoLabel(string text, int height) => new()
    {
        AutoSize = false,
        Width = 500,
        Height = height,
        Margin = new Padding(0, 4, 0, 6),
        Text = text,
        ForeColor = TextSoft,
        BackColor = Card,
        Font = new Font("Segoe UI", 9.0f)
    };

    private static string Blank(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
