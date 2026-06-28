using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace GuideVaultReaderLauncher;

internal sealed class GuideVaultReaderForm : Form, IMessageFilter
{
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);

    private readonly string _initialUrl;
    private readonly string _requestedTitle;
    private readonly string _settingsPath;
    private readonly string _pluginDirectory;
    private readonly string _requestedTargetUrl;
    private readonly GuideVaultLauncherSettings _settings;
    private readonly WebView2 _webView;
    private readonly ToolStrip _toolStrip;
    private readonly ToolStripButton _backButton;
    private readonly ToolStripButton _forwardButton;
    private readonly ToolStripButton _refreshButton;
    private readonly ToolStripButton _homeButton;
    private readonly ToolStripButton _openExternalButton;
    private readonly ToolStripButton _fullscreenButton;
    private readonly ToolStripTextBox _addressText;
    private readonly ToolStripLabel _statusLabel;

    private string _baseOrigin = string.Empty;
    private string _effectiveInitialUrl = string.Empty;
    private string _lastRequestedUrl = string.Empty;
    private string _desiredTargetUrl = string.Empty;
    private bool _initialNavigateStarted;
    private int _automaticRetryCount;
    private int _autoLoginAttemptCount;
    private int _postLoginRetryCount;
    private bool _loginSubmitInProgress;
    private bool _targetReached;
    private bool _closeRequested;
    private bool _fullscreenActive;
    private bool _readerFocusFullscreenRequested;
    private bool _messageFilterInstalled;
    private bool _escapeScriptInstalled;
    private System.Drawing.Rectangle _restoreBounds;
    private FormBorderStyle _restoreBorderStyle;
    private FormWindowState _restoreWindowState;

    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;

    public GuideVaultReaderForm(string initialUrl, string title, string settingsPath, string pluginDirectory, string requestedTargetUrl)
    {
        _initialUrl = initialUrl ?? string.Empty;
        _requestedTitle = string.IsNullOrWhiteSpace(title) ? "GuideVault" : title.Trim();
        _settingsPath = settingsPath ?? string.Empty;
        _pluginDirectory = pluginDirectory ?? string.Empty;
        _requestedTargetUrl = requestedTargetUrl ?? string.Empty;
        _settings = GuideVaultLauncherSettings.Load(_settingsPath, _pluginDirectory);
        _effectiveInitialUrl = _initialUrl;
        _desiredTargetUrl = !string.IsNullOrWhiteSpace(_requestedTargetUrl)
            ? NormalizeUrl(_requestedTargetUrl)
            : NormalizeUrl(GuideVaultLoginBridgeClient.ExtractTargetUrl(_initialUrl) ?? _initialUrl);

        Text = _requestedTitle;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        ShowInTaskbar = true;
        Width = 1480;
        Height = 940;
        MinimumSize = new System.Drawing.Size(1120, 720);
        FormBorderStyle = FormBorderStyle.Sizable;
        WindowState = FormWindowState.Normal;

        _webView = new WebView2 { Dock = DockStyle.Fill };

        _toolStrip = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };
        _backButton = new ToolStripButton("Back");
        _forwardButton = new ToolStripButton("Forward");
        _refreshButton = new ToolStripButton("Refresh");
        _homeButton = new ToolStripButton("Home");
        _openExternalButton = new ToolStripButton("Open in Browser");
        _fullscreenButton = new ToolStripButton("Fullscreen");
        _addressText = new ToolStripTextBox { AutoSize = false, Width = 680 };
        _statusLabel = new ToolStripLabel("Ready.");

        _backButton.Click += (s, e) => { if (_webView.CanGoBack) _webView.GoBack(); };
        _forwardButton.Click += (s, e) => { if (_webView.CanGoForward) _webView.GoForward(); };
        _refreshButton.Click += (s, e) => SafeReload();
        _homeButton.Click += (s, e) =>
        {
            if (!string.IsNullOrWhiteSpace(_baseOrigin))
                SafeNavigate(_baseOrigin + "/", "home button");
        };
        _openExternalButton.Click += (s, e) => OpenExternal();
        _fullscreenButton.Click += (s, e) => ToggleFullscreen();
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.F11)
            {
                e.SuppressKeyPress = true;
                ToggleFullscreen();
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                Close();
            }
        };
        _addressText.KeyDown += (s, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            SafeNavigate(NormalizeUrl(_addressText.Text), "address bar");
        };

        _toolStrip.Items.Add(_backButton);
        _toolStrip.Items.Add(_forwardButton);
        _toolStrip.Items.Add(_refreshButton);
        _toolStrip.Items.Add(_homeButton);
        _toolStrip.Items.Add(_openExternalButton);
        _toolStrip.Items.Add(_fullscreenButton);
        _toolStrip.Items.Add(new ToolStripSeparator());
        _toolStrip.Items.Add(_addressText);
        _toolStrip.Items.Add(new ToolStripSeparator());
        _toolStrip.Items.Add(_statusLabel);

        Controls.Add(_webView);
        Controls.Add(_toolStrip);

        InstallMessageFilter();

        Shown += async (s, e) =>
        {
            if (_settings.OpenReaderFullscreen)
                SetFullscreen(true);
            else if (_settings.OpenReaderMaximized)
                WindowState = FormWindowState.Maximized;

            await InitializeAsync();
        };
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _closeRequested = true;
        RemoveMessageFilter();
        _webView.Dispose();
        base.OnFormClosed(e);
    }

    public bool PreFilterMessage(ref Message m)
    {
        if (_closeRequested) return false;
        if (m.Msg != WmKeyDown && m.Msg != WmSysKeyDown) return false;

        var keyCode = (Keys)((int)m.WParam & 0xFFFF);
        if (keyCode == Keys.Escape)
        {
            LauncherLogger.Log("Escape intercepted by message filter; closing GuideVault reader window.");
            BeginInvoke((Action)Close);
            return true;
        }

        if (keyCode == Keys.F11)
        {
            LauncherLogger.Log("F11 intercepted by message filter; toggling fullscreen.");
            BeginInvoke((Action)ToggleFullscreen);
            return true;
        }

        return false;
    }

    private void InstallMessageFilter()
    {
        if (_messageFilterInstalled) return;
        Application.AddMessageFilter(this);
        _messageFilterInstalled = true;
        LauncherLogger.Log("GuideVault reader message filter installed.");
    }

    private void RemoveMessageFilter()
    {
        if (!_messageFilterInstalled) return;
        Application.RemoveMessageFilter(this);
        _messageFilterInstalled = false;
        LauncherLogger.Log("GuideVault reader message filter removed.");
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if ((keyData & Keys.KeyCode) == Keys.Escape)
        {
            LauncherLogger.Log("Escape pressed; closing GuideVault reader window.");
            Close();
            return true;
        }

        if ((keyData & Keys.KeyCode) == Keys.F11)
        {
            ToggleFullscreen();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void ToggleFullscreen() => SetFullscreen(!_fullscreenActive);

    private void SetFullscreen(bool fullscreen)
    {
        if (fullscreen == _fullscreenActive)
        {
            if (fullscreen) _ = RequestReaderFocusFullscreenAsync("fullscreen already active");
            return;
        }

        if (fullscreen)
        {
            _restoreBounds = Bounds;
            _restoreBorderStyle = FormBorderStyle;
            _restoreWindowState = WindowState;
            WindowState = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.None;
            Bounds = Screen.FromControl(this).Bounds;
            _toolStrip.Visible = false;
            _fullscreenButton.Text = "Exit Fullscreen";
            _fullscreenActive = true;
            LauncherLogger.Log("Window fullscreen enabled.");
            _ = RequestReaderFocusFullscreenAsync("window fullscreen enabled");
            return;
        }

        _ = ExitReaderFocusFullscreenAsync("window fullscreen disabled");
        _toolStrip.Visible = true;
        FormBorderStyle = _restoreBorderStyle;
        if (!_restoreBounds.IsEmpty) Bounds = _restoreBounds;
        WindowState = _restoreWindowState;
        _fullscreenButton.Text = "Fullscreen";
        _fullscreenActive = false;
        LauncherLogger.Log("Window fullscreen disabled.");
    }

    private void ScheduleReaderFocusFullscreen(string reason, int delayMilliseconds = 650)
    {
        if (_closeRequested || !_settings.OpenReaderFullscreen) return;
        var timer = new System.Windows.Forms.Timer { Interval = Math.Max(100, delayMilliseconds) };
        timer.Tick += async (s, e) =>
        {
            timer.Stop();
            timer.Dispose();
            await RequestReaderFocusFullscreenAsync(reason).ConfigureAwait(true);
        };
        timer.Start();
    }

    private async Task RequestReaderFocusFullscreenAsync(string reason)
    {
        if (_closeRequested || _webView.CoreWebView2 is null) return;

        try
        {
            // GuideVault has two different fullscreen concepts:
            // 1. The helper window can be borderless/fullscreen.
            // 2. The web reader itself needs its focus/fullscreen layout so the
            //    sidebar/topbar/library shell disappear.
            // The LaunchBox fullscreen option should mean both, matching the in-app
            // GuideVault reader fullscreen view rather than a fullscreen shell.
            var script = """
                (async () => {
                  const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
                  const isReaderVisible = () => {
                    const view = document.getElementById('readerView');
                    const stage = document.getElementById('readerStage');
                    const hidden = el => !el || el.classList.contains('hidden') || getComputedStyle(el).display === 'none' || getComputedStyle(el).visibility === 'hidden';
                    return !!view && !hidden(view) && !!stage && !hidden(stage);
                  };
                  const refresh = () => {
                    try { window.refreshReaderBookSize?.(); } catch {}
                    try { window.scheduleReaderFullscreenLayoutRefresh?.(); } catch {}
                    try { window.renderSpread?.(window.state?.reader?.index || 0, { preserveSize: false }); } catch {}
                  };
                  for (let i = 0; i < 40; i++) {
                    if (isReaderVisible()) {
                      try {
                        if (typeof window.enterReaderFullscreenFromHomeAssistant === 'function') {
                          await window.enterReaderFullscreenFromHomeAssistant();
                        } else if (typeof window.setReaderVirtualFullscreen === 'function') {
                          window.setReaderVirtualFullscreen(true);
                        } else {
                          document.getElementById('readerView')?.classList.add('fullscreen-reader');
                          document.body.classList.add('reader-is-fullscreen');
                        }
                      } catch {
                        document.getElementById('readerView')?.classList.add('fullscreen-reader');
                        document.body.classList.add('reader-is-fullscreen');
                      }
                      refresh();
                      setTimeout(refresh, 150);
                      setTimeout(refresh, 500);
                      return 'reader-focus-fullscreen-entered';
                    }
                    await sleep(250);
                  }
                  return 'reader-not-visible';
                })();
                """;

            var result = await _webView.CoreWebView2.ExecuteScriptAsync(script).ConfigureAwait(true);
            _readerFocusFullscreenRequested = result.Contains("entered", StringComparison.OrdinalIgnoreCase);
            LauncherLogger.Log("Reader focus fullscreen request: reason=" + reason + " result=" + result);
        }
        catch (Exception ex)
        {
            LauncherLogger.Log("Reader focus fullscreen request failed: " + ex.Message);
        }
    }

    private async Task ExitReaderFocusFullscreenAsync(string reason)
    {
        if (_closeRequested || _webView.CoreWebView2 is null) return;

        try
        {
            var script = """
                (async () => {
                  try {
                    if (typeof window.exitReaderFullscreenOnly === 'function') {
                      await window.exitReaderFullscreenOnly();
                    } else {
                      document.getElementById('readerView')?.classList.remove('fullscreen-reader');
                      document.body.classList.remove('reader-is-fullscreen');
                    }
                    return 'reader-focus-fullscreen-exited';
                  } catch {
                    document.getElementById('readerView')?.classList.remove('fullscreen-reader');
                    document.body.classList.remove('reader-is-fullscreen');
                    return 'reader-focus-fullscreen-exited-fallback';
                  }
                })();
                """;

            var result = await _webView.CoreWebView2.ExecuteScriptAsync(script).ConfigureAwait(true);
            LauncherLogger.Log("Reader focus fullscreen exit: reason=" + reason + " result=" + result);
        }
        catch (Exception ex)
        {
            LauncherLogger.Log("Reader focus fullscreen exit failed: " + ex.Message);
        }
    }

    private async Task InitializeAsync()
    {
        try
        {
            LauncherLogger.Log("InitializeAsync start.");
            PrepareNativeWebViewLoaderPath();

            var userDataFolder = ResolveWebView2UserDataFolder();

            Directory.CreateDirectory(userDataFolder);
            LauncherLogger.Log("WebView2 userDataFolder=" + userDataFolder);

            var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await _webView.EnsureCoreWebView2Async(environment);

            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            _webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            _webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            await InstallEscapeCloseScriptAsync().ConfigureAwait(true);
            _webView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
            _webView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
            _webView.CoreWebView2.DOMContentLoaded += CoreWebView2_DOMContentLoaded;
            _webView.CoreWebView2.SourceChanged += (s, e) => UpdateAddress();
            _webView.CoreWebView2.DocumentTitleChanged += (s, e) =>
            {
                try
                {
                    var docTitle = _webView.CoreWebView2.DocumentTitle;
                    Text = string.IsNullOrWhiteSpace(docTitle) ? _requestedTitle : "GuideVault - " + docTitle;
                }
                catch
                {
                    // Ignore title update failures.
                }
            };

            LauncherLogger.Log(
                "Launch auth settings: settingsPath=" + _settingsPath +
                " pluginDirectory=" + ResolvePluginDirectory() +
                " useBridge=" + _settings.UseBrowserLoginBridge +
                " hasProfile=" + _settings.HasBrowserLoginProfile +
                " maximized=" + _settings.OpenReaderMaximized +
                " fullscreen=" + _settings.OpenReaderFullscreen +
                " usernameSet=" + !string.IsNullOrWhiteSpace(_settings.GuideVaultUsername) +
                " emailSet=" + !string.IsNullOrWhiteSpace(_settings.GuideVaultEmail) +
                " passwordSet=" + !string.IsNullOrWhiteSpace(_settings.GuideVaultPassword));

            _effectiveInitialUrl = await GuideVaultLoginBridgeClient
                .BuildAuthenticatedLaunchUrlAsync(_settings, _initialUrl, _desiredTargetUrl, "initial")
                .ConfigureAwait(true);

            _baseOrigin = GetOrigin(_effectiveInitialUrl);
            if (string.IsNullOrWhiteSpace(_baseOrigin))
                _baseOrigin = GetOrigin(_desiredTargetUrl);
            if (string.IsNullOrWhiteSpace(_effectiveInitialUrl))
                throw new InvalidOperationException("The GuideVault URL is blank.");

            await NavigateInitialUrlAsync();
        }
        catch (Exception ex)
        {
            LauncherLogger.Log("Initialize failed: " + ex);
            MessageBox.Show(
                "GuideVault WebView2 launcher failed to initialize." + Environment.NewLine + Environment.NewLine + ex.Message,
                "GuideVault Reader Launcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            Close();
        }
    }

    private string ResolveWebView2UserDataFolder()
    {
        // Do not put the WebView2 profile under LaunchBox\Plugins. LaunchBox
        // recursively scans plugin folders for DLLs, and the WebView2 profile can
        // create native DLLs under EBWebView that LaunchBox then tries to load as
        // managed plugins. Keep it relative, but relative to the external helper
        // folder under LaunchBox\ThirdParty instead.
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "WebView2Profile"));
    }

    private string ResolvePluginDirectory()
    {
        // Preferred path: the LaunchBox plugin passes its own folder. This is
        // still used for settings/logging, but not for the WebView2 user profile.
        if (!string.IsNullOrWhiteSpace(_pluginDirectory))
            return Path.GetFullPath(_pluginDirectory);

        // Fallback: settings.json is also kept in LaunchBox\Plugins\GuideVault.
        if (!string.IsNullOrWhiteSpace(_settingsPath))
        {
            try
            {
                var settingsFullPath = Path.GetFullPath(_settingsPath);
                var settingsDirectory = Path.GetDirectoryName(settingsFullPath);
                if (!string.IsNullOrWhiteSpace(settingsDirectory))
                    return settingsDirectory;
            }
            catch
            {
                // Fall through to deployed-layout fallback.
            }
        }

        // Deployed helper layout fallback:
        // LaunchBox\ThirdParty\GuideVaultReaderLauncher -> LaunchBox\Plugins\GuideVault
        try
        {
            var helperDirectory = new DirectoryInfo(AppContext.BaseDirectory);
            var launchBoxRoot = helperDirectory.Parent?.Parent;
            if (launchBoxRoot is not null)
                return Path.Combine(launchBoxRoot.FullName, "Plugins", "GuideVault");
        }
        catch
        {
            // Last-resort fallback below.
        }

        return Path.Combine(AppContext.BaseDirectory, "GuideVaultPlugin");
    }

    private void PrepareNativeWebViewLoaderPath()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var nativeDir = Path.Combine(baseDir, "runtimes", "win-x64", "native");
            if (!Directory.Exists(nativeDir))
            {
                LauncherLogger.Log("Native WebView2 loader directory not found, using default resolution. Checked=" + nativeDir);
                return;
            }

            SetDllDirectory(nativeDir);
            LauncherLogger.Log("SetDllDirectory=" + nativeDir);
        }
        catch (Exception ex)
        {
            LauncherLogger.Log("PrepareNativeWebViewLoaderPath failed: " + ex.Message);
        }
    }

    private async Task InstallEscapeCloseScriptAsync()
    {
        if (_escapeScriptInstalled || _webView.CoreWebView2 is null) return;

        try
        {
            const string script = """
                (() => {
                  if (window.__guideVaultLaunchBoxEscapeCloseInstalled) return;
                  window.__guideVaultLaunchBoxEscapeCloseInstalled = true;
                  const sendClose = () => {
                    try { window.chrome?.webview?.postMessage({ type: 'guidevault-close-window', source: 'escape' }); } catch {}
                  };
                  const sendFullscreen = () => {
                    try { window.chrome?.webview?.postMessage({ type: 'guidevault-toggle-fullscreen', source: 'f11' }); } catch {}
                  };
                  window.addEventListener('keydown', event => {
                    if (event.key === 'Escape' || event.code === 'Escape' || event.keyCode === 27) {
                      event.preventDefault();
                      event.stopPropagation();
                      event.stopImmediatePropagation?.();
                      sendClose();
                    } else if (event.key === 'F11' || event.code === 'F11' || event.keyCode === 122) {
                      event.preventDefault();
                      event.stopPropagation();
                      event.stopImmediatePropagation?.();
                      sendFullscreen();
                    }
                  }, true);
                })();
                """;

            await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(script).ConfigureAwait(true);
            _escapeScriptInstalled = true;
            LauncherLogger.Log("Escape/F11 WebView2 bridge script installed.");
        }
        catch (Exception ex)
        {
            LauncherLogger.Log("Escape/F11 WebView2 bridge script install failed: " + ex.Message);
        }
    }

    private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.WebMessageAsJson ?? string.Empty;
            if (json.Contains("guidevault-close-window", StringComparison.OrdinalIgnoreCase))
            {
                LauncherLogger.Log("Escape received from WebView2 script bridge; closing GuideVault reader window.");
                Close();
                return;
            }

            if (json.Contains("guidevault-toggle-fullscreen", StringComparison.OrdinalIgnoreCase))
            {
                LauncherLogger.Log("F11 received from WebView2 script bridge; toggling fullscreen.");
                ToggleFullscreen();
            }
        }
        catch (Exception ex)
        {
            LauncherLogger.Log("WebView2 message handling failed: " + ex.Message);
        }
    }

    private async Task NavigateInitialUrlAsync()
    {
        if (_initialNavigateStarted) return;
        if (_webView.CoreWebView2 is null) return;

        _initialNavigateStarted = true;
        await Task.Delay(250);
        SafeNavigate(_effectiveInitialUrl, "initial");
    }

    private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        _statusLabel.Text = "Loading...";
        _addressText.Text = e.Uri ?? string.Empty;
        LauncherLogger.Log("NavigationStarting: " + (e.Uri ?? string.Empty));
        UpdateButtons();
    }

    private async void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        try
        {
            var currentUrl = _webView.Source?.ToString() ?? string.Empty;
            _statusLabel.Text = e.IsSuccess ? "Ready." : "Navigation failed: " + e.WebErrorStatus;
            UpdateAddress();
            UpdateButtons();

            LauncherLogger.Log(
                "NavigationCompleted success=" + e.IsSuccess +
                " status=" + e.WebErrorStatus +
                " url=" + currentUrl);

            if (!e.IsSuccess)
            {
                await TryAutomaticRetryAsync("navigation failed");
                return;
            }

            if (UrlsMatch(currentUrl, _desiredTargetUrl))
            {
                _targetReached = true;
                LauncherLogger.Log("Requested target reached.");
                ScheduleReaderFocusFullscreen("requested target reached", 450);
                return;
            }

            if (!_targetReached && LooksLikeLoginUrl(currentUrl))
            {
                var submitted = await TryAutoSubmitLoginAsync(currentUrl).ConfigureAwait(true);
                if (submitted) return;
            }

            if (!_targetReached && ShouldRetryTargetNavigation(currentUrl))
            {
                await TryAutomaticRetryAsync("landed away from requested target");
                return;
            }
        }
        catch (Exception ex)
        {
            LauncherLogger.Log("NavigationCompleted handler failed: " + ex);
        }
    }

    private bool ShouldRetryTargetNavigation(string currentUrl)
    {
        var requestedTarget = !string.IsNullOrWhiteSpace(_desiredTargetUrl) ? _desiredTargetUrl : _lastRequestedUrl;
        if (string.IsNullOrWhiteSpace(currentUrl) || string.IsNullOrWhiteSpace(requestedTarget))
            return false;

        if (UrlsMatch(currentUrl, requestedTarget))
            return false;

        // This recovers when a direct reader launch authenticates successfully but lands on
        // home/create/login instead of the matched manual/guide target.
        var normalized = NormalizeComparableUrl(currentUrl);
        if (normalized.Contains("/login", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/sign-in", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/create", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith("/home", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(_baseOrigin) &&
            (UrlsMatch(normalized, _baseOrigin) || UrlsMatch(normalized, _baseOrigin + "/")))
            return true;

        return false;
    }

    private async Task TryAutomaticRetryAsync(string reason)
    {
        if (_closeRequested || _webView.CoreWebView2 is null) return;
        var retryTarget = !string.IsNullOrWhiteSpace(_desiredTargetUrl) ? _desiredTargetUrl : _lastRequestedUrl;
        if (string.IsNullOrWhiteSpace(retryTarget)) return;
        if (_automaticRetryCount >= 2) return;

        _automaticRetryCount++;
        _lastRequestedUrl = retryTarget;
        LauncherLogger.Log("Automatic retry " + _automaticRetryCount + ": " + reason + " -> " + retryTarget);
        await Task.Delay(650);

        if (_closeRequested || _webView.CoreWebView2 is null) return;
        _webView.CoreWebView2.Navigate(retryTarget);
    }


    private async void CoreWebView2_DOMContentLoaded(object? sender, CoreWebView2DOMContentLoadedEventArgs e)
    {
        try
        {
            var currentUrl = _webView.Source?.ToString() ?? string.Empty;
            if (_settings.OpenReaderFullscreen && !_readerFocusFullscreenRequested)
                ScheduleReaderFocusFullscreen("DOMContentLoaded", 250);

            if (!_targetReached && LooksLikeLoginUrl(currentUrl))
                await TryAutoSubmitLoginAsync(currentUrl).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            LauncherLogger.Log("DOMContentLoaded login handler failed: " + ex.Message);
        }
    }

    private async Task<bool> TryAutoSubmitLoginAsync(string currentUrl)
    {
        if (_closeRequested || _webView.CoreWebView2 is null) return false;

        if (!_settings.UseBrowserLoginBridge)
        {
            LauncherLogger.Log("WebView2 form auto-login skipped: useBrowserLoginBridge is false.");
            return false;
        }

        if (!_settings.HasBrowserLoginProfile)
        {
            LauncherLogger.Log(
                "WebView2 form auto-login skipped: missing username/email or password. " +
                "usernameSet=" + !string.IsNullOrWhiteSpace(_settings.GuideVaultUsername) +
                " emailSet=" + !string.IsNullOrWhiteSpace(_settings.GuideVaultEmail) +
                " passwordSet=" + !string.IsNullOrWhiteSpace(_settings.GuideVaultPassword));
            return false;
        }

        if (_autoLoginAttemptCount >= 3 || _loginSubmitInProgress) return false;

        _autoLoginAttemptCount++;
        _loginSubmitInProgress = true;

        LauncherLogger.Log(
            "Attempting WebView2 form auto-login. attempt=" + _autoLoginAttemptCount +
            " url=" + currentUrl +
            " usernameSet=" + !string.IsNullOrWhiteSpace(_settings.GuideVaultUsername) +
            " emailSet=" + !string.IsNullOrWhiteSpace(_settings.GuideVaultEmail));

        var script = BuildAutoLoginScript(
            _settings.GuideVaultUsername,
            _settings.GuideVaultEmail,
            _settings.GuideVaultPassword,
            _desiredTargetUrl);
        try
        {
            // Blazor/Razor login forms may finish rendering after NavigationCompleted,
            // so poll the DOM briefly instead of making one immediate attempt.
            for (var poll = 1; poll <= 24; poll++)
            {
                if (_closeRequested || _webView.CoreWebView2 is null) return false;

                await Task.Delay(poll == 1 ? 350 : 500).ConfigureAwait(true);

                var liveUrl = _webView.Source?.ToString() ?? string.Empty;
                if (_targetReached || UrlsMatch(liveUrl, _desiredTargetUrl))
                {
                    _targetReached = true;
                    LauncherLogger.Log("WebView2 form auto-login no longer needed; target reached during DOM polling.");
                    ScheduleReaderFocusFullscreen("target reached during DOM polling", 450);
                    return true;
                }

                if (!LooksLikeLoginUrl(liveUrl))
                {
                    LauncherLogger.Log("WebView2 form auto-login stopped polling because current URL is no longer login-like: " + liveUrl);
                    SchedulePostLoginTargetRetry("left login page during auto-login", 1500);
                    return true;
                }

                string result;
                try
                {
                    result = await _webView.CoreWebView2.ExecuteScriptAsync(script).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    LauncherLogger.Log("WebView2 form auto-login script failed on poll " + poll + ": " + ex.Message);
                    return false;
                }

                LauncherLogger.Log("WebView2 form auto-login poll=" + poll + " result=" + result);

                if (result.Contains("submitted", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(_desiredTargetUrl))
                        _lastRequestedUrl = _desiredTargetUrl;

                    SchedulePostLoginTargetRetry("after form auto-login submit", 2500);
                    SchedulePostLoginTargetRetry("after form auto-login submit second pass", 6500);
                    return true;
                }

                if (result.Contains("blocked-empty", StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            LauncherLogger.Log("WebView2 form auto-login gave up waiting for rendered login inputs.");
            return false;
        }
        finally
        {
            _loginSubmitInProgress = false;
        }
    }

    private void SchedulePostLoginTargetRetry(string reason, int delayMilliseconds)
    {
        if (_closeRequested || string.IsNullOrWhiteSpace(_desiredTargetUrl)) return;
        if (_postLoginRetryCount >= 4) return;

        _postLoginRetryCount++;
        var attempt = _postLoginRetryCount;
        LauncherLogger.Log("Scheduled post-login target retry " + attempt + " in " + delayMilliseconds + "ms: " + reason + " -> " + _desiredTargetUrl);

        var timer = new System.Windows.Forms.Timer { Interval = Math.Max(500, delayMilliseconds) };
        timer.Tick += (s, e) =>
        {
            timer.Stop();
            timer.Dispose();

            if (_closeRequested || _webView.CoreWebView2 is null || _targetReached) return;

            var currentUrl = _webView.Source?.ToString() ?? string.Empty;
            if (UrlsMatch(currentUrl, _desiredTargetUrl))
            {
                _targetReached = true;
                LauncherLogger.Log("Post-login target retry skipped; target already reached.");
                return;
            }

            LauncherLogger.Log("Post-login target retry " + attempt + ": current=" + currentUrl + " target=" + _desiredTargetUrl);
            SafeNavigate(_desiredTargetUrl, "post-login target retry " + attempt);
        };
        timer.Start();
    }

    private static string BuildAutoLoginScript(string username, string email, string password, string targetUrl)
    {
        var usernameJson = JsonSerializer.Serialize(username ?? string.Empty);
        var emailJson = JsonSerializer.Serialize(email ?? string.Empty);
        var passwordJson = JsonSerializer.Serialize(password ?? string.Empty);
        var targetJson = JsonSerializer.Serialize(targetUrl ?? string.Empty);

        return """
            (() => {
              const usernameValue = __USERNAME__;
              const emailValue = __EMAIL__;
              const passwordValue = __PASSWORD__;
              const targetValue = __TARGET__;

              if ((!usernameValue && !emailValue) || !passwordValue) return 'blocked-empty-credentials';

              const visible = el => {
                if (!el || el.disabled || el.type === 'hidden') return false;
                const rect = el.getBoundingClientRect ? el.getBoundingClientRect() : { width: 1, height: 1 };
                const style = window.getComputedStyle ? window.getComputedStyle(el) : null;
                return (!style || (style.visibility !== 'hidden' && style.display !== 'none')) && (rect.width > 0 || rect.height > 0 || el.offsetParent !== null);
              };
              const lower = v => (v || '').toLowerCase();
              const fieldText = el => lower([
                el.name,
                el.id,
                el.autocomplete,
                el.placeholder,
                el.getAttribute('aria-label'),
                el.getAttribute('data-val-required'),
                el.getAttribute('data-testid'),
                el.closest('label')?.innerText,
                document.querySelector(`label[for="${el.id}"]`)?.innerText
              ].join(' '));

              const setValue = (el, value) => {
                if (!el || !value) return false;
                el.focus();
                const proto = Object.getPrototypeOf(el);
                const ownDescriptor = Object.getOwnPropertyDescriptor(el, 'value');
                const protoDescriptor = Object.getOwnPropertyDescriptor(proto, 'value');
                if (protoDescriptor && protoDescriptor.set) protoDescriptor.set.call(el, value);
                else if (ownDescriptor && ownDescriptor.set) ownDescriptor.set.call(el, value);
                else el.value = value;
                el.dispatchEvent(new InputEvent('input', { bubbles: true, composed: true, inputType: 'insertText', data: value }));
                el.dispatchEvent(new Event('change', { bubbles: true, composed: true }));
                el.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, key: 'a' }));
                el.dispatchEvent(new KeyboardEvent('keyup', { bubbles: true, key: 'a' }));
                el.dispatchEvent(new FocusEvent('blur', { bubbles: true }));
                return true;
              };

              const allInputs = Array.from(document.querySelectorAll('input'));
              const passwordInput = allInputs.find(el => visible(el) && lower(el.type) === 'password')
                || document.querySelector('input[name="password"], input[name="Password"], input[name="Input.Password"], input[id*="password" i]');
              if (!passwordInput) return 'no-password-input:inputs=' + allInputs.length;

              const scoreLogin = el => {
                const text = fieldText(el);
                const type = lower(el.type);
                if (type === 'email') return 140;
                if (text.includes('email')) return 130;
                if (text.includes('username') || text.includes('user name')) return 120;
                if (text.includes('user')) return 110;
                if (text.includes('login')) return 95;
                if (text.includes('account')) return 80;
                return ['text', 'email', ''].includes(type) ? 35 : 0;
              };

              const loginCandidates = allInputs
                .filter(el => visible(el) && el !== passwordInput && ['email','text','search',''].includes(lower(el.type)))
                .sort((a, b) => scoreLogin(b) - scoreLogin(a));

              // Fill any separate explicit username/email fields first. The previous
              // version always preferred email when both values existed. That fails on
              // username-based GuideVault installs, where the visible sign-in field is
              // usually Username/UserName. This version chooses based on the actual field.
              let filled = [];
              for (const input of loginCandidates) {
                const text = fieldText(input);
                const type = lower(input.type);
                let value = '';

                if ((type === 'email' || text.includes('email')) && emailValue) value = emailValue;
                else if ((text.includes('username') || text.includes('user name') || text.includes('user')) && usernameValue) value = usernameValue;
                else if (usernameValue) value = usernameValue;
                else value = emailValue;

                if (value && setValue(input, value)) {
                  filled.push((input.name || input.id || type || 'login') + '=' + (value === usernameValue ? 'username' : 'email'));
                  break;
                }
              }

              if (filled.length === 0) return 'no-login-input:inputs=' + allInputs.length;
              if (!setValue(passwordInput, passwordValue)) return 'no-password-set';

              const targetInput = document.querySelector('input[name="target"], input[name="Target"], input[name="returnUrl"], input[name="ReturnUrl"], input[name="return_url"], input[name="redirectUri"], input[name="RedirectUri"]');
              if (targetInput && targetValue) setValue(targetInput, targetValue);

              const form = passwordInput.closest('form') || loginCandidates[0]?.closest('form') || document.querySelector('form');
              const submitCandidates = Array.from((form || document).querySelectorAll('button, input[type="submit"]'))
                .filter(visible)
                .map(el => ({
                  el,
                  text: lower([el.innerText, el.textContent, el.value, el.name, el.id, el.getAttribute('aria-label')].join(' '))
                }))
                .sort((a, b) => {
                  const score = x =>
                    x.text.includes('sign in') ? 120 :
                    x.text.includes('signin') ? 110 :
                    x.text.includes('log in') ? 100 :
                    x.text.includes('login') ? 90 :
                    x.text.includes('submit') ? 70 : 10;
                  return score(b) - score(a);
                });

              const submit = submitCandidates.length > 0 ? submitCandidates[0].el : null;

              if (submit) {
                submit.focus();
                submit.click();
                return 'submitted-click:' + filled.join(',') + ':' + (submit.innerText || submit.value || submit.id || submit.name || 'button');
              }

              if (form && form.requestSubmit) {
                form.requestSubmit();
                return 'submitted-requestSubmit:' + filled.join(',');
              }

              passwordInput.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', code: 'Enter', bubbles: true }));
              passwordInput.dispatchEvent(new KeyboardEvent('keyup', { key: 'Enter', code: 'Enter', bubbles: true }));
              return 'submitted-enter:' + filled.join(',');
            })();
            """
            .Replace("__USERNAME__", usernameJson)
            .Replace("__EMAIL__", emailJson)
            .Replace("__PASSWORD__", passwordJson)
            .Replace("__TARGET__", targetJson);
    }

    private static bool LooksLikeLoginUrl(string url)
    {
        var normalized = NormalizeComparableUrl(url);
        return normalized.Contains("/login", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/sign-in", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/signin", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/account", StringComparison.OrdinalIgnoreCase);
    }

    private void SafeNavigate(string url, string reason)
    {
        if (_webView.CoreWebView2 is null || string.IsNullOrWhiteSpace(url)) return;

        var normalized = NormalizeUrl(url);
        _lastRequestedUrl = normalized;
        _addressText.Text = normalized;
        LauncherLogger.Log("Navigate " + reason + ": " + normalized);
        _webView.CoreWebView2.Navigate(normalized);
    }

    private void SafeReload()
    {
        try
        {
            LauncherLogger.Log("Manual reload.");
            _webView.Reload();
        }
        catch (Exception ex)
        {
            LauncherLogger.Log("Reload failed: " + ex.Message);
        }
    }

    private void OpenExternal()
    {
        try
        {
            var targetUrl = _webView.Source?.ToString() ?? _addressText.Text;
            if (string.IsNullOrWhiteSpace(targetUrl)) return;
            Process.Start(new ProcessStartInfo { FileName = targetUrl, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LauncherLogger.Log("OpenExternal failed: " + ex.Message);
        }
    }

    private void UpdateAddress()
    {
        try
        {
            _addressText.Text = _webView.Source?.ToString() ?? _addressText.Text;
        }
        catch
        {
            // Ignore address update failures.
        }
    }

    private void UpdateButtons()
    {
        _backButton.Enabled = _webView.CanGoBack;
        _forwardButton.Enabled = _webView.CanGoForward;
    }

    private static string NormalizeUrl(string url)
    {
        var trimmed = (url ?? string.Empty).Trim();
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        return "http://" + trimmed;
    }

    private static string NormalizeComparableUrl(string url)
        => (url ?? string.Empty).Trim().TrimEnd('/');

    private static bool UrlsMatch(string left, string right)
        => string.Equals(NormalizeComparableUrl(left), NormalizeComparableUrl(right), StringComparison.OrdinalIgnoreCase);

    private static string GetOrigin(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? uri.GetLeftPart(UriPartial.Authority).TrimEnd('/')
            : string.Empty;
    }
}
