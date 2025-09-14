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
        private Color fillColor = Color.Transparent;
        private bool isVisible = true;

        // Performance optimization fields
    // Legacy incremental rendering fields replaced by central renderer
    private CrosshairProfile? _lastRenderedProfile; // retained temporarily for menu diff logic
    private readonly object _renderLock = new object();
    private CrosshairRenderer _renderer = new CrosshairRenderer();
    private Bitmap? _currentFrame; // last produced bitmap copy
    private bool _configDirty = true; // marks need to request new bitmap from renderer
        private float _dpiScaleFactor = 1.0f;

        // Object pooling for graphics resources
        private readonly Dictionary<Color, SolidBrush> _brushCache = new Dictionary<Color, SolidBrush>();
        private readonly Dictionary<(Color, float), Pen> _penCache = new Dictionary<(Color, float), Pen>();

        // Screen recording detection
    private System.Windows.Forms.Timer? _recordingDetectionTimer;
        private bool _isRecordingDetected = false;
        private bool _wasVisibleBeforeRecording = true;

        // Constants for message handling
        private const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

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
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOOWNERZORDER = 0x0200;
        private const uint SWP_NOSENDCHANGING = 0x0400;
        private const uint SWP_SHOWWINDOW = 0x0040;


        // Modifiers for hotkeys
        private const int MOD_ALT = 0x0001;
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_SHIFT = 0x0004;
        private const int MOD_WIN = 0x0008;

        // Hotkey IDs
        private const int TOGGLE_VISIBILITY_HOTKEY_ID = 9000;
    private bool _suppressMenuEvents = false; // prevent feedback loops while syncing menu state

    // Autosave centralized in ProfileService now
        private readonly UndoStack<CrosshairProfile> _undo = new(10);
        private StatusStrip? _statusStrip; // simple status surface for Saved HH:MM:SS
        private ToolStripStatusLabel? _lblSaved;
    private CrosshairProfile CurrentProfile => _profileService.Current;

        public Form1() : this(null) {}
        public Form1(IProfileService? service)
        {
            _profileService = service ?? ProfileService.Instance;
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

            _profileService.CurrentChanged += (_, p) =>
            {
                try { Program.LogDebug($"CurrentChanged -> {p.Name} ({p.Id})", nameof(Form1)); Service_CurrentChanged(p); }
                catch (Exception ex) { Program.LogError(ex, "Form1.CurrentChanged handler"); }
            };

            // Initialize system tray icon and menu
            InitializeNotifyIcon();
            InitializeContextMenu();

            // Add paint handler (now only blits cached bitmap)
            this.Paint += (s, e) =>
            {
                try { Form1_Paint(s, e); }
                catch (Exception ex) { Program.LogError(ex, "Form1_Paint"); }
            };

            // Initialize DPI awareness
            InitializeDpiAwareness();

            // Initialize screen recording detection
            InitializeScreenRecordingDetection();

            // Setup minimal status strip (optional visual feedback)
            SetupStatusStrip();

            // Center the form on screen
            CenterCrosshair();
            this.Load += Form1_Load; // async profiles load & migration
            _profileService.Persisted += (_, args) =>
            {
                try
                {
                    if (_lblSaved == null || _lblSaved.IsDisposed) return;
                    if (!IsHandleCreated || IsDisposed) return;
                    Program.LogDebug($"Persisted event -> success={args.Success} at {args.Timestamp:O}", nameof(Form1));
                    BeginInvoke(new Action(() => _lblSaved.Text = args.Success ? $"Saved {args.Timestamp:HH:mm:ss}" : "Save error"));
                }
                catch (Exception ex) { Program.LogError(ex, "Form1.Persisted handler"); }
            };
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

        private async void Form1_Load(object? sender, EventArgs e)
        {
            try
            {
                if (_profileService.Profiles.Count == 0)
                    await _profileService.InitializeAsync();
                var current = _profileService.Current;
                _undo.Push(current.Clone());
                MarkConfigDirty();
                UpdateMenuItems();
            }
            catch (Exception ex) { Program.LogError(ex, "Form1_Load"); }
        }

        private void InitializeDpiAwareness()
        {
            // Get current DPI scaling factor
            using (Graphics g = this.CreateGraphics())
            {
                _dpiScaleFactor = g.DpiX / 96.0f; // 96 DPI is the standard
            }
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
        }

    private void RecordingDetectionTimer_Tick(object? sender, EventArgs e)
        {
            if (_profileService.Current.HideDuringScreenRecording)
            {
                bool isRecording = IsScreenRecordingActive();

                if (isRecording && !_isRecordingDetected)
                {
                    // Recording started
                    _isRecordingDetected = true;
                    _wasVisibleBeforeRecording = this.Visible;
                    this.Visible = false;
                }
                else if (!isRecording && _isRecordingDetected)
                {
                    // Recording stopped
                    _isRecordingDetected = false;
                    this.Visible = _wasVisibleBeforeRecording && isVisible;
                }
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

                    if (processes.Length > 0)
                    {
                        // Additional check for OBS - look for recording indicator
                        if (software.StartsWith("obs"))
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
                if (gameBarProcesses.Length > 0)
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
                    Invalidate();
                }
            }
            // Update menu checkmarks
            try { if (SafeVisible(contextMenu)) UpdateMenuItems(); } catch (Exception ex) { Program.LogError(ex, "Form1.UpdateMenuItems (from CurrentChanged)"); }

            // Ensure Profiles submenu reflects ordering & names after reorder/rename
            if (contextMenu != null)
            {
                var profilesMenu = FindMenuItemByText(contextMenu.Items, "Profiles");
                if (profilesMenu != null)
                {
                    // First two items: Manage Profiles..., separator
                    bool needsRebuild = false;
                    int expectedCount = _profileService.Profiles.Count + 2; // manage + separator
                    if (profilesMenu.DropDownItems.Count != expectedCount) needsRebuild = true;
                    else
                    {
                        for (int i = 0; i < _profileService.Profiles.Count; i++)
                        {
                            var prof = _profileService.Profiles[i];
                            var item = profilesMenu.DropDownItems[i + 2] as ToolStripMenuItem; // offset
                            if (item == null || !string.Equals(item.Text, prof.Name, StringComparison.Ordinal))
                            { needsRebuild = true; break; }
                        }
                    }
                    if (needsRebuild)
                    {
                        // Rebuild only the dynamic portion
                        while (profilesMenu.DropDownItems.Count > 2)
                            profilesMenu.DropDownItems.RemoveAt(2);
                        foreach (var p in _profileService.Profiles)
                        {
                            var mi = new ToolStripMenuItem(p.Name);
                            mi.Click += (_, __) => { try { Program.LogDebug($"Switch profile -> {p.Name}", nameof(Form1)); _profileService.Switch(p.Id); } catch (Exception ex) { Program.LogError(ex, "Profiles menu Switch click"); } };
                            profilesMenu.DropDownItems.Add(mi);
                        }
                        try { if (SafeVisible(contextMenu)) UpdateMenuItems(); } catch (Exception ex) { Program.LogError(ex, "Form1.UpdateMenuItems (profiles rebuild)"); }
                    }
                }
            }
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

        private void UpdateMenuItems(string? changedProperty = null)
        {
            Program.LogDebug($"UpdateMenuItems start (changed={changedProperty ?? "ALL"})", nameof(Form1));
            _suppressMenuEvents = true;
            try
            {
            // If a specific property changed, only update relevant menus
            if (changedProperty != null)
            {
                switch (changedProperty)
                {
                    case "Shape":
                        UpdateShapeMenuItems();
                        // Shape change affects inner shape and gap size menus
                        UpdateInnerShapeMenuItems();
                        UpdateGapSizeMenuItems();
                        return;
                    case "Size":
                        UpdateSizeMenuItems();
                        return;
                    case "InnerSize":
                        if (contextMenu == null) return; // null guard
                        var innerShapeMenu = FindMenuItemByText(contextMenu.Items, "Inner Shape");
                        if (innerShapeMenu != null)
                        {
                            var innerSizeMenu = FindMenuItemByText(innerShapeMenu.DropDownItems, "Size");
                            if (innerSizeMenu != null) UpdateInnerSizeMenu(innerSizeMenu);
                        }
                        return;
                    case "Thickness":
                        UpdateThicknessMenuItems();
                        return;
                    case "EdgeThickness":
                        UpdateEdgeThicknessMenuItems();
                        return;
                    case "InnerThickness":
                        if (contextMenu == null) return;
                        innerShapeMenu = FindMenuItemByText(contextMenu.Items, "Inner Shape");
                        if (innerShapeMenu != null)
                        {
                            var innerThicknessMenu = FindMenuItemByText(innerShapeMenu.DropDownItems, "Thickness");
                            if (innerThicknessMenu != null) UpdateInnerThicknessMenu(innerThicknessMenu);
                        }
                        return;
                    case "GapSize":
                        UpdateGapSizeMenuItems();
                        return;
                    case "InnerGapSize":
                        if (contextMenu == null) return;
                        innerShapeMenu = FindMenuItemByText(contextMenu.Items, "Inner Shape");
                        if (innerShapeMenu != null)
                        {
                            var innerGapSizeMenu = FindMenuItemByText(innerShapeMenu.DropDownItems, "Gap Size");
                            if (innerGapSizeMenu != null) UpdateInnerGapSizeMenu(innerGapSizeMenu);
                        }
                        return;
                    case "EdgeColor":
                    case "InnerColor":
                    case "FillColor":
                        UpdateColorMenuItems();
                        return;
                    case "InnerShapeEdge":
                    case "InnerShapeInner":
                        if (contextMenu == null) return;
                        innerShapeMenu = FindMenuItemByText(contextMenu.Items, "Inner Shape");
                        if (innerShapeMenu != null)
                        {
                            if (changedProperty == "InnerShapeEdge")
                            {
                                var innerEdgeColorMenu = FindMenuItemByText(innerShapeMenu.DropDownItems, "Edge Color");
                                if (innerEdgeColorMenu != null) UpdateInnerEdgeColorMenu(innerEdgeColorMenu);
                            }
                            else
                            {
                                var innerInnerColorMenu = FindMenuItemByText(innerShapeMenu.DropDownItems, "Inner Color");
                                if (innerInnerColorMenu != null) UpdateInnerInnerColorMenu(innerInnerColorMenu);
                            }
                        }
                        return;
                }
            }

            // If no specific property or unknown property, update all menus
            try { UpdateShapeMenuItems(); } catch (Exception ex) { Program.LogError(ex, "UpdateShapeMenuItems"); }
            try { UpdateSizeMenuItems(); } catch (Exception ex) { Program.LogError(ex, "UpdateSizeMenuItems"); }
            try { UpdateThicknessMenuItems(); } catch (Exception ex) { Program.LogError(ex, "UpdateThicknessMenuItems"); }
            try { UpdateEdgeThicknessMenuItems(); } catch (Exception ex) { Program.LogError(ex, "UpdateEdgeThicknessMenuItems"); }
            try { UpdateGapSizeMenuItems(); } catch (Exception ex) { Program.LogError(ex, "UpdateGapSizeMenuItems"); }
            try { UpdateColorMenuItems(); } catch (Exception ex) { Program.LogError(ex, "UpdateColorMenuItems"); }
            try { UpdateInnerShapeMenuItems(); } catch (Exception ex) { Program.LogError(ex, "UpdateInnerShapeMenuItems"); }
            }
            finally
            {
                _suppressMenuEvents = false;
            }
        }

        private void UpdateShapeMenuItems()
        {
            if (contextMenu == null) return;
            var shapeMenu = FindMenuItemByText(contextMenu.Items, "Shape");
            if (shapeMenu == null) return;

            // Update all shape items (basic + combined) based on CurrentProfile.Shape
            foreach (var group in new[] { "Basic Shapes", "Combined Shapes" })
            {
                var grpMenu = FindMenuItemByText(shapeMenu.DropDownItems, group);
                if (grpMenu == null) continue;
                foreach (ToolStripItem item in grpMenu.DropDownItems)
                {
                    if (item is ToolStripMenuItem mi && mi.Tag is string shapeTag)
                    {
                        mi.Checked = string.Equals(CurrentProfile.Shape, shapeTag, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
        }

        private void UpdateSizeMenuItems()
        {
            if (contextMenu == null) return;
            // Outer shape size menu (used even for combined shapes; tagged as percentage ints)
            var sizeMenu = FindMenuItemByText(contextMenu.Items, "Outer Shape Size")
                           ?? FindMenuItemByText(contextMenu.Items, "Size");
            if (sizeMenu == null) return;
            foreach (ToolStripItem item in sizeMenu.DropDownItems)
            {
                if (item is ToolStripMenuItem mi && mi.Tag is int val)
                {
                    mi.Checked = CurrentProfile.Size == val;
                }
            }
        }

        private void UpdateThicknessMenuItems()
        {
            // Find the thickness menu
            if (contextMenu == null) return;
            var thicknessMenu = FindMenuItemByText(contextMenu.Items, "Thickness");
            if (thicknessMenu == null) return;

            foreach (ToolStripItem item in thicknessMenu.DropDownItems)
            {
                // Skip separators and non-menu items
                if (!(item is ToolStripMenuItem menuItem)) continue;

                if (menuItem.Tag is int thickness)
                {
                    menuItem.Checked = CurrentProfile.Thickness == thickness;
                }
            }
        }

        private void UpdateEdgeThicknessMenuItems()
        {
            // Find the edge color menu first
            if (contextMenu == null) return;
            var edgeColorMenu = FindMenuItemByText(contextMenu.Items, "Edge Color");
            if (edgeColorMenu == null) return;

            // Find the edge thickness submenu within the edge color menu
            var edgeThicknessMenu = FindMenuItemByText(edgeColorMenu.DropDownItems, "Edge Thickness");
            if (edgeThicknessMenu == null) return;

            foreach (ToolStripItem item in edgeThicknessMenu.DropDownItems)
            {
                // Skip separators and non-menu items
                if (!(item is ToolStripMenuItem menuItem)) continue;

                if (menuItem.Tag is int thickness)
                {
                    menuItem.Checked = CurrentProfile.EdgeThickness == thickness;
                }
            }
        }

        private void UpdateGapSizeMenuItems()
        {
            // Find the gap size menu
            if (contextMenu == null) return;
            var gapSizeMenu = FindMenuItemByText(contextMenu.Items, "Gap Size");
            if (gapSizeMenu == null) return;

            bool isGapShape = CurrentProfile.Shape == "Cross" || CurrentProfile.Shape == "CircleCross" || CurrentProfile.Shape == "CrossDot";
            gapSizeMenu.Enabled = isGapShape;
            gapSizeMenu.Text = "Gap Size";

            foreach (ToolStripItem item in gapSizeMenu.DropDownItems)
            {
                if (item is ToolStripMenuItem mi && mi.Tag is int gap)
                {
                    mi.Checked = CurrentProfile.GapSize == gap;
                }
            }
        }

        private void UpdateColorMenuItems()
        {
            // Update edge color menu
            if (contextMenu == null) return;
            var edgeColorMenu = FindMenuItemByText(contextMenu.Items, "Edge Color");
            if (edgeColorMenu != null)
            {
                foreach (ToolStripItem item in edgeColorMenu.DropDownItems)
                {
                    // Skip separators and non-menu items
                    if (!(item is ToolStripMenuItem menuItem)) continue;

                    if (menuItem.Tag is Color color)
                    {
                        menuItem.Checked = ColorEquals(CurrentProfile.EdgeColor, color);
                    }
                }
            }

            // Update inner color menu
            var innerColorMenu = FindMenuItemByText(contextMenu.Items, "Inner Color");
            if (innerColorMenu != null)
            {
                foreach (ToolStripItem item in innerColorMenu.DropDownItems)
                {
                    // Skip separators and non-menu items
                    if (!(item is ToolStripMenuItem menuItem)) continue;

                    if (menuItem.Tag is Color color)
                    {
                        menuItem.Checked = ColorEquals(CurrentProfile.InnerColor, color);
                    }
                }
            }
        }

        private void UpdateInnerShapeMenuItems()
        {
            // Find the inner shape menu
            if (contextMenu == null) return;
            var innerShapeMenu = FindMenuItemByText(contextMenu.Items, "Inner Shape");
            if (innerShapeMenu == null) return;

            // Enable/disable based on shape type
            bool isCombinedShape = CurrentProfile.Shape.StartsWith("Circle", StringComparison.OrdinalIgnoreCase) || CurrentProfile.Shape == "CrossDot";
            innerShapeMenu.Enabled = isCombinedShape;

            if (!isCombinedShape) return;

            // Update the menu text to indicate which shape it applies to
            string innerShapeName = "";
            if (CurrentProfile.Shape == "CircleDot" || CurrentProfile.Shape == "CrossDot")
                innerShapeName = "Dot";
            else if (CurrentProfile.Shape == "CircleCross")
                innerShapeName = "Cross";
            
            else if (CurrentProfile.Shape == "CircleX")
                innerShapeName = "X";

            innerShapeMenu.Text = innerShapeName.Length > 0 ? $"{innerShapeName} Settings" : "Inner Shape";

            // Update inner size menu
            var innerSizeMenu = FindMenuItemByText(innerShapeMenu.DropDownItems, "Size");
            if (innerSizeMenu != null)
            {
                foreach (ToolStripItem item in innerSizeMenu.DropDownItems)
                {
                    // Skip separators and non-menu items
                    if (!(item is ToolStripMenuItem menuItem)) continue;

                    if (menuItem.Tag is int size) menuItem.Checked = CurrentProfile.InnerSize == size;
                }
            }

            // Update inner thickness menu
            var innerThicknessMenu = FindMenuItemByText(innerShapeMenu.DropDownItems, "Thickness");
            if (innerThicknessMenu != null)
            {
                foreach (ToolStripItem item in innerThicknessMenu.DropDownItems)
                {
                    // Skip separators and non-menu items
                    if (!(item is ToolStripMenuItem menuItem)) continue;

                    if (menuItem.Tag is int thickness) menuItem.Checked = CurrentProfile.InnerThickness == thickness;
                }
            }

            // Update inner gap size menu
            var innerGapSizeMenu = FindMenuItemByText(innerShapeMenu.DropDownItems, "Gap Size");
            if (innerGapSizeMenu != null)
            {
                // Enable/disable the gap size menu based on the current shape
                bool enableInnerGap = CurrentProfile.Shape == "CircleCross" || CurrentProfile.Shape == "CircleX";
                innerGapSizeMenu.Enabled = enableInnerGap;

                foreach (ToolStripItem item in innerGapSizeMenu.DropDownItems)
                {
                    // Skip separators and non-menu items
                    if (!(item is ToolStripMenuItem menuItem)) continue;

                    if (menuItem.Tag is int gap) menuItem.Checked = CurrentProfile.InnerGapSize == gap;
                }
            }

            // Update inner edge color menu
            var innerEdgeColorMenu = FindMenuItemByText(innerShapeMenu.DropDownItems, "Edge Color");
            if (innerEdgeColorMenu != null)
            {
                foreach (ToolStripItem item in innerEdgeColorMenu.DropDownItems)
                {
                    // Skip separators and non-menu items
                    if (!(item is ToolStripMenuItem menuItem)) continue;

                    if (menuItem.Tag is Color color)
                    {
                        menuItem.Checked = ColorEquals(CurrentProfile.InnerShapeEdgeColor, color);
                    }
                }
            }

            // Update inner inner color menu
            var innerInnerColorMenu = FindMenuItemByText(innerShapeMenu.DropDownItems, "Inner Color");
            if (innerInnerColorMenu != null)
            {
                foreach (ToolStripItem item in innerInnerColorMenu.DropDownItems)
                {
                    // Skip separators and non-menu items
                    if (!(item is ToolStripMenuItem menuItem)) continue;

                    if (menuItem.Tag is Color color)
                    {
                        menuItem.Checked = ColorEquals(CurrentProfile.InnerShapeInnerColor, color);
                    }
                }
            }
        }

        private void UpdateInnerSizeMenu(ToolStripMenuItem menu)
        {
            foreach (ToolStripItem item in menu.DropDownItems)
            {
                // Skip separators and non-menu items
                if (!(item is ToolStripMenuItem menuItem)) continue;

                if (menuItem.Tag is int size)
                {
                    menuItem.Checked = CurrentProfile.InnerSize == size;
                }
            }
        }

        private void UpdateInnerThicknessMenu(ToolStripMenuItem menu)
        {
            foreach (ToolStripItem item in menu.DropDownItems)
            {
                // Skip separators and non-menu items
                if (!(item is ToolStripMenuItem menuItem)) continue;

                if (menuItem.Tag is int thickness)
                {
                    menuItem.Checked = CurrentProfile.InnerThickness == thickness;
                }
            }
        }

        private void UpdateInnerGapSizeMenu(ToolStripMenuItem menu)
        {
            // Enable/disable the gap size menu based on the current shape
            bool enableInnerGap = CurrentProfile.Shape == "CircleCross" || CurrentProfile.Shape == "CircleX";
            menu.Enabled = enableInnerGap;

            foreach (ToolStripItem item in menu.DropDownItems)
            {
                // Skip separators and non-menu items
                if (!(item is ToolStripMenuItem menuItem)) continue;

                if (menuItem.Tag is int gap)
                {
                    menuItem.Checked = CurrentProfile.InnerGapSize == gap;
                }
            }
        }

        private void UpdateInnerEdgeColorMenu(ToolStripMenuItem menu)
        {
            foreach (ToolStripItem item in menu.DropDownItems)
            {
                // Skip separators and non-menu items
                if (!(item is ToolStripMenuItem menuItem)) continue;

                if (menuItem.Tag is Color color)
                {
                    menuItem.Checked = ColorEquals(CurrentProfile.InnerShapeEdgeColor, color);
                }
            }
        }

        private void UpdateInnerInnerColorMenu(ToolStripMenuItem menu)
        {
            foreach (ToolStripItem item in menu.DropDownItems)
            {
                // Skip separators and non-menu items
                if (!(item is ToolStripMenuItem menuItem)) continue;

                if (menuItem.Tag is Color color)
                {
                    menuItem.Checked = ColorEquals(CurrentProfile.InnerShapeInnerColor, color);
                }
            }
        }

    private ToolStripMenuItem? FindMenuItemByText(ToolStripItemCollection items, string text)
        {
            foreach (ToolStripItem item in items)
            {
                if (item is ToolStripMenuItem menuItem && menuItem.Text == text)
                {
                    return menuItem;
                }
            }
            return null;
        }

        private static bool SafeVisible(ContextMenuStrip? menu) => menu != null && !menu.IsDisposed && menu.Visible;

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
                notifyIcon.DoubleClick += (sender, e) => ToggleVisibility();
                notifyIcon.MouseClick += (sender, e) => { if (e.Button == MouseButtons.Left) ShowSettingsWindow(); };
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
                notifyIcon.DoubleClick += (sender, e) => ToggleVisibility();
                notifyIcon.MouseClick += (sender, e) => { if (e.Button == MouseButtons.Left) ShowSettingsWindow(); };
            }
        }

        private void InitializeContextMenu()
        {
            contextMenu = new ContextMenuStrip();

            // Configure menu behavior for staying open
            contextMenu.Closing += ContextMenu_Closing;

            // Create menu items
            var visibilityItem = new ToolStripMenuItem("Toggle Visibility");
            visibilityItem.Click += (sender, e) => ToggleVisibility();

            // Edit menu with Undo
            var editMenu = new ToolStripMenuItem("Edit");
            var miUndo = new ToolStripMenuItem("Undo") { ShortcutKeys = Keys.Control | Keys.Z };
            miUndo.Click += (_, __) => UndoLastChange();
            editMenu.DropDownItems.Add(miUndo);

            // Profiles submenu
            var profilesMenu = new ToolStripMenuItem("Profiles");

            // Add profile management option
            var manageProfilesItem = new ToolStripMenuItem("Manage Profiles...");
            manageProfilesItem.Click += (sender, e) => ShowProfileManager();
            profilesMenu.DropDownItems.Add(manageProfilesItem);

            // Add separator
            profilesMenu.DropDownItems.Add(new ToolStripSeparator());

            // Add profiles from profile manager
                foreach (var profile in _profileService.Profiles)
            {
                var profileItem = new ToolStripMenuItem(profile.Name);
                    profileItem.Click += (sender, e) => { _profileService.Switch(profile.Id); };
                profilesMenu.DropDownItems.Add(profileItem);
            }

            // Shape submenu
            var shapeMenu = new ToolStripMenuItem("Shape");

            // Basic shapes
            var basicShapesMenu = new ToolStripMenuItem("Basic Shapes") { Padding = new Padding(8, 6, 8, 6) };
            var crossItem = new ToolStripMenuItem("Cross") { Padding = new Padding(10, 8, 10, 8), AutoSize = true }; crossItem.MouseEnter += (_, __) => crossItem.Select();
            crossItem.Tag = "Cross";
            crossItem.Checked = CurrentProfile.Shape == "Cross";
            crossItem.Click += (sender, e) => { UpdateShape(CrosshairShape.Cross, "Cross"); };

            var circleItem = new ToolStripMenuItem("Circle") { Padding = new Padding(10, 8, 10, 8), AutoSize = true }; circleItem.MouseEnter += (_, __) => circleItem.Select();
            circleItem.Tag = "Circle";
            circleItem.Checked = CurrentProfile.Shape == "Circle";
            circleItem.Click += (sender, e) => { UpdateShape(CrosshairShape.Circle, "Circle"); };

            var dotItem = new ToolStripMenuItem("Dot") { Padding = new Padding(10, 8, 10, 8), AutoSize = true }; dotItem.MouseEnter += (_, __) => dotItem.Select();
            dotItem.Tag = "Dot";
            dotItem.Checked = CurrentProfile.Shape == "Dot";
            dotItem.Click += (sender, e) => { UpdateShape(CrosshairShape.Dot, "Dot"); };


            var xItem = new ToolStripMenuItem("X") { Padding = new Padding(10, 8, 10, 8), AutoSize = true }; xItem.MouseEnter += (_, __) => xItem.Select();
            xItem.Tag = "X";
            xItem.Checked = CurrentProfile.Shape == "X";
            xItem.Click += (sender, e) => { UpdateShape(CrosshairShape.X, "X"); };

            basicShapesMenu.DropDownItems.AddRange(new ToolStripItem[] { crossItem, circleItem, dotItem, xItem });

            // Combined shapes
            var combinedShapesMenu = new ToolStripMenuItem("Combined Shapes") { Padding = new Padding(8, 6, 8, 6) };

            var circleDotItem = new ToolStripMenuItem("Circle + Dot") { Padding = new Padding(10, 8, 10, 8), AutoSize = true }; circleDotItem.MouseEnter += (_, __) => circleDotItem.Select();
            circleDotItem.Tag = "CircleDot";
            circleDotItem.Checked = CurrentProfile.Shape == "CircleDot";
            circleDotItem.Click += (sender, e) => { UpdateShape(CrosshairShape.Custom, "CircleDot"); };

            var crossDotItem = new ToolStripMenuItem("Cross + Dot") { Padding = new Padding(10, 8, 10, 8), AutoSize = true }; crossDotItem.MouseEnter += (_, __) => crossDotItem.Select();
            crossDotItem.Tag = "CrossDot";
            crossDotItem.Checked = CurrentProfile.Shape == "CrossDot";
            crossDotItem.Click += (sender, e) => { UpdateShape(CrosshairShape.Custom, "CrossDot"); };

            var circleCrossItem = new ToolStripMenuItem("Circle + Cross") { Padding = new Padding(10, 8, 10, 8), AutoSize = true }; circleCrossItem.MouseEnter += (_, __) => circleCrossItem.Select();
            circleCrossItem.Tag = "CircleCross";
            circleCrossItem.Checked = CurrentProfile.Shape == "CircleCross";
            circleCrossItem.Click += (sender, e) => { UpdateShape(CrosshairShape.Custom, "CircleCross"); };


            var circleXItem = new ToolStripMenuItem("Circle + X") { Padding = new Padding(10, 8, 10, 8), AutoSize = true }; circleXItem.MouseEnter += (_, __) => circleXItem.Select();
            circleXItem.Tag = "CircleX";
            circleXItem.Checked = CurrentProfile.Shape == "CircleX";
            circleXItem.Click += (sender, e) => { UpdateShape(CrosshairShape.Custom, "CircleX"); };

            combinedShapesMenu.DropDownItems.AddRange(new ToolStripItem[] { circleDotItem, crossDotItem, circleCrossItem, circleXItem });

            // Add both submenus to the shape menu
            shapeMenu.DropDownItems.AddRange(new ToolStripItem[] { basicShapesMenu, combinedShapesMenu });

            // Size submenu (for outer shape in combined shapes)
            var sizeMenu = new ToolStripMenuItem("Outer Shape Size");
            bool isCombinedShape = CurrentProfile.Shape.StartsWith("Circle", StringComparison.OrdinalIgnoreCase) ||
                                  CurrentProfile.Shape == "CrossDot";

            for (int size = 5; size <= 100; size += 5)
            {
                var sizeItem = new ToolStripMenuItem($"{size}%");
                sizeItem.Tag = size; // Store the size value in the Tag property
                sizeItem.Checked = CurrentProfile.Size == size;
                int capturedSize = size; // Capture the size value for the lambda
                sizeItem.Click += (sender, e) => { UpdateCurrentProfileProperty("Size", capturedSize); };
                sizeMenu.DropDownItems.Add(sizeItem);
            }

            // Inner Shape submenu (for inner shape in combined shapes)
            var innerShapeMenu = new ToolStripMenuItem("Inner Shape");
            innerShapeMenu.Enabled = isCombinedShape; // Only enable for combined shapes

            // Inner Size submenu
            var innerSizeMenu = new ToolStripMenuItem("Size");
            for (int size = 5; size <= 50; size += 5)
            {
                var sizeItem = new ToolStripMenuItem($"{size}%");
                sizeItem.Tag = size; // Store the size value in the Tag property
                sizeItem.Checked = CurrentProfile.InnerSize == size;
                int capturedSize = size; // Capture the size value for the lambda
                sizeItem.Click += (sender, e) => { UpdateCurrentProfileProperty("InnerSize", capturedSize); };
                innerSizeMenu.DropDownItems.Add(sizeItem);
            }

            // Inner Thickness submenu
            var innerThicknessMenu = new ToolStripMenuItem("Thickness");
            for (int thickness = 1; thickness <= 10; thickness++)
            {
                var thicknessItem = new ToolStripMenuItem(thickness.ToString());
                thicknessItem.Tag = thickness; // Store the thickness value in the Tag property
                thicknessItem.Checked = CurrentProfile.InnerThickness == thickness;
                int capturedThickness = thickness; // Capture the thickness value for the lambda
                thicknessItem.Click += (sender, e) => { UpdateCurrentProfileProperty("InnerThickness", capturedThickness); };
                innerThicknessMenu.DropDownItems.Add(thicknessItem);
            }

            // Inner Gap Size submenu (for inner cross/X in composites)
            var innerGapSizeMenu = new ToolStripMenuItem("Gap Size");
            innerGapSizeMenu.Enabled = CurrentProfile.Shape == "CirclePlus" || CurrentProfile.Shape == "CircleCross" || CurrentProfile.Shape == "CircleX";

            for (int gap = 2; gap <= 20; gap += 2)
            {
                var gapItem = new ToolStripMenuItem(gap.ToString());
                gapItem.Tag = gap; // Store the gap value in the Tag property
                gapItem.Checked = CurrentProfile.InnerGapSize == gap;
                int capturedGap = gap; // Capture the gap value for the lambda
                gapItem.Click += (sender, e) => { UpdateCurrentProfileProperty("InnerGapSize", capturedGap); };
                innerGapSizeMenu.DropDownItems.Add(gapItem);
            }

            // Inner Edge Color submenu
            var innerEdgeColorMenu = new ToolStripMenuItem("Edge Color");

            // Add specified colors
            AddColorMenuItem(innerEdgeColorMenu, "Neon Fuchsia", Color.FromArgb(255, 30, 255), "InnerShapeEdge"); // #FF1EFF
            AddColorMenuItem(innerEdgeColorMenu, "Electric Red", Color.FromArgb(255, 0, 51), "InnerShapeEdge"); // #FF0033
            AddColorMenuItem(innerEdgeColorMenu, "Neon Yellow", Color.FromArgb(238, 255, 0), "InnerShapeEdge"); // #EEFF00
            AddColorMenuItem(innerEdgeColorMenu, "Neon Green", Color.FromArgb(15, 255, 80), "InnerShapeEdge"); // #0FFF50
            AddColorMenuItem(innerEdgeColorMenu, "Neon Cyan", Color.FromArgb(0, 249, 255), "InnerShapeEdge"); // #00F9FF
            AddColorMenuItem(innerEdgeColorMenu, "White", Color.FromArgb(255, 255, 255), "InnerShapeEdge"); // #FFFFFF
            AddColorMenuItem(innerEdgeColorMenu, "Transparent", Color.Transparent, "InnerShapeEdge");

            // Add custom color option
            var customInnerEdgeColorItem = new ToolStripMenuItem("Custom...");
            customInnerEdgeColorItem.Click += (sender, e) => ShowColorPicker("InnerShapeEdge");
            innerEdgeColorMenu.DropDownItems.Add(new ToolStripSeparator());
            innerEdgeColorMenu.DropDownItems.Add(customInnerEdgeColorItem);

            // Inner Inner Color submenu
            var innerInnerColorMenu = new ToolStripMenuItem("Inner Color");

            // Add specified colors
            AddColorMenuItem(innerInnerColorMenu, "Neon Fuchsia", Color.FromArgb(255, 30, 255), "InnerShapeInner"); // #FF1EFF
            AddColorMenuItem(innerInnerColorMenu, "Electric Red", Color.FromArgb(255, 0, 51), "InnerShapeInner"); // #FF0033
            AddColorMenuItem(innerInnerColorMenu, "Neon Yellow", Color.FromArgb(238, 255, 0), "InnerShapeInner"); // #EEFF00
            AddColorMenuItem(innerInnerColorMenu, "Neon Green", Color.FromArgb(15, 255, 80), "InnerShapeInner"); // #0FFF50
            AddColorMenuItem(innerInnerColorMenu, "Neon Cyan", Color.FromArgb(0, 249, 255), "InnerShapeInner"); // #00F9FF
            AddColorMenuItem(innerInnerColorMenu, "White", Color.FromArgb(255, 255, 255), "InnerShapeInner"); // #FFFFFF
            AddColorMenuItem(innerInnerColorMenu, "Transparent", Color.Transparent, "InnerShapeInner");

            // Add custom color option
            var customInnerInnerColorItem = new ToolStripMenuItem("Custom...");
            customInnerInnerColorItem.Click += (sender, e) => ShowColorPicker("InnerShapeInner");
            innerInnerColorMenu.DropDownItems.Add(new ToolStripSeparator());
            innerInnerColorMenu.DropDownItems.Add(customInnerInnerColorItem);

            // Add all inner shape submenus
            innerShapeMenu.DropDownItems.AddRange(new ToolStripItem[] {
                innerSizeMenu,
                innerThicknessMenu,
                innerGapSizeMenu,
                innerEdgeColorMenu,
                innerInnerColorMenu
            });

            // Thickness submenu
            var thicknessMenu = new ToolStripMenuItem("Thickness");
            for (int thickness = 1; thickness <= 10; thickness++)
            {
                var thicknessItem = new ToolStripMenuItem(thickness.ToString());
                thicknessItem.Tag = thickness; // Store the thickness value in the Tag property
                thicknessItem.Checked = CurrentProfile.Thickness == thickness;
                int capturedThickness = thickness; // Capture the thickness value for the lambda
                thicknessItem.Click += (sender, e) => { UpdateCurrentProfileProperty("Thickness", capturedThickness); };
                thicknessMenu.DropDownItems.Add(thicknessItem);
            }

            // Gap Size submenu (for Cross and CrossDot)
            var gapSizeMenu = new ToolStripMenuItem("Gap Size");
            gapSizeMenu.Enabled = CurrentProfile.Shape == "Cross" || CurrentProfile.Shape == "CrossDot" || CurrentProfile.Shape == "CircleCross";
            for (int gap = 2; gap <= 20; gap += 2)
            {
                var gapItem = new ToolStripMenuItem(gap.ToString());
                gapItem.Tag = gap; // Store the gap value in the Tag property
                gapItem.Checked = CurrentProfile.GapSize == gap;
                int capturedGap = gap; // Capture the gap value for the lambda
                gapItem.Click += (sender, e) => { UpdateCurrentProfileProperty("GapSize", capturedGap); };
                gapSizeMenu.DropDownItems.Add(gapItem);
            }

            // Rendering submenu (AA toggle)
            var renderingMenu = new ToolStripMenuItem("Rendering");
            var aaItem = new ToolStripMenuItem("Smooth (Anti-alias)") { CheckOnClick = true };
            aaItem.Checked = _profileService.Current.AntiAlias;
            aaItem.CheckedChanged += (_, __) => {
                if (_suppressMenuEvents) return;
                _renderer.AntiAlias = aaItem.Checked;
                var cur = _profileService.Current.Clone();
                cur.AntiAlias = aaItem.Checked;
                _profileService.Update(cur);
                _configDirty = true; Invalidate(); };
            renderingMenu.DropDownItems.Add(aaItem);

            // Edge Color submenu
            var edgeColorMenu = new ToolStripMenuItem("Edge Color");

            // Add specified colors
            AddColorMenuItem(edgeColorMenu, "Neon Fuchsia", Color.FromArgb(255, 30, 255), "Edge"); // #FF1EFF
            AddColorMenuItem(edgeColorMenu, "Electric Red", Color.FromArgb(255, 0, 51), "Edge"); // #FF0033
            AddColorMenuItem(edgeColorMenu, "Neon Yellow", Color.FromArgb(238, 255, 0), "Edge"); // #EEFF00
            AddColorMenuItem(edgeColorMenu, "Neon Green", Color.FromArgb(15, 255, 80), "Edge"); // #0FFF50
            AddColorMenuItem(edgeColorMenu, "Neon Cyan", Color.FromArgb(0, 249, 255), "Edge"); // #00F9FF
            AddColorMenuItem(edgeColorMenu, "White", Color.FromArgb(255, 255, 255), "Edge"); // #FFFFFF
            AddColorMenuItem(edgeColorMenu, "Black", Color.FromArgb(0, 0, 0), "Edge"); // #000000
            AddColorMenuItem(edgeColorMenu, "Transparent", Color.Transparent, "Edge");

            // Add custom color option
            var customEdgeColorItem = new ToolStripMenuItem("Custom...");
            customEdgeColorItem.Click += (sender, e) => ShowColorPicker("Edge");
            edgeColorMenu.DropDownItems.Add(new ToolStripSeparator());
            edgeColorMenu.DropDownItems.Add(customEdgeColorItem);

            // Add Edge Thickness submenu
            var edgeThicknessMenu = new ToolStripMenuItem("Edge Thickness");
            for (int thickness = 1; thickness <= 10; thickness++)
            {
                var thicknessItem = new ToolStripMenuItem($"{thickness}px");
                thicknessItem.Tag = thickness;
                thicknessItem.Checked = CurrentProfile.EdgeThickness == thickness;
                int capturedThickness = thickness;
                thicknessItem.Click += (sender, e) => { UpdateCurrentProfileProperty("EdgeThickness", capturedThickness); };
                edgeThicknessMenu.DropDownItems.Add(thicknessItem);
            }
            edgeColorMenu.DropDownItems.Add(edgeThicknessMenu);

            // Inner Color submenu
            var innerColorMenu = new ToolStripMenuItem("Inner Color");

            // Add specified colors
            AddColorMenuItem(innerColorMenu, "Neon Fuchsia", Color.FromArgb(255, 30, 255), "Inner"); // #FF1EFF
            AddColorMenuItem(innerColorMenu, "Electric Red", Color.FromArgb(255, 0, 51), "Inner"); // #FF0033
            AddColorMenuItem(innerColorMenu, "Neon Yellow", Color.FromArgb(238, 255, 0), "Inner"); // #EEFF00
            AddColorMenuItem(innerColorMenu, "Neon Green", Color.FromArgb(15, 255, 80), "Inner"); // #0FFF50
            AddColorMenuItem(innerColorMenu, "Neon Cyan", Color.FromArgb(0, 249, 255), "Inner"); // #00F9FF
            AddColorMenuItem(innerColorMenu, "White", Color.FromArgb(255, 255, 255), "Inner"); // #FFFFFF
            AddColorMenuItem(innerColorMenu, "Transparent", Color.Transparent, "Inner");

            // Add custom color option
            var customInnerColorItem = new ToolStripMenuItem("Custom...");
            customInnerColorItem.Click += (sender, e) => ShowColorPicker("Inner");
            innerColorMenu.DropDownItems.Add(new ToolStripSeparator());
            innerColorMenu.DropDownItems.Add(customInnerColorItem);

            // Save current profile option
            var saveProfileItem = new ToolStripMenuItem("Save Current Profile");
            saveProfileItem.Click += (sender, e) => SaveCurrentProfile();

            // Screen recording detection option
            var hideRecordingItem = new ToolStripMenuItem("Hide during screen recording");
            hideRecordingItem.Checked = CurrentProfile.HideDuringScreenRecording;
            hideRecordingItem.Click += (sender, e) =>
            {
                var baseProfile = CurrentProfile;
                var profile = baseProfile.Clone();
                profile.HideDuringScreenRecording = !profile.HideDuringScreenRecording;
                _profileService.Update(profile);
                hideRecordingItem.Checked = profile.HideDuringScreenRecording;
            };

            // Close Menu option for user convenience
            var closeMenuItem = new ToolStripMenuItem("Close Menu");
            closeMenuItem.Click += (sender, e) =>
            {
                contextMenu?.Close();
            };



            // Minimal tray menu: About + Exit
            contextMenu.Items.Clear();

            var aboutItem = new ToolStripMenuItem("About");
            aboutItem.Click += (sender, e) =>
            {
                string text = "LightCrosshair\nAuthor: PrimeBuild\nLicense: MIT (2025)\nWebsite: https://primebuild.website/\nGitHub: https://github.com/PrimeBuild-pc/LightCrosshair";
                MessageBox.Show(this, text, "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (sender, e) =>
            {
                if (notifyIcon != null)
                {
                    notifyIcon.Visible = false;
                    notifyIcon.Dispose();
                }
                Application.Exit();
            };

            contextMenu.Items.Add(aboutItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(exitItem);

            // Set the context menu for both the form and notify icon

            this.ContextMenuStrip = contextMenu;


            if (notifyIcon != null)
                notifyIcon.ContextMenuStrip = contextMenu;
        }

    private void ContextMenu_Closing(object? sender, ToolStripDropDownClosingEventArgs e)
        {
            // Importante: permettiamo sempre la chiusura del menu.
            // In passato qui veniva annullata la chiusura e riaperto il menu subito dopo,
            // causando rientranza mentre gli handler di click modificavano il modello e ricostruivano il menu.
            // Questo portava a ObjectDisposedException/InvalidOperationException.
            Program.LogDebug($"ContextMenu_Closing reason={e.CloseReason}", nameof(Form1));
        }

        private void AddColorMenuItem(ToolStripMenuItem parentMenu, string name, Color color, string colorType)
        {
            var colorItem = new ToolStripMenuItem(name);
            colorItem.Tag = color; // Store the color in the Tag property for later comparison

            // Set initial checkmark based on current profile
            switch (colorType)
            {


                case "Edge":
                    colorItem.Checked = ColorEquals(CurrentProfile.EdgeColor, color);
                    break;
                case "Inner":
                    colorItem.Checked = ColorEquals(CurrentProfile.InnerColor, color);
                    break;
                case "Fill":
                    colorItem.Checked = ColorEquals(CurrentProfile.FillColor, color);
                    break;
            }

            colorItem.Click += (sender, e) =>
            {
                try
                {
                    Program.LogDebug($"Color click -> {name} ({color}) type={colorType}", nameof(Form1));
                    switch (colorType)
                    {
                        case "Edge":
                            UpdateCurrentProfileProperty("EdgeColor", color);
                            break;
                        case "Inner":
                            UpdateCurrentProfileProperty("InnerColor", color);
                            break;
                        case "Fill":
                            UpdateCurrentProfileProperty("FillColor", color);
                            break;
                    }
                    UpdateMenuItems();
                }
                catch (Exception ex) { Program.LogError(ex, "Color menu click"); }
            };
            parentMenu.DropDownItems.Add(colorItem);
        }

        private bool ColorEquals(Color c1, Color c2)
        {
            // Special case for transparent colors
            if (c1.A == 0 && c2.A == 0) return true;

            // If one is transparent and the other isn't, they're not equal
            if (c1.A == 0 || c2.A == 0) return false;

            // Compare RGB values for non-transparent colors
            return c1.R == c2.R && c1.G == c2.G && c1.B == c2.B;
        }

        private void UpdateCurrentProfileProperty(string propertyName, object value)
        {
            Program.LogDebug($"Update property {propertyName} -> {value}", nameof(Form1));
            var baseProfile = _profileService.Current;
            // No-op short-circuit to avoid redundant updates
            switch (propertyName)
            {
                case "Shape": if (string.Equals(baseProfile.Shape, (string)value, StringComparison.Ordinal)) return; break;
                case "Size": if (baseProfile.Size == (int)value) return; break;
                case "InnerSize": if (baseProfile.InnerSize == (int)value) return; break;
                case "Thickness": if (baseProfile.Thickness == (int)value) return; break;
                case "EdgeThickness": if (baseProfile.EdgeThickness == (int)value) return; break;
                case "InnerThickness": if (baseProfile.InnerThickness == (int)value) return; break;
                case "GapSize": if (baseProfile.GapSize == (int)value) return; break;
                case "InnerGapSize": if (baseProfile.InnerGapSize == (int)value) return; break;
                case "EdgeColor": if (baseProfile.EdgeColor.ToArgb() == ((Color)value).ToArgb()) return; break;
                case "InnerColor": if (baseProfile.InnerColor.ToArgb() == ((Color)value).ToArgb()) return; break;
                case "InnerShapeEdge": if (baseProfile.InnerShapeEdgeColor.ToArgb() == ((Color)value).ToArgb()) return; break;
                case "InnerShapeInner": if (baseProfile.InnerShapeInnerColor.ToArgb() == ((Color)value).ToArgb()) return; break;
                case "FillColor": if (baseProfile.FillColor.ToArgb() == ((Color)value).ToArgb()) return; break;
            }
            var profile = baseProfile.Clone();

            switch (propertyName)
            {
                case "Shape":
                    profile.Shape = (string)value;
                    break;
                case "Size":
                    profile.Size = (int)value;
                    break;
                case "InnerSize":
                    profile.InnerSize = (int)value;
                    break;
                case "Thickness":
                    profile.Thickness = (int)value;
                    break;
                case "EdgeThickness":
                    profile.EdgeThickness = (int)value;
                    break;
                case "InnerThickness":
                    profile.InnerThickness = (int)value;
                    break;
                case "GapSize":
                    profile.GapSize = (int)value;
                    break;
                case "InnerGapSize":
                    profile.InnerGapSize = (int)value;
                    break;
                case "EdgeColor":
                    profile.EdgeColor = (Color)value;
                    break;
                case "InnerColor":
                    profile.InnerColor = (Color)value;
                    break;
                case "InnerShapeEdge":
                    profile.InnerShapeEdgeColor = (Color)value;
                    break;
                case "InnerShapeInner":
                    profile.InnerShapeInnerColor = (Color)value;
                    break;
                case "FillColor":
                    profile.FillColor = (Color)value;
                    break;
            }

            try
            {
                _undo.Push(baseProfile.Clone());
                _profileService.Update(profile);
                // Mark for redraw and invalidate efficiently
                lock (_renderLock)
                {
                    _configDirty = true;
                    Invalidate();
                }
                UpdateMenuItems(propertyName);
            }
            catch (Exception ex)
            {
                Program.LogError(ex, $"UpdateCurrentProfileProperty({propertyName})");
            }

            // Autosave now handled by service (debounced)
        }

        private void ShowColorPicker(string colorType)
        {
            using (var colorDialog = new ColorDialog())
            {
                var baseProfile = CurrentProfile;
                switch (colorType)
                {
                    case "Edge":
                        colorDialog.Color = baseProfile.EdgeColor; break;
                    case "Inner":
                        colorDialog.Color = baseProfile.InnerColor; break;
                    case "InnerShapeEdge":
                        colorDialog.Color = baseProfile.InnerShapeEdgeColor; break;
                    case "InnerShapeInner":
                        colorDialog.Color = baseProfile.InnerShapeInnerColor; break;
                    case "Fill":
                        colorDialog.Color = baseProfile.FillColor.A == 0 ? Color.White : Color.FromArgb(255, baseProfile.FillColor.R, baseProfile.FillColor.G, baseProfile.FillColor.B); break;
                }

                colorDialog.FullOpen = true;
                colorDialog.AnyColor = true;
                colorDialog.AllowFullOpen = true;
                colorDialog.SolidColorOnly = false;

                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    switch (colorType)
                    {
                        case "Edge":   UpdateCurrentProfileProperty("EdgeColor", Color.FromArgb(255, colorDialog.Color)); break;
                        case "Inner":  UpdateCurrentProfileProperty("InnerColor", Color.FromArgb(255, colorDialog.Color)); break;
                        case "InnerShapeEdge": UpdateCurrentProfileProperty("InnerShapeEdge", Color.FromArgb(255, colorDialog.Color)); break;
                        case "InnerShapeInner": UpdateCurrentProfileProperty("InnerShapeInner", Color.FromArgb(255, colorDialog.Color)); break;
                        case "Fill":   UpdateCurrentProfileProperty("FillColor", Color.FromArgb(180, colorDialog.Color)); break;
                    }
                }
            }
        }

        private void SaveCurrentProfile()
        {
            var baseProfile = CurrentProfile.Clone();
            string name = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter a name for this profile:", "Save Profile", baseProfile.Name);

            if (!string.IsNullOrWhiteSpace(name))
            {
                baseProfile.Name = name;
                // Add as new clone (keep current too)
                var newClone = _profileService.AddClone(CurrentProfile, name);
                _profileService.Switch(newClone.Id);
                InitializeContextMenu();
            }
            _ = _profileService.PersistAsync();
        }

        private void ShowProfileManager()
        {
            using (var dlg = new ProfilesDialog(_profileService))
            {
                dlg.ShowDialog(this);
                // Rebuild context menu to reflect profile name / hotkey changes
                InitializeContextMenu();
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (this.Handle != IntPtr.Zero)
            {
                int exStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);
                exStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW;
                SetWindowLong(this.Handle, GWL_EXSTYLE, exStyle);
            }
            RegisterHotKey(this.Handle, TOGGLE_VISIBILITY_HOTKEY_ID, MOD_ALT, (int)Keys.X);
            RegisterHotKey(this.Handle, TOGGLE_VISIBILITY_HOTKEY_ID + 1, MOD_CONTROL | MOD_SHIFT, (int)Keys.Right);
            RegisterHotKey(this.Handle, TOGGLE_VISIBILITY_HOTKEY_ID + 2, MOD_CONTROL | MOD_SHIFT, (int)Keys.Left);
            _profileService.RegisterHotkeys(this.Handle);
        }

        protected override void WndProc(ref Message m)
        {
            // Handle hotkey messages
            if (m.Msg == WM_HOTKEY)
            {
                int hotkeyId = m.WParam.ToInt32();

                if (hotkeyId == TOGGLE_VISIBILITY_HOTKEY_ID)
                {
                    ToggleVisibility();
                    return;
                }
                if (hotkeyId == TOGGLE_VISIBILITY_HOTKEY_ID + 1)
                {
                    CycleProfile(1);
                    return;
                }
                if (hotkeyId == TOGGLE_VISIBILITY_HOTKEY_ID + 2)
                {
                    CycleProfile(-1);
                    return;
                }

                // Let profile service process per-profile custom hotkeys
                if (_profileService.ProcessHotkeyMessage(m)) return;
            }

            base.WndProc(ref m);
        }

    private void Form1_Paint(object? sender, PaintEventArgs e)
        {
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
                    try
                    {
                        e.Graphics.Clear(this.TransparencyKey);
                        // Center bitmap in client area in case form larger than bitmap
                        int x = (ClientSize.Width - _currentFrame.Width) / 2;
                        int y = (ClientSize.Height - _currentFrame.Height) / 2;
                        e.Graphics.DrawImageUnscaled(_currentFrame, x, y);
                    }
                    catch (Exception ex) when (ex is ArgumentException || ex is ObjectDisposedException)
                    {
                        // The cached frame became invalid (disposed or corrupted) mid-draw.
                        // Reset and request a fresh render on next paint.
                        try { Program.LogError(ex, "Form1_Paint: frame invalid"); } catch { }
                        _currentFrame = null;
                        _configDirty = true;
                        Invalidate();
                    }
                }
            }
        }

        private void ToggleVisibility()
        {
            isVisible = !isVisible;
            this.Visible = isVisible;
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

        private void UpdateShape(CrosshairShape shape, string legacyString)
        {
            try
            {
                Program.LogDebug($"Update shape -> {legacyString} ({shape})", nameof(Form1));
                var baseProfile = _profileService.Current;
                var clone = baseProfile.Clone();
                clone.EnumShape = shape;
                clone.Shape = legacyString; // keep for legacy persisted field
                // Apply recommended defaults for specific composite shapes
                try
                {
                    if (string.Equals(legacyString, "CircleDot", StringComparison.OrdinalIgnoreCase))
                    {
                        var d = CompositeDefaults.GetCompositeDefaults(CompositeShapeType.CircleDot);
                        if (d != null)
                        {
                            clone.Size = d.OuterSize;
                            clone.Thickness = d.OuterThickness;
                            clone.GapSize = d.OuterGapSize;
                            clone.InnerSize = d.InnerSize;
                            clone.InnerThickness = d.InnerThickness;
                            clone.InnerGapSize = d.InnerGapSize;
                        }
                    }
                    else if (string.Equals(legacyString, "CrossDot", StringComparison.OrdinalIgnoreCase))
                    {
                        var d = CompositeDefaults.GetCompositeDefaults(CompositeShapeType.CrossDot);
                        if (d != null)
                        {
                            clone.Size = d.OuterSize;
                            clone.Thickness = d.OuterThickness;
                            clone.GapSize = d.OuterGapSize;
                            clone.InnerSize = d.InnerSize;
                            clone.InnerThickness = d.InnerThickness;
                            clone.InnerGapSize = d.InnerGapSize;
                        }
                    }
                }
                catch { }
                _profileService.Update(clone);
                _undo.Push(baseProfile.Clone());
                lock (_renderLock) { _configDirty = true; Invalidate(); }
                UpdateMenuItems("Shape");
            }
            catch (Exception ex) { Program.LogError(ex, "UpdateShape"); }
        }

        private void CenterCrosshair()
        {
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
                int padding = Math.Max(p.Thickness * 2, 10);
                int formSize = Math.Max(100, maxCrosshairSize + padding * 2);
                if (formSize % 2 == 0) formSize++;
                this.Size = new Size(formSize, formSize);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Unregister profile service hotkeys
            _profileService.UnregisterHotkeys();

            // Unregister the toggle visibility hotkey
            UnregisterHotKey(this.Handle, TOGGLE_VISIBILITY_HOTKEY_ID);

            // Clean up resources
            if (notifyIcon != null)
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
            }
            contextMenu?.Dispose();

            // Stop and dispose recording detection timer
            if (_recordingDetectionTimer != null)
            {
                _recordingDetectionTimer.Stop();
                _recordingDetectionTimer.Dispose();
            }


            // Clear graphics cache
            ClearGraphicsCache();

            try { _ = _profileService.PersistAsync(); } catch (Exception ex) { Program.LogError(ex, "PersistAsync on close"); }

            base.OnFormClosing(e);
        }


        private void ReinforceTopMost()
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOOWNERZORDER | SWP_NOSENDCHANGING); } catch { }
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
                catch { }
            };

            WpfSettingsHost.GetPosition = () =>
            {
                try { return this.Location; } catch { return this.Location; }
            };

            WpfSettingsHost.ResetCenter = () =>
            {
                try { CenterCrosshair(); } catch { }
            };
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // Renderer owns cached bitmap; dispose renderer on form close to release GDI resources
            try { _renderer.Dispose(); } catch { }
            _currentFrame = null;
            base.OnFormClosed(e);
        }


        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            CenterCrosshair();
        }

        // TriggerAutosave removed; persistence handled by ProfileService.

        private void UndoLastChange()
        {
            if (_undo.TryPop(out var prev))
            {
                _profileService.Update(prev);
                _profileService.Switch(prev.Id);
                if (_lblSaved != null) _lblSaved.Text = "Reverted";
            }
        }

        private void MarkConfigDirty()
        {
            lock (_renderLock)
            {
                _configDirty = true;
                Invalidate();
            }
        }


    }
}
