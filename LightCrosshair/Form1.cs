using System.Runtime.InteropServices; // ensure attribute available at top
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;


namespace LightCrosshair
{
    public partial class Form1 : Form
    {
    private NotifyIcon? notifyIcon;
    private ContextMenuStrip? contextMenu;
    private readonly IProfileService _profileService; // unified service
    private readonly AppPreferences _prefs;
        private Color fillColor = Color.Transparent;
        private bool isVisible = true;

        // Performance optimization fields
    // Legacy incremental rendering fields replaced by central renderer
    private CrosshairProfile? _lastRenderedProfile; // retained temporarily for menu diff logic
    private readonly object _renderLock = new object();
    private ICrosshairRenderBackend _renderer = new CrosshairRenderer();
    private Bitmap? _currentFrame; // last produced bitmap copy
    private bool _configDirty = true; // marks need to request new bitmap from renderer
        private float _dpiScaleFactor = 1.0f;

        // Object pooling for graphics resources
        private readonly Dictionary<Color, SolidBrush> _brushCache = new Dictionary<Color, SolidBrush>();
        private readonly Dictionary<(Color, float), Pen> _penCache = new Dictionary<(Color, float), Pen>();

        // Screen recording detection
    private System.Windows.Forms.Timer? _recordingDetectionTimer;
    private System.Windows.Forms.Timer? _fpsTimer;
    private FpsOverlayForm? _fpsOverlayForm;
        private bool _isRecordingDetected = false;
        private bool _wasVisibleBeforeRecording = true;
        private int _recordingDetectionRunning;

        // Constants for message handling
        private const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_TOPMOST = 0x8;
    private const int WS_EX_TOOLWINDOW = 0x00000080; // Hide from Alt-Tab / task switcher
    private const int WS_EX_APPWINDOW = 0x00040000;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    [DllImport("user32.dll", SetLastError = true)] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_NOOWNERZORDER = 0x0200;
        private const uint SWP_NOSENDCHANGING = 0x0400;
    private bool _isExiting;
    private bool _runtimeInitialized;
    private bool _shutdownWatchdogStarted;

    // Autosave centralized in ProfileService now
        private StatusStrip? _statusStrip; // simple status surface for Saved HH:MM:SS
        private ToolStripStatusLabel? _lblSaved;
    private CrosshairProfile CurrentProfile => _profileService.Current;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                cp.ExStyle &= ~WS_EX_APPWINDOW;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation => true;

        public Form1() : this(null) {}
        public Form1(IProfileService? service)
        {
            _profileService = service ?? ProfileService.Instance;
            _prefs = PreferencesStore.Load();
            InitializeComponent();

            // Basic form setup
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.Size = new Size(100, 100);

            // Set transparency - use a color that won't interfere with crosshair colors
            this.BackColor = Color.FromArgb(1, 1, 1); // Very dark color for transparency
            this.TransparencyKey = Color.FromArgb(1, 1, 1);

            // Enable double buffering and performance optimizations
            this.SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor,
                true);

            // Additional performance optimizations
            this.SetStyle(ControlStyles.Selectable, false);

            _profileService.CurrentChanged += Service_CurrentChanged_Handler;
            CrosshairConfig.Instance.SettingsChanged += Config_SettingsChanged_Handler;
            CrosshairConfig.Instance.HotkeysRegistrationRequested += Config_HotkeysRegistrationRequested_Handler;

            // Initialize system tray icon and menu
            InitializeNotifyIcon();
            InitializeContextMenu();

            // Add paint handler (now only blits cached bitmap)
            this.Paint += Form1_Paint_Handler;

            // Initialize DPI awareness
            InitializeDpiAwareness();
            this.DpiChanged += Form1_DpiChanged;

            // Try the low-level Skia backend first, fallback to GDI renderer if unavailable.
            _renderer.Dispose();
            _renderer = CreateRenderBackend();
            _renderer.DpiScale = _dpiScaleFactor;
            _renderer.AntiAlias = _profileService.Current.AntiAlias;

            // Initialize screen recording detection
            InitializeScreenRecordingDetection();

            // Setup minimal status strip (optional visual feedback)
            SetupStatusStrip();

            // Center the form on screen
            CenterCrosshair();
            this.Load += Form1_Load; // keep for compatibility; runtime init is now handle-based
            _profileService.Persisted += ProfileService_Persisted_Handler;

            // Subscribe to application exit events safely
            Application.ApplicationExit += OnApplication_Exit;
            AppDomain.CurrentDomain.ProcessExit += OnAppDomain_ProcessExit;
        }

        private void SetupStatusStrip()
        {
            if (_statusStrip != null) return;
            _statusStrip = new StatusStrip { SizingGrip = false, Visible = false, Dock = DockStyle.None };
            _lblSaved = new ToolStripStatusLabel(" ");
            _statusStrip.Items.Add(_lblSaved);
            Controls.Add(_statusStrip);
            // Leave hidden by default to avoid obstructing gameplay overlay
        }

        private void InitializeRuntimeIfNeeded()
        {
            if (_runtimeInitialized)
            {
                return;
            }

            _runtimeInitialized = true;

            SystemFpsMonitor.Start();
            DisplayManager.StartMonitoring();
            EnsureFpsOverlayForm();
            UpdateFpsOverlaySurface();
            try
            {
                if (_profileService.Profiles.Count == 0)
                    _profileService.InitializeAsync().GetAwaiter().GetResult();
                var current = _profileService.Current;
                isVisible = _prefs.OverlayVisible && CrosshairConfig.Instance.Visible;
                Program.LogDebug($"Startup visibility restore -> prefs={_prefs.OverlayVisible}, config={CrosshairConfig.Instance.Visible}, effective={isVisible}, profileEnabled={current.EnableCustomCrosshair}", nameof(Form1));
                Service_CurrentChanged(current);
                MarkConfigDirty();
                UpdateCrosshairVisibilityState();

            // Hardware-only logic is now enforced globally; no explicit toggling required.
            GammaController.SaveOriginal();
        }
        catch (Exception ex) { Program.LogError(ex, "Form1_Load"); }
    }

        private void Form1_Load(object? sender, EventArgs e)
        {
            InitializeRuntimeIfNeeded();
        }

        private void InitializeDpiAwareness()
        {
            // Get current DPI scaling factor
            using (Graphics g = this.CreateGraphics())
            {
                _dpiScaleFactor = Math.Clamp(g.DpiX / 96.0f, 0.75f, 3.0f); // 96 DPI is the standard
            }
            _renderer.DpiScale = _dpiScaleFactor;
        }

        private void Form1_DpiChanged(object? sender, DpiChangedEventArgs e)
        {
            _dpiScaleFactor = Math.Clamp(e.DeviceDpiNew / 96.0f, 0.75f, 3.0f);
            _renderer.DpiScale = _dpiScaleFactor;
            MarkConfigDirty();
            InvalidateThrottled();
        }

        private Icon LoadApplicationIcon()
        {
            try
            {
                // First, try to load from embedded resources
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceName = "LightCrosshair.assets.icon.ico";

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        return new Icon(stream);
                    }
                }

                // If embedded resource fails, try to load from file system
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "icon.ico");
                if (File.Exists(iconPath))
                {
                    return new Icon(iconPath);
                }

                // If both fail, try alternative embedded resource name
                resourceName = "LightCrosshair.icon.ico";
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        return new Icon(stream);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load application icon: {ex.Message}");
            }

            // Fallback to system icon
            return SystemIcons.Application;
        }

        private SolidBrush? GetCachedBrush(Color color)
        {
            if (color.A == 0) return null; // Don't cache transparent brushes

            if (_brushCache.Count > 50) ClearGraphicsCache();

            if (!_brushCache.TryGetValue(color, out SolidBrush? brush))
            {
                brush = new SolidBrush(color);
                _brushCache[color] = brush;
            }
            return brush;
        }

        private Pen? GetCachedPen(Color color, float width)
        {
            if (color.A == 0) return null; // Don't cache transparent pens

            if (_penCache.Count > 150) ClearGraphicsCache();

            var key = (color, width);
            if (!_penCache.TryGetValue(key, out Pen? pen))
            {
                pen = new Pen(color, width);

                // Enhanced pen settings for better rendering quality
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                // Use high-quality smoothing for better anti-aliasing
                pen.Alignment = PenAlignment.Center;

                _penCache[key] = pen;
            }
            return pen;
        }

        private void ClearGraphicsCache()
        {
            foreach (var brush in _brushCache.Values)
            {
                brush?.Dispose();
            }
            _brushCache.Clear();

            foreach (var pen in _penCache.Values)
            {
                pen?.Dispose();
            }
            _penCache.Clear();
        }

        // Helper methods for pixel-perfect drawing with float coordinates
        private void DrawLineF(Graphics g, Pen pen, float x1, float y1, float x2, float y2)
        {
            g.DrawLine(pen,
                (int)Math.Round(x1), (int)Math.Round(y1),
                (int)Math.Round(x2), (int)Math.Round(y2));
        }

        private void DrawEllipseF(Graphics g, Pen pen, float x, float y, float width, float height)
        {
            g.DrawEllipse(pen,
                (int)Math.Round(x), (int)Math.Round(y),
                (int)Math.Round(width), (int)Math.Round(height));
        }

        private void FillEllipseF(Graphics g, Brush brush, float x, float y, float width, float height)
        {
            g.FillEllipse(brush,
                (int)Math.Round(x), (int)Math.Round(y),
                (int)Math.Round(width), (int)Math.Round(height));
        }

        private void InitializeScreenRecordingDetection()
        {
            _recordingDetectionTimer = new System.Windows.Forms.Timer();
            _recordingDetectionTimer.Interval = 2000; // Check every 2 seconds
            _recordingDetectionTimer.Tick += RecordingDetectionTimer_Tick;
            _recordingDetectionTimer.Start();

            _fpsTimer = new System.Windows.Forms.Timer();
            _fpsTimer.Interval = GetFpsOverlayTimerIntervalMs();
            _fpsTimer.Tick += FpsTimer_Tick;
            _fpsTimer.Start();
        }

        private void FpsTimer_Tick(object? sender, EventArgs e)
        {
            SyncFpsTimerInterval();

            UpdateFpsOverlaySurface();
        }

        private void SyncFpsTimerInterval()
        {
            if (_fpsTimer == null)
            {
                return;
            }

            int targetInterval = GetFpsOverlayTimerIntervalMs();
            if (_fpsTimer.Interval != targetInterval)
            {
                _fpsTimer.Interval = targetInterval;
            }
        }

        private static int GetFpsOverlayTimerIntervalMs()
        {
            return FpsOverlayRuntimePolicy.FromConfig(CrosshairConfig.Instance).TimerIntervalMs;
        }

        private void EnsureFpsOverlayForm()
        {
            if (_fpsOverlayForm != null && !_fpsOverlayForm.IsDisposed) return;

            _fpsOverlayForm = new FpsOverlayForm();
            _fpsOverlayForm.Show();
            _fpsOverlayForm.Hide();
        }

        private void UpdateFpsOverlaySurface()
        {
            EnsureFpsOverlayForm();
            if (_fpsOverlayForm == null || _fpsOverlayForm.IsDisposed) return;

            bool shouldShow = FpsOverlayRuntimePolicy.FromConfig(CrosshairConfig.Instance).ShouldShow && !_isRecordingDetected;
            if (!shouldShow)
            {
                if (_fpsOverlayForm.Visible) _fpsOverlayForm.Hide();
                return;
            }

            bool fpsOverlayBecameVisible = !_fpsOverlayForm.Visible;
            if (fpsOverlayBecameVisible) _fpsOverlayForm.Show();

            var snapshot = SystemFpsMonitor.GetSnapshot();
            _fpsOverlayForm.UpdateState(snapshot, SystemFpsMonitor.ActiveSource, SystemFpsMonitor.StatusText);

            if (ShouldReinforceCrosshairAfterFpsOverlayUpdate(ShouldDisplayCrosshairOverlay(), shouldShow, fpsOverlayBecameVisible))
            {
                ReinforceTopMost();
            }
        }

        internal static bool ShouldReinforceCrosshairAfterFpsOverlayUpdate(
            bool shouldDisplayCrosshairOverlay,
            bool shouldShowFpsOverlay,
            bool fpsOverlayBecameVisible)
        {
            return shouldDisplayCrosshairOverlay && shouldShowFpsOverlay && fpsOverlayBecameVisible;
        }

        private bool ShouldDisplayCrosshairOverlay()
        {
            bool result = isVisible && CurrentProfile.EnableCustomCrosshair && !_isRecordingDetected;
            Program.LogDebug($"[DIAG] ShouldDisplayCrosshairOverlay={result} (isVisible={isVisible}, EnableCustomCrosshair={CurrentProfile.EnableCustomCrosshair}, _isRecordingDetected={_isRecordingDetected})", nameof(Form1));
            return result;
        }

        private void UpdateCrosshairVisibilityState()
        {
            bool shouldShow = ShouldDisplayCrosshairOverlay();
            if (Visible != shouldShow)
            {
                Visible = shouldShow;
            }

            if (shouldShow)
            {
                ReinforceTopMost();
                MarkConfigDirty();
            }

            UpdateFpsOverlaySurface();
        }

    private async void RecordingDetectionTimer_Tick(object? sender, EventArgs e)
        {
            if (Interlocked.Exchange(ref _recordingDetectionRunning, 1) != 0)
            {
                return;
            }

            try
            {
            if (_profileService.Current.HideDuringScreenRecording)
            {
                bool isRecording = await Task.Run(() => IsScreenRecordingActive());

                if (isRecording && !_isRecordingDetected)
                {
                    // Recording started
                    _isRecordingDetected = true;
                    _wasVisibleBeforeRecording = isVisible;
                    UpdateCrosshairVisibilityState();
                }
                else if (!isRecording && _isRecordingDetected)
                {
                    // Recording stopped
                    _isRecordingDetected = false;
                    isVisible = _wasVisibleBeforeRecording;
                    UpdateCrosshairVisibilityState();
                }
            }
            }
            finally
            {
                Volatile.Write(ref _recordingDetectionRunning, 0);
            }
        }

        private bool IsScreenRecordingActive()
        {
            try
            {
                // Check for common screen recording software
                string[] recordingSoftware = {
                    "obs64.exe",           // OBS Studio 64-bit
                    "obs32.exe",           // OBS Studio 32-bit
                    "XSplit.Core.exe",     // XSplit
                    "Streamlabs OBS",      // Streamlabs OBS
                    "NVIDIA GeForce Experience", // NVIDIA ShadowPlay
                    "Bandicam",            // Bandicam
                    "Camtasia",            // Camtasia
                    "Fraps",               // Fraps
                    "Action!",             // Action!
                    "Dxtory",              // Dxtory
                    "MSIAfterburner.exe"   // MSI Afterburner
                };

                foreach (string software in recordingSoftware)
                {
                    var processes = System.Diagnostics.Process.GetProcessesByName(
                        software.Replace(".exe", ""));

                    bool foundAny = false;
                    for (int i = 0; i < processes.Length; i++)
                    {
                        if (!foundAny)
                        {
                            foundAny = true;
                        }

                        processes[i].Dispose();
                    }

                    if (foundAny)
                    {
                        // Additional check for OBS - look for recording indicator
                        if (software.StartsWith("obs", StringComparison.OrdinalIgnoreCase))
                        {
                            IntPtr obsWindow = FindWindow(null, "OBS");
                            if (obsWindow == IntPtr.Zero)
                                obsWindow = FindWindow(null, "OBS Studio");

                            if (obsWindow != IntPtr.Zero && IsWindowVisible(obsWindow))
                            {
                                return true;
                            }
                        }
                        else
                        {
                            return true;
                        }
                    }
                }

                // Check for Windows Game Bar recording
                var gameBarProcesses = System.Diagnostics.Process.GetProcessesByName("GameBarPresenceWriter");
                bool gameBarFound = false;
                for (int i = 0; i < gameBarProcesses.Length; i++)
                {
                    if (!gameBarFound)
                    {
                        gameBarFound = true;
                    }

                    gameBarProcesses[i].Dispose();
                }

                if (gameBarFound)
                {
                    return true;
                }

                return false;
            }
            catch
            {
                // If detection fails, assume no recording to avoid hiding unnecessarily
                return false;
            }
        }

    private void Service_CurrentChanged(CrosshairProfile profile)
        {
            ApplyDisplaySettingsFromProfile(profile);

            // Mark that we need a redraw and invalidate only if profile actually changed
            lock (_renderLock)
            {
        Program.LogDebug($"Apply profile: {profile.Name} size={profile.Size} th={profile.Thickness} shape={profile.Shape}", nameof(Form1));
                // Sync renderer flags from profile (e.g., AntiAlias)
                _renderer.AntiAlias = profile.AntiAlias;

                if (_lastRenderedProfile == null || !ProfilesEqual(_lastRenderedProfile, profile))
                {
                    _configDirty = true; // trigger renderer regeneration
                    UpdateFormSize();
                    CenterCrosshair();
                    if (Visible)
                    {
                        InvalidateThrottled();
                    }
                }
            }
            UpdateCrosshairVisibilityState();
        }

        private void ApplyDisplaySettingsFromProfile(CrosshairProfile profile)
        {
            if (!profile.HasDisplayColorProfile)
            {
                return;
            }

            var cfg = CrosshairConfig.Instance;
            bool changed = false;

            if (cfg.EnableGammaOverride != profile.DisplayEnableGammaOverride)
            {
                cfg.EnableGammaOverride = profile.DisplayEnableGammaOverride;
                changed = true;
            }

            int gamma = Math.Clamp(profile.DisplayGammaValue, 50, 150);
            int contrast = Math.Clamp(profile.DisplayContrastValue, 50, 150);
            int brightness = Math.Clamp(profile.DisplayBrightnessValue, 50, 150);
            int vibrance = Math.Clamp(profile.DisplayVibranceValue, 0, 100);
            string target = NormalizeDisplayTargetProcessName(profile.DisplayTargetProcessName);

            if (cfg.GammaValue != gamma)
            {
                cfg.GammaValue = gamma;
                changed = true;
            }

            if (cfg.ContrastValue != contrast)
            {
                cfg.ContrastValue = contrast;
                changed = true;
            }

            if (cfg.BrightnessValue != brightness)
            {
                cfg.BrightnessValue = brightness;
                changed = true;
            }

            if (cfg.VibranceValue != vibrance)
            {
                cfg.VibranceValue = vibrance;
                changed = true;
            }

            if (!string.Equals(cfg.TargetProcessName, target, StringComparison.OrdinalIgnoreCase))
            {
                cfg.TargetProcessName = target;
                changed = true;
            }

            if (changed)
            {
                cfg.SaveSettings();
            }

            DisplayManager.CheckForegroundAndApply(forceUpdate: true);
        }

        private static string NormalizeDisplayTargetProcessName(string? value)
        {
            var trimmed = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return string.Empty;
            }

            if (trimmed.Contains('\\') || trimmed.Contains('/'))
            {
                trimmed = Path.GetFileName(trimmed);
            }

            if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            return trimmed.Contains('.') ? trimmed : $"{trimmed}.exe";
        }

        private bool ProfilesEqual(CrosshairProfile a, CrosshairProfile b)
        {
            return a.EnumShape == b.EnumShape &&
                   a.Shape == b.Shape &&
                   a.Size == b.Size &&
                   a.Thickness == b.Thickness &&
                   a.EdgeThickness == b.EdgeThickness &&
                   a.GapSize == b.GapSize &&
                   a.InnerSize == b.InnerSize &&
                   a.InnerThickness == b.InnerThickness &&
                   a.InnerGapSize == b.InnerGapSize &&
                   a.EdgeColor == b.EdgeColor &&
                   a.InnerColor == b.InnerColor &&
                   a.FillColor == b.FillColor &&
                   a.InnerShape == b.InnerShape &&
                   a.InnerShapeEdgeColor == b.InnerShapeEdgeColor &&
                   a.InnerShapeInnerColor == b.InnerShapeInnerColor &&
                   a.InnerShapeFillColor == b.InnerShapeFillColor;
        }

        private void InitializeNotifyIcon()
        {
            try
            {
                // Try to load icon from embedded resources first, then file system
                Icon appIcon = LoadApplicationIcon();

                notifyIcon = new NotifyIcon
                {
                    Text = "Light Crosshair",
                    Visible = true,
                    Icon = appIcon
                };
                notifyIcon.DoubleClick += NotifyIcon_DoubleClick;
                notifyIcon.MouseClick += NotifyIcon_MouseClick;
            }
            catch (Exception)
            {
                // Fallback to system icon if loading fails
                notifyIcon = new NotifyIcon
                {
                    Text = "Light Crosshair",
                    Visible = true,
                    Icon = SystemIcons.Application
                };
                notifyIcon.DoubleClick += NotifyIcon_DoubleClick;
                notifyIcon.MouseClick += NotifyIcon_MouseClick;
            }
        }

        private void InitializeContextMenu()
        {
            contextMenu = new ContextMenuStrip();

            var aboutItem = new ToolStripMenuItem("About");
            aboutItem.Click += (sender, e) =>
            {
                const string text = "LightCrosshair\nAuthor: PrimeBuild\nLicense: MIT (2025)\nWebsite: https://primebuild.website/\nGitHub: https://github.com/PrimeBuild-pc/LightCrosshair";
                MessageBox.Show(this, text, "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (sender, e) => { RequestFullShutdown(); };

            contextMenu.Items.Add(aboutItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(exitItem);

            // Set the context menu for both the form and notify icon
            this.ContextMenuStrip = contextMenu;
            if (notifyIcon != null)
            {
                notifyIcon.ContextMenuStrip = contextMenu;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (Handle != IntPtr.Zero)
            {
                ApplyOverlayWindowStyles(Handle);
            }
            HotkeyManager.Instance.SetWindowHandle(this.Handle);
            RegisterConfiguredGlobalHotkeys();

            _profileService.RegisterHotkeys(this.Handle);
            InitializeRuntimeIfNeeded();
        }

        private void ApplyOverlayWindowStyles(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return;

            int exStyle = GetWindowLong(handle, GWL_EXSTYLE);
            int desired = (exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE) & ~WS_EX_APPWINDOW;
            if (desired != exStyle)
            {
                _ = SetWindowLong(handle, GWL_EXSTYLE, desired);
                int lastError = Marshal.GetLastWin32Error();
                if (lastError != 0)
                {
                    Program.LogDebug($"SetWindowLong(GWL_EXSTYLE) returned Win32 error {lastError}.", nameof(Form1));
                }
            }

            SetWindowPos(handle, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED | SWP_NOOWNERZORDER | SWP_NOSENDCHANGING);
        }

        protected override void WndProc(ref Message m)
        {
            // Handle hotkey messages
            if (m.Msg == WM_HOTKEY)
            {
                int hotkeyId = m.WParam.ToInt32();

                if (hotkeyId == HotkeyManager.TOGGLE_VISIBILITY_ID)
                {
                    ToggleVisibility();
                    return;
                }
                if (hotkeyId == HotkeyManager.CYCLE_PROFILE_NEXT_ID)
                {
                    CycleProfile(1);
                    return;
                }
                if (hotkeyId == HotkeyManager.CYCLE_PROFILE_PREV_ID)
                {
                    CycleProfile(-1);
                    return;
                }
                if (hotkeyId == HotkeyManager.TOGGLE_SETTINGS_WINDOW_ID)
                {
                    ToggleSettingsWindow();
                    return;
                }

                // Let profile service process per-profile custom hotkeys
                if (_profileService.ProcessHotkeyMessage(m)) return;
            }

            base.WndProc(ref m);
        }

        private DateTime _lastPaintTime = DateTime.MinValue;
        private void InvalidateThrottled()
        {
            if ((DateTime.UtcNow - _lastPaintTime).TotalMilliseconds >= 16)
            {
                _lastPaintTime = DateTime.UtcNow;
                base.Invalidate();
            }
        }

    private void Form1_Paint(object? sender, PaintEventArgs e)
        {
            if (!ShouldDisplayCrosshairOverlay()) return;

            lock (_renderLock)
            {
                if (_configDirty)
                {
                    // Do NOT dispose _currentFrame here; renderer owns the bitmap and may return the same instance.
                    var p = CurrentProfile;
                    _currentFrame = _renderer.RenderIfNeeded(p);
                    _lastRenderedProfile = p.Clone();
                    _configDirty = false;
                }

                if (_currentFrame != null)
                {
                    e.Graphics.Clear(this.TransparencyKey);
                    // Center bitmap in client area in case form larger than bitmap
                    int x = (ClientSize.Width - _currentFrame.Width) / 2;
                    int y = (ClientSize.Height - _currentFrame.Height) / 2;
                    e.Graphics.DrawImageUnscaled(_currentFrame, x, y);
                }
            }
        }

        private void ToggleVisibility()
        {
            isVisible = !isVisible;
            Program.LogDebug($"Overlay visibility toggled: {(isVisible ? "ON" : "OFF")}", nameof(Form1));
            SaveOverlayVisibilityPreference();
            UpdateCrosshairVisibilityState();

            if (isVisible)
            {
                SystemFpsMonitor.RequestTrackingRefresh();
            }
        }

        private void RequestFullShutdown()
        {
            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(RequestFullShutdown));
                }
                catch
                {
                    // Best-effort shutdown.
                }
                return;
            }

            if (_isExiting)
            {
                return;
            }

            _isExiting = true;

            try
            {
                Hide();
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "Form1.RequestFullShutdown: hide");
            }

            try
            {
                Close();
                Application.ExitThread();
                Application.Exit();
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "Form1.RequestFullShutdown: Exit/Close");
            }

            StartShutdownWatchdog();
        }

        private void StartShutdownWatchdog()
        {
            if (_shutdownWatchdogStarted)
            {
                return;
            }

            _shutdownWatchdogStarted = true;

            int pid = Environment.ProcessId;
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000).ConfigureAwait(false);
                if (_isExiting)
                {
                    try
                    {
                        Environment.Exit(0);
                    }
                    catch
                    {
                        // Final fallback below.
                    }

                    try
                    {
                        Process.GetProcessById(pid).Kill(true);
                    }
                    catch
                    {
                        // Last-resort best effort.
                    }
                }
            });
        }

        private void CycleProfile(int direction)
        {
            var list = new List<CrosshairProfile>(_profileService.Profiles);
            if (list.Count == 0) return;
            var current = _profileService.Current;
            int currentIndex = list.FindIndex(p => p.Id == current.Id);
            if (currentIndex < 0) currentIndex = 0;
            int next = (currentIndex + direction + list.Count) % list.Count;
            _profileService.Switch(list[next].Id);
        }

        private void CenterCrosshair()
        {
            if (!ShouldDisplayCrosshairOverlay()) return;

            // First ensure the form is properly sized
            UpdateFormSize();

            Rectangle screenBounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);

            // Calculate the exact pixel center of the screen
            // For 1440p (2560x1440), center should be at (1280, 720)
            double screenCenterX = screenBounds.X + (screenBounds.Width / 2.0);
            double screenCenterY = screenBounds.Y + (screenBounds.Height / 2.0);

            // Calculate form center (no DPI scaling needed for positioning)
            double formCenterX = this.Width / 2.0;
            double formCenterY = this.Height / 2.0;

            // Position the form for pixel-perfect centering
            // Use Math.Floor to ensure we don't round up and cause offset
            int formX = (int)Math.Floor(screenCenterX - formCenterX);
            int formY = (int)Math.Floor(screenCenterY - formCenterY);

            // For even screen dimensions, adjust to ensure perfect centering
            if (screenBounds.Width % 2 == 0)
            {
                formX = (int)Math.Round(screenCenterX - formCenterX);
            }
            if (screenBounds.Height % 2 == 0)
            {
                formY = (int)Math.Round(screenCenterY - formCenterY);
            }

            this.Location = new Point(formX, formY);
        }

        private void UpdateFormSize()
        {
            var p = CurrentProfile;
            if (p != null)
            {
                int maxCrosshairSize = Math.Max(p.Size, p.InnerSize);
                // Increased padding to account for outlines and anti-aliasing bounds
                int outlinePadding = p.OutlineEnabled ? Math.Max(2, (p.EdgeThickness > 0 ? p.EdgeThickness : p.Thickness) + 2) : 0;
                int totalThickness = Math.Max(p.Thickness, p.InnerThickness) + outlinePadding;
                int padding = Math.Max(totalThickness * 4, 32);

                int formSize = Math.Max(100, maxCrosshairSize + padding);
                if (formSize % 2 == 0) formSize++;
                this.Size = new Size(formSize, formSize);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel) return;

            _isExiting = true;
            StartShutdownWatchdog();
            CleanupEvents();
            SaveOverlayVisibilityPreference();
            DisplayManager.StopMonitoring();
            WpfSettingsHost.Shutdown();
            
            // Unregister profile service hotkeys
            _profileService.UnregisterHotkeys();

            // Unregister global hotkeys managed centrally
            HotkeyManager.Instance.UnregisterAll();

            // Clean up resources
            if (notifyIcon != null)
            {
                notifyIcon.Visible = false; // Hide immediately, disposal happens in Designer
                notifyIcon.Dispose();
                notifyIcon = null;
            }

            if (contextMenu != null)
            {
                contextMenu.Dispose();
                contextMenu = null;
            }

            // Stop and dispose timers
            if (_recordingDetectionTimer != null)
            {
                _recordingDetectionTimer.Stop();
                _recordingDetectionTimer.Dispose();
            }

            if (_fpsTimer != null)
            {
                _fpsTimer.Stop();
                _fpsTimer.Dispose();
            }

            if (_fpsOverlayForm != null && !_fpsOverlayForm.IsDisposed)
            {
                _fpsOverlayForm.Hide();
                _fpsOverlayForm.Close();
                _fpsOverlayForm.Dispose();
                _fpsOverlayForm = null;
            }

            SystemFpsMonitor.Stop();

            // Clear graphics cache
            ClearGraphicsCache();

            try { _ = _profileService.PersistAsync(); } catch (Exception ex) { Program.LogError(ex, "PersistAsync on close"); }
        }

        private ICrosshairRenderBackend CreateRenderBackend()
        {
            try
            {
                var backend = new SkiaCrosshairRenderer();
                Program.LogDebug("Rendering backend selected: SkiaSharp", nameof(Form1));
                return backend;
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "Skia renderer init failed, fallback to GDI");
                Program.LogDebug("Rendering backend selected: GDI fallback", nameof(Form1));
                return new CrosshairRenderer();
            }
        }

        private void SaveOverlayVisibilityPreference()
        {
            try
            {
                Program.LogDebug($"[DIAG] SaveOverlayVisibilityPreference saving isVisible={isVisible}", nameof(Form1));
                _prefs.OverlayVisible = isVisible;
                PreferencesStore.Save(_prefs);

                CrosshairConfig.Instance.Visible = isVisible;
                CrosshairConfig.Instance.SaveSettings();
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "SaveOverlayVisibilityPreference");
            }
        }


        private void ReinforceTopMost()
        {
            if (!ShouldDisplayCrosshairOverlay()) return;
            if (IsDisposed || !IsHandleCreated) return;
            try { SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER | SWP_NOSENDCHANGING); }
            catch (Exception ex)
            {
                Program.LogDebug($"ReinforceTopMost SetWindowPos failed: {ex.Message}", nameof(Form1));
            }
        }


        private void ShowSettingsWindow()
        {
            if (IsDisposed) return;


            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(() => { ReinforceTopMost(); HookNudgeBridge(); WpfSettingsHost.Show(_profileService); ReinforceTopMost(); })); }
                catch { /* ignore if handle is not ready */ }
                return;
            }
            ReinforceTopMost();
            HookNudgeBridge();
            WpfSettingsHost.Show(_profileService);
            ReinforceTopMost();
        }

        private void ToggleSettingsWindow()
        {
            if (IsDisposed) return;

            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(ToggleSettingsWindow)); }
                catch (Exception ex)
                {
                    Program.LogError(ex, "Form1.ToggleSettingsWindow BeginInvoke");
                }
                return;
            }

            if (WpfSettingsHost.IsVisible)
            {
                WpfSettingsHost.Hide();
                ReinforceTopMost();
                return;
            }

            ShowSettingsWindow();
        }

        private static int BuildHotkeyModifiers(bool useAlt, bool useControl, bool useShift, bool useWin)
        {
            int modifiers = 0;
            if (useAlt) modifiers |= HotkeyManager.MOD_ALT;
            if (useControl) modifiers |= HotkeyManager.MOD_CONTROL;
            if (useShift) modifiers |= HotkeyManager.MOD_SHIFT;
            if (useWin) modifiers |= HotkeyManager.MOD_WIN;
            return modifiers;
        }

        private void RegisterConfiguredGlobalHotkeys()
        {
            if (IsDisposed || !IsHandleCreated) return;

            var cfg = CrosshairConfig.Instance;

            HotkeyManager.Instance.UnregisterHotkey(HotkeyManager.TOGGLE_VISIBILITY_ID);
            HotkeyManager.Instance.UnregisterHotkey(HotkeyManager.CYCLE_PROFILE_NEXT_ID);
            HotkeyManager.Instance.UnregisterHotkey(HotkeyManager.CYCLE_PROFILE_PREV_ID);
            HotkeyManager.Instance.UnregisterHotkey(HotkeyManager.TOGGLE_SETTINGS_WINDOW_ID);

            int visibilityModifiers = BuildHotkeyModifiers(cfg.HotkeyUseAlt, cfg.HotkeyUseControl, cfg.HotkeyUseShift, cfg.HotkeyUseWin);
            if (!HotkeyManager.Instance.RegisterHotkeyWithId(HotkeyManager.TOGGLE_VISIBILITY_ID, visibilityModifiers, (int)cfg.HotkeyKey))
            {
                Program.LogDebug("Toggle Visibility hotkey conflict", nameof(Form1));
            }

            int cycleModifiers = BuildHotkeyModifiers(
                cfg.CycleProfileHotkeyUseAlt,
                cfg.CycleProfileHotkeyUseControl,
                cfg.CycleProfileHotkeyUseShift,
                cfg.CycleProfileHotkeyUseWin);
            if (!HotkeyManager.Instance.RegisterHotkeyWithId(HotkeyManager.CYCLE_PROFILE_NEXT_ID, cycleModifiers, (int)cfg.CycleProfileHotkeyKey))
            {
                Program.LogDebug("Cycle Next hotkey conflict", nameof(Form1));
            }

            int cyclePrevModifiers = BuildHotkeyModifiers(
                cfg.CycleProfilePrevHotkeyUseAlt,
                cfg.CycleProfilePrevHotkeyUseControl,
                cfg.CycleProfilePrevHotkeyUseShift,
                cfg.CycleProfilePrevHotkeyUseWin);
            if (!HotkeyManager.Instance.RegisterHotkeyWithId(
                HotkeyManager.CYCLE_PROFILE_PREV_ID,
                cyclePrevModifiers,
                (int)cfg.CycleProfilePrevHotkeyKey))
            {
                Program.LogDebug("Cycle Prev hotkey conflict", nameof(Form1));
            }

            int settingsModifiers = BuildHotkeyModifiers(
                cfg.SettingsWindowHotkeyUseAlt,
                cfg.SettingsWindowHotkeyUseControl,
                cfg.SettingsWindowHotkeyUseShift,
                cfg.SettingsWindowHotkeyUseWin);
            if (!HotkeyManager.Instance.RegisterHotkeyWithId(
                HotkeyManager.TOGGLE_SETTINGS_WINDOW_ID,
                settingsModifiers,
                (int)cfg.SettingsWindowHotkeyKey))
            {
                Program.LogDebug("Toggle Settings Window hotkey conflict", nameof(Form1));
            }
        }

        private void HookNudgeBridge()
        {
            // Ensure single assignment
            WpfSettingsHost.OnNudge = (dx, dy) =>
            {
                try
                {
                    if (IsDisposed) return;
                    var loc = this.Location;
                    var next = new Point(loc.X + dx, loc.Y + dy);
                    this.Location = next;
                    // no need to redraw; bitmap stays the same; only position changes
                }
                catch (Exception ex)
                {
                    Program.LogDebug($"OnNudge failed: {ex.Message}", nameof(Form1));
                }
            };

            WpfSettingsHost.GetPosition = () =>
            {
                try { return this.Location; } catch { return this.Location; }
            };

            WpfSettingsHost.ResetCenter = () =>
            {
                try { CenterCrosshair(); }
                catch (Exception ex)
                {
                    Program.LogDebug($"ResetCenter failed: {ex.Message}", nameof(Form1));
                }
            };
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // Renderer owns cached bitmap; dispose renderer on form close to release GDI resources
            try { _renderer.Dispose(); }
            catch (Exception ex)
            {
                Program.LogError(ex, "Form1.OnFormClosed renderer dispose");
            }
            _currentFrame = null;
            base.OnFormClosed(e);
        }


        protected override void OnResize(EventArgs e)
        {
            if (!ShouldDisplayCrosshairOverlay()) return;
            base.OnResize(e);
            CenterCrosshair();
        }

        protected override void SetVisibleCore(bool value)
        {
            if (value && !ShouldDisplayCrosshairOverlay())
            {
                Program.LogDebug($"[DIAG] SetVisibleCore BLOCKED visibility (value={value}, isVisible={isVisible}, EnableCustomCrosshair={CurrentProfile.EnableCustomCrosshair}, _isRecordingDetected={_isRecordingDetected})", nameof(Form1));
                value = false;
            }

            base.SetVisibleCore(value);
        }

        // TriggerAutosave removed; persistence handled by ProfileService.

        private void MarkConfigDirty()
        {
            lock (_renderLock)
            {
                _configDirty = true;
                InvalidateThrottled();
            }
        }

        private void Service_CurrentChanged_Handler(object? sender, CrosshairProfile profile)
        {
            try { Program.LogDebug($"CurrentChanged -> {profile.Name} ({profile.Id})", nameof(Form1)); Service_CurrentChanged(profile); }
            catch (Exception ex) { Program.LogError(ex, "Form1.CurrentChanged handler"); }
        }

        private void Form1_Paint_Handler(object? sender, PaintEventArgs e)
        {
            try { Form1_Paint(sender, e); }
            catch (Exception ex) { Program.LogError(ex, "Form1_Paint"); }
        }

        private void ProfileService_Persisted_Handler(object? sender, ProfilesPersistedEventArgs args)
        {
            try
            {
                if (_lblSaved == null || _lblSaved.IsDisposed) return;
                if (!IsHandleCreated || IsDisposed) return;
                Program.LogDebug($"Persisted event -> success={args.Success} at {args.Timestamp:O}", nameof(Form1));
                BeginInvoke(new Action(() => _lblSaved.Text = args.Success ? $"Saved {args.Timestamp:HH:mm:ss}" : "Save error"));
            }
            catch (Exception ex) { Program.LogError(ex, "Form1.Persisted handler"); }
        }

        private void Config_SettingsChanged_Handler(object? sender, EventArgs e)
        {
            if (_isExiting || IsDisposed)
            {
                return;
            }

            void Apply()
            {
                if (_isExiting || IsDisposed)
                {
                    return;
                }

                SyncFpsTimerInterval();
                UpdateCrosshairVisibilityState();
            }

            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke((Action)Apply);
                }
                else
                {
                    Apply();
                }
            }
            catch (ObjectDisposedException)
            {
                // No-op during shutdown races.
            }
            catch (InvalidOperationException)
            {
                // No-op when the window handle is not available yet.
            }
        }

        private void Config_HotkeysRegistrationRequested_Handler(object? sender, EventArgs e)
        {
            if (_isExiting || IsDisposed)
            {
                return;
            }

            void Apply()
            {
                if (_isExiting || IsDisposed)
                {
                    return;
                }

                RegisterConfiguredGlobalHotkeys();
            }

            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke((Action)Apply);
                }
                else
                {
                    Apply();
                }
            }
            catch (ObjectDisposedException)
            {
                // No-op during shutdown races.
            }
            catch (InvalidOperationException)
            {
                // No-op when the window handle is not available yet.
            }
        }

        private void OnApplication_Exit(object? sender, EventArgs e)
        {
            GammaController.RestoreOriginal();
        }

        private void OnAppDomain_ProcessExit(object? sender, EventArgs e)
        {
            GammaController.RestoreOriginal();
        }

        private void NotifyIcon_DoubleClick(object? sender, EventArgs e)
        {
            ToggleVisibility();
        }

        private void NotifyIcon_MouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) ShowSettingsWindow();
        }

        private void CleanupEvents()
        {
            if (_profileService != null)
            {
                _profileService.CurrentChanged -= Service_CurrentChanged_Handler;
                _profileService.Persisted -= ProfileService_Persisted_Handler;
            }

            CrosshairConfig.Instance.SettingsChanged -= Config_SettingsChanged_Handler;
            CrosshairConfig.Instance.HotkeysRegistrationRequested -= Config_HotkeysRegistrationRequested_Handler;

            this.Paint -= Form1_Paint_Handler;
            this.DpiChanged -= Form1_DpiChanged;

            if (notifyIcon != null)
            {
                notifyIcon.DoubleClick -= NotifyIcon_DoubleClick;
                notifyIcon.MouseClick -= NotifyIcon_MouseClick;
            }

            Application.ApplicationExit -= OnApplication_Exit;
            AppDomain.CurrentDomain.ProcessExit -= OnAppDomain_ProcessExit;
        }
    }
}

