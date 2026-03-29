using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Windows.Threading;

namespace LightCrosshair
{
    public partial class SettingsWindow : Window
    {
        private const int DefaultSettingsWindowWidth = 1020;
        private const int DefaultSettingsWindowHeight = 500;
        private readonly IProfileService _profiles;
        private AppPreferences _prefs;
        private bool _suppressUiEvents = false; // prevent feedback during programmatic updates
        private bool _isUpdatingProcessPicker;
        private readonly Debouncer _displayApplyDebouncer = new(100);
        private const string LatestReleaseUrl = "https://github.com/PrimeBuild-pc/LightCrosshair/releases/latest";
        private const string LatestReleaseApiUrl = "https://api.github.com/repos/PrimeBuild-pc/LightCrosshair/releases/latest";
        private readonly string _currentVersion;
        private ResourceDictionary? _activeThemeDictionary;
        private static readonly HttpClient Http = new();
        private readonly EventHandler<CrosshairProfile> _profilesCurrentChangedHandler;
        private bool _allowWindowClose;
        private readonly DispatcherTimer _settingsSaveTimer;
        private bool _settingsSavePending;
        private CrosshairConfig? _pendingSettingsConfig;
        private const int SettingsSaveDebounceMs = 400;

        public SettingsWindow(IProfileService profiles)
        {
            _profiles = profiles;
            _prefs = PreferencesStore.Load();
            _profilesCurrentChangedHandler = (_, p) => Dispatcher.Invoke(() => { LoadFromProfile(p); UpdatePositionStatus(); });

            // One-time migration to the new default settings window dimensions.
            if (!_prefs.SettingsWindowSizeMigrated)
            {
                _prefs.WindowWidth = DefaultSettingsWindowWidth;
                _prefs.WindowHeight = DefaultSettingsWindowHeight;
                _prefs.SettingsWindowSizeMigrated = true;
                PreferencesStore.Save(_prefs);
            }

            // One-time v2 migration to the updated default dimensions (1020x500).
            if (!_prefs.SettingsWindowSizeMigratedV2)
            {
                _prefs.WindowWidth = DefaultSettingsWindowWidth;
                _prefs.WindowHeight = DefaultSettingsWindowHeight;
                _prefs.SettingsWindowSizeMigratedV2 = true;
                PreferencesStore.Save(_prefs);
            }

            // Ensure DynamicResource keys exist before InitializeComponent parses XAML styles.
            _activeThemeDictionary = CreateThemeDictionary(_prefs.Theme);
            Resources.MergedDictionaries.Add(_activeThemeDictionary);

            InitializeComponent();

            _settingsSaveTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(SettingsSaveDebounceMs)
            };
            _settingsSaveTimer.Tick += (_, __) => FlushPendingSettingsSave();

            _currentVersion = GetCurrentVersion();
            VersionText.Text = $"Version: {_currentVersion}";

            ConfigureHttpClient();

            // Window position and size
            if (_prefs.WindowX >= 0 && _prefs.WindowY >= 0)
            {
                Left = _prefs.WindowX;
                Top = _prefs.WindowY;
            }
            Width = _prefs.WindowWidth > 0 ? _prefs.WindowWidth : DefaultSettingsWindowWidth;
            Height = _prefs.WindowHeight > 0 ? _prefs.WindowHeight : DefaultSettingsWindowHeight;

            // Theme
            ApplyTheme(_prefs.Theme);
            UpdateThemeButtonIcon();

            // Wire controls
            EnableCustomCrosshairCheckbox.Checked += (_, __) =>
            {
                ApplyChange(p => p.EnableCustomCrosshair = true);
                UpdateBuilderControlsState();
            };
            EnableCustomCrosshairCheckbox.Unchecked += (_, __) =>
            {
                ApplyChange(p => p.EnableCustomCrosshair = false);
                UpdateBuilderControlsState();
            };

            ShapeCombo.SelectionChanged += (_, __) =>
            {
                ApplyChange(p =>
                {
                    var item = (ShapeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Cross";
                    p.Shape = item;
                    p.EnumShape = MapShape(item);
                });
            };

            OutlineCheckbox.Checked += (_, __) => ApplyChange(p => p.OutlineEnabled = true);
            OutlineCheckbox.Unchecked += (_, __) => ApplyChange(p => p.OutlineEnabled = false);

            SizeSlider.ValueChanged += (_, __) => { SizeValue.Text = ((int)SizeSlider.Value).ToString(); ApplyChange(p => p.Size = (int)SizeSlider.Value); };
            ThicknessSlider.ValueChanged += (_, __) => { ThicknessValue.Text = ((int)ThicknessSlider.Value).ToString(); ApplyChange(p => p.Thickness = (int)ThicknessSlider.Value); };
            GapSlider.ValueChanged += (_, __) => { GapValue.Text = ((int)GapSlider.Value).ToString(); ApplyChange(p => p.GapSize = (int)GapSlider.Value); };

            OuterColorBtn.Click += (_, __) => PickColor(c => ApplyChange(p =>
            {
                var col = System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
                p.OuterColor = col;
                p.InnerColor = col;
            }));
            InnerShapeColorBtn.Click += (_, __) => PickColor(c => ApplyChange(p =>
            {
                var col = System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
                p.EdgeColor = col;
                p.InnerShapeColor = col;
            }));

            if (NudgeLeft != null) NudgeLeft.Click += (_, __) => { RequestNudge(-1, 0); UpdatePositionStatus(); };
            if (NudgeRight != null) NudgeRight.Click += (_, __) => { RequestNudge(1, 0); UpdatePositionStatus(); };
            if (NudgeUp != null) NudgeUp.Click += (_, __) => { RequestNudge(0, -1); UpdatePositionStatus(); };
            if (NudgeDown != null) NudgeDown.Click += (_, __) => { RequestNudge(0, 1); UpdatePositionStatus(); };

            if (ResetCenterBtn != null) ResetCenterBtn.Click += (_, __) => { try { WpfSettingsHost.ResetToCenter(); } catch (Exception ex) { Program.LogError(ex, "SettingsWindow.ResetCenterBtn click"); } UpdatePositionStatus(); };

            ThemeToggle.Click += (_, __) => { _prefs.Theme = _prefs.Theme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark; ApplyTheme(_prefs.Theme); UpdateThemeButtonIcon(); SavePrefs(); };

            // Reflect current profile
            LoadFromProfile(_profiles.Current);
            LoadAdvancedSettings();
            WireAdvancedSettings();
            RefreshDisplayBackendInfo();

            // Profiles tab wiring
            SaveCurrentProfileBtn.Click += (_, __) => SaveCurrentProfileToSelected();
            RefreshProfilesUI();

            _profiles.CurrentChanged += _profilesCurrentChangedHandler;

            // Initial status
            UpdatePositionStatus();
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
            ShapeCombo.SelectedIndex = -1;

            EnableCustomCrosshairCheckbox.IsChecked = p.EnableCustomCrosshair;
            OutlineCheckbox.IsChecked = p.OutlineEnabled;

            string builderShape = GetBuilderShape(p);

            foreach (var item in ShapeCombo.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Content?.ToString(), builderShape, StringComparison.OrdinalIgnoreCase))
                {
                    ShapeCombo.SelectedItem = item;
                    break;
                }
            }

            SizeSlider.Value = p.Size;
            SizeValue.Text = p.Size.ToString();
            ThicknessSlider.Value = p.Thickness;
            ThicknessValue.Text = p.Thickness.ToString();
            GapSlider.Value = p.GapSize;
            GapValue.Text = p.GapSize.ToString();

            _suppressUiEvents = false;
            UpdateBuilderControlsState();
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
            // Add new theme dictionary first, then remove old one to avoid transient missing resources.
            var dict = CreateThemeDictionary(theme);

            Resources.MergedDictionaries.Add(dict);
            if (_activeThemeDictionary != null)
            {
                Resources.MergedDictionaries.Remove(_activeThemeDictionary);
            }
            _activeThemeDictionary = dict;
            Program.LogDebug($"ApplyTheme({theme}) dictionaries={Resources.MergedDictionaries.Count}", nameof(SettingsWindow));

            Background = GetThemeBrush("WindowBg", System.Windows.Media.Brushes.White);
            RootPanel.Background = GetThemeBrush("PanelBg", System.Windows.Media.Brushes.White);
            Foreground = GetThemeBrush("TextFg", System.Windows.Media.Brushes.Black);
        }

        private static ResourceDictionary CreateThemeDictionary(AppTheme theme)
        {
            var dict = new ResourceDictionary();
            if (theme == AppTheme.Dark)
            {
                // Neutral gray palette
                dict["WindowBg"] = new SolidColorBrush(ColorFromRgb(30, 31, 34));   // #1E1F22
                dict["PanelBg"] = new SolidColorBrush(ColorFromRgb(43, 45, 49));    // #2B2D31
                dict["TextFg"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb((byte)230, (byte)230, (byte)230)); // #E6E6E6
                dict["AccentBrush"] = new SolidColorBrush(Colors.Orange);
                dict["ControlBg"] = new SolidColorBrush(ColorFromRgb(47, 49, 54));  // #2F3136
                dict["ControlBorder"] = new SolidColorBrush(ColorFromRgb(60, 63, 68)); // #3C3F44
                dict["TabSelectedBg"] = new SolidColorBrush(ColorFromRgb(50, 52, 56)); // #323438
            }
            else
            {
                dict["WindowBg"] = new SolidColorBrush(Colors.White);
                dict["PanelBg"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb((byte)245, (byte)245, (byte)245));
                dict["TextFg"] = new SolidColorBrush(Colors.Black);
                dict["AccentBrush"] = new SolidColorBrush(Colors.Orange);
                dict["ControlBg"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb((byte)250, (byte)250, (byte)250));
                dict["ControlBorder"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb((byte)220, (byte)220, (byte)220));
                dict["TabSelectedBg"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb((byte)235, (byte)235, (byte)235));
            }

            return dict;
        }

        private System.Windows.Media.Brush GetThemeBrush(string key, System.Windows.Media.Brush fallback)
        {
            return TryFindResource(key) as System.Windows.Media.Brush ?? fallback;
        }

        private void RefreshProfilesUI()
        {
            if (ProfilesList == null) return;
            ProfilesList.Items.Clear();
            var list = _profiles.Profiles.ToList();
            for (int i = 0; i < ProfileService.MaxProfiles; i++)
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
                    sp.Children.Add(new System.Windows.Controls.TextBlock { Text = $"Slot {i + 1}: (Empty)", Width = 260, Foreground = GetThemeBrush("TextFg", System.Windows.Media.Brushes.Black) });
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
            sp.Children.Add(new System.Windows.Controls.TextBlock { Text = $"Slot {index + 1}:", Width = 64, Foreground = GetThemeBrush("TextFg", System.Windows.Media.Brushes.Black) });
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
                if (string.IsNullOrWhiteSpace(text)) return;

                string newName = text.Trim();
                if (_profiles.Profiles.Any(x => x.Id != p.Id && string.Equals(x.Name, newName, StringComparison.OrdinalIgnoreCase)))
                {
                    ShowAppDialog("Rename profile", "A profile with this name already exists.", MessageBoxImage.Warning);
                    return;
                }

                var updated = p.Clone();
                updated.Name = newName;
                _profiles.Update(updated);
                RefreshProfilesUI();
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
            try
            {
                var name = $"Profile {targetIndex + 1}";
                var created = _profiles.AddClone(_profiles.Current, name);
                int idx = _profiles.Profiles.ToList().FindIndex(x => x.Id == created.Id);
                int delta = targetIndex - idx;
                if (delta != 0) _profiles.Move(created.Id, delta);
                RefreshProfilesUI();
            }
            catch (InvalidOperationException ex)
            {
                ShowAppDialog("Profiles", ex.Message, MessageBoxImage.Information);
            }
        }

        private string? ShowInputDialog(string title, string initial)
        {
            var dlg = new System.Windows.Window
            {
                Title = title,
                WindowStyle = WindowStyle.ToolWindow,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = GetThemeBrush("WindowBg", System.Windows.Media.Brushes.White),
                Foreground = GetThemeBrush("TextFg", System.Windows.Media.Brushes.Black),
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
            catch (Exception ex)
            {
                Program.LogDebug($"UpdateThemeButtonIcon failed: {ex.Message}", nameof(SettingsWindow));
            }
        }

        private static System.Windows.Media.Color ColorFromRgb(byte r, byte g, byte b) => System.Windows.Media.Color.FromRgb(r, g, b);

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_allowWindowClose)
            {
                FlushPendingSettingsSave();
                _profiles.CurrentChanged -= _profilesCurrentChangedHandler;
                base.OnClosing(e);
                return;
            }

            base.OnClosing(e);
            FlushPendingSettingsSave();
            if (!_suppressUiEvents)
            {
                try
                {
                    SaveTargetProcessFromUi(CrosshairConfig.Instance);
                }
                catch (Exception ex)
                {
                    Program.LogError(ex, "SettingsWindow.OnClosing SaveTargetProcessFromUi");
                }
            }

            // Do not shutdown app; only hide settings window
            e.Cancel = true;
            Hide();
        }

        internal void AllowCloseForShutdown()
        {
            _allowWindowClose = true;
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

        private void QueueSettingsSave(CrosshairConfig cfg)
        {
            if (_suppressUiEvents)
            {
                return;
            }

            _pendingSettingsConfig = cfg;
            _settingsSavePending = true;
            _settingsSaveTimer.Stop();
            _settingsSaveTimer.Start();
        }

        private void FlushPendingSettingsSave()
        {
            _settingsSaveTimer.Stop();
            if (!_settingsSavePending || _pendingSettingsConfig == null)
            {
                return;
            }

            _settingsSavePending = false;
            _pendingSettingsConfig.SaveSettings();
            _pendingSettingsConfig = null;
        }

        private void OnHyperlinkNavigate(object sender, RequestNavigateEventArgs e)
        {
            OpenUrl(e.Uri.AbsoluteUri);
            e.Handled = true;
        }


        private async void OnCheckUpdatesClick(object sender, RoutedEventArgs e)
        {
            if (CheckUpdatesBtn != null && !CheckUpdatesBtn.IsEnabled)
            {
                return;
            }

            if (CheckUpdatesBtn != null)
            {
                CheckUpdatesBtn.IsEnabled = false;
            }

            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));

                var fetchTask = FetchLatestVersionSafe(cts.Token);
                _ = ShowTransientDialog("Check for updates", "Checking for a newer version...", 800);

                var latest = await fetchTask;
                if (latest == null)
                {
                    ShowAppDialog("Check for updates", "Could not check for updates right now. Please try again later.", MessageBoxImage.Warning);
                    return;
                }

                int? comparison = UpdateVersionComparer.CompareVersions(latest, _currentVersion);
                if (!comparison.HasValue)
                {
                    ShowAppDialog("Check for updates", $"Latest release is v{latest}, but this build version (v{_currentVersion}) could not be compared automatically.", MessageBoxImage.Warning);
                    return;
                }

                if (comparison.Value > 0)
                {
                    ShowAppDialog("Update available", $"A newer version is available: v{latest}. Opening the releases page.", MessageBoxImage.Information);
                    OpenUrl(LatestReleaseUrl);
                }
                else if (comparison.Value == 0)
                {
                    ShowAppDialog("Up to date", $"You are on the latest version (v{_currentVersion}).", MessageBoxImage.Information);
                }
                else
                {
                    ShowAppDialog("Version check", $"You are running a newer build (v{_currentVersion}) than the latest release (v{latest}).", MessageBoxImage.Information);
                }
            }
            catch (OperationCanceledException)
            {
                ShowAppDialog("Check for updates", "The update check timed out. Please try again.", MessageBoxImage.Warning);
            }
            finally
            {
                if (CheckUpdatesBtn != null)
                {
                    CheckUpdatesBtn.IsEnabled = true;
                }
            }
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "SettingsWindow.OpenUrl");
            }
        }

        private void ConfigureHttpClient()
        {
            if (!Http.DefaultRequestHeaders.Contains("User-Agent"))
            {
                Http.DefaultRequestHeaders.UserAgent.ParseAdd("LightCrosshair/1.0");
            }
        }

        private async System.Threading.Tasks.Task<string?> FetchLatestVersionSafe(System.Threading.CancellationToken cancellationToken)
        {
            try
            {
                var dto = await Http.GetFromJsonAsync<GithubReleaseDto>(LatestReleaseApiUrl, cancellationToken);
                var tag = dto?.TagName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(tag)) return null;
                return UpdateVersionComparer.NormalizeVersionTag(tag);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return null;
            }
        }

        private string GetCurrentVersion()
        {
            try
            {
                var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                if (v != null)
                {
                    // Use Major.Minor.Build for display
                    return new Version(v.Major, v.Minor, v.Build < 0 ? 0 : v.Build).ToString();
                }
            }
            catch (Exception ex)
            {
                Program.LogDebug($"GetCurrentVersion failed: {ex.Message}", nameof(SettingsWindow));
            }
            return "1.0.0";
        }

        private void ShowAppDialog(string title, string message, MessageBoxImage icon)
        {
            var dlg = new System.Windows.Window
            {
                Title = title,
                WindowStyle = WindowStyle.ToolWindow,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = GetThemeBrush("WindowBg", System.Windows.Media.Brushes.White),
                Foreground = GetThemeBrush("TextFg", System.Windows.Media.Brushes.Black),
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false
            };

            var sp = new System.Windows.Controls.StackPanel { Margin = new Thickness(16), Width = 380 };
            var txt = new System.Windows.Controls.TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
            sp.Children.Add(txt);

            var buttons = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
            var ok = new System.Windows.Controls.Button { Content = "OK", Width = 80, Margin = new Thickness(6, 0, 0, 0) };
            ok.Click += (_, __) => dlg.Close();
            buttons.Children.Add(ok);
            sp.Children.Add(buttons);

            dlg.Content = sp;
            dlg.ShowDialog();
        }

        private async System.Threading.Tasks.Task ShowTransientDialog(string title, string message, int milliseconds)
        {
            var dlg = new System.Windows.Window
            {
                Title = title,
                WindowStyle = WindowStyle.ToolWindow,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = GetThemeBrush("WindowBg", System.Windows.Media.Brushes.White),
                Foreground = GetThemeBrush("TextFg", System.Windows.Media.Brushes.Black),
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false
            };

            var sp = new System.Windows.Controls.StackPanel { Margin = new Thickness(16), Width = 380 };
            var txt = new System.Windows.Controls.TextBlock { Text = message, TextWrapping = TextWrapping.Wrap };
            sp.Children.Add(txt);

            dlg.Content = sp;
            dlg.Show();

            await System.Threading.Tasks.Task.Delay(milliseconds);
            dlg.Close();
        }

        private sealed class GithubReleaseDto
        {
            [JsonPropertyName("tag_name")]
            public string? TagName { get; set; }
        }


        // Removed CompositeShapes here

        // Removed UpdateInnerTabEnabled here

        private void UpdateBuilderControlsState()
        {
            bool enabled = EnableCustomCrosshairCheckbox.IsChecked == true;
            ShapeCombo.IsEnabled = enabled;
            SizeSlider.IsEnabled = enabled;
            ThicknessSlider.IsEnabled = enabled;
            GapSlider.IsEnabled = enabled;
            OutlineCheckbox.IsEnabled = enabled;
            OuterColorBtn.IsEnabled = enabled;
            InnerShapeColorBtn.IsEnabled = enabled;
        }

        private static string GetBuilderShape(CrosshairProfile p)
        {
            if (p.EnumShape == CrosshairShape.Dot || string.Equals(p.Shape, "Dot", StringComparison.OrdinalIgnoreCase))
            {
                return "Dot";
            }

            if (p.EnumShape == CrosshairShape.Circle || p.EnumShape == CrosshairShape.CircleOutlined)
            {
                return "Circle";
            }

            if (!string.IsNullOrWhiteSpace(p.Shape) && p.Shape.StartsWith("Circle", StringComparison.OrdinalIgnoreCase))
            {
                return "Circle";
            }

            return "Cross";
        }

        private static CrosshairShape MapShape(string s) => s switch
        {
            "Cross" => CrosshairShape.Cross,
            "Circle" => CrosshairShape.Circle,
            "Dot" => CrosshairShape.Dot,
            _ => CrosshairShape.Cross,
        };

        private void RequestNudge(int dx, int dy)
        {
            try { WpfSettingsHost.Nudge(dx, dy); }
            catch (Exception ex)
            {
                Program.LogDebug($"RequestNudge failed: {ex.Message}", nameof(SettingsWindow));
            }
        }

        private void UpdatePositionStatus()
        {
            try
            {
                var pt = WpfSettingsHost.QueryPosition();
                if (pt.HasValue && CurrentPositionText != null)
                    CurrentPositionText.Text = $"Current Position: X={pt.Value.X}, Y={pt.Value.Y}";
            }
            catch (Exception ex)
            {
                Program.LogDebug($"UpdatePositionStatus failed: {ex.Message}", nameof(SettingsWindow));
            }
        }
    
        private void LoadAdvancedSettings()
        {
            _suppressUiEvents = true;
            var cfg = CrosshairConfig.Instance;
            
            // Hotkeys
            HkAlt1.IsChecked = cfg.HotkeyUseAlt;
            HkCtrl1.IsChecked = cfg.HotkeyUseControl;
            HkShift1.IsChecked = cfg.HotkeyUseShift;
            LoadKeysCombo(HkKey1Combo, cfg.HotkeyKey);

            HkAlt2.IsChecked = cfg.CycleProfileHotkeyUseAlt;
            HkCtrl2.IsChecked = cfg.CycleProfileHotkeyUseControl;
            HkShift2.IsChecked = cfg.CycleProfileHotkeyUseShift;
            LoadKeysCombo(HkKey2Combo, cfg.CycleProfileHotkeyKey);

            HkAlt4.IsChecked = cfg.CycleProfilePrevHotkeyUseAlt;
            HkCtrl4.IsChecked = cfg.CycleProfilePrevHotkeyUseControl;
            HkShift4.IsChecked = cfg.CycleProfilePrevHotkeyUseShift;
            LoadKeysCombo(HkKey4Combo, cfg.CycleProfilePrevHotkeyKey);

            HkAlt3.IsChecked = cfg.SettingsWindowHotkeyUseAlt;
            HkCtrl3.IsChecked = cfg.SettingsWindowHotkeyUseControl;
            HkShift3.IsChecked = cfg.SettingsWindowHotkeyUseShift;
            LoadKeysCombo(HkKey3Combo, cfg.SettingsWindowHotkeyKey);

            // Gamma
            EnableGammaCheckbox.IsChecked = cfg.EnableGammaOverride;
            GammaSlider.Value = cfg.GammaValue;
            GammaValueText.Text = cfg.GammaValue.ToString();
            ContrastSlider.Value = cfg.ContrastValue;
            ContrastValueText.Text = cfg.ContrastValue.ToString();
            BrightnessSlider.Value = cfg.BrightnessValue;
            BrightnessValueText.Text = cfg.BrightnessValue.ToString();
            VibranceSlider.Value = cfg.VibranceValue;
            VibranceValueText.Text = cfg.VibranceValue.ToString();
            TargetProcessTextBox.Text = cfg.TargetProcessName;
            RefreshProcessPickerItems();

            // FPS Overlay
            EnableFpsCheckbox.IsChecked = cfg.EnableFpsOverlay;
            FpsXSlider.Value = cfg.FpsOverlayX;
            FpsXValueText.Text = cfg.FpsOverlayX.ToString();
            FpsYSlider.Value = cfg.FpsOverlayY;
            FpsYValueText.Text = cfg.FpsOverlayY.ToString();
            FpsScaleSlider.Value = cfg.FpsOverlayScale;
            FpsScaleValueText.Text = $"{cfg.FpsOverlayScale}%";
            ShowFrametimeCheckbox.IsChecked = cfg.ShowFrametimeGraph;
            SelectGraphRefreshPreset(cfg.GraphRefreshRateMs);
            SelectGraphTimeWindowPreset(cfg.GraphTimeWindowMs);
            Show1PercentCheckbox.IsChecked = cfg.Show1PercentLows;
            ShowGenFramesCheckbox.IsChecked = cfg.ShowGenFrames;
            UpdateGraphRefreshUiState();
            _suppressUiEvents = false;
            RefreshDisplayBackendInfo();
        }

        private void RefreshDisplayBackendInfo()
        {
            var info = DisplayManager.GetBackendInfo();
            bool isSoftwareFallback = info.BackendName.IndexOf("Software Overlay", StringComparison.OrdinalIgnoreCase) >= 0;
            ColorBackendText.Text = isSoftwareFallback
                ? $"{info.BackendName} (active)"
                : info.BackendName;
            ColorBackendDetailsText.Text = $"GPU: {info.AdapterDescription} | {info.StatusMessage}";
        }

        private void ApplyDisplaySettingsDebounced(CrosshairConfig cfg)
        {
            if (!cfg.EnableGammaOverride)
            {
                return;
            }

            _displayApplyDebouncer.Trigger(() =>
            {
                DisplayManager.CheckForegroundAndApply(forceUpdate: true);
                Dispatcher.BeginInvoke(new Action(RefreshDisplayBackendInfo));
            });
        }

        private void ApplyDisplaySettingsImmediate(CrosshairConfig cfg)
        {
            if (!cfg.EnableGammaOverride)
            {
                return;
            }

            DisplayManager.CheckForegroundAndApply(forceUpdate: true);
            RefreshDisplayBackendInfo();
        }

        private static string NormalizeTargetProcessInput(string input)
        {
            var trimmed = (input ?? string.Empty).Trim();
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

        private void RefreshProcessPickerItems()
        {
            if (ProcessPickerComboBox == null)
            {
                return;
            }

            _isUpdatingProcessPicker = true;
            try
            {
                var selected = NormalizeTargetProcessInput(TargetProcessTextBox.Text);
                ProcessPickerComboBox.Items.Clear();
                ProcessPickerComboBox.Items.Add("(select running process)");

                var names = Process
                    .GetProcesses()
                    .Select(p =>
                    {
                        try
                        {
                            return p.ProcessName;
                        }
                        catch
                        {
                            return string.Empty;
                        }
                    })
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Select(n => NormalizeTargetProcessInput(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var name in names)
                {
                    ProcessPickerComboBox.Items.Add(name);
                }

                if (!string.IsNullOrWhiteSpace(selected))
                {
                    var match = names.FirstOrDefault(n => string.Equals(n, selected, StringComparison.OrdinalIgnoreCase));
                    ProcessPickerComboBox.SelectedItem = match ?? "(select running process)";
                }
                else
                {
                    ProcessPickerComboBox.SelectedItem = "(select running process)";
                }
            }
            finally
            {
                _isUpdatingProcessPicker = false;
            }
        }

        private void SaveTargetProcessFromUi(CrosshairConfig cfg)
        {
            cfg.TargetProcessName = NormalizeTargetProcessInput(TargetProcessTextBox.Text);
            TargetProcessTextBox.Text = cfg.TargetProcessName;
            RefreshProcessPickerItems();
            cfg.SaveSettings();

            if (cfg.EnableGammaOverride)
            {
                DisplayManager.CheckForegroundAndApply(forceUpdate: true);
            }
        }

        private void LoadKeysCombo(System.Windows.Controls.ComboBox combo, System.Windows.Forms.Keys currentKey)
        {
            combo.Items.Clear();
            foreach (var key in Enum.GetValues(typeof(System.Windows.Forms.Keys)).Cast<System.Windows.Forms.Keys>())
            {
                var item = new System.Windows.Controls.ComboBoxItem { Content = key.ToString(), Tag = key };
                combo.Items.Add(item);
                if (key == currentKey)
                    combo.SelectedItem = item;
            }
        }

        private void WireAdvancedSettings()
        {
            var cfg = CrosshairConfig.Instance;

            // Hotkey Toggle
            HkAlt1.Checked += (_, __) => { if (!_suppressUiEvents) { cfg.HotkeyUseAlt = true; cfg.SaveSettings(); cfg.ReRegisterHotkeys(); } };
            HkAlt1.Unchecked += (_, __) => { if (!_suppressUiEvents) { cfg.HotkeyUseAlt = false; cfg.SaveSettings(); cfg.ReRegisterHotkeys(); } };
            HkCtrl1.Checked += (_, __) => { if (!_suppressUiEvents) { cfg.HotkeyUseControl = true; cfg.SaveSettings(); cfg.ReRegisterHotkeys(); } };
            HkCtrl1.Unchecked += (_, __) => { if (!_suppressUiEvents) { cfg.HotkeyUseControl = false; cfg.SaveSettings(); cfg.ReRegisterHotkeys(); } };
            HkShift1.Checked += (_, __) => { if (!_suppressUiEvents) { cfg.HotkeyUseShift = true; cfg.SaveSettings(); cfg.ReRegisterHotkeys(); } };
            HkShift1.Unchecked += (_, __) => { if (!_suppressUiEvents) { cfg.HotkeyUseShift = false; cfg.SaveSettings(); cfg.ReRegisterHotkeys(); } };
            HkKey1Combo.SelectionChanged += (_, __) => 
            {
                if (_suppressUiEvents || HkKey1Combo.SelectedItem == null) return;
                cfg.HotkeyKey = (System.Windows.Forms.Keys)((System.Windows.Controls.ComboBoxItem)HkKey1Combo.SelectedItem).Tag;
                cfg.SaveSettings();
                cfg.ReRegisterHotkeys();
            };

            // Hotkey Cycle
            HkAlt2.Checked += (_, __) => { if (!_suppressUiEvents) { cfg.CycleProfileHotkeyUseAlt = true; cfg.SaveSettings(); cfg.ReRegisterHotkeys(); } };
            HkAlt2.Unchecked += (_, __) => { if (!_suppressUiEvents) { cfg.CycleProfileHotkeyUseAlt = false; cfg.SaveSettings(); cfg.ReRegisterHotkeys(); } };
            HkCtrl2.Checked += (_, __) => { if (!_suppressUiEvents) { cfg.CycleProfileHotkeyUseControl = true; cfg.SaveSettings(); cfg.ReRegisterHotkeys(); } };
            HkCtrl2.Unchecked += (_, __) => { if (!_suppressUiEvents) { cfg.CycleProfileHotkeyUseControl = false; cfg.SaveSettings(); cfg.ReRegisterHotkeys(); } };
            HkShift2.Checked += (_, __) => { if (!_suppressUiEvents) { cfg.CycleProfileHotkeyUseShift = true; cfg.SaveSettings(); cfg.ReRegisterHotkeys(); } };
            HkShift2.Unchecked += (_, __) => { if (!_suppressUiEvents) { cfg.CycleProfileHotkeyUseShift = false; cfg.SaveSettings(); cfg.ReRegisterHotkeys(); } };
            HkKey2Combo.SelectionChanged += (_, __) => 
            {
                if (_suppressUiEvents || HkKey2Combo.SelectedItem == null) return;
                cfg.CycleProfileHotkeyKey = (System.Windows.Forms.Keys)((System.Windows.Controls.ComboBoxItem)HkKey2Combo.SelectedItem).Tag;
                cfg.SaveSettings();
                cfg.ReRegisterHotkeys();
            };

            // Hotkey Cycle Back
            HkAlt4.Checked += (_, __) => { if (!_suppressUiEvents) { cfg.CycleProfilePrevHotkeyUseAlt = true; cfg.SaveSettings(); cfg.ReRegisterHotkeys(); } };
            HkAlt4.Unchecked += (_, __) => { if (!_suppressUiEvents) { cfg.CycleProfilePrevHotkeyUseAlt = false; cfg.SaveSettings(); cfg.ReRegisterHotkeys(); } };
            HkCtrl4.Checked += (_, __) => { if (!_suppressUiEvents) { cfg.CycleProfilePrevHotkeyUseControl = true; cfg.SaveSettings(); cfg.ReRegisterHotkeys(); } };
            HkCtrl4.Unchecked += (_, __) => { if (!_suppressUiEvents) { cfg.CycleProfilePrevHotkeyUseControl = false; cfg.SaveSettings(); cfg.ReRegisterHotkeys(); } };
            HkShift4.Checked += (_, __) => { if (!_suppressUiEvents) { cfg.CycleProfilePrevHotkeyUseShift = true; cfg.SaveSettings(); cfg.ReRegisterHotkeys(); } };
            HkShift4.Unchecked += (_, __) => { if (!_suppressUiEvents) { cfg.CycleProfilePrevHotkeyUseShift = false; cfg.SaveSettings(); cfg.ReRegisterHotkeys(); } };
            HkKey4Combo.SelectionChanged += (_, __) =>
            {
                if (_suppressUiEvents || HkKey4Combo.SelectedItem == null) return;
                cfg.CycleProfilePrevHotkeyKey = (System.Windows.Forms.Keys)((System.Windows.Controls.ComboBoxItem)HkKey4Combo.SelectedItem).Tag;
                cfg.SaveSettings();
                cfg.ReRegisterHotkeys();
            };

            // Hotkey Toggle Settings Window
            HkAlt3.Checked += (_, __) => { if (!_suppressUiEvents) { cfg.SettingsWindowHotkeyUseAlt = true; cfg.SaveSettings(); cfg.ReRegisterHotkeys(); } };
            HkAlt3.Unchecked += (_, __) => { if (!_suppressUiEvents) { cfg.SettingsWindowHotkeyUseAlt = false; cfg.SaveSettings(); cfg.ReRegisterHotkeys(); } };
            HkCtrl3.Checked += (_, __) => { if (!_suppressUiEvents) { cfg.SettingsWindowHotkeyUseControl = true; cfg.SaveSettings(); cfg.ReRegisterHotkeys(); } };
            HkCtrl3.Unchecked += (_, __) => { if (!_suppressUiEvents) { cfg.SettingsWindowHotkeyUseControl = false; cfg.SaveSettings(); cfg.ReRegisterHotkeys(); } };
            HkShift3.Checked += (_, __) => { if (!_suppressUiEvents) { cfg.SettingsWindowHotkeyUseShift = true; cfg.SaveSettings(); cfg.ReRegisterHotkeys(); } };
            HkShift3.Unchecked += (_, __) => { if (!_suppressUiEvents) { cfg.SettingsWindowHotkeyUseShift = false; cfg.SaveSettings(); cfg.ReRegisterHotkeys(); } };
            HkKey3Combo.SelectionChanged += (_, __) =>
            {
                if (_suppressUiEvents || HkKey3Combo.SelectedItem == null) return;
                cfg.SettingsWindowHotkeyKey = (System.Windows.Forms.Keys)((System.Windows.Controls.ComboBoxItem)HkKey3Combo.SelectedItem).Tag;
                cfg.SaveSettings();
                cfg.ReRegisterHotkeys();
            };

            // Gamma
            EnableGammaCheckbox.Checked += (_,__) =>
            {
                if (_suppressUiEvents) return;
                cfg.EnableGammaOverride = true;
                cfg.SaveSettings();
                ApplyDisplaySettingsImmediate(cfg);
            };
            EnableGammaCheckbox.Unchecked += (_,__) =>
            {
                if (_suppressUiEvents) return;
                cfg.EnableGammaOverride = false;
                cfg.SaveSettings();
                DisplayManager.CheckForegroundAndApply(forceUpdate: true);
                RefreshDisplayBackendInfo();
            };
            GammaSlider.ValueChanged += (_, __) =>
            {
                if (_suppressUiEvents) return;
                cfg.GammaValue = (int)GammaSlider.Value;
                GammaValueText.Text = cfg.GammaValue.ToString();
                QueueSettingsSave(cfg);
                ApplyDisplaySettingsDebounced(cfg);
            };
            ContrastSlider.ValueChanged += (_, __) =>
            {
                if (_suppressUiEvents) return;
                cfg.ContrastValue = (int)ContrastSlider.Value;
                ContrastValueText.Text = cfg.ContrastValue.ToString();
                QueueSettingsSave(cfg);
                ApplyDisplaySettingsDebounced(cfg);
            };
            BrightnessSlider.ValueChanged += (_, __) =>
            {
                if (_suppressUiEvents) return;
                cfg.BrightnessValue = (int)BrightnessSlider.Value;
                BrightnessValueText.Text = cfg.BrightnessValue.ToString();
                QueueSettingsSave(cfg);
                ApplyDisplaySettingsDebounced(cfg);
            };
            VibranceSlider.ValueChanged += (_, __) =>
            {
                if (_suppressUiEvents) return;
                cfg.VibranceValue = (int)VibranceSlider.Value;
                VibranceValueText.Text = cfg.VibranceValue.ToString();
                QueueSettingsSave(cfg);
                ApplyDisplaySettingsDebounced(cfg);
            };

            // Flush one final hardware apply when dragging ends.
            DragCompletedEventHandler dragCompleteHandler = (_, __) =>
            {
                if (_suppressUiEvents) return;
                FlushPendingSettingsSave();
                ApplyDisplaySettingsImmediate(cfg);
            };
            GammaSlider.AddHandler(Thumb.DragCompletedEvent, dragCompleteHandler);
            ContrastSlider.AddHandler(Thumb.DragCompletedEvent, dragCompleteHandler);
            BrightnessSlider.AddHandler(Thumb.DragCompletedEvent, dragCompleteHandler);
            VibranceSlider.AddHandler(Thumb.DragCompletedEvent, dragCompleteHandler);

            DragCompletedEventHandler saveFlushDragCompleteHandler = (_, __) =>
            {
                if (_suppressUiEvents) return;
                FlushPendingSettingsSave();
            };

            // Additional UI helpers: reset buttons for color parameters
            ResetGammaBtn.Click += (_, __) => { GammaSlider.Value = 100; ApplyDisplaySettingsImmediate(cfg); };
            ResetContrastBtn.Click += (_, __) => { ContrastSlider.Value = 100; ApplyDisplaySettingsImmediate(cfg); };
            ResetBrightnessBtn.Click += (_, __) => { BrightnessSlider.Value = 100; ApplyDisplaySettingsImmediate(cfg); };
            ResetVibranceBtn.Click += (_, __) => { VibranceSlider.Value = 50; ApplyDisplaySettingsImmediate(cfg); };

            TargetProcessTextBox.LostFocus += (_, __) =>
            {
                if (_suppressUiEvents) return;
                SaveTargetProcessFromUi(cfg);
            };
            TargetProcessTextBox.KeyDown += (_, e) =>
            {
                if (_suppressUiEvents) return;
                if (e.Key != System.Windows.Input.Key.Enter)
                {
                    return;
                }

                SaveTargetProcessFromUi(cfg);
                e.Handled = true;
            };
            ProcessPickerComboBox.SelectionChanged += (_, __) =>
            {
                if (_suppressUiEvents || _isUpdatingProcessPicker) return;
                if (ProcessPickerComboBox.SelectedItem is not string selected || selected.StartsWith("(", StringComparison.Ordinal))
                {
                    return;
                }

                TargetProcessTextBox.Text = selected;
                SaveTargetProcessFromUi(cfg);
            };
            RefreshProcessListBtn.Click += (_, __) =>
            {
                if (_suppressUiEvents) return;
                RefreshProcessPickerItems();
            };
            BrowseProcessExeBtn.Click += (_, __) =>
            {
                if (_suppressUiEvents) return;

                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                    Title = "Select game executable"
                };

                if (dlg.ShowDialog(this) == true)
                {
                    TargetProcessTextBox.Text = Path.GetFileName(dlg.FileName);
                    SaveTargetProcessFromUi(cfg);
                }
            };
            ClearTargetProcessBtn.Click += (_, __) =>
            {
                if (_suppressUiEvents) return;
                TargetProcessTextBox.Text = string.Empty;
                SaveTargetProcessFromUi(cfg);
            };
            // FPS Overlay
            EnableFpsCheckbox.Checked += (_,__) =>
            {
                if (_suppressUiEvents) return;
                cfg.EnableFpsOverlay = true;
                UpdateGraphRefreshUiState();
                cfg.SaveSettings();
            };
            EnableFpsCheckbox.Unchecked += (_,__) =>
            {
                if (_suppressUiEvents) return;
                cfg.EnableFpsOverlay = false;
                UpdateGraphRefreshUiState();
                cfg.SaveSettings();
            };
            FpsXSlider.ValueChanged += (_,__) => { if (!_suppressUiEvents) { cfg.FpsOverlayX = (int)FpsXSlider.Value; FpsXValueText.Text = cfg.FpsOverlayX.ToString(); QueueSettingsSave(cfg); } };
            FpsYSlider.ValueChanged += (_,__) => { if (!_suppressUiEvents) { cfg.FpsOverlayY = (int)FpsYSlider.Value; FpsYValueText.Text = cfg.FpsOverlayY.ToString(); QueueSettingsSave(cfg); } };
            FpsScaleSlider.ValueChanged += (_,__) =>
            {
                if (_suppressUiEvents) return;
                cfg.FpsOverlayScale = CrosshairConfig.NormalizeFpsOverlayScale((int)FpsScaleSlider.Value);
                if ((int)FpsScaleSlider.Value != cfg.FpsOverlayScale)
                {
                    FpsScaleSlider.Value = cfg.FpsOverlayScale;
                }
                FpsScaleValueText.Text = $"{cfg.FpsOverlayScale}%";
                QueueSettingsSave(cfg);
            };
            FpsXSlider.AddHandler(Thumb.DragCompletedEvent, saveFlushDragCompleteHandler);
            FpsYSlider.AddHandler(Thumb.DragCompletedEvent, saveFlushDragCompleteHandler);
            FpsScaleSlider.AddHandler(Thumb.DragCompletedEvent, saveFlushDragCompleteHandler);
            ShowFrametimeCheckbox.Checked += (_,__) =>
            {
                if (_suppressUiEvents) return;
                cfg.ShowFrametimeGraph = true;
                UpdateGraphRefreshUiState();
                cfg.SaveSettings();
            };
            ShowFrametimeCheckbox.Unchecked += (_,__) =>
            {
                if (_suppressUiEvents) return;
                cfg.ShowFrametimeGraph = false;
                UpdateGraphRefreshUiState();
                cfg.SaveSettings();
            };
            GraphRefreshRateCombo.SelectionChanged += (_, __) =>
            {
                if (_suppressUiEvents || GraphRefreshRateCombo.SelectedItem is not ComboBoxItem item) return;
                if (item.Tag is not string tagValue || !int.TryParse(tagValue, out int parsedMs)) return;
                cfg.GraphRefreshRateMs = parsedMs;
                cfg.SaveSettings();
            };
            GraphTimeWindowCombo.SelectionChanged += (_, __) =>
            {
                if (_suppressUiEvents || GraphTimeWindowCombo.SelectedItem is not ComboBoxItem item) return;
                if (item.Tag is not string tagValue || !int.TryParse(tagValue, out int parsedMs)) return;
                cfg.GraphTimeWindowMs = parsedMs;
                cfg.SaveSettings();
            };
            Show1PercentCheckbox.Checked += (_,__) => { if (!_suppressUiEvents) { cfg.Show1PercentLows = true; cfg.SaveSettings(); } };
            Show1PercentCheckbox.Unchecked += (_,__) => { if (!_suppressUiEvents) { cfg.Show1PercentLows = false; cfg.SaveSettings(); } };
            ShowGenFramesCheckbox.Checked += (_,__) => { if (!_suppressUiEvents) { cfg.ShowGenFrames = true; cfg.SaveSettings(); } };
            ShowGenFramesCheckbox.Unchecked += (_,__) => { if (!_suppressUiEvents) { cfg.ShowGenFrames = false; cfg.SaveSettings(); } };
            FpsTextColorBtn.Click += (_, __) =>
            {
                if (_suppressUiEvents) return;
                PickColor(c =>
                {
                    cfg.FpsOverlayColorSerialized = SerializeRgb(c.R, c.G, c.B);
                    cfg.SaveSettings();
                });
            };
            FpsBgColorBtn.Click += (_, __) =>
            {
                if (_suppressUiEvents) return;
                PickColor(c =>
                {
                    byte alpha = ParseOverlayAlpha(cfg.FpsOverlayBgColorSerialized, 128);
                    cfg.FpsOverlayBgColorSerialized = SerializeRgba(c.R, c.G, c.B, alpha);
                    cfg.SaveSettings();
                });
            };
        }

        private static string SerializeRgb(byte r, byte g, byte b) => $"{r},{g},{b}";

        private static string SerializeRgba(byte r, byte g, byte b, byte a) => $"{r},{g},{b},{a}";

        private static byte ParseOverlayAlpha(string? value, byte fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            var parts = value.Split(',');
            if (parts.Length == 4 && byte.TryParse(parts[3], out byte alpha)) return alpha;
            return fallback;
        }

        private void SelectGraphRefreshPreset(int refreshMs)
        {
            if (GraphRefreshRateCombo == null)
            {
                return;
            }

            int target = CrosshairConfig.NormalizeGraphRefreshRatePreset(refreshMs);
            foreach (var item in GraphRefreshRateCombo.Items.OfType<ComboBoxItem>())
            {
                if (item.Tag is string tag && int.TryParse(tag, out int value) && value == target)
                {
                    GraphRefreshRateCombo.SelectedItem = item;
                    return;
                }
            }
        }

        private void UpdateGraphRefreshUiState()
        {
            if (GraphRefreshRateCombo == null || GraphTimeWindowCombo == null)
            {
                return;
            }

            bool fpsOverlayEnabled = EnableFpsCheckbox?.IsChecked == true;
            bool showGraph = ShowFrametimeCheckbox?.IsChecked == true;
            bool controlsEnabled = fpsOverlayEnabled && showGraph;

            GraphRefreshRateCombo.IsEnabled = controlsEnabled;
            GraphRefreshRateCombo.Opacity = controlsEnabled ? 1.0 : 0.55;
            GraphTimeWindowCombo.IsEnabled = controlsEnabled;
            GraphTimeWindowCombo.Opacity = controlsEnabled ? 1.0 : 0.55;
        }

        private void SelectGraphTimeWindowPreset(int timeWindowMs)
        {
            if (GraphTimeWindowCombo == null)
            {
                return;
            }

            int target = CrosshairConfig.NormalizeGraphTimeWindowPreset(timeWindowMs);
            foreach (var item in GraphTimeWindowCombo.Items.OfType<ComboBoxItem>())
            {
                if (item.Tag is string tag && int.TryParse(tag, out int value) && value == target)
                {
                    GraphTimeWindowCombo.SelectedItem = item;
                    return;
                }
            }
        }

    }
}
