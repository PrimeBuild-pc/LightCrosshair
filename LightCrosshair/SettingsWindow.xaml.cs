using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Diagnostics;

namespace LightCrosshair
{
    public partial class SettingsWindow : Window
    {
        private readonly IProfileService _profiles;
        private AppPreferences _prefs;
        private bool _suppressUiEvents = false; // prevent feedback during programmatic updates

        public SettingsWindow(IProfileService profiles)
        {
            _profiles = profiles;
            _prefs = PreferencesStore.Load();
            InitializeComponent();

            // Window position and size
            if (_prefs.WindowX >= 0 && _prefs.WindowY >= 0)
            {
                Left = _prefs.WindowX;
                Top = _prefs.WindowY;
            }
            Width = _prefs.WindowWidth;
            Height = _prefs.WindowHeight;

            // Theme
            ApplyTheme(_prefs.Theme);
            UpdateThemeButtonIcon();

            // Wire controls
            ShapeCombo.SelectionChanged += (_, __) =>
            {
                // Clear composite selection to avoid conflicts
                if (CompositeCombo.SelectedIndex != -1) CompositeCombo.SelectedIndex = -1;
                ApplyChange(p =>
                {
                    var item = (ShapeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Cross";
                    p.Shape = item;
                    p.EnumShape = MapShape(item);
                });
                UpdateInnerTabEnabled();
            };
            CompositeCombo.SelectionChanged += (_, __) =>
            {
                // Clear simple selection to avoid conflicts
                if (ShapeCombo.SelectedIndex != -1) ShapeCombo.SelectedIndex = -1;
                ApplyChange(p =>
                {
                    var item = (CompositeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    if (!string.IsNullOrEmpty(item))
                    {
                        p.Shape = item;
                        p.EnumShape = MapShape(item);
                    }
                });
                UpdateInnerTabEnabled();
            };

            SizeSlider.ValueChanged += (_, __) => { SizeValue.Text = ((int)SizeSlider.Value).ToString(); ApplyChange(p => p.Size = (int)SizeSlider.Value); };
            ThicknessSlider.ValueChanged += (_, __) => { ThicknessValue.Text = ((int)ThicknessSlider.Value).ToString(); ApplyChange(p => p.Thickness = (int)ThicknessSlider.Value); };
            GapSlider.ValueChanged += (_, __) => { GapValue.Text = ((int)GapSlider.Value).ToString(); ApplyChange(p => p.GapSize = (int)GapSlider.Value); };

            InnerSizeSlider.ValueChanged += (_, __) => { InnerSizeValue.Text = ((int)InnerSizeSlider.Value).ToString(); ApplyChange(p => p.InnerSize = (int)InnerSizeSlider.Value); };
            InnerThicknessSlider.ValueChanged += (_, __) => { InnerThicknessValue.Text = ((int)InnerThicknessSlider.Value).ToString(); ApplyChange(p => p.InnerThickness = (int)InnerThicknessSlider.Value); };
            InnerGapSlider.ValueChanged += (_, __) => { InnerGapValue.Text = ((int)InnerGapSlider.Value).ToString(); ApplyChange(p => p.InnerGapSize = (int)InnerGapSlider.Value); };

            OuterColorBtn.Click += (_, __) => PickColor(c => ApplyChange(p => p.OuterColor = System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B)));
            InnerShapeColorBtn.Click += (_, __) => PickColor(c => ApplyChange(p => p.InnerShapeColor = System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B)));

            ThemeToggle.Click += (_, __) => { _prefs.Theme = _prefs.Theme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark; ApplyTheme(_prefs.Theme); UpdateThemeButtonIcon(); SavePrefs(); };

            // Reflect current profile
            LoadFromProfile(_profiles.Current);
            // Profiles tab wiring
            SaveCurrentProfileBtn.Click += (_, __) => SaveCurrentProfileToSelected();
            RefreshProfilesUI();

            _profiles.CurrentChanged += (_, p) => Dispatcher.Invoke(() => LoadFromProfile(p));
        }

        private void PickColor(Action<System.Windows.Media.Color> onPick)
        {
            using var dlg = new System.Windows.Forms.ColorDialog { FullOpen = true, AllowFullOpen = true, AnyColor = true };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var c = System.Windows.Media.Color.FromArgb(dlg.Color.A, dlg.Color.R, dlg.Color.G, dlg.Color.B);
                onPick(c);
            }
        }

        private void LoadFromProfile(CrosshairProfile p)
        {
            _suppressUiEvents = true;
            // Clear both selections to avoid conflicting visual states
            ShapeCombo.SelectedIndex = -1;
            CompositeCombo.SelectedIndex = -1;

            // Choose the correct selector based on shape kind
            bool isComposite = CompositeShapes.Contains(p.Shape);
            var targetCombo = isComposite ? CompositeCombo : ShapeCombo;
            foreach (var item in targetCombo.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Content?.ToString(), p.Shape, StringComparison.OrdinalIgnoreCase))
                {
                    targetCombo.SelectedItem = item;
                    break;
                }
            }

            UpdateInnerTabEnabled();

            SizeSlider.Value = p.Size;
            SizeValue.Text = p.Size.ToString();
            ThicknessSlider.Value = p.Thickness;
            ThicknessValue.Text = p.Thickness.ToString();
            GapSlider.Value = p.GapSize;
            GapValue.Text = p.GapSize.ToString();

            InnerSizeSlider.Value = p.InnerSize;
            InnerSizeValue.Text = p.InnerSize.ToString();
            InnerThicknessSlider.Value = p.InnerThickness;
            InnerThicknessValue.Text = p.InnerThickness.ToString();
            InnerGapSlider.Value = p.InnerGapSize;
            InnerGapValue.Text = p.InnerGapSize.ToString();
            _suppressUiEvents = false;
        }

        private void ApplyChange(Action<CrosshairProfile> change)
        {
            if (_suppressUiEvents) return;
            var cur = _profiles.Current.Clone();
            change(cur);
            if (!cur.ContentEquals(_profiles.Current))
            {
                _profiles.Update(cur);
            }
        }

        private void ApplyTheme(AppTheme theme)
        {
            // Use clean ResourceDictionary replacement to avoid mixed states after repeated toggles
            Resources.MergedDictionaries.Clear();
            var dict = new ResourceDictionary();
            if (theme == AppTheme.Dark)
            {
                // Neutral gray palette
                dict["WindowBg"] = new SolidColorBrush(ColorFromRgb(30, 31, 34));   // #1E1F22
                dict["PanelBg"]  = new SolidColorBrush(ColorFromRgb(43, 45, 49));   // #2B2D31
                dict["TextFg"]    = new SolidColorBrush(System.Windows.Media.Color.FromRgb((byte)230,(byte)230,(byte)230)); // #E6E6E6
                dict["AccentBrush"] = new SolidColorBrush(Colors.Orange);
                dict["ControlBg"] = new SolidColorBrush(ColorFromRgb(47, 49, 54));  // #2F3136
                dict["ControlBorder"] = new SolidColorBrush(ColorFromRgb(60, 63, 68)); // #3C3F44
                dict["TabSelectedBg"] = new SolidColorBrush(ColorFromRgb(50, 52, 56)); // #323438
            }
            else
            {
                dict["WindowBg"] = new SolidColorBrush(Colors.White);
                dict["PanelBg"]  = new SolidColorBrush(System.Windows.Media.Color.FromRgb((byte)245,(byte)245,(byte)245));
                dict["TextFg"]    = new SolidColorBrush(Colors.Black);
                dict["AccentBrush"] = new SolidColorBrush(Colors.Orange);
                dict["ControlBg"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb((byte)250,(byte)250,(byte)250));
                dict["ControlBorder"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb((byte)220,(byte)220,(byte)220));
                dict["TabSelectedBg"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb((byte)235,(byte)235,(byte)235));
            }
            Resources.MergedDictionaries.Add(dict);

            Background = (System.Windows.Media.Brush)dict["WindowBg"];
            RootPanel.Background = (System.Windows.Media.Brush)dict["PanelBg"];
            Foreground = (System.Windows.Media.Brush)dict["TextFg"];
        }

        private void RefreshProfilesUI()
        {
            if (ProfilesList == null) return;
            ProfilesList.Items.Clear();
            var list = _profiles.Profiles.ToList();
            for (int i = 0; i < 10; i++)
            {
                if (i < list.Count)
                {
                    var item = BuildProfileListItem(list[i], i);
                    ProfilesList.Items.Add(item);
                }
                else
                {
                    var placeholder = new System.Windows.Controls.ListBoxItem { Tag = i };
                    var sp = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(4) };
                    sp.Children.Add(new System.Windows.Controls.TextBlock { Text = $"Slot {i + 1}: (Empty)", Width = 260, Foreground = (System.Windows.Media.Brush)FindResource("TextFg") });
                    var btn = new System.Windows.Controls.Button { Content = "Save Here", Width = 90, Margin = new Thickness(6, 0, 0, 0), Tag = i };
                    btn.Click += (s, e) => SaveCurrentToNewAtIndex((int)((System.Windows.Controls.Button)s!).Tag);
                    sp.Children.Add(btn);
                    placeholder.Content = sp;
                    ProfilesList.Items.Add(placeholder);
                }
            }
        }

        private System.Windows.Controls.ListBoxItem BuildProfileListItem(CrosshairProfile p, int index)
        {
            var item = new System.Windows.Controls.ListBoxItem { Tag = p.Id };
            var sp = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(4) };
            sp.Children.Add(new System.Windows.Controls.TextBlock { Text = $"Slot {index + 1}:", Width = 64, Foreground = (System.Windows.Media.Brush)FindResource("TextFg") });
            var nameBox = new System.Windows.Controls.TextBox { Text = p.Name, Width = 190, IsReadOnly = true, Margin = new Thickness(0, 0, 4, 0) };
            sp.Children.Add(nameBox);
            var loadBtn = new System.Windows.Controls.Button { Content = "Load", Width = 70, Margin = new Thickness(6, 0, 0, 0) };
            loadBtn.Click += (_, __) => { _profiles.Switch(p.Id); };
            var saveBtn = new System.Windows.Controls.Button { Content = "Save", Width = 70, Margin = new Thickness(6, 0, 0, 0) };
            saveBtn.Click += (_, __) => { var cur = _profiles.Current.Clone(); cur.Id = p.Id; cur.Name = p.Name; _profiles.Update(cur); };
            var renameBtn = new System.Windows.Controls.Button { Content = "Rename", Width = 80, Margin = new Thickness(6, 0, 0, 0) };
            renameBtn.Click += (_, __) =>
            {
                var text = ShowInputDialog("Rename Profile", p.Name);
                if (!string.IsNullOrWhiteSpace(text)) { p.Name = text.Trim(); _profiles.Update(p); RefreshProfilesUI(); }
            };
            var deleteBtn = new System.Windows.Controls.Button { Content = "Delete", Width = 80, Margin = new Thickness(6, 0, 0, 0) };
            deleteBtn.Click += (_, __) => { if (_profiles.Delete(p.Id)) RefreshProfilesUI(); };
            sp.Children.Add(loadBtn);
            sp.Children.Add(saveBtn);
            sp.Children.Add(renameBtn);
            sp.Children.Add(deleteBtn);
            item.Content = sp;
            return item;
        }

        private void SaveCurrentProfileToSelected()
        {
            if (ProfilesList == null || ProfilesList.SelectedItem == null) return;
            if (ProfilesList.SelectedItem is System.Windows.Controls.ListBoxItem lbi)
            {
                if (lbi.Tag is string id && _profiles.Profiles.FirstOrDefault(x => x.Id == id) is CrosshairProfile existing)
                {
                    var cur = _profiles.Current.Clone(); cur.Id = existing.Id; cur.Name = existing.Name; _profiles.Update(cur);
                }
                else if (lbi.Tag is int index)
                {
                    SaveCurrentToNewAtIndex(index);
                }
            }
        }

        private void SaveCurrentToNewAtIndex(int targetIndex)
        {
            var name = $"Profile {targetIndex + 1}";
            var created = _profiles.AddClone(_profiles.Current, name);
            int idx = _profiles.Profiles.ToList().FindIndex(x => x.Id == created.Id);
            int delta = targetIndex - idx;
            if (delta != 0) _profiles.Move(created.Id, delta);
            RefreshProfilesUI();
        }

        private string? ShowInputDialog(string title, string initial)
        {
            var dlg = new System.Windows.Window
            {
                Title = title,
                WindowStyle = WindowStyle.ToolWindow,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = (System.Windows.Media.Brush)FindResource("WindowBg"),
                Foreground = (System.Windows.Media.Brush)FindResource("TextFg"),
                Owner = this
            };
            var sp = new System.Windows.Controls.StackPanel { Margin = new Thickness(12) };
            var tb = new System.Windows.Controls.TextBox { Text = initial, Width = 280 };
            var btns = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
            var ok = new System.Windows.Controls.Button { Content = "OK", Width = 70, Margin = new Thickness(6, 0, 0, 0) };
            var cancel = new System.Windows.Controls.Button { Content = "Cancel", Width = 70, Margin = new Thickness(6, 0, 0, 0) };
            string? result = null;
            ok.Click += (_, __) => { result = tb.Text; dlg.Close(); };
            cancel.Click += (_, __) => { dlg.Close(); };
            tb.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) { result = tb.Text; dlg.Close(); e.Handled = true; } };
            btns.Children.Add(ok);
            btns.Children.Add(cancel);
            sp.Children.Add(tb);
            sp.Children.Add(btns);
            dlg.Content = sp;
            dlg.ShowDialog();
            return result;
        }

        private void UpdateThemeButtonIcon()
        {
            try
            {
                if (ThemeGlyph != null)
                {
                    ThemeGlyph.Text = _prefs.Theme == AppTheme.Dark ? "\uD83C\uDF19" : "\u2600";
                }
            }
            catch { }
        }

        private static System.Windows.Media.Color ColorFromRgb(byte r, byte g, byte b) => System.Windows.Media.Color.FromRgb(r, g, b);

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            // Do not shutdown app; only hide settings window
            e.Cancel = true;
            Hide();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            SavePrefs();
        }
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            SavePrefs();
        }

        private void SavePrefs()
        {
            _prefs.WindowX = (int)Left;
            _prefs.WindowY = (int)Top;
            _prefs.WindowWidth = (int)Width;
            _prefs.WindowHeight = (int)Height;
            PreferencesStore.Save(_prefs);
        }

        private void OnHyperlinkNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch { }
            e.Handled = true;
        }


        private static readonly System.Collections.Generic.HashSet<string> CompositeShapes = new(System.StringComparer.OrdinalIgnoreCase)
        {
            "CircleDot", "CrossDot", "CircleCross", "CirclePlus", "CircleX"
        };

        private void UpdateInnerTabEnabled()
        {
            var shape = (CompositeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString()
                        ?? (ShapeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString()
                        ?? _profiles.Current.Shape;
            bool composite = !string.IsNullOrEmpty(shape) && CompositeShapes.Contains(shape);
            if (InnerTab != null)
            {
                InnerTab.IsEnabled = composite;
                if (!composite && InnerTab.IsSelected && OuterTab != null)
                    OuterTab.IsSelected = true;
            }
        }

        private static CrosshairShape MapShape(string s) => s switch
        {
            "Cross" => CrosshairShape.Cross,
            "Circle" => CrosshairShape.Circle,
            "Dot" => CrosshairShape.Dot,
            "Plus" => CrosshairShape.GapCross,
            "X" => CrosshairShape.X,
            _ => CrosshairShape.Custom,
        };
    }
}

