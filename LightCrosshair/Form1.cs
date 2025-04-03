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

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

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

            // Update menu checkmarks
            UpdateMenuItems();
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
            // Find the size menu
            var sizeMenu = FindMenuItemByText(contextMenu.Items, "Outer Shape Size");
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
            this.Invalidate();

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
            // Clear background
            e.Graphics.Clear(this.TransparencyKey);

            // Set up graphics for high quality rendering of circles and dots
            e.Graphics.SmoothingMode = SmoothingMode.HighQuality; // Use highest quality for smooth circles
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic; // Use highest quality interpolation
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality; // Use high quality pixel offset
            e.Graphics.CompositingQuality = CompositingQuality.HighQuality; // Use high quality compositing
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias; // Use antialiased text

            // Get current profile
            var profile = profileManager.CurrentProfile;

            // Calculate center and size
            int centerX = this.ClientSize.Width / 2;
            int centerY = this.ClientSize.Height / 2;
            int size = profile.Size / 2; // Convert percentage to actual size

            // Create pens and brushes only if needed
            using (Pen edgePen = profile.EdgeColor.A > 0 ? new Pen(profile.EdgeColor, profile.Thickness) : null)
            using (Pen innerPen = profile.InnerColor.A > 0 ? new Pen(profile.InnerColor, Math.Max(1, profile.Thickness - 2)) : null)
            using (SolidBrush fillBrush = profile.FillColor.A > 0 ? new SolidBrush(profile.FillColor) : null)
            {
                switch (profile.Shape)
                {
                    case "Circle":
                        // Circle is a hollow circle with a border
                        if (edgePen != null)
                        {
                            // For smoother circles, use a custom approach
                            // First, create a path for the outer circle
                            using (GraphicsPath path = new GraphicsPath())
                            {
                                // Add the outer circle to the path
                                path.AddEllipse(centerX - size, centerY - size, size * 2, size * 2);

                                // If we have an inner color and sufficient thickness, create a hole in the path
                                if (profile.Thickness > 0 && innerPen != null)
                                {
                                    // Calculate the inner circle dimensions
                                    float innerRadius = size - profile.Thickness;
                                    if (innerRadius > 0)
                                    {
                                        // Add the inner circle to the path (in reverse direction to create a hole)
                                        path.AddEllipse(
                                            centerX - innerRadius,
                                            centerY - innerRadius,
                                            innerRadius * 2,
                                            innerRadius * 2);
                                    }
                                }

                                // Draw the path with the edge color
                                using (SolidBrush edgeBrush = new SolidBrush(profile.EdgeColor))
                                {
                                    e.Graphics.FillPath(edgeBrush, path);
                                }
                            }

                            // If we have an inner color and sufficient thickness, draw the inner circle
                            if (profile.Thickness > 0 && innerPen != null)
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
                        break;

                    case "Dot":
                        // Dot is a solid filled circle with an outer edge and inner fill
                        // For smoother dots, use a custom approach with GraphicsPath

                        // First, create a path for the outer circle (the dot)
                        using (GraphicsPath dotPath = new GraphicsPath())
                        {
                            // Add the outer circle to the path
                            float dotDiameter = size;
                            dotPath.AddEllipse(centerX - dotDiameter/2, centerY - dotDiameter/2, dotDiameter, dotDiameter);

                            // Draw the outer circle with the edge color
                            if (profile.EdgeColor.A > 0)
                            {
                                using (SolidBrush dotBrush = new SolidBrush(profile.EdgeColor))
                                {
                                    e.Graphics.FillPath(dotBrush, dotPath);
                                }
                            }
                        }

                        // Then draw the inner part with inner color if thickness allows
                        if (profile.InnerColor.A > 0 && profile.Thickness > 0)
                        {
                            float innerDiameter = size - (profile.Thickness * 2);
                            if (innerDiameter > 0)
                            {
                                using (GraphicsPath innerDotPath = new GraphicsPath())
                                {
                                    // Add the inner circle to the path
                                    innerDotPath.AddEllipse(
                                        centerX - innerDiameter/2,
                                        centerY - innerDiameter/2,
                                        innerDiameter,
                                        innerDiameter);

                                    // Draw the inner circle with the inner color
                                    using (SolidBrush innerBrush = new SolidBrush(profile.InnerColor))
                                    {
                                        e.Graphics.FillPath(innerBrush, innerDotPath);
                                    }
                                }
                            }
                        }
                        break;

                    case "Plus":
                        // Plus has a gap in the center
                        int actualGapSize = profile.GapSize;

                        if (edgePen != null)
                        {
                            // Draw horizontal lines with gap
                            e.Graphics.DrawLine(edgePen, centerX - size, centerY, centerX - actualGapSize, centerY);
                            e.Graphics.DrawLine(edgePen, centerX + actualGapSize, centerY, centerX + size, centerY);

                            // Draw vertical lines with gap
                            e.Graphics.DrawLine(edgePen, centerX, centerY - size, centerX, centerY - actualGapSize);
                            e.Graphics.DrawLine(edgePen, centerX, centerY + actualGapSize, centerX, centerY + size);
                        }

                        // Draw inner lines if thickness allows
                        if (profile.Thickness > 2 && innerPen != null)
                        {
                            // Horizontal inner lines
                            e.Graphics.DrawLine(innerPen, centerX - size + 1, centerY, centerX - actualGapSize, centerY);
                            e.Graphics.DrawLine(innerPen, centerX + actualGapSize, centerY, centerX + size - 1, centerY);

                            // Vertical inner lines
                            e.Graphics.DrawLine(innerPen, centerX, centerY - size + 1, centerX, centerY - actualGapSize);
                            e.Graphics.DrawLine(innerPen, centerX, centerY + actualGapSize, centerX, centerY + size - 1);
                        }
                        break;

                    case "X":
                        // X is a diagonal crosshair
                        // Draw the X shape with edge color
                        if (edgePen != null)
                        {
                            e.Graphics.DrawLine(edgePen, centerX - size, centerY - size, centerX + size, centerY + size);
                            e.Graphics.DrawLine(edgePen, centerX - size, centerY + size, centerX + size, centerY - size);
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
                                    new Point(centerX, centerY - halfThickness),
                                    new Point(centerX + halfThickness, centerY),
                                    new Point(centerX, centerY + halfThickness),
                                    new Point(centerX - halfThickness, centerY)
                                });
                                e.Graphics.FillPath(fillBrush, path);
                            }
                        }
                        break;

                    case "Cross":
                        // Cross is a full crosshair without a gap
                        // Draw horizontal and vertical lines
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
                        break;

                    // Combined shapes
                    case "CircleDot":
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

                        // Draw dot in center (inner shape)
                        // For smoother dots, use a custom approach with GraphicsPath
                        float circleDotDiameter = profile.InnerSize;

                        // First, create a path for the outer circle (the dot)
                        using (GraphicsPath dotPath = new GraphicsPath())
                        {
                            // Add the outer circle to the path
                            dotPath.AddEllipse(centerX - circleDotDiameter/2, centerY - circleDotDiameter/2, circleDotDiameter, circleDotDiameter);

                            // Draw the outer circle with the edge color
                            if (profile.InnerShapeEdgeColor.A > 0)
                            {
                                using (SolidBrush dotBrush = new SolidBrush(profile.InnerShapeEdgeColor))
                                {
                                    e.Graphics.FillPath(dotBrush, dotPath);
                                }
                            }
                        }

                        // Then draw the inner part with inner color if thickness allows
                        if (profile.InnerShapeInnerColor.A > 0 && profile.InnerThickness > 0)
                        {
                            float innerDotDiameter = circleDotDiameter - (profile.InnerThickness * 2);
                            if (innerDotDiameter > 0)
                            {
                                using (GraphicsPath innerDotPath = new GraphicsPath())
                                {
                                    // Add the inner circle to the path
                                    innerDotPath.AddEllipse(
                                        centerX - innerDotDiameter/2,
                                        centerY - innerDotDiameter/2,
                                        innerDotDiameter,
                                        innerDotDiameter);

                                    // Draw the inner circle with the inner color
                                    using (SolidBrush innerDotBrush = new SolidBrush(profile.InnerShapeInnerColor))
                                    {
                                        e.Graphics.FillPath(innerDotBrush, innerDotPath);
                                    }
                                }
                            }
                        }
                        break;

                    case "CrossDot":
                        // Draw cross (outer shape)
                        // Draw horizontal and vertical lines
                        using (Pen crossPen = new Pen(profile.EdgeColor, profile.Thickness))
                        {
                            e.Graphics.DrawLine(crossPen, centerX - size, centerY, centerX + size, centerY);
                            e.Graphics.DrawLine(crossPen, centerX, centerY - size, centerX, centerY + size);
                        }

                        // Draw inner lines if thickness allows
                        if (profile.Thickness > 2 && profile.InnerColor.A > 0)
                        {
                            using (Pen crossInnerPen = new Pen(profile.InnerColor, Math.Max(1, profile.Thickness - 2)))
                            {
                                e.Graphics.DrawLine(crossInnerPen, centerX - size + 1, centerY, centerX + size - 1, centerY);
                                e.Graphics.DrawLine(crossInnerPen, centerX, centerY - size + 1, centerX, centerY + size - 1);
                            }
                        }

                        // Draw dot in center (inner shape)
                        // For smoother dots, use a custom approach with GraphicsPath
                        float crossDotDiameter = profile.InnerSize;

                        // First, create a path for the outer circle (the dot)
                        using (GraphicsPath dotPath = new GraphicsPath())
                        {
                            // Add the outer circle to the path
                            dotPath.AddEllipse(centerX - crossDotDiameter/2, centerY - crossDotDiameter/2, crossDotDiameter, crossDotDiameter);

                            // Draw the outer circle with the edge color
                            if (profile.InnerShapeEdgeColor.A > 0)
                            {
                                using (SolidBrush dotBrush = new SolidBrush(profile.InnerShapeEdgeColor))
                                {
                                    e.Graphics.FillPath(dotBrush, dotPath);
                                }
                            }
                        }

                        // Then draw the inner part with inner color if thickness allows
                        if (profile.InnerShapeInnerColor.A > 0 && profile.InnerThickness > 0)
                        {
                            float crossInnerDotDiameter = crossDotDiameter - (profile.InnerThickness * 2);
                            if (crossInnerDotDiameter > 0)
                            {
                                using (GraphicsPath innerDotPath = new GraphicsPath())
                                {
                                    // Add the inner circle to the path
                                    innerDotPath.AddEllipse(
                                        centerX - crossInnerDotDiameter/2,
                                        centerY - crossInnerDotDiameter/2,
                                        crossInnerDotDiameter,
                                        crossInnerDotDiameter);

                                    // Draw the inner circle with the inner color
                                    using (SolidBrush innerDotBrush = new SolidBrush(profile.InnerShapeInnerColor))
                                    {
                                        e.Graphics.FillPath(innerDotBrush, innerDotPath);
                                    }
                                }
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

            // Unregister the toggle visibility hotkey
            UnregisterHotKey(this.Handle, TOGGLE_VISIBILITY_HOTKEY_ID);

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
