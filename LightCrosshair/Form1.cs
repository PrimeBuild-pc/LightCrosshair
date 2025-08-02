using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LightCrosshair
{
    public partial class Form1 : Form
    {
        private NotifyIcon notifyIcon;
        private ContextMenuStrip contextMenu;
        private ProfileManager profileManager;
        private Color fillColor = Color.Transparent;
        private bool isVisible = true;

        // Performance optimization fields
        private CrosshairProfile _lastRenderedProfile;
        private bool _needsRedraw = true;
        private readonly object _renderLock = new object();
        private float _dpiScaleFactor = 1.0f;

        // Object pooling for graphics resources
        private readonly Dictionary<Color, SolidBrush> _brushCache = new Dictionary<Color, SolidBrush>();
        private readonly Dictionary<(Color, float), Pen> _penCache = new Dictionary<(Color, float), Pen>();

        // Screen recording detection
        private System.Windows.Forms.Timer _recordingDetectionTimer;
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

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_TOPMOST = 0x8;

        // Modifiers for hotkeys
        private const int MOD_ALT = 0x0001;
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_SHIFT = 0x0004;
        private const int MOD_WIN = 0x0008;

        // Hotkey IDs
        private const int TOGGLE_VISIBILITY_HOTKEY_ID = 9000;

        public Form1()
        {
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

            // Initialize profile manager
            profileManager = new ProfileManager(this.Handle);
            profileManager.ProfileChanged += ProfileManager_ProfileChanged;

            // Initialize system tray icon and menu
            InitializeNotifyIcon();
            InitializeContextMenu();

            // Add paint handler
            this.Paint += Form1_Paint;

            // Initialize DPI awareness
            InitializeDpiAwareness();

            // Initialize screen recording detection
            InitializeScreenRecordingDetection();

            // Center the form on screen
            CenterCrosshair();
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

        private SolidBrush GetCachedBrush(Color color)
        {
            if (color.A == 0) return null; // Don't cache transparent brushes

            if (!_brushCache.TryGetValue(color, out SolidBrush brush))
            {
                brush = new SolidBrush(color);
                _brushCache[color] = brush;
            }
            return brush;
        }

        private Pen GetCachedPen(Color color, float width)
        {
            if (color.A == 0) return null; // Don't cache transparent pens

            var key = (color, width);
            if (!_penCache.TryGetValue(key, out Pen pen))
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

        private void RecordingDetectionTimer_Tick(object sender, EventArgs e)
        {
            if (profileManager?.CurrentProfile?.HideDuringScreenRecording == true)
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

        private void ProfileManager_ProfileChanged(object sender, CrosshairProfile profile)
        {
            // Mark that we need a redraw and invalidate only if profile actually changed
            lock (_renderLock)
            {
                if (_lastRenderedProfile == null || !ProfilesEqual(_lastRenderedProfile, profile))
                {
                    _needsRedraw = true;

                    // Update form size and recenter for optimal rendering
                    UpdateFormSize();
                    CenterCrosshair();

                    this.Invalidate();
                }
            }

            // Update menu checkmarks
            UpdateMenuItems();
        }

        private bool ProfilesEqual(CrosshairProfile a, CrosshairProfile b)
        {
            return a.Shape == b.Shape &&
                   a.Size == b.Size &&
                   a.Thickness == b.Thickness &&
                   a.EdgeColor == b.EdgeColor &&
                   a.InnerColor == b.InnerColor &&
                   a.FillColor == b.FillColor &&
                   a.InnerShape == b.InnerShape &&
                   a.InnerSize == b.InnerSize &&
                   a.InnerThickness == b.InnerThickness &&
                   a.InnerShapeEdgeColor == b.InnerShapeEdgeColor &&
                   a.InnerShapeInnerColor == b.InnerShapeInnerColor &&
                   a.InnerShapeFillColor == b.InnerShapeFillColor;
        }

        private void UpdateMenuItems(string? changedProperty = null)
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
                        // Only update inner size menu within inner shape menu
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
                        // Only update inner thickness menu within inner shape menu
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
                        // Only update inner gap size menu within inner shape menu
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
                        // Only update inner shape color menus
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
            UpdateShapeMenuItems();
            UpdateSizeMenuItems();
            UpdateThicknessMenuItems();
            UpdateEdgeThicknessMenuItems();
            UpdateGapSizeMenuItems();
            UpdateColorMenuItems();
            UpdateInnerShapeMenuItems();
        }

        private void UpdateShapeMenuItems()
        {
            // Find the shape menu
            var shapeMenu = FindMenuItemByText(contextMenu.Items, "Shape");
            if (shapeMenu == null) return;

            // Update basic shapes
            var basicShapesMenu = FindMenuItemByText(shapeMenu.DropDownItems, "Basic Shapes");
            if (basicShapesMenu != null)
            {
                foreach (ToolStripItem item in basicShapesMenu.DropDownItems)
                {
                    // Skip separators and non-menu items
                    if (!(item is ToolStripMenuItem menuItem)) continue;

                    if (menuItem.Tag is string shape)
                    {
                        menuItem.Checked = profileManager.CurrentProfile.Shape == shape;
                    }
                }
            }

            // Update combined shapes
            var combinedShapesMenu = FindMenuItemByText(shapeMenu.DropDownItems, "Combined Shapes");
            if (combinedShapesMenu != null)
            {
                foreach (ToolStripItem item in combinedShapesMenu.DropDownItems)
                {
                    // Skip separators and non-menu items
                    if (!(item is ToolStripMenuItem menuItem)) continue;

                    if (menuItem.Tag is string shape)
                    {
                        menuItem.Checked = profileManager.CurrentProfile.Shape == shape;
                    }
                }
            }
        }

        private void UpdateSizeMenuItems()
        {
            // Find the size menu - try both possible names
            var sizeMenu = FindMenuItemByText(contextMenu.Items, "Outer Shape Size");
            if (sizeMenu == null)
            {
                sizeMenu = FindMenuItemByText(contextMenu.Items, "Size");
            }
            if (sizeMenu == null) return;

            // Enable/disable based on shape type
            bool isCombinedShape = profileManager.CurrentProfile.Shape.StartsWith("Circle") ||
                                  profileManager.CurrentProfile.Shape == "CrossDot";
            sizeMenu.Text = isCombinedShape ? "Outer Shape Size" : "Size";

            foreach (ToolStripItem item in sizeMenu.DropDownItems)
            {
                // Skip separators and non-menu items
                if (!(item is ToolStripMenuItem menuItem)) continue;

                if (menuItem.Tag is int size)
                {
                    menuItem.Checked = profileManager.CurrentProfile.Size == size;
                }
            }
        }

        private void UpdateInnerSizeMenuItems()
        {
            // Find the inner size menu
            var innerSizeMenu = FindMenuItemByText(contextMenu.Items, "Inner Shape Size");
            if (innerSizeMenu == null) return;

            // Enable/disable based on shape type
            bool isCombinedShape = profileManager.CurrentProfile.Shape.StartsWith("Circle") ||
                                  profileManager.CurrentProfile.Shape == "CrossDot";
            innerSizeMenu.Enabled = isCombinedShape;

            // Update the menu text to indicate which shape it applies to
            if (profileManager.CurrentProfile.Shape == "CircleDot")
                innerSizeMenu.Text = "Dot Size";
            else if (profileManager.CurrentProfile.Shape == "CircleCross")
                innerSizeMenu.Text = "Cross Size";
            else if (profileManager.CurrentProfile.Shape == "CirclePlus")
                innerSizeMenu.Text = "Plus Size";
            else if (profileManager.CurrentProfile.Shape == "CircleX")
                innerSizeMenu.Text = "X Size";
            else if (profileManager.CurrentProfile.Shape == "CrossDot")
                innerSizeMenu.Text = "Dot Size";
            else
                innerSizeMenu.Text = "Inner Shape Size";

            foreach (ToolStripItem item in innerSizeMenu.DropDownItems)
            {
                // Skip separators and non-menu items
                if (!(item is ToolStripMenuItem menuItem)) continue;

                if (menuItem.Tag is int size)
                {
                    menuItem.Checked = profileManager.CurrentProfile.InnerSize == size;
                }
            }
        }

        private void UpdateThicknessMenuItems()
        {
            // Find the thickness menu
            var thicknessMenu = FindMenuItemByText(contextMenu.Items, "Thickness");
            if (thicknessMenu == null) return;

            foreach (ToolStripItem item in thicknessMenu.DropDownItems)
            {
                // Skip separators and non-menu items
                if (!(item is ToolStripMenuItem menuItem)) continue;

                if (menuItem.Tag is int thickness)
                {
                    menuItem.Checked = profileManager.CurrentProfile.Thickness == thickness;
                }
            }
        }

        private void UpdateEdgeThicknessMenuItems()
        {
            // Find the edge color menu first
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
                    menuItem.Checked = profileManager.CurrentProfile.EdgeThickness == thickness;
                }
            }
        }

        private void UpdateGapSizeMenuItems()
        {
            // Find the gap size menu
            var gapSizeMenu = FindMenuItemByText(contextMenu.Items, "Gap Size");
            if (gapSizeMenu == null) return;

            // Enable/disable the gap size menu based on the current shape
            bool isPlus = profileManager.CurrentProfile.Shape == "Plus" ||
                          profileManager.CurrentProfile.Shape == "CirclePlus";
            gapSizeMenu.Enabled = isPlus;

            // Update the menu text to indicate which shape it applies to
            if (profileManager.CurrentProfile.Shape == "Plus")
                gapSizeMenu.Text = "Plus Gap Size";
            else if (profileManager.CurrentProfile.Shape == "CirclePlus")
                gapSizeMenu.Text = "Plus Gap Size";
            else
                gapSizeMenu.Text = "Gap Size";

            foreach (ToolStripItem item in gapSizeMenu.DropDownItems)
            {
                // Skip separators and non-menu items
                if (!(item is ToolStripMenuItem menuItem)) continue;

                if (menuItem.Tag is int gap)
                {
                    menuItem.Checked = profileManager.CurrentProfile.GapSize == gap;
                }
            }
        }

        private void UpdateColorMenuItems()
        {
            // Update edge color menu
            var edgeColorMenu = FindMenuItemByText(contextMenu.Items, "Edge Color");
            if (edgeColorMenu != null)
            {
                foreach (ToolStripItem item in edgeColorMenu.DropDownItems)
                {
                    // Skip separators and non-menu items
                    if (!(item is ToolStripMenuItem menuItem)) continue;

                    if (menuItem.Tag is Color color)
                    {
                        menuItem.Checked = ColorEquals(profileManager.CurrentProfile.EdgeColor, color);
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
                        menuItem.Checked = ColorEquals(profileManager.CurrentProfile.InnerColor, color);
                    }
                }
            }
        }

        private void UpdateInnerShapeMenuItems()
        {
            // Find the inner shape menu
            var innerShapeMenu = FindMenuItemByText(contextMenu.Items, "Inner Shape");
            if (innerShapeMenu == null) return;

            // Enable/disable based on shape type
            bool isCombinedShape = profileManager.CurrentProfile.Shape.StartsWith("Circle") ||
                                  profileManager.CurrentProfile.Shape == "CrossDot";
            innerShapeMenu.Enabled = isCombinedShape;

            if (!isCombinedShape) return;

            // Update the menu text to indicate which shape it applies to
            string innerShapeName = "";
            if (profileManager.CurrentProfile.Shape == "CircleDot" || profileManager.CurrentProfile.Shape == "CrossDot")
                innerShapeName = "Dot";
            else if (profileManager.CurrentProfile.Shape == "CircleCross")
                innerShapeName = "Cross";
            else if (profileManager.CurrentProfile.Shape == "CirclePlus")
                innerShapeName = "Plus";
            else if (profileManager.CurrentProfile.Shape == "CircleX")
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

                    if (menuItem.Tag is int size)
                    {
                        menuItem.Checked = profileManager.CurrentProfile.InnerSize == size;
                    }
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

                    if (menuItem.Tag is int thickness)
                    {
                        menuItem.Checked = profileManager.CurrentProfile.InnerThickness == thickness;
                    }
                }
            }

            // Update inner gap size menu
            var innerGapSizeMenu = FindMenuItemByText(innerShapeMenu.DropDownItems, "Gap Size");
            if (innerGapSizeMenu != null)
            {
                // Enable/disable the gap size menu based on the current shape
                bool isInnerPlus = profileManager.CurrentProfile.Shape == "CirclePlus";
                innerGapSizeMenu.Enabled = isInnerPlus;

                foreach (ToolStripItem item in innerGapSizeMenu.DropDownItems)
                {
                    // Skip separators and non-menu items
                    if (!(item is ToolStripMenuItem menuItem)) continue;

                    if (menuItem.Tag is int gap)
                    {
                        menuItem.Checked = profileManager.CurrentProfile.InnerGapSize == gap;
                    }
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
                        menuItem.Checked = ColorEquals(profileManager.CurrentProfile.InnerShapeEdgeColor, color);
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
                        menuItem.Checked = ColorEquals(profileManager.CurrentProfile.InnerShapeInnerColor, color);
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
                    menuItem.Checked = profileManager.CurrentProfile.InnerSize == size;
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
                    menuItem.Checked = profileManager.CurrentProfile.InnerThickness == thickness;
                }
            }
        }

        private void UpdateInnerGapSizeMenu(ToolStripMenuItem menu)
        {
            // Enable/disable the gap size menu based on the current shape
            bool isInnerPlus = profileManager.CurrentProfile.Shape == "CirclePlus";
            menu.Enabled = isInnerPlus;

            foreach (ToolStripItem item in menu.DropDownItems)
            {
                // Skip separators and non-menu items
                if (!(item is ToolStripMenuItem menuItem)) continue;

                if (menuItem.Tag is int gap)
                {
                    menuItem.Checked = profileManager.CurrentProfile.InnerGapSize == gap;
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
                    menuItem.Checked = ColorEquals(profileManager.CurrentProfile.InnerShapeEdgeColor, color);
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
                    menuItem.Checked = ColorEquals(profileManager.CurrentProfile.InnerShapeInnerColor, color);
                }
            }
        }

        private ToolStripMenuItem FindMenuItemByText(ToolStripItemCollection items, string text)
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

            // Profiles submenu
            var profilesMenu = new ToolStripMenuItem("Profiles");

            // Add profile management option
            var manageProfilesItem = new ToolStripMenuItem("Manage Profiles...");
            manageProfilesItem.Click += (sender, e) => ShowProfileManager();
            profilesMenu.DropDownItems.Add(manageProfilesItem);

            // Add separator
            profilesMenu.DropDownItems.Add(new ToolStripSeparator());

            // Add profiles from profile manager
            foreach (var profile in profileManager.Profiles)
            {
                var profileItem = new ToolStripMenuItem(profile.Name);
                profileItem.Click += (sender, e) => profileManager.SwitchToProfile(profile.Name);
                profilesMenu.DropDownItems.Add(profileItem);
            }

            // Shape submenu
            var shapeMenu = new ToolStripMenuItem("Shape");

            // Basic shapes
            var basicShapesMenu = new ToolStripMenuItem("Basic Shapes");
            var crossItem = new ToolStripMenuItem("Cross");
            crossItem.Tag = "Cross";
            crossItem.Checked = profileManager.CurrentProfile.Shape == "Cross";
            crossItem.Click += (sender, e) => { UpdateCurrentProfileProperty("Shape", "Cross"); };

            var circleItem = new ToolStripMenuItem("Circle");
            circleItem.Tag = "Circle";
            circleItem.Checked = profileManager.CurrentProfile.Shape == "Circle";
            circleItem.Click += (sender, e) => { UpdateCurrentProfileProperty("Shape", "Circle"); };

            var dotItem = new ToolStripMenuItem("Dot");
            dotItem.Tag = "Dot";
            dotItem.Checked = profileManager.CurrentProfile.Shape == "Dot";
            dotItem.Click += (sender, e) => { UpdateCurrentProfileProperty("Shape", "Dot"); };

            var plusItem = new ToolStripMenuItem("Plus");
            plusItem.Tag = "Plus";
            plusItem.Checked = profileManager.CurrentProfile.Shape == "Plus";
            plusItem.Click += (sender, e) => { UpdateCurrentProfileProperty("Shape", "Plus"); };

            var xItem = new ToolStripMenuItem("X");
            xItem.Tag = "X";
            xItem.Checked = profileManager.CurrentProfile.Shape == "X";
            xItem.Click += (sender, e) => { UpdateCurrentProfileProperty("Shape", "X"); };

            basicShapesMenu.DropDownItems.AddRange(new ToolStripItem[] { crossItem, circleItem, dotItem, plusItem, xItem });

            // Combined shapes
            var combinedShapesMenu = new ToolStripMenuItem("Combined Shapes");

            var circleDotItem = new ToolStripMenuItem("Circle + Dot");
            circleDotItem.Tag = "CircleDot";
            circleDotItem.Checked = profileManager.CurrentProfile.Shape == "CircleDot";
            circleDotItem.Click += (sender, e) => { UpdateCurrentProfileProperty("Shape", "CircleDot"); };

            var crossDotItem = new ToolStripMenuItem("Cross + Dot");
            crossDotItem.Tag = "CrossDot";
            crossDotItem.Checked = profileManager.CurrentProfile.Shape == "CrossDot";
            crossDotItem.Click += (sender, e) => { UpdateCurrentProfileProperty("Shape", "CrossDot"); };

            var circleCrossItem = new ToolStripMenuItem("Circle + Cross");
            circleCrossItem.Tag = "CircleCross";
            circleCrossItem.Checked = profileManager.CurrentProfile.Shape == "CircleCross";
            circleCrossItem.Click += (sender, e) => { UpdateCurrentProfileProperty("Shape", "CircleCross"); };

            var circlePlusItem = new ToolStripMenuItem("Circle + Plus");
            circlePlusItem.Tag = "CirclePlus";
            circlePlusItem.Checked = profileManager.CurrentProfile.Shape == "CirclePlus";
            circlePlusItem.Click += (sender, e) => { UpdateCurrentProfileProperty("Shape", "CirclePlus"); };

            var circleXItem = new ToolStripMenuItem("Circle + X");
            circleXItem.Tag = "CircleX";
            circleXItem.Checked = profileManager.CurrentProfile.Shape == "CircleX";
            circleXItem.Click += (sender, e) => { UpdateCurrentProfileProperty("Shape", "CircleX"); };

            combinedShapesMenu.DropDownItems.AddRange(new ToolStripItem[] { circleDotItem, crossDotItem, circleCrossItem, circlePlusItem, circleXItem });

            // Add both submenus to the shape menu
            shapeMenu.DropDownItems.AddRange(new ToolStripItem[] { basicShapesMenu, combinedShapesMenu });

            // Size submenu (for outer shape in combined shapes)
            var sizeMenu = new ToolStripMenuItem("Outer Shape Size");
            bool isCombinedShape = profileManager.CurrentProfile.Shape.StartsWith("Circle") ||
                                  profileManager.CurrentProfile.Shape == "CrossDot";

            for (int size = 5; size <= 100; size += 5)
            {
                var sizeItem = new ToolStripMenuItem($"{size}%");
                sizeItem.Tag = size; // Store the size value in the Tag property
                sizeItem.Checked = profileManager.CurrentProfile.Size == size;
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
                sizeItem.Checked = profileManager.CurrentProfile.InnerSize == size;
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
                thicknessItem.Checked = profileManager.CurrentProfile.InnerThickness == thickness;
                int capturedThickness = thickness; // Capture the thickness value for the lambda
                thicknessItem.Click += (sender, e) => { UpdateCurrentProfileProperty("InnerThickness", capturedThickness); };
                innerThicknessMenu.DropDownItems.Add(thicknessItem);
            }

            // Inner Gap Size submenu (for Plus shape)
            var innerGapSizeMenu = new ToolStripMenuItem("Gap Size");
            innerGapSizeMenu.Enabled = profileManager.CurrentProfile.Shape == "CirclePlus"; // Only enable for CirclePlus

            for (int gap = 2; gap <= 20; gap += 2)
            {
                var gapItem = new ToolStripMenuItem(gap.ToString());
                gapItem.Tag = gap; // Store the gap value in the Tag property
                gapItem.Checked = profileManager.CurrentProfile.InnerGapSize == gap;
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
                thicknessItem.Checked = profileManager.CurrentProfile.Thickness == thickness;
                int capturedThickness = thickness; // Capture the thickness value for the lambda
                thicknessItem.Click += (sender, e) => { UpdateCurrentProfileProperty("Thickness", capturedThickness); };
                thicknessMenu.DropDownItems.Add(thicknessItem);
            }

            // Gap Size submenu (for Plus shape)
            var gapSizeMenu = new ToolStripMenuItem("Gap Size");
            gapSizeMenu.Enabled = profileManager.CurrentProfile.Shape == "Plus" ||
                                  profileManager.CurrentProfile.Shape == "CirclePlus";
            for (int gap = 2; gap <= 20; gap += 2)
            {
                var gapItem = new ToolStripMenuItem(gap.ToString());
                gapItem.Tag = gap; // Store the gap value in the Tag property
                gapItem.Checked = profileManager.CurrentProfile.GapSize == gap;
                int capturedGap = gap; // Capture the gap value for the lambda
                gapItem.Click += (sender, e) => { UpdateCurrentProfileProperty("GapSize", capturedGap); };
                gapSizeMenu.DropDownItems.Add(gapItem);
            }

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
                thicknessItem.Checked = profileManager.CurrentProfile.EdgeThickness == thickness;
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
            hideRecordingItem.Checked = profileManager.CurrentProfile.HideDuringScreenRecording;
            hideRecordingItem.Click += (sender, e) =>
            {
                var profile = profileManager.CurrentProfile.Clone();
                profile.HideDuringScreenRecording = !profile.HideDuringScreenRecording;
                profileManager.UpdateProfile(profile);
                hideRecordingItem.Checked = profile.HideDuringScreenRecording;
            };

            // Close Menu option for user convenience
            var closeMenuItem = new ToolStripMenuItem("Close Menu");
            closeMenuItem.Click += (sender, e) =>
            {
                contextMenu.Close();
            };

            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (sender, e) =>
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
                Application.Exit();
            };

            // Add all items to context menu
            contextMenu.Items.AddRange(new ToolStripItem[]
            {
                visibilityItem,
                profilesMenu,
                new ToolStripSeparator(),
                shapeMenu,
                sizeMenu,
                thicknessMenu,
                gapSizeMenu,
                edgeColorMenu,
                innerColorMenu,
                new ToolStripSeparator(),
                innerShapeMenu,
                new ToolStripSeparator(),
                hideRecordingItem,
                saveProfileItem,
                new ToolStripSeparator(),
                closeMenuItem,
                exitItem
            });

            // Set the context menu for both the form and notify icon
            this.ContextMenuStrip = contextMenu;
            notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void ContextMenu_Closing(object sender, ToolStripDropDownClosingEventArgs e)
        {
            // Allow closing for these specific reasons:
            // - User clicked outside the menu
            // - User pressed Escape
            // - Programmatic close (like our Close Menu button)
            // - Application shutdown
            if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked)
            {
                // Check if the clicked item should close the menu
                var clickedItem = contextMenu.GetItemAt(contextMenu.PointToClient(Cursor.Position));
                if (clickedItem != null)
                {
                    // Allow closing for specific items
                    string itemText = clickedItem.Text;
                    if (itemText == "Close Menu" || itemText == "Exit" || itemText == "Toggle Visibility")
                    {
                        return; // Allow closing
                    }
                }

                // For all other menu items, prevent closing
                e.Cancel = true;

                // Re-show the menu at the same position after a brief delay
                Task.Delay(50).ContinueWith(_ =>
                {
                    if (!contextMenu.IsDisposed && contextMenu.Visible == false)
                    {
                        this.Invoke(new Action(() =>
                        {
                            if (!contextMenu.IsDisposed)
                            {
                                contextMenu.Show(Cursor.Position);
                            }
                        }));
                    }
                });
            }
        }

        private void AddColorMenuItem(ToolStripMenuItem parentMenu, string name, Color color, string colorType)
        {
            var colorItem = new ToolStripMenuItem(name);
            colorItem.Tag = color; // Store the color in the Tag property for later comparison

            // Set initial checkmark based on current profile
            switch (colorType)
            {
                case "Edge":
                    colorItem.Checked = ColorEquals(profileManager.CurrentProfile.EdgeColor, color);
                    break;
                case "Inner":
                    colorItem.Checked = ColorEquals(profileManager.CurrentProfile.InnerColor, color);
                    break;
                case "Fill":
                    colorItem.Checked = ColorEquals(profileManager.CurrentProfile.FillColor, color);
                    break;
            }

            colorItem.Click += (sender, e) =>
            {
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

                // Update checkmarks
                UpdateMenuItems();
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
            var profile = profileManager.CurrentProfile.Clone();

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

            profileManager.UpdateProfile(profile);

            // Mark for redraw and invalidate efficiently
            lock (_renderLock)
            {
                _needsRedraw = true;
                this.Invalidate();
            }

            // Update only the relevant menu items
            UpdateMenuItems(propertyName);
        }

        private void ShowColorPicker(string colorType)
        {
            using (var colorDialog = new ColorDialog())
            {
                switch (colorType)
                {
                    case "Edge":
                        colorDialog.Color = profileManager.CurrentProfile.EdgeColor;
                        break;
                    case "Inner":
                        colorDialog.Color = profileManager.CurrentProfile.InnerColor;
                        break;
                    case "InnerShapeEdge":
                        colorDialog.Color = profileManager.CurrentProfile.InnerShapeEdgeColor;
                        break;
                    case "InnerShapeInner":
                        colorDialog.Color = profileManager.CurrentProfile.InnerShapeInnerColor;
                        break;
                    case "Fill":
                        // If fill color is transparent, use a default color for the dialog
                        if (profileManager.CurrentProfile.FillColor.A == 0)
                            colorDialog.Color = Color.White;
                        else
                            colorDialog.Color = Color.FromArgb(255, profileManager.CurrentProfile.FillColor.R,
                                profileManager.CurrentProfile.FillColor.G, profileManager.CurrentProfile.FillColor.B);
                        break;
                }

                colorDialog.FullOpen = true;
                colorDialog.AnyColor = true;
                colorDialog.AllowFullOpen = true;
                colorDialog.SolidColorOnly = false;

                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    switch (colorType)
                    {
                        case "Edge":
                            // Use full opacity for edge color
                            Color edgeColor = Color.FromArgb(255, colorDialog.Color);
                            UpdateCurrentProfileProperty("EdgeColor", edgeColor);
                            break;
                        case "Inner":
                            // Use full opacity for inner color
                            Color innerColor = Color.FromArgb(255, colorDialog.Color);
                            UpdateCurrentProfileProperty("InnerColor", innerColor);
                            break;
                        case "InnerShapeEdge":
                            // Use full opacity for inner shape edge color
                            Color innerShapeEdgeColor = Color.FromArgb(255, colorDialog.Color);
                            UpdateCurrentProfileProperty("InnerShapeEdge", innerShapeEdgeColor);
                            break;
                        case "InnerShapeInner":
                            // Use full opacity for inner shape inner color
                            Color innerShapeInnerColor = Color.FromArgb(255, colorDialog.Color);
                            UpdateCurrentProfileProperty("InnerShapeInner", innerShapeInnerColor);
                            break;
                        case "Fill":
                            // For fill color, we want some transparency
                            Color fillColor = Color.FromArgb(180, colorDialog.Color);
                            UpdateCurrentProfileProperty("FillColor", fillColor);
                            break;
                    }
                }
            }
        }

        private void SaveCurrentProfile()
        {
            // Clone the current profile to avoid modifying it directly
            var profile = profileManager.CurrentProfile.Clone();

            // Ask for a profile name
            string name = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter a name for this profile:",
                "Save Profile",
                profile.Name);

            if (!string.IsNullOrWhiteSpace(name))
            {
                profile.Name = name;
                profileManager.UpdateProfile(profile);

                // Refresh the context menu to show the new profile
                InitializeContextMenu();
            }
        }

        private void ShowProfileManager()
        {
            using (var profileForm = new ProfileForm(profileManager))
            {
                profileForm.ShowDialog();

                // Refresh the context menu to show any changes
                InitializeContextMenu();
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            // Set click-through
            if (this.Handle != IntPtr.Zero)
            {
                int exStyle = SetWindowLong(this.Handle, GWL_EXSTYLE, WS_EX_LAYERED | WS_EX_TRANSPARENT);
            }

            // Register the ALT+X hotkey for toggling visibility
            RegisterHotKey(this.Handle, TOGGLE_VISIBILITY_HOTKEY_ID, MOD_ALT, (int)Keys.X);
        }

        protected override void WndProc(ref Message m)
        {
            // Handle hotkey messages
            if (m.Msg == WM_HOTKEY)
            {
                int hotkeyId = m.WParam.ToInt32();

                if (hotkeyId == TOGGLE_VISIBILITY_HOTKEY_ID)
                {
                    // Toggle visibility when ALT+X is pressed
                    ToggleVisibility();
                    return; // Message was handled
                }
                else
                {
                    // Let the profile manager handle it
                    if (profileManager.ProcessHotkey(m))
                    {
                        return; // Message was handled
                    }
                }
            }

            base.WndProc(ref m);
        }

        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            lock (_renderLock)
            {
                // Skip rendering if no changes are needed
                if (!_needsRedraw && _lastRenderedProfile != null)
                {
                    return;
                }

                // Clear background
                e.Graphics.Clear(this.TransparencyKey);

                // Get current profile
                var profile = profileManager.CurrentProfile;

                // Enhanced graphics settings for optimal visual quality
                // Use high quality settings for all shapes with performance optimizations
                bool hasCircularShapes = profile.Shape == "Circle" || profile.Shape == "Dot" ||
                                        profile.Shape.Contains("Circle") || profile.Shape.Contains("Dot");

                if (hasCircularShapes)
                {
                    // Maximum quality for circular shapes to eliminate pixelation
                    e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
                    e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
                }
                else
                {
                    // Enhanced quality for linear shapes with crisp edges
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality; // Improved from HighSpeed
                    e.Graphics.CompositingQuality = CompositingQuality.HighQuality; // Improved from HighSpeed
                }

                // Additional quality enhancements
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                // Enable sub-pixel rendering for better line quality
                e.Graphics.CompositingMode = CompositingMode.SourceOver;

            // Calculate center and size with pixel-perfect precision
            float centerX = this.ClientSize.Width / 2.0f;
            float centerY = this.ClientSize.Height / 2.0f;
            float size = profile.Size / 2.0f; // Convert percentage to actual size

            // Ensure center coordinates are at exact pixel boundaries for crisp rendering
            centerX = (float)Math.Round(centerX);
            centerY = (float)Math.Round(centerY);

            // Get cached pens and brushes for better performance
            Pen edgePen = GetCachedPen(profile.EdgeColor, profile.EdgeThickness);
            Pen innerPen = GetCachedPen(profile.InnerColor, Math.Max(1, profile.Thickness - 2));
            SolidBrush fillBrush = GetCachedBrush(profile.FillColor);

            switch (profile.Shape)
                {
                    case "Circle":
                        // Circle is a hollow circle (outline only, transparent interior)
                        if (edgePen != null)
                        {
                            // Draw the circle outline
                            DrawEllipseF(e.Graphics, edgePen, centerX - size, centerY - size, size * 2, size * 2);

                            // Draw inner outline if thickness allows and inner color is specified
                            if (innerPen != null && profile.Thickness > 2)
                            {
                                float innerSize = size - profile.Thickness;
                                if (innerSize > 0)
                                {
                                    DrawEllipseF(e.Graphics, innerPen,
                                        centerX - innerSize, centerY - innerSize,
                                        innerSize * 2, innerSize * 2);
                                }
                            }
                        }
                        break;

                    case "Dot":
                        // Dot is a solid filled circle
                        if (profile.EdgeColor.A > 0)
                        {
                            SolidBrush dotBrush = GetCachedBrush(profile.EdgeColor);
                            if (dotBrush != null)
                            {
                                // Draw filled circle using the size as diameter
                                float dotSize = size * 2;
                                FillEllipseF(e.Graphics, dotBrush,
                                    centerX - size, centerY - size,
                                    dotSize, dotSize);
                            }
                        }
                        break;

                    case "Plus":
                        // Plus has a gap in the center
                        int actualGapSize = profile.GapSize;

                        if (edgePen != null)
                        {
                            // Draw horizontal lines with gap
                            DrawLineF(e.Graphics, edgePen, centerX - size, centerY, centerX - actualGapSize, centerY);
                            DrawLineF(e.Graphics, edgePen, centerX + actualGapSize, centerY, centerX + size, centerY);

                            // Draw vertical lines with gap
                            DrawLineF(e.Graphics, edgePen, centerX, centerY - size, centerX, centerY - actualGapSize);
                            DrawLineF(e.Graphics, edgePen, centerX, centerY + actualGapSize, centerX, centerY + size);
                        }

                        // Draw inner lines if thickness allows
                        if (profile.Thickness > 2 && innerPen != null)
                        {
                            // Horizontal inner lines
                            DrawLineF(e.Graphics, innerPen, centerX - size + 1, centerY, centerX - actualGapSize, centerY);
                            DrawLineF(e.Graphics, innerPen, centerX + actualGapSize, centerY, centerX + size - 1, centerY);

                            // Vertical inner lines
                            DrawLineF(e.Graphics, innerPen, centerX, centerY - size + 1, centerX, centerY - actualGapSize);
                            DrawLineF(e.Graphics, innerPen, centerX, centerY + actualGapSize, centerX, centerY + size - 1);
                        }
                        break;

                    case "X":
                        // X is a diagonal crosshair
                        // Draw the X shape with edge color
                        if (edgePen != null)
                        {
                            DrawLineF(e.Graphics, edgePen, centerX - size, centerY - size, centerX + size, centerY + size);
                            DrawLineF(e.Graphics, edgePen, centerX - size, centerY + size, centerX + size, centerY - size);
                        }

                        // Draw inner lines if thickness allows and inner color is not transparent
                        if (profile.Thickness > 2 && innerPen != null)
                        {
                            // Calculate offset for inner lines
                            double offset = profile.Thickness / 2.0 * 0.707; // 0.707 is approximately sin(45°) or cos(45°)
                            int offsetInt = (int)Math.Ceiling(offset);

                            // Draw inner lines with inner color
                            e.Graphics.DrawLine(innerPen,
                                centerX - size + offsetInt, centerY - size + offsetInt,
                                centerX + size - offsetInt, centerY + size - offsetInt);
                            e.Graphics.DrawLine(innerPen,
                                centerX - size + offsetInt, centerY + size - offsetInt,
                                centerX + size - offsetInt, centerY - size + offsetInt);
                        }

                        // Draw fill in center if not transparent
                        if (fillBrush != null)
                        {
                            using (GraphicsPath path = new GraphicsPath())
                            {
                                int halfThickness = profile.Thickness / 2;
                                path.AddPolygon(new Point[] {
                                    new Point((int)Math.Round(centerX), (int)Math.Round(centerY - halfThickness)),
                                    new Point((int)Math.Round(centerX + halfThickness), (int)Math.Round(centerY)),
                                    new Point((int)Math.Round(centerX), (int)Math.Round(centerY + halfThickness)),
                                    new Point((int)Math.Round(centerX - halfThickness), (int)Math.Round(centerY))
                                });
                                e.Graphics.FillPath(fillBrush, path);
                            }
                        }
                        break;

                    case "Cross":
                        // Cross is a full crosshair without a gap
                        // Draw horizontal and vertical lines with pixel-perfect positioning
                        if (edgePen != null)
                        {
                            DrawLineF(e.Graphics, edgePen, centerX - size, centerY, centerX + size, centerY);
                            DrawLineF(e.Graphics, edgePen, centerX, centerY - size, centerX, centerY + size);
                        }

                        // Draw inner lines if thickness allows
                        if (profile.Thickness > 2 && innerPen != null)
                        {
                            DrawLineF(e.Graphics, innerPen, centerX - size + 1, centerY, centerX + size - 1, centerY);
                            DrawLineF(e.Graphics, innerPen, centerX, centerY - size + 1, centerX, centerY + size - 1);
                        }
                        break;

                    // Combined shapes
                    case "CircleDot":
                        // Draw circle (outer shape) - hollow circle outline
                        if (edgePen != null)
                        {
                            e.Graphics.DrawEllipse(edgePen, centerX - size, centerY - size, size * 2, size * 2);

                            // Draw inner outline if thickness allows and inner color is specified
                            if (innerPen != null && profile.Thickness > 2)
                            {
                                float innerSize = size - profile.Thickness;
                                if (innerSize > 0)
                                {
                                    e.Graphics.DrawEllipse(innerPen,
                                        centerX - innerSize, centerY - innerSize,
                                        innerSize * 2, innerSize * 2);
                                }
                            }
                        }

                        // Draw dot in center (inner shape) - solid filled circle
                        if (profile.InnerShapeEdgeColor.A > 0)
                        {
                            SolidBrush dotBrush = GetCachedBrush(profile.InnerShapeEdgeColor);
                            if (dotBrush != null)
                            {
                                float dotRadius = profile.InnerSize / 2;
                                e.Graphics.FillEllipse(dotBrush,
                                    centerX - dotRadius, centerY - dotRadius,
                                    profile.InnerSize, profile.InnerSize);
                            }
                        }
                        break;

                    case "CrossDot":
                        // Draw cross (outer shape)
                        if (edgePen != null)
                        {
                            e.Graphics.DrawLine(edgePen, centerX - size, centerY, centerX + size, centerY);
                            e.Graphics.DrawLine(edgePen, centerX, centerY - size, centerX, centerY + size);
                        }

                        // Draw inner lines if thickness allows
                        if (profile.Thickness > 2 && innerPen != null)
                        {
                            e.Graphics.DrawLine(innerPen, centerX - size + 1, centerY, centerX + size - 1, centerY);
                            e.Graphics.DrawLine(innerPen, centerX, centerY - size + 1, centerX, centerY + size - 1);
                        }

                        // Draw dot in center (inner shape) - solid filled circle
                        if (profile.InnerShapeEdgeColor.A > 0)
                        {
                            SolidBrush dotBrush = GetCachedBrush(profile.InnerShapeEdgeColor);
                            if (dotBrush != null)
                            {
                                float dotRadius = profile.InnerSize / 2;
                                e.Graphics.FillEllipse(dotBrush,
                                    centerX - dotRadius, centerY - dotRadius,
                                    profile.InnerSize, profile.InnerSize);
                            }
                        }
                        break;

                    case "CircleCross":
                        // Draw circle (outer shape)
                        // Circle is a hollow circle with a border
                        if (profile.EdgeColor.A > 0)
                        {
                            // For smoother circles, use a custom approach
                            // First, create a path for the outer circle
                            using (GraphicsPath circlePath = new GraphicsPath())
                            {
                                // Add the outer circle to the path
                                circlePath.AddEllipse(centerX - size, centerY - size, size * 2, size * 2);

                                // If we have an inner color and sufficient thickness, create a hole in the path
                                if (profile.Thickness > 0 && profile.InnerColor.A > 0)
                                {
                                    // Calculate the inner circle dimensions
                                    float innerRadius = size - profile.Thickness;
                                    if (innerRadius > 0)
                                    {
                                        // Add the inner circle to the path (in reverse direction to create a hole)
                                        circlePath.AddEllipse(
                                            centerX - innerRadius,
                                            centerY - innerRadius,
                                            innerRadius * 2,
                                            innerRadius * 2);
                                    }
                                }

                                // Draw the path with the edge color
                                using (SolidBrush edgeBrush = new SolidBrush(profile.EdgeColor))
                                {
                                    e.Graphics.FillPath(edgeBrush, circlePath);
                                }
                            }

                            // If we have an inner color and sufficient thickness, draw the inner circle
                            if (profile.Thickness > 0 && profile.InnerColor.A > 0)
                            {
                                // Calculate the inner circle dimensions
                                float innerRadius = size - profile.Thickness;
                                if (innerRadius > 0)
                                {
                                    // Draw the inner circle with the inner color
                                    using (SolidBrush innerBrush = new SolidBrush(profile.InnerColor))
                                    {
                                        e.Graphics.FillEllipse(
                                            innerBrush,
                                            centerX - innerRadius,
                                            centerY - innerRadius,
                                            innerRadius * 2,
                                            innerRadius * 2);
                                    }
                                }
                            }
                        }

                        // Draw cross (inner shape)
                        // Use InnerSize for the cross
                        int crossSize = profile.InnerSize / 2;
                        using (Pen crossPen = new Pen(profile.InnerShapeEdgeColor, profile.InnerThickness))
                        {
                            e.Graphics.DrawLine(crossPen, centerX - crossSize, centerY, centerX + crossSize, centerY);
                            e.Graphics.DrawLine(crossPen, centerX, centerY - crossSize, centerX, centerY + crossSize);
                        }

                        // Draw inner lines if thickness allows
                        if (profile.InnerThickness > 2 && profile.InnerShapeInnerColor.A > 0)
                        {
                            using (Pen crossInnerPen = new Pen(profile.InnerShapeInnerColor, Math.Max(1, profile.InnerThickness - 2)))
                            {
                                e.Graphics.DrawLine(crossInnerPen, centerX - crossSize + 1, centerY, centerX + crossSize - 1, centerY);
                                e.Graphics.DrawLine(crossInnerPen, centerX, centerY - crossSize + 1, centerX, centerY + crossSize - 1);
                            }
                        }
                        break;

                    case "CirclePlus":
                        // Draw circle (outer shape)
                        // Circle is a hollow circle with a border
                        if (profile.EdgeColor.A > 0)
                        {
                            // For smoother circles, use a custom approach
                            // First, create a path for the outer circle
                            using (GraphicsPath circlePath = new GraphicsPath())
                            {
                                // Add the outer circle to the path
                                circlePath.AddEllipse(centerX - size, centerY - size, size * 2, size * 2);

                                // If we have an inner color and sufficient thickness, create a hole in the path
                                if (profile.Thickness > 0 && profile.InnerColor.A > 0)
                                {
                                    // Calculate the inner circle dimensions
                                    float innerRadius = size - profile.Thickness;
                                    if (innerRadius > 0)
                                    {
                                        // Add the inner circle to the path (in reverse direction to create a hole)
                                        circlePath.AddEllipse(
                                            centerX - innerRadius,
                                            centerY - innerRadius,
                                            innerRadius * 2,
                                            innerRadius * 2);
                                    }
                                }

                                // Draw the path with the edge color
                                using (SolidBrush edgeBrush = new SolidBrush(profile.EdgeColor))
                                {
                                    e.Graphics.FillPath(edgeBrush, circlePath);
                                }
                            }

                            // If we have an inner color and sufficient thickness, draw the inner circle
                            if (profile.Thickness > 0 && profile.InnerColor.A > 0)
                            {
                                // Calculate the inner circle dimensions
                                float innerRadius = size - profile.Thickness;
                                if (innerRadius > 0)
                                {
                                    // Draw the inner circle with the inner color
                                    using (SolidBrush innerBrush = new SolidBrush(profile.InnerColor))
                                    {
                                        e.Graphics.FillEllipse(
                                            innerBrush,
                                            centerX - innerRadius,
                                            centerY - innerRadius,
                                            innerRadius * 2,
                                            innerRadius * 2);
                                    }
                                }
                            }
                        }

                        // Draw plus (inner shape)
                        // Use InnerSize for the plus
                        int plusSize = profile.InnerSize / 2;
                        int plusGapSize = profile.InnerGapSize;
                        using (Pen plusPen = new Pen(profile.InnerShapeEdgeColor, profile.InnerThickness))
                        {
                            // Horizontal lines with gap
                            e.Graphics.DrawLine(plusPen, centerX - plusSize, centerY, centerX - plusGapSize, centerY);
                            e.Graphics.DrawLine(plusPen, centerX + plusGapSize, centerY, centerX + plusSize, centerY);
                            // Vertical lines with gap
                            e.Graphics.DrawLine(plusPen, centerX, centerY - plusSize, centerX, centerY - plusGapSize);
                            e.Graphics.DrawLine(plusPen, centerX, centerY + plusGapSize, centerX, centerY + plusSize);
                        }

                        // Draw inner lines if thickness allows
                        if (profile.InnerThickness > 2 && profile.InnerShapeInnerColor.A > 0)
                        {
                            using (Pen plusInnerPen = new Pen(profile.InnerShapeInnerColor, Math.Max(1, profile.InnerThickness - 2)))
                            {
                                // Horizontal inner lines
                                e.Graphics.DrawLine(plusInnerPen, centerX - plusSize + 1, centerY, centerX - plusGapSize, centerY);
                                e.Graphics.DrawLine(plusInnerPen, centerX + plusGapSize, centerY, centerX + plusSize - 1, centerY);
                                // Vertical inner lines
                                e.Graphics.DrawLine(plusInnerPen, centerX, centerY - plusSize + 1, centerX, centerY - plusGapSize);
                                e.Graphics.DrawLine(plusInnerPen, centerX, centerY + plusGapSize, centerX, centerY + plusSize - 1);
                            }
                        }
                        break;

                    case "CircleX":
                        // Draw circle (outer shape)
                        // Circle is a hollow circle with a border
                        if (profile.EdgeColor.A > 0)
                        {
                            // For smoother circles, use a custom approach
                            // First, create a path for the outer circle
                            using (GraphicsPath circlePath = new GraphicsPath())
                            {
                                // Add the outer circle to the path
                                circlePath.AddEllipse(centerX - size, centerY - size, size * 2, size * 2);

                                // If we have an inner color and sufficient thickness, create a hole in the path
                                if (profile.Thickness > 0 && profile.InnerColor.A > 0)
                                {
                                    // Calculate the inner circle dimensions
                                    float innerRadius = size - profile.Thickness;
                                    if (innerRadius > 0)
                                    {
                                        // Add the inner circle to the path (in reverse direction to create a hole)
                                        circlePath.AddEllipse(
                                            centerX - innerRadius,
                                            centerY - innerRadius,
                                            innerRadius * 2,
                                            innerRadius * 2);
                                    }
                                }

                                // Draw the path with the edge color
                                using (SolidBrush edgeBrush = new SolidBrush(profile.EdgeColor))
                                {
                                    e.Graphics.FillPath(edgeBrush, circlePath);
                                }
                            }

                            // If we have an inner color and sufficient thickness, draw the inner circle
                            if (profile.Thickness > 0 && profile.InnerColor.A > 0)
                            {
                                // Calculate the inner circle dimensions
                                float innerRadius = size - profile.Thickness;
                                if (innerRadius > 0)
                                {
                                    // Draw the inner circle with the inner color
                                    using (SolidBrush innerBrush = new SolidBrush(profile.InnerColor))
                                    {
                                        e.Graphics.FillEllipse(
                                            innerBrush,
                                            centerX - innerRadius,
                                            centerY - innerRadius,
                                            innerRadius * 2,
                                            innerRadius * 2);
                                    }
                                }
                            }
                        }

                        // Draw X (inner shape)
                        // Use InnerSize for the X
                        int xSize = profile.InnerSize / 2;
                        using (Pen xPen = new Pen(profile.InnerShapeEdgeColor, profile.InnerThickness))
                        {
                            e.Graphics.DrawLine(xPen, centerX - xSize, centerY - xSize, centerX + xSize, centerY + xSize);
                            e.Graphics.DrawLine(xPen, centerX - xSize, centerY + xSize, centerX + xSize, centerY - xSize);
                        }

                        // Draw inner lines if thickness allows and inner color is not transparent
                        if (profile.InnerThickness > 2 && profile.InnerShapeInnerColor.A > 0)
                        {
                            // Calculate offset for inner lines
                            double offset = profile.InnerThickness / 2.0 * 0.707; // 0.707 is approximately sin(45°) or cos(45°)
                            int offsetInt = (int)Math.Ceiling(offset);

                            // Draw inner lines with inner color
                            using (Pen xInnerPen = new Pen(profile.InnerShapeInnerColor, Math.Max(1, profile.InnerThickness - 2)))
                            {
                                e.Graphics.DrawLine(xInnerPen,
                                    centerX - xSize + offsetInt, centerY - xSize + offsetInt,
                                    centerX + xSize - offsetInt, centerY + xSize - offsetInt);
                                e.Graphics.DrawLine(xInnerPen,
                                    centerX - xSize + offsetInt, centerY + xSize - offsetInt,
                                    centerX + xSize - offsetInt, centerY - xSize + offsetInt);
                            }
                        }
                        break;

                    default:
                        // Default to cross if shape not recognized
                        e.Graphics.DrawLine(edgePen, centerX - size, centerY, centerX + size, centerY);
                        e.Graphics.DrawLine(edgePen, centerX, centerY - size, centerX, centerY + size);
                        break;
                }

                // Update cached profile and mark as rendered
                _lastRenderedProfile = profile;
                _needsRedraw = false;
            }
        }

        private void ToggleVisibility()
        {
            isVisible = !isVisible;
            this.Visible = isVisible;
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
            if (profileManager?.CurrentProfile != null)
            {
                // Calculate optimal form size based on crosshair size and thickness
                int maxCrosshairSize = Math.Max(profileManager.CurrentProfile.Size,
                                              profileManager.CurrentProfile.InnerSize);
                int padding = Math.Max(profileManager.CurrentProfile.Thickness * 2, 10);

                // Ensure minimum size and add padding for anti-aliasing
                int formSize = Math.Max(100, maxCrosshairSize + padding * 2);

                // Make sure the size is odd for perfect center pixel alignment
                if (formSize % 2 == 0) formSize++;

                this.Size = new Size(formSize, formSize);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Unregister all hotkeys
            if (profileManager != null)
            {
                profileManager.UnregisterAllHotkeys();
            }

            // Unregister the toggle visibility hotkey
            UnregisterHotKey(this.Handle, TOGGLE_VISIBILITY_HOTKEY_ID);

            // Clean up resources
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            contextMenu.Dispose();

            // Stop and dispose recording detection timer
            if (_recordingDetectionTimer != null)
            {
                _recordingDetectionTimer.Stop();
                _recordingDetectionTimer.Dispose();
            }

            // Clear graphics cache
            ClearGraphicsCache();

            base.OnFormClosing(e);
        }



        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            CenterCrosshair();
        }


    }
}
