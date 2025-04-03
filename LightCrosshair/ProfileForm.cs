using System;
using System.Drawing;
using System.Windows.Forms;

namespace LightCrosshair
{
    public partial class ProfileForm : Form
    {
        private readonly ProfileManager _profileManager;
        private CrosshairProfile _currentProfile;
        private bool _isCapturingHotkey = false;

        public ProfileForm(ProfileManager profileManager)
        {
            InitializeComponent();
            _profileManager = profileManager;
            _currentProfile = _profileManager.CurrentProfile.Clone();

            // Set form properties
            this.Text = "Crosshair Profile";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(450, 550);

            // Use default icon - don't try to load from file
            this.Icon = SystemIcons.Application;

            // Create controls
            CreateControls();
            LoadProfileList();
            UpdateUIFromProfile();
        }

        private void CreateControls()
        {
            // Profile selection
            var profileLabel = new Label
            {
                Text = "Profile:",
                Location = new Point(20, 20),
                AutoSize = true
            };

            profileComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(120, 20),
                Width = 200
            };
            profileComboBox.SelectedIndexChanged += ProfileComboBox_SelectedIndexChanged;

            var newProfileButton = new Button
            {
                Text = "New",
                Location = new Point(330, 20),
                Width = 80
            };
            newProfileButton.Click += NewProfileButton_Click;

            var deleteProfileButton = new Button
            {
                Text = "Delete",
                Location = new Point(330, 50),
                Width = 80
            };
            deleteProfileButton.Click += DeleteProfileButton_Click;

            // Profile name
            var nameLabel = new Label
            {
                Text = "Name:",
                Location = new Point(20, 50),
                AutoSize = true
            };

            nameTextBox = new TextBox
            {
                Location = new Point(120, 50),
                Width = 200
            };
            nameTextBox.TextChanged += NameTextBox_TextChanged;

            // Hotkey
            var hotkeyLabel = new Label
            {
                Text = "Hotkey:",
                Location = new Point(20, 80),
                AutoSize = true
            };

            hotkeyTextBox = new TextBox
            {
                Location = new Point(120, 80),
                Width = 200,
                ReadOnly = true
            };

            var setHotkeyButton = new Button
            {
                Text = "Set Hotkey",
                Location = new Point(330, 80),
                Width = 80
            };
            setHotkeyButton.Click += SetHotkeyButton_Click;

            // Shape selection
            var shapeLabel = new Label
            {
                Text = "Shape:",
                Location = new Point(20, 110),
                AutoSize = true
            };

            shapeComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(120, 110),
                Width = 200
            };
            shapeComboBox.Items.AddRange(new object[] { "Cross", "Circle", "Dot", "Plus", "X" });
            shapeComboBox.SelectedIndexChanged += ShapeComboBox_SelectedIndexChanged;

            // Size selection
            var sizeLabel = new Label
            {
                Text = "Size:",
                Location = new Point(20, 140),
                AutoSize = true
            };

            sizeTrackBar = new TrackBar
            {
                Minimum = 5,
                Maximum = 100,
                TickFrequency = 5,
                Location = new Point(120, 140),
                Width = 200
            };
            sizeTrackBar.ValueChanged += SizeTrackBar_ValueChanged;

            sizeValueLabel = new Label
            {
                Text = "5%",
                Location = new Point(330, 140),
                AutoSize = true
            };

            // Thickness selection
            var thicknessLabel = new Label
            {
                Text = "Thickness:",
                Location = new Point(20, 180),
                AutoSize = true
            };

            thicknessTrackBar = new TrackBar
            {
                Minimum = 1,
                Maximum = 20,
                TickFrequency = 1,
                Location = new Point(120, 180),
                Width = 200
            };
            thicknessTrackBar.ValueChanged += ThicknessTrackBar_ValueChanged;

            thicknessValueLabel = new Label
            {
                Text = "1",
                Location = new Point(330, 180),
                AutoSize = true
            };

            // Edge color
            var edgeColorLabel = new Label
            {
                Text = "Edge Color:",
                Location = new Point(20, 220),
                AutoSize = true
            };

            edgeColorPanel = new Panel
            {
                Location = new Point(120, 220),
                Size = new Size(30, 20),
                BorderStyle = BorderStyle.FixedSingle
            };

            var edgeColorButton = new Button
            {
                Text = "Change",
                Location = new Point(160, 220),
                Width = 80
            };
            edgeColorButton.Click += EdgeColorButton_Click;

            // Inner color
            var innerColorLabel = new Label
            {
                Text = "Inner Color:",
                Location = new Point(20, 250),
                AutoSize = true
            };

            innerColorPanel = new Panel
            {
                Location = new Point(120, 250),
                Size = new Size(30, 20),
                BorderStyle = BorderStyle.FixedSingle
            };

            var innerColorButton = new Button
            {
                Text = "Change",
                Location = new Point(160, 250),
                Width = 80
            };
            innerColorButton.Click += InnerColorButton_Click;

            // Fill color
            var fillColorLabel = new Label
            {
                Text = "Fill Color:",
                Location = new Point(20, 280),
                AutoSize = true
            };

            fillColorPanel = new Panel
            {
                Location = new Point(120, 280),
                Size = new Size(30, 20),
                BorderStyle = BorderStyle.FixedSingle
            };

            var fillColorButton = new Button
            {
                Text = "Change",
                Location = new Point(160, 280),
                Width = 80
            };
            fillColorButton.Click += FillColorButton_Click;

            // Preview
            var previewLabel = new Label
            {
                Text = "Preview:",
                Location = new Point(20, 310),
                AutoSize = true
            };

            previewPanel = new Panel
            {
                Location = new Point(120, 310),
                Size = new Size(200, 150),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.Black
            };
            previewPanel.Paint += PreviewPanel_Paint;

            // Save and Cancel buttons
            var saveButton = new Button
            {
                Text = "Save",
                DialogResult = DialogResult.OK,
                Location = new Point(120, 470),
                Width = 100
            };
            saveButton.Click += SaveButton_Click;

            var cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(230, 470),
                Width = 100
            };

            // Add controls to form
            this.Controls.AddRange(new Control[]
            {
                profileLabel, profileComboBox, newProfileButton, deleteProfileButton,
                nameLabel, nameTextBox,
                hotkeyLabel, hotkeyTextBox, setHotkeyButton,
                shapeLabel, shapeComboBox,
                sizeLabel, sizeTrackBar, sizeValueLabel,
                thicknessLabel, thicknessTrackBar, thicknessValueLabel,
                edgeColorLabel, edgeColorPanel, edgeColorButton,
                innerColorLabel, innerColorPanel, innerColorButton,
                fillColorLabel, fillColorPanel, fillColorButton,
                previewLabel, previewPanel,
                saveButton, cancelButton
            });

            this.AcceptButton = saveButton;
            this.CancelButton = cancelButton;

            // Set up hotkey capture
            this.KeyDown += ProfileForm_KeyDown;
            this.KeyPreview = true;
        }

        private void LoadProfileList()
        {
            profileComboBox.Items.Clear();
            foreach (var profile in _profileManager.Profiles)
            {
                profileComboBox.Items.Add(profile.Name);
                if (profile.Name == _profileManager.CurrentProfile.Name)
                {
                    profileComboBox.SelectedItem = profile.Name;
                }
            }

            if (profileComboBox.SelectedIndex == -1 && profileComboBox.Items.Count > 0)
            {
                profileComboBox.SelectedIndex = 0;
            }
        }

        private void UpdateUIFromProfile()
        {
            // Update UI controls from current profile
            nameTextBox.Text = _currentProfile.Name;
            hotkeyTextBox.Text = _currentProfile.HotKey == Keys.None ? "None" : _currentProfile.HotKey.ToString();
            shapeComboBox.SelectedItem = _currentProfile.Shape;
            sizeTrackBar.Value = _currentProfile.Size;
            sizeValueLabel.Text = $"{_currentProfile.Size}%";
            thicknessTrackBar.Value = _currentProfile.Thickness;
            thicknessValueLabel.Text = _currentProfile.Thickness.ToString();
            edgeColorPanel.BackColor = _currentProfile.EdgeColor;
            innerColorPanel.BackColor = _currentProfile.InnerColor;
            fillColorPanel.BackColor = _currentProfile.FillColor;

            // Refresh preview
            previewPanel.Invalidate();
        }

        private void ProfileComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (profileComboBox.SelectedItem != null)
            {
                string profileName = profileComboBox.SelectedItem.ToString();
                var profile = _profileManager.Profiles.Find(p => p.Name == profileName);
                if (profile != null)
                {
                    _currentProfile = profile.Clone();
                    UpdateUIFromProfile();
                }
            }
        }

        private void NewProfileButton_Click(object sender, EventArgs e)
        {
            // Create a new profile based on the current one
            var newProfile = _currentProfile.Clone();
            newProfile.Name = "New Profile " + (_profileManager.Profiles.Count + 1);
            newProfile.HotKey = Keys.None;

            // Update UI
            _currentProfile = newProfile;
            UpdateUIFromProfile();

            // Add to combo box and select it
            profileComboBox.Items.Add(newProfile.Name);
            profileComboBox.SelectedItem = newProfile.Name;
        }

        private void DeleteProfileButton_Click(object sender, EventArgs e)
        {
            if (_profileManager.Profiles.Count <= 1)
            {
                MessageBox.Show("Cannot delete the last profile.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (MessageBox.Show("Are you sure you want to delete this profile?", "Confirm Delete",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                var profileToDelete = _profileManager.Profiles.Find(p => p.Name == _currentProfile.Name);
                if (profileToDelete != null)
                {
                    _profileManager.DeleteProfile(profileToDelete);
                    LoadProfileList();
                }
            }
        }

        private void NameTextBox_TextChanged(object sender, EventArgs e)
        {
            _currentProfile.Name = nameTextBox.Text;
        }

        private void SetHotkeyButton_Click(object sender, EventArgs e)
        {
            hotkeyTextBox.Text = "Press any key...";
            _isCapturingHotkey = true;
            hotkeyTextBox.Focus();
        }

        private void ProfileForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (_isCapturingHotkey)
            {
                // Ignore modifier keys by themselves
                if (e.KeyCode != Keys.ShiftKey && e.KeyCode != Keys.ControlKey &&
                    e.KeyCode != Keys.Menu && e.KeyCode != Keys.None)
                {
                    _currentProfile.HotKey = e.KeyCode;
                    hotkeyTextBox.Text = e.KeyCode.ToString();
                    _isCapturingHotkey = false;
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            }
        }

        private void ShapeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (shapeComboBox.SelectedItem != null)
            {
                _currentProfile.Shape = shapeComboBox.SelectedItem.ToString();
                previewPanel.Invalidate();
            }
        }

        private void SizeTrackBar_ValueChanged(object sender, EventArgs e)
        {
            _currentProfile.Size = sizeTrackBar.Value;
            sizeValueLabel.Text = $"{_currentProfile.Size}%";
            previewPanel.Invalidate();
        }

        private void ThicknessTrackBar_ValueChanged(object sender, EventArgs e)
        {
            _currentProfile.Thickness = thicknessTrackBar.Value;
            thicknessValueLabel.Text = _currentProfile.Thickness.ToString();
            previewPanel.Invalidate();
        }

        private void EdgeColorButton_Click(object sender, EventArgs e)
        {
            using (var colorDialog = new ColorDialog())
            {
                colorDialog.Color = _currentProfile.EdgeColor;
                colorDialog.FullOpen = true;

                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    _currentProfile.EdgeColor = colorDialog.Color;
                    edgeColorPanel.BackColor = colorDialog.Color;
                    previewPanel.Invalidate();
                }
            }
        }

        private void InnerColorButton_Click(object sender, EventArgs e)
        {
            using (var colorDialog = new ColorDialog())
            {
                colorDialog.Color = _currentProfile.InnerColor;
                colorDialog.FullOpen = true;

                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    _currentProfile.InnerColor = colorDialog.Color;
                    innerColorPanel.BackColor = colorDialog.Color;
                    previewPanel.Invalidate();
                }
            }
        }

        private void FillColorButton_Click(object sender, EventArgs e)
        {
            using (var colorDialog = new ColorDialog())
            {
                colorDialog.Color = _currentProfile.FillColor;
                colorDialog.FullOpen = true;
                colorDialog.AllowFullOpen = true;

                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    _currentProfile.FillColor = colorDialog.Color;
                    fillColorPanel.BackColor = colorDialog.Color;
                    previewPanel.Invalidate();
                }
            }
        }

        private void PreviewPanel_Paint(object sender, PaintEventArgs e)
        {
            // Set up graphics for smoother rendering
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            e.Graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

            // Draw crosshair based on current profile
            int centerX = previewPanel.Width / 2;
            int centerY = previewPanel.Height / 2;
            int size = _currentProfile.Size / 2; // Convert percentage to actual size

            // Create pens and brushes
            using (Pen edgePen = new Pen(_currentProfile.EdgeColor, _currentProfile.Thickness))
            using (Pen innerPen = new Pen(_currentProfile.InnerColor, Math.Max(1, _currentProfile.Thickness - 2)))
            using (SolidBrush fillBrush = new SolidBrush(_currentProfile.FillColor))
            {
                switch (_currentProfile.Shape)
                {
                    case "Circle":
                        // Circle is a hollow circle with a border
                        // Draw fill if not transparent
                        if (_currentProfile.FillColor.A > 0)
                        {
                            e.Graphics.FillEllipse(fillBrush, centerX - size, centerY - size, size * 2, size * 2);
                        }

                        // Draw inner line (always draw it for better visibility)
                        e.Graphics.DrawEllipse(innerPen, centerX - size + _currentProfile.Thickness/2, centerY - size + _currentProfile.Thickness/2,
                            size * 2 - _currentProfile.Thickness, size * 2 - _currentProfile.Thickness);

                        // Draw edge
                        e.Graphics.DrawEllipse(edgePen, centerX - size, centerY - size, size * 2, size * 2);
                        break;

                    case "Dot":
                        // Dot is a solid filled circle
                        // Draw the filled circle
                        using (SolidBrush dotBrush = new SolidBrush(_currentProfile.EdgeColor))
                        {
                            e.Graphics.FillEllipse(dotBrush, centerX - size/2, centerY - size/2, size, size);
                        }

                        // Add inner color ring if thickness allows
                        if (_currentProfile.Thickness > 2)
                        {
                            using (SolidBrush innerBrush = new SolidBrush(_currentProfile.InnerColor))
                            {
                                e.Graphics.FillEllipse(innerBrush, centerX - size/2 + _currentProfile.Thickness/2,
                                    centerY - size/2 + _currentProfile.Thickness/2,
                                    size - _currentProfile.Thickness, size - _currentProfile.Thickness);
                            }
                        }

                        // Add fill color in center if not transparent
                        if (_currentProfile.FillColor.A > 0)
                        {
                            e.Graphics.FillEllipse(fillBrush, centerX - size/4, centerY - size/4, size/2, size/2);
                        }
                        break;

                    case "Plus":
                        // Plus is a thin crosshair with equal length horizontal and vertical lines
                        // Draw fill in center if not transparent
                        if (_currentProfile.FillColor.A > 0)
                        {
                            e.Graphics.FillRectangle(fillBrush, centerX - _currentProfile.Thickness, centerY - _currentProfile.Thickness,
                                _currentProfile.Thickness * 2, _currentProfile.Thickness * 2);
                        }

                        // Always draw inner lines for better visibility
                        e.Graphics.DrawLine(innerPen, centerX - size + _currentProfile.Thickness/2, centerY, centerX + size - _currentProfile.Thickness/2, centerY);
                        e.Graphics.DrawLine(innerPen, centerX, centerY - size + _currentProfile.Thickness/2, centerX, centerY + size - _currentProfile.Thickness/2);

                        // Draw edge lines
                        e.Graphics.DrawLine(edgePen, centerX - size, centerY, centerX + size, centerY);
                        e.Graphics.DrawLine(edgePen, centerX, centerY - size, centerX, centerY + size);
                        break;

                    case "X":
                        // X is a diagonal crosshair
                        // Draw fill in center if not transparent
                        if (_currentProfile.FillColor.A > 0)
                        {
                            using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
                            {
                                path.AddPolygon(new Point[] {
                                    new Point(centerX, centerY - _currentProfile.Thickness),
                                    new Point(centerX + _currentProfile.Thickness, centerY),
                                    new Point(centerX, centerY + _currentProfile.Thickness),
                                    new Point(centerX - _currentProfile.Thickness, centerY)
                                });
                                e.Graphics.FillPath(fillBrush, path);
                            }
                        }

                        // Always draw inner lines for better visibility
                        e.Graphics.DrawLine(innerPen, centerX - size + _currentProfile.Thickness/2, centerY - size + _currentProfile.Thickness/2,
                            centerX + size - _currentProfile.Thickness/2, centerY + size - _currentProfile.Thickness/2);
                        e.Graphics.DrawLine(innerPen, centerX - size + _currentProfile.Thickness/2, centerY + size - _currentProfile.Thickness/2,
                            centerX + size - _currentProfile.Thickness/2, centerY - size + _currentProfile.Thickness/2);

                        // Draw edge lines
                        e.Graphics.DrawLine(edgePen, centerX - size, centerY - size, centerX + size, centerY + size);
                        e.Graphics.DrawLine(edgePen, centerX - size, centerY + size, centerX + size, centerY - size);
                        break;

                    case "Cross":
                    default:
                        // Cross is a thicker crosshair with a gap in the middle
                        // Draw fill in center if not transparent
                        if (_currentProfile.FillColor.A > 0)
                        {
                            e.Graphics.FillEllipse(fillBrush, centerX - _currentProfile.Thickness, centerY - _currentProfile.Thickness,
                                _currentProfile.Thickness * 2, _currentProfile.Thickness * 2);
                        }

                        // Always draw inner lines for better visibility
                        // Horizontal lines with gap
                        e.Graphics.DrawLine(innerPen, centerX - size + _currentProfile.Thickness/2, centerY, centerX - _currentProfile.Thickness * 2, centerY);
                        e.Graphics.DrawLine(innerPen, centerX + _currentProfile.Thickness * 2, centerY, centerX + size - _currentProfile.Thickness/2, centerY);
                        // Vertical lines with gap
                        e.Graphics.DrawLine(innerPen, centerX, centerY - size + _currentProfile.Thickness/2, centerX, centerY - _currentProfile.Thickness * 2);
                        e.Graphics.DrawLine(innerPen, centerX, centerY + _currentProfile.Thickness * 2, centerX, centerY + size - _currentProfile.Thickness/2);

                        // Draw edge lines with gap
                        // Horizontal lines
                        e.Graphics.DrawLine(edgePen, centerX - size, centerY, centerX - _currentProfile.Thickness, centerY);
                        e.Graphics.DrawLine(edgePen, centerX + _currentProfile.Thickness, centerY, centerX + size, centerY);
                        // Vertical lines
                        e.Graphics.DrawLine(edgePen, centerX, centerY - size, centerX, centerY - _currentProfile.Thickness);
                        e.Graphics.DrawLine(edgePen, centerX, centerY + _currentProfile.Thickness, centerX, centerY + size);
                        break;
                }
            }
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            // Check if name is unique or unchanged
            bool isNameUnique = true;
            string originalName = profileComboBox.SelectedItem?.ToString();

            if (_currentProfile.Name != originalName)
            {
                foreach (var profile in _profileManager.Profiles)
                {
                    if (profile.Name == _currentProfile.Name)
                    {
                        isNameUnique = false;
                        break;
                    }
                }
            }

            if (!isNameUnique)
            {
                MessageBox.Show("A profile with this name already exists. Please choose a different name.",
                    "Duplicate Name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Save the profile
            if (originalName != null && _profileManager.Profiles.Exists(p => p.Name == originalName))
            {
                // Update existing profile
                _profileManager.UpdateProfile(_currentProfile);
            }
            else
            {
                // Add new profile
                _profileManager.AddProfile(_currentProfile);
            }

            // Switch to this profile
            _profileManager.SwitchToProfile(_currentProfile.Name);
        }

        // Designer-generated fields
        private ComboBox profileComboBox;
        private TextBox nameTextBox;
        private TextBox hotkeyTextBox;
        private ComboBox shapeComboBox;
        private TrackBar sizeTrackBar;
        private Label sizeValueLabel;
        private TrackBar thicknessTrackBar;
        private Label thicknessValueLabel;
        private Panel edgeColorPanel;
        private Panel innerColorPanel;
        private Panel fillColorPanel;
        private Panel previewPanel;

        private void InitializeComponent()
        {
            this.SuspendLayout();
            //
            // ProfileForm
            //
            this.ClientSize = new System.Drawing.Size(450, 500);
            this.Name = "ProfileForm";
            this.ResumeLayout(false);
        }
    }
}
