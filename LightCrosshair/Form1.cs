using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;
using System.IO;
using System.Collections.Generic;

namespace LightCrosshair
{
    public partial class Form1 : Form
    {
        private NotifyIcon notifyIcon;
        private ContextMenuStrip contextMenu;
        private ProfileManager profileManager;
        private Color fillColor = Color.Transparent;
        private bool isVisible = true;

        // Constants for message handling
        private const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;

        public Form1()
        {
            InitializeComponent();

            // Basic form setup
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.Size = new Size(100, 100);

            // Set transparency
            this.BackColor = Color.Magenta;
            this.TransparencyKey = Color.Magenta;

            // Enable double buffering
            this.SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer,
                true);

            // Initialize profile manager
            profileManager = new ProfileManager(this.Handle);
            profileManager.ProfileChanged += ProfileManager_ProfileChanged;

            // Initialize system tray icon and menu
            InitializeNotifyIcon();
            InitializeContextMenu();

            // Add paint handler
            this.Paint += Form1_Paint;

            // Center the form on screen
            CenterCrosshair();
        }

        private void ProfileManager_ProfileChanged(object sender, CrosshairProfile profile)
        {
            // Update the form when the profile changes
            this.Invalidate();
        }

        private void InitializeNotifyIcon()
        {
            try
            {
                // Try to load icon from resources
                Icon appIcon = null;

                try
                {
                    // Try to load the icon from the embedded resource
                    string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "icon.ico");
                    if (File.Exists(iconPath))
                    {
                        appIcon = new Icon(iconPath);
                    }
                    else
                    {
                        // If file doesn't exist, use the system icon
                        appIcon = SystemIcons.Application;
                    }
                }
                catch
                {
                    // If loading fails, use the system icon
                    appIcon = SystemIcons.Application;
                }

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
            var crossItem = new ToolStripMenuItem("Cross");
            crossItem.Click += (sender, e) => { UpdateCurrentProfileProperty("Shape", "Cross"); };
            var circleItem = new ToolStripMenuItem("Circle");
            circleItem.Click += (sender, e) => { UpdateCurrentProfileProperty("Shape", "Circle"); };
            var dotItem = new ToolStripMenuItem("Dot");
            dotItem.Click += (sender, e) => { UpdateCurrentProfileProperty("Shape", "Dot"); };
            var plusItem = new ToolStripMenuItem("Plus");
            plusItem.Click += (sender, e) => { UpdateCurrentProfileProperty("Shape", "Plus"); };
            var xItem = new ToolStripMenuItem("X");
            xItem.Click += (sender, e) => { UpdateCurrentProfileProperty("Shape", "X"); };
            shapeMenu.DropDownItems.AddRange(new ToolStripItem[] { crossItem, circleItem, dotItem, plusItem, xItem });

            // Size submenu
            var sizeMenu = new ToolStripMenuItem("Size");
            for (int size = 5; size <= 100; size += 5)
            {
                var sizeItem = new ToolStripMenuItem($"{size}%");
                int capturedSize = size; // Capture the size value for the lambda
                sizeItem.Click += (sender, e) => { UpdateCurrentProfileProperty("Size", capturedSize); };
                sizeMenu.DropDownItems.Add(sizeItem);
            }

            // Thickness submenu
            var thicknessMenu = new ToolStripMenuItem("Thickness");
            for (int thickness = 1; thickness <= 10; thickness++)
            {
                var thicknessItem = new ToolStripMenuItem(thickness.ToString());
                int capturedThickness = thickness; // Capture the thickness value for the lambda
                thicknessItem.Click += (sender, e) => { UpdateCurrentProfileProperty("Thickness", capturedThickness); };
                thicknessMenu.DropDownItems.Add(thicknessItem);
            }

            // Edge Color submenu
            var edgeColorMenu = new ToolStripMenuItem("Edge Color");

            // Add high contrast predefined colors
            AddColorMenuItem(edgeColorMenu, "Red", Color.FromArgb(255, 0, 0), "Edge");
            AddColorMenuItem(edgeColorMenu, "Bright Green", Color.FromArgb(0, 255, 0), "Edge");
            AddColorMenuItem(edgeColorMenu, "Bright Blue", Color.FromArgb(0, 0, 255), "Edge");
            AddColorMenuItem(edgeColorMenu, "Bright Yellow", Color.FromArgb(255, 255, 0), "Edge");
            AddColorMenuItem(edgeColorMenu, "Magenta", Color.FromArgb(255, 0, 255), "Edge");
            AddColorMenuItem(edgeColorMenu, "Cyan", Color.FromArgb(0, 255, 255), "Edge");
            AddColorMenuItem(edgeColorMenu, "White", Color.FromArgb(255, 255, 255), "Edge");
            AddColorMenuItem(edgeColorMenu, "Black", Color.FromArgb(0, 0, 0), "Edge");
            AddColorMenuItem(edgeColorMenu, "Orange", Color.FromArgb(255, 165, 0), "Edge");
            AddColorMenuItem(edgeColorMenu, "Hot Pink", Color.FromArgb(255, 105, 180), "Edge");

            // Add custom color option
            var customEdgeColorItem = new ToolStripMenuItem("Custom...");
            customEdgeColorItem.Click += (sender, e) => ShowColorPicker("Edge");
            edgeColorMenu.DropDownItems.Add(new ToolStripSeparator());
            edgeColorMenu.DropDownItems.Add(customEdgeColorItem);

            // Inner Color submenu
            var innerColorMenu = new ToolStripMenuItem("Inner Color");

            // Add high contrast predefined colors
            AddColorMenuItem(innerColorMenu, "Red", Color.FromArgb(255, 0, 0), "Inner");
            AddColorMenuItem(innerColorMenu, "Bright Green", Color.FromArgb(0, 255, 0), "Inner");
            AddColorMenuItem(innerColorMenu, "Bright Blue", Color.FromArgb(0, 0, 255), "Inner");
            AddColorMenuItem(innerColorMenu, "Bright Yellow", Color.FromArgb(255, 255, 0), "Inner");
            AddColorMenuItem(innerColorMenu, "Magenta", Color.FromArgb(255, 0, 255), "Inner");
            AddColorMenuItem(innerColorMenu, "Cyan", Color.FromArgb(0, 255, 255), "Inner");
            AddColorMenuItem(innerColorMenu, "White", Color.FromArgb(255, 255, 255), "Inner");
            AddColorMenuItem(innerColorMenu, "Black", Color.FromArgb(0, 0, 0), "Inner");
            AddColorMenuItem(innerColorMenu, "Orange", Color.FromArgb(255, 165, 0), "Inner");
            AddColorMenuItem(innerColorMenu, "Hot Pink", Color.FromArgb(255, 105, 180), "Inner");

            // Add custom color option
            var customInnerColorItem = new ToolStripMenuItem("Custom...");
            customInnerColorItem.Click += (sender, e) => ShowColorPicker("Inner");
            innerColorMenu.DropDownItems.Add(new ToolStripSeparator());
            innerColorMenu.DropDownItems.Add(customInnerColorItem);

            // Fill Color submenu
            var fillColorMenu = new ToolStripMenuItem("Fill Color");

            // Add predefined colors
            AddColorMenuItem(fillColorMenu, "None (Transparent)", Color.Transparent, "Fill");
            AddColorMenuItem(fillColorMenu, "Red", Color.FromArgb(180, 255, 0, 0), "Fill");
            AddColorMenuItem(fillColorMenu, "Bright Green", Color.FromArgb(180, 0, 255, 0), "Fill");
            AddColorMenuItem(fillColorMenu, "Bright Blue", Color.FromArgb(180, 0, 0, 255), "Fill");
            AddColorMenuItem(fillColorMenu, "Bright Yellow", Color.FromArgb(180, 255, 255, 0), "Fill");
            AddColorMenuItem(fillColorMenu, "Magenta", Color.FromArgb(180, 255, 0, 255), "Fill");
            AddColorMenuItem(fillColorMenu, "Cyan", Color.FromArgb(180, 0, 255, 255), "Fill");
            AddColorMenuItem(fillColorMenu, "White", Color.FromArgb(180, 255, 255, 255), "Fill");
            AddColorMenuItem(fillColorMenu, "Black", Color.FromArgb(180, 0, 0, 0), "Fill");
            AddColorMenuItem(fillColorMenu, "Orange", Color.FromArgb(180, 255, 165, 0), "Fill");
            AddColorMenuItem(fillColorMenu, "Hot Pink", Color.FromArgb(180, 255, 105, 180), "Fill");

            // Add custom color option
            var customFillColorItem = new ToolStripMenuItem("Custom...");
            customFillColorItem.Click += (sender, e) => ShowColorPicker("Fill");
            fillColorMenu.DropDownItems.Add(new ToolStripSeparator());
            fillColorMenu.DropDownItems.Add(customFillColorItem);

            // Save current profile option
            var saveProfileItem = new ToolStripMenuItem("Save Current Profile");
            saveProfileItem.Click += (sender, e) => SaveCurrentProfile();

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
                edgeColorMenu,
                innerColorMenu,
                fillColorMenu,
                new ToolStripSeparator(),
                saveProfileItem,
                new ToolStripSeparator(),
                exitItem
            });

            // Set the context menu for both the form and notify icon
            this.ContextMenuStrip = contextMenu;
            notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void AddColorMenuItem(ToolStripMenuItem parentMenu, string name, Color color, string colorType)
        {
            var colorItem = new ToolStripMenuItem(name);
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
            };
            parentMenu.DropDownItems.Add(colorItem);
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
                case "Thickness":
                    profile.Thickness = (int)value;
                    break;
                case "EdgeColor":
                    profile.EdgeColor = (Color)value;
                    break;
                case "InnerColor":
                    profile.InnerColor = (Color)value;
                    break;
                case "FillColor":
                    profile.FillColor = (Color)value;
                    break;
            }

            profileManager.UpdateProfile(profile);
            this.Invalidate();
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
                int exStyle = SetWindowLong(this.Handle, GWL_EXSTYLE,
                    WS_EX_LAYERED | WS_EX_TRANSPARENT);
            }
        }

        protected override void WndProc(ref Message m)
        {
            // Check if this is a hotkey message
            if (m.Msg == WM_HOTKEY)
            {
                // Let the profile manager handle it
                if (profileManager.ProcessHotkey(m))
                {
                    return; // Message was handled
                }
            }

            base.WndProc(ref m);
        }

        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            // Clear background
            e.Graphics.Clear(this.TransparencyKey);

            // Set up graphics for smoother rendering
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.CompositingQuality = CompositingQuality.HighQuality;

            // Get current profile
            var profile = profileManager.CurrentProfile;

            // Calculate center and size
            int centerX = this.ClientSize.Width / 2;
            int centerY = this.ClientSize.Height / 2;
            int size = profile.Size / 2; // Convert percentage to actual size

            // Create pens and brushes
            using (Pen edgePen = new Pen(profile.EdgeColor, profile.Thickness))
            using (Pen innerPen = new Pen(profile.InnerColor, Math.Max(1, profile.Thickness - 2)))
            using (SolidBrush fillBrush = new SolidBrush(profile.FillColor))
            {
                switch (profile.Shape)
                {
                    case "Circle":
                        // Circle is a hollow circle with a border
                        // Draw fill if not transparent
                        if (profile.FillColor.A > 0)
                        {
                            e.Graphics.FillEllipse(fillBrush, centerX - size, centerY - size, size * 2, size * 2);
                        }

                        // Draw inner line (always draw it for better visibility)
                        e.Graphics.DrawEllipse(innerPen, centerX - size + profile.Thickness/2, centerY - size + profile.Thickness/2,
                            size * 2 - profile.Thickness, size * 2 - profile.Thickness);

                        // Draw edge
                        e.Graphics.DrawEllipse(edgePen, centerX - size, centerY - size, size * 2, size * 2);
                        break;

                    case "Dot":
                        // Dot is a solid filled circle
                        // Draw the filled circle
                        using (SolidBrush dotBrush = new SolidBrush(profile.EdgeColor))
                        {
                            e.Graphics.FillEllipse(dotBrush, centerX - size/2, centerY - size/2, size, size);
                        }

                        // Add inner color ring if thickness allows
                        if (profile.Thickness > 2)
                        {
                            using (SolidBrush innerBrush = new SolidBrush(profile.InnerColor))
                            {
                                e.Graphics.FillEllipse(innerBrush, centerX - size/2 + profile.Thickness/2,
                                    centerY - size/2 + profile.Thickness/2,
                                    size - profile.Thickness, size - profile.Thickness);
                            }
                        }

                        // Add fill color in center if not transparent
                        if (profile.FillColor.A > 0)
                        {
                            e.Graphics.FillEllipse(fillBrush, centerX - size/4, centerY - size/4, size/2, size/2);
                        }
                        break;

                    case "Plus":
                        // Plus is a thin crosshair with equal length horizontal and vertical lines
                        // Draw fill in center if not transparent
                        if (profile.FillColor.A > 0)
                        {
                            e.Graphics.FillRectangle(fillBrush, centerX - profile.Thickness, centerY - profile.Thickness,
                                profile.Thickness * 2, profile.Thickness * 2);
                        }

                        // Always draw inner lines for better visibility
                        e.Graphics.DrawLine(innerPen, centerX - size + profile.Thickness/2, centerY, centerX + size - profile.Thickness/2, centerY);
                        e.Graphics.DrawLine(innerPen, centerX, centerY - size + profile.Thickness/2, centerX, centerY + size - profile.Thickness/2);

                        // Draw edge lines
                        e.Graphics.DrawLine(edgePen, centerX - size, centerY, centerX + size, centerY);
                        e.Graphics.DrawLine(edgePen, centerX, centerY - size, centerX, centerY + size);
                        break;

                    case "X":
                        // X is a diagonal crosshair
                        // Draw fill in center if not transparent
                        if (profile.FillColor.A > 0)
                        {
                            using (GraphicsPath path = new GraphicsPath())
                            {
                                path.AddPolygon(new Point[] {
                                    new Point(centerX, centerY - profile.Thickness),
                                    new Point(centerX + profile.Thickness, centerY),
                                    new Point(centerX, centerY + profile.Thickness),
                                    new Point(centerX - profile.Thickness, centerY)
                                });
                                e.Graphics.FillPath(fillBrush, path);
                            }
                        }

                        // Always draw inner lines for better visibility
                        e.Graphics.DrawLine(innerPen, centerX - size + profile.Thickness/2, centerY - size + profile.Thickness/2,
                            centerX + size - profile.Thickness/2, centerY + size - profile.Thickness/2);
                        e.Graphics.DrawLine(innerPen, centerX - size + profile.Thickness/2, centerY + size - profile.Thickness/2,
                            centerX + size - profile.Thickness/2, centerY - size + profile.Thickness/2);

                        // Draw edge lines
                        e.Graphics.DrawLine(edgePen, centerX - size, centerY - size, centerX + size, centerY + size);
                        e.Graphics.DrawLine(edgePen, centerX - size, centerY + size, centerX + size, centerY - size);
                        break;

                    case "Cross":
                    default:
                        // Cross is a thicker crosshair with a gap in the middle
                        // Draw fill in center if not transparent
                        if (profile.FillColor.A > 0)
                        {
                            e.Graphics.FillEllipse(fillBrush, centerX - profile.Thickness, centerY - profile.Thickness,
                                profile.Thickness * 2, profile.Thickness * 2);
                        }

                        // Always draw inner lines for better visibility
                        // Horizontal lines with gap
                        e.Graphics.DrawLine(innerPen, centerX - size + profile.Thickness/2, centerY, centerX - profile.Thickness * 2, centerY);
                        e.Graphics.DrawLine(innerPen, centerX + profile.Thickness * 2, centerY, centerX + size - profile.Thickness/2, centerY);
                        // Vertical lines with gap
                        e.Graphics.DrawLine(innerPen, centerX, centerY - size + profile.Thickness/2, centerX, centerY - profile.Thickness * 2);
                        e.Graphics.DrawLine(innerPen, centerX, centerY + profile.Thickness * 2, centerX, centerY + size - profile.Thickness/2);

                        // Draw edge lines with gap
                        // Horizontal lines
                        e.Graphics.DrawLine(edgePen, centerX - size, centerY, centerX - profile.Thickness, centerY);
                        e.Graphics.DrawLine(edgePen, centerX + profile.Thickness, centerY, centerX + size, centerY);
                        // Vertical lines
                        e.Graphics.DrawLine(edgePen, centerX, centerY - size, centerX, centerY - profile.Thickness);
                        e.Graphics.DrawLine(edgePen, centerX, centerY + profile.Thickness, centerX, centerY + size);
                        break;
                }
            }
        }

        private void ToggleVisibility()
        {
            isVisible = !isVisible;
            this.Visible = isVisible;
        }

        private void CenterCrosshair()
        {
            Rectangle screenBounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
            this.Location = new Point(
                (screenBounds.Width - this.Width) / 2,
                (screenBounds.Height - this.Height) / 2
            );
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Unregister all hotkeys
            if (profileManager != null)
            {
                profileManager.UnregisterAllHotkeys();
            }

            // Clean up resources
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            contextMenu.Dispose();

            base.OnFormClosing(e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            CenterCrosshair();
        }
    }
}
