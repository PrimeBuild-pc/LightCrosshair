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
using LightCrosshair.FrameLimiting;
using LightCrosshair.GpuDriver;
using GpuDetResult = LightCrosshair.GpuDriver.GpuDetectionResult;

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
        private IGpuDriverService? _gpuDriverService;
        private GpuDetResult _gpuDetectionResult;
        private bool _hasLoadedNvidiaProfileAudit;
        private string _lastNvidiaProfileAuditTarget = string.Empty;

        public SettingsWindow(IProfileService profiles)
        {
            _profiles = profiles;
            _prefs = PreferencesStore.Load();
            _profilesCurrentChangedHandler = (_, p) => Dispatcher.Invoke(() =>
            {
                LoadFromProfile(p);
                SyncDisplaySettingsFromConfig();
                UpdatePositionStatus();
            });

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
            if (VisibilityPresetCombo != null)
            {
                VisibilityPresetCombo.SelectedIndex = 0;
            }
            if (ApplyVisibilityPresetBtn != null)
            {
                ApplyVisibilityPresetBtn.Click += (_, __) => ApplySelectedVisibilityPreset();
            }

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

            // GPU Driver service initialization
            InitializeGpuDriverService();

            // GPU Driver event wiring
            GpuRefreshButton.Click += (_, __) => GpuRefreshButton_Click();
            SettingsTabControl.SelectionChanged += (_, e) =>
            {
                if (!ReferenceEquals(e.OriginalSource, SettingsTabControl))
                {
                    return;
                }

                if (GpuDriverTab.IsSelected && ShouldRefreshNvidiaProfileAudit())
                {
                    RefreshNvidiaProfileAudit();
                }
            };
            NvidiaProfileRefreshButton.Click += (_, __) => RefreshNvidiaProfileAudit();
            NvidiaLowLatencyApplyButton.Click += (_, __) => ApplyNvidiaProfileSettingFromUi(
                NvidiaProfileSettingCatalog.LowLatencyModeSettingId,
                NvidiaLowLatencyComboBox,
                NvidiaProfileLowLatencyStatusText);
            NvidiaLowLatencyRestoreButton.Click += (_, __) => RestoreNvidiaProfileSettingFromUi(
                NvidiaProfileSettingCatalog.LowLatencyModeSettingId,
                NvidiaProfileLowLatencyStatusText);
            NvidiaVSyncApplyButton.Click += (_, __) => ApplyNvidiaProfileSettingFromUi(
                NvidiaProfileSettingCatalog.VerticalSyncSettingId,
                NvidiaVSyncComboBox,
                NvidiaProfileVSyncStatusText);
            NvidiaVSyncRestoreButton.Click += (_, __) => RestoreNvidiaProfileSettingFromUi(
                NvidiaProfileSettingCatalog.VerticalSyncSettingId,
                NvidiaProfileVSyncStatusText);
            NvidiaLowLatencyComboBox.SelectedIndex = 0;
            NvidiaVSyncComboBox.SelectedIndex = 0;
            NvidiaFpsCapSlider.ValueChanged += (_, __) =>
            {
                if (NvidiaFpsCapValueText != null)
                    NvidiaFpsCapValueText.Text = ((int)NvidiaFpsCapSlider.Value).ToString();
            };
            NvidiaFpsCapApplyButton.Click += (_, __) => NvidiaFpsCapApplyButton_Click();
            NvidiaFpsCapClearButton.Click += (_, __) => NvidiaFpsCapClearButton_Click();
            NvidiaVibranceSlider.ValueChanged += (_, __) =>
            {
                if (NvidiaVibranceValueText != null)
                    NvidiaVibranceValueText.Text = ((int)NvidiaVibranceSlider.Value).ToString();
            };
            NvidiaVibranceApplyButton.Click += (_, __) => NvidiaVibranceApplyButton_Click();
            NvidiaVibranceResetButton.Click += (_, __) => NvidiaVibranceResetButton_Click();
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
            bool immutableDefault = p.IsImmutableDefault;
            var item = new System.Windows.Controls.ListBoxItem { Tag = p.Id };
            var sp = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(4) };
            sp.Children.Add(new System.Windows.Controls.TextBlock { Text = $"Slot {index + 1}:", Width = 64, Foreground = GetThemeBrush("TextFg", System.Windows.Media.Brushes.Black) });
            var nameBox = new System.Windows.Controls.TextBox { Text = p.Name, Width = 190, IsReadOnly = true, Margin = new Thickness(0, 0, 4, 0) };
            sp.Children.Add(nameBox);
            var loadBtn = new System.Windows.Controls.Button { Content = "Load", Width = 70, Margin = new Thickness(6, 0, 0, 0) };
            loadBtn.Click += (_, __) => { _profiles.Switch(p.Id); };
            var saveBtn = new System.Windows.Controls.Button { Content = "Save", Width = 70, Margin = new Thickness(6, 0, 0, 0), IsEnabled = !immutableDefault };
            saveBtn.Click += (_, __) =>
            {
                if (immutableDefault)
                {
                    ShowAppDialog("Profiles", "The Default profile is immutable and cannot be overwritten.", MessageBoxImage.Information);
                    return;
                }

                var cur = StampDisplaySettingsIntoProfile(_profiles.Current.Clone());
                cur.Id = p.Id;
                cur.Name = p.Name;
                _profiles.Update(cur);
            };
            var renameBtn = new System.Windows.Controls.Button { Content = "Rename", Width = 80, Margin = new Thickness(6, 0, 0, 0) };
            renameBtn.Click += (_, __) =>
            {
                if (immutableDefault)
                {
                    ShowAppDialog("Profiles", "The Default profile is immutable and cannot be renamed.", MessageBoxImage.Information);
                    return;
                }

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
            var deleteBtn = new System.Windows.Controls.Button { Content = "Delete", Width = 80, Margin = new Thickness(6, 0, 0, 0), IsEnabled = !immutableDefault };
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
                    if (existing.IsImmutableDefault)
                    {
                        ShowAppDialog("Profiles", "The Default profile is immutable and cannot be overwritten.", MessageBoxImage.Information);
                        return;
                    }

                    var cur = StampDisplaySettingsIntoProfile(_profiles.Current.Clone());
                    cur.Id = existing.Id;
                    cur.Name = existing.Name;
                    _profiles.Update(cur);
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
                var source = StampDisplaySettingsIntoProfile(_profiles.Current.Clone());
                var created = _profiles.AddClone(source, name);
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
                var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                var userAgent = $"LightCrosshair/{version?.Major}.{version?.Minor}.{version?.Build}";
                Http.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
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
            SelectFpsOverlayMode(cfg.FpsOverlayMode);
            UltraLightweightCheckbox.IsChecked = cfg.UltraLightweightMode;
            FpsXSlider.Value = cfg.FpsOverlayX;
            FpsXValueText.Text = cfg.FpsOverlayX.ToString();
            FpsYSlider.Value = cfg.FpsOverlayY;
            FpsYValueText.Text = cfg.FpsOverlayY.ToString();
            FpsScaleSlider.Value = cfg.FpsOverlayScale;
            FpsScaleValueText.Text = $"{cfg.FpsOverlayScale}%";
            ShowFrametimeCheckbox.IsChecked = cfg.ShowFrametimeGraph;
            SelectGraphRefreshPreset(cfg.GraphRefreshRateMs);
            SelectGraphTimeWindowPreset(cfg.GraphTimeWindowMs);
            ShowFpsMetricCheckbox.IsChecked = cfg.ShowFps;
            ShowFrameTimeMetricCheckbox.IsChecked = cfg.ShowFrameTime;
            Show1PercentCheckbox.IsChecked = cfg.Show1PercentLows;
            ShowFpsDiagnosticsCheckbox.IsChecked = cfg.ShowFpsDiagnostics;
            ShowGenFramesCheckbox.IsChecked = cfg.ShowGenFrames;
            UpdateGraphRefreshUiState();

            FrameCapRefreshRateSlider.Value = cfg.FrameCapAssistantRefreshRateHz;
            FrameCapRefreshRateText.Text = $"{cfg.FrameCapAssistantRefreshRateHz:0} Hz";
            FrameCapTargetFpsSlider.Value = cfg.FrameCapAssistantTargetFps;
            FrameCapTargetFpsText.Text = cfg.FrameCapAssistantTargetFps.ToString();
            RefreshFrameCapAssistantStatus(cfg);
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

        internal void SyncDisplaySettingsFromConfig()
        {
            if (!Dispatcher.CheckAccess())
            {
                _ = Dispatcher.BeginInvoke(new Action(SyncDisplaySettingsFromConfig));
                return;
            }

            var cfg = CrosshairConfig.Instance;
            _suppressUiEvents = true;
            try
            {
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
            }
            finally
            {
                _suppressUiEvents = false;
            }

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
            SyncDisplaySettingsToCurrentProfile(cfg);
            cfg.SaveSettings();
            MarkNvidiaProfileAuditStale();

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
                SyncDisplaySettingsToCurrentProfile(cfg);
                cfg.SaveSettings();
                ApplyDisplaySettingsImmediate(cfg);
            };
            EnableGammaCheckbox.Unchecked += (_,__) =>
            {
                if (_suppressUiEvents) return;
                cfg.EnableGammaOverride = false;
                SyncDisplaySettingsToCurrentProfile(cfg);
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
                SyncDisplaySettingsToCurrentProfile(cfg);
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
            ResetGammaBtn.Click += (_, __) => { GammaSlider.Value = 100; SyncDisplaySettingsToCurrentProfile(cfg); ApplyDisplaySettingsImmediate(cfg); };
            ResetContrastBtn.Click += (_, __) => { ContrastSlider.Value = 100; SyncDisplaySettingsToCurrentProfile(cfg); ApplyDisplaySettingsImmediate(cfg); };
            ResetBrightnessBtn.Click += (_, __) => { BrightnessSlider.Value = 100; SyncDisplaySettingsToCurrentProfile(cfg); ApplyDisplaySettingsImmediate(cfg); };
            ResetVibranceBtn.Click += (_, __) => { VibranceSlider.Value = 50; SyncDisplaySettingsToCurrentProfile(cfg); ApplyDisplaySettingsImmediate(cfg); };

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
                if (cfg.FpsOverlayMode == FpsOverlayDisplayMode.Off)
                {
                    cfg.FpsOverlayMode = FpsOverlayDisplayMode.Minimal;
                    SelectFpsOverlayMode(cfg.FpsOverlayMode);
                }
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
            FpsOverlayModeCombo.SelectionChanged += (_, __) =>
            {
                if (_suppressUiEvents || FpsOverlayModeCombo.SelectedItem is not ComboBoxItem item) return;
                if (item.Tag is not string tagValue) return;
                if (!Enum.TryParse(tagValue, ignoreCase: true, out FpsOverlayDisplayMode mode)) return;
                cfg.FpsOverlayMode = mode;
                cfg.EnableFpsOverlay = mode != FpsOverlayDisplayMode.Off && EnableFpsCheckbox.IsChecked == true;
                if (mode == FpsOverlayDisplayMode.Off && EnableFpsCheckbox.IsChecked == true)
                {
                    _suppressUiEvents = true;
                    EnableFpsCheckbox.IsChecked = false;
                    _suppressUiEvents = false;
                }
                UpdateGraphRefreshUiState();
                cfg.SaveSettings();
            };
            UltraLightweightCheckbox.Checked += (_,__) =>
            {
                if (_suppressUiEvents) return;
                cfg.UltraLightweightMode = true;
                UpdateGraphRefreshUiState();
                cfg.SaveSettings();
            };
            UltraLightweightCheckbox.Unchecked += (_,__) =>
            {
                if (_suppressUiEvents) return;
                cfg.UltraLightweightMode = false;
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
            ShowFpsMetricCheckbox.Checked += (_,__) => { if (!_suppressUiEvents) { cfg.ShowFps = true; cfg.SaveSettings(); } };
            ShowFpsMetricCheckbox.Unchecked += (_,__) => { if (!_suppressUiEvents) { cfg.ShowFps = false; cfg.SaveSettings(); } };
            ShowFrameTimeMetricCheckbox.Checked += (_,__) => { if (!_suppressUiEvents) { cfg.ShowFrameTime = true; cfg.SaveSettings(); } };
            ShowFrameTimeMetricCheckbox.Unchecked += (_,__) => { if (!_suppressUiEvents) { cfg.ShowFrameTime = false; cfg.SaveSettings(); } };
            Show1PercentCheckbox.Checked += (_,__) => { if (!_suppressUiEvents) { cfg.Show1PercentLows = true; cfg.SaveSettings(); } };
            Show1PercentCheckbox.Unchecked += (_,__) => { if (!_suppressUiEvents) { cfg.Show1PercentLows = false; cfg.SaveSettings(); } };
            ShowFpsDiagnosticsCheckbox.Checked += (_,__) => { if (!_suppressUiEvents) { cfg.ShowFpsDiagnostics = true; cfg.ShowFramePacing = true; cfg.SaveSettings(); } };
            ShowFpsDiagnosticsCheckbox.Unchecked += (_,__) => { if (!_suppressUiEvents) { cfg.ShowFpsDiagnostics = false; cfg.ShowFramePacing = false; cfg.SaveSettings(); } };
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
                    cfg.FpsOverlayBgColorSerialized = SerializeRgb(c.R, c.G, c.B);
                    cfg.SaveSettings();
                });
            };
            FrameCapRefreshRateSlider.ValueChanged += (_, __) =>
            {
                if (_suppressUiEvents) return;
                cfg.FrameCapAssistantRefreshRateHz = CrosshairConfig.NormalizeFrameCapAssistantRefreshRate(FrameCapRefreshRateSlider.Value);
                FrameCapRefreshRateText.Text = $"{cfg.FrameCapAssistantRefreshRateHz:0} Hz";
                QueueSettingsSave(cfg);
                RefreshFrameCapAssistantStatus(cfg);
            };
            FrameCapTargetFpsSlider.ValueChanged += (_, __) =>
            {
                if (_suppressUiEvents) return;
                cfg.FrameCapAssistantTargetFps = CrosshairConfig.NormalizeFrameCapAssistantTargetFps((int)FrameCapTargetFpsSlider.Value);
                FrameCapTargetFpsText.Text = cfg.FrameCapAssistantTargetFps.ToString();
                QueueSettingsSave(cfg);
                RefreshFrameCapAssistantStatus(cfg);
            };
            FrameCapRefreshRateSlider.AddHandler(Thumb.DragCompletedEvent, saveFlushDragCompleteHandler);
            FrameCapTargetFpsSlider.AddHandler(Thumb.DragCompletedEvent, saveFlushDragCompleteHandler);
            FrameCapUseRecommendationBtn.Click += (_, __) =>
            {
                if (_suppressUiEvents) return;
                cfg.FrameCapAssistantTargetFps = FrameCapAssistant.RecommendTargetFps(cfg.FrameCapAssistantRefreshRateHz);
                FrameCapTargetFpsSlider.Value = cfg.FrameCapAssistantTargetFps;
                FrameCapTargetFpsText.Text = cfg.FrameCapAssistantTargetFps.ToString();
                cfg.SaveSettings();
                RefreshFrameCapAssistantStatus(cfg);
            };
        }

        private static string SerializeRgb(byte r, byte g, byte b) => $"{r},{g},{b}";

        private static string SerializeRgba(byte r, byte g, byte b, byte a) => $"{r},{g},{b},{a}";

        private CrosshairProfile StampDisplaySettingsIntoProfile(CrosshairProfile profile)
        {
            var cfg = CrosshairConfig.Instance;
            profile.HasDisplayColorProfile = true;
            profile.DisplayEnableGammaOverride = cfg.EnableGammaOverride;
            profile.DisplayGammaValue = cfg.GammaValue;
            profile.DisplayContrastValue = cfg.ContrastValue;
            profile.DisplayBrightnessValue = cfg.BrightnessValue;
            profile.DisplayVibranceValue = cfg.VibranceValue;
            profile.DisplayTargetProcessName = NormalizeTargetProcessInput(cfg.TargetProcessName);
            return profile;
        }

        private void SyncDisplaySettingsToCurrentProfile(CrosshairConfig cfg)
        {
            if (_suppressUiEvents)
            {
                return;
            }

            var updated = StampDisplaySettingsIntoProfile(_profiles.Current.Clone());
            if (!updated.ContentEquals(_profiles.Current))
            {
                _profiles.Update(updated);
            }
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

        private void SelectFpsOverlayMode(FpsOverlayDisplayMode mode)
        {
            if (FpsOverlayModeCombo == null)
            {
                return;
            }

            string target = Enum.IsDefined(typeof(FpsOverlayDisplayMode), mode)
                ? mode.ToString()
                : FpsOverlayDisplayMode.Minimal.ToString();

            foreach (var item in FpsOverlayModeCombo.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Tag as string, target, StringComparison.OrdinalIgnoreCase))
                {
                    FpsOverlayModeCombo.SelectedItem = item;
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

            var cfg = CrosshairConfig.Instance;
            bool fpsOverlayEnabled = EnableFpsCheckbox?.IsChecked == true;
            bool showGraph = ShowFrametimeCheckbox?.IsChecked == true;
            bool isDetailed = cfg.FpsOverlayMode == FpsOverlayDisplayMode.Detailed;
            bool isUltra = UltraLightweightCheckbox?.IsChecked == true;
            bool detailedControlsEnabled = fpsOverlayEnabled && isDetailed && !isUltra;
            bool controlsEnabled = detailedControlsEnabled && showGraph;

            GraphRefreshRateCombo.IsEnabled = controlsEnabled;
            GraphRefreshRateCombo.Opacity = controlsEnabled ? 1.0 : 0.55;
            GraphTimeWindowCombo.IsEnabled = controlsEnabled;
            GraphTimeWindowCombo.Opacity = controlsEnabled ? 1.0 : 0.55;
            if (ShowFrametimeCheckbox != null) ShowFrametimeCheckbox.IsEnabled = detailedControlsEnabled;
            if (Show1PercentCheckbox != null) Show1PercentCheckbox.IsEnabled = detailedControlsEnabled;
            if (ShowFpsDiagnosticsCheckbox != null) ShowFpsDiagnosticsCheckbox.IsEnabled = detailedControlsEnabled;
            if (ShowGenFramesCheckbox != null) ShowGenFramesCheckbox.IsEnabled = detailedControlsEnabled;
        }

        private void ApplySelectedVisibilityPreset()
        {
            if (_suppressUiEvents || VisibilityPresetCombo?.SelectedItem is not ComboBoxItem item)
            {
                return;
            }

            if (item.Tag is not string tag || !Enum.TryParse(tag, ignoreCase: true, out CrosshairVisibilityPresetKind kind))
            {
                return;
            }

            ApplyChange(p => CrosshairVisibilityPreset.Apply(p, kind));
            LoadFromProfile(_profiles.Current);
        }

        private void RefreshFrameCapAssistantStatus(CrosshairConfig cfg)
        {
            if (FrameCapAssistantStatusText == null)
            {
                return;
            }

            var capability = FrameLimiterCapability.Unavailable(
                FrameLimiterBackendKind.None,
                "No limiter backend",
                "No active limiter backend; assistant only.");
            var status = FrameCapAssistant.BuildStatus(
                cfg.FrameCapAssistantRefreshRateHz,
                cfg.FrameCapAssistantTargetFps,
                capability);

            FrameCapAssistantStatusText.Text =
                $"{status.StatusText} Suggested target: {status.TargetFps} FPS. {status.HelpText}";
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

        // ----- GPU Driver Integration -----

        private void InitializeGpuDriverService()
        {
            try
            {
                _gpuDriverService = GpuDriverServiceFactory.Create();
                _gpuDetectionResult = _gpuDriverService.Detect();
                RefreshGpuDriverUI();
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "SettingsWindow.InitializeGpuDriverService");
                _gpuDetectionResult = GpuDetResult.Unknown();
                _gpuDriverService = null;
                GpuVendorText.Text = "Detection failed";
                GpuDriverApiText.Text = "Error";
                GpuDriverStatusText.Text = $"Initialization error: {ex.Message}";
            }
        }

        private void RefreshGpuDriverUI()
        {
            try
            {
                // Update detection info
                string vendorText = _gpuDetectionResult.Vendor switch
                {
                    GpuVendorKind.Nvidia => "NVIDIA",
                    GpuVendorKind.Amd => "AMD",
                    GpuVendorKind.Intel => "Intel",
                    _ => "Unknown"
                };

                if (!string.IsNullOrEmpty(_gpuDetectionResult.AdapterDescription))
                {
                    GpuVendorText.Text = $"{vendorText} {_gpuDetectionResult.AdapterDescription}";
                }
                else
                {
                    GpuVendorText.Text = vendorText;
                }

                GpuDriverApiText.Text = _gpuDetectionResult.IsDriverApiAvailable ? "Available" : "Not available";
                GpuDriverStatusText.Text = _gpuDetectionResult.DriverApiStatusMessage;

                // Update display settings status rows (color management + sync)
                var caps = _gpuDetectionResult.Capabilities;
                UpdateDisplaySettingsStatus(caps);

                // Enable/disable NVIDIA FPS cap controls
                bool fpsCapSupported = caps.NvidiaFpsCap == GpuCapabilityStatus.Supported;
                NvidiaFpsCapGroup.IsEnabled = fpsCapSupported;
                NvidiaProfileControlsGroup.IsEnabled = fpsCapSupported;
                if (!fpsCapSupported)
                {
                    NvidiaFpsCapGroup.Opacity = 0.55;
                    NvidiaProfileControlsGroup.Opacity = 0.55;
                    NvidiaFpsCapGroup.ToolTip = caps.NvidiaFpsCap switch
                    {
                        GpuCapabilityStatus.Unavailable => "NVIDIA driver API not available. Ensure NVIDIA drivers are installed.",
                        GpuCapabilityStatus.ReadOnly => "NVIDIA FPS cap is read-only on this system.",
                        GpuCapabilityStatus.Unsupported => "NVIDIA FPS cap not supported on this hardware/driver.",
                        _ => "NVIDIA FPS cap not available."
                    };
                    NvidiaProfileControlsGroup.ToolTip = NvidiaFpsCapGroup.ToolTip;
                    NvidiaProfileStatusText.Text = CapabilityStatusText(caps.NvidiaFpsCap);
                }
                else
                {
                    NvidiaFpsCapGroup.Opacity = 1.0;
                    NvidiaFpsCapGroup.ToolTip = null;
                    NvidiaProfileControlsGroup.Opacity = 1.0;
                    NvidiaProfileControlsGroup.ToolTip = null;
                }

                // Enable/disable NVIDIA vibrance controls
                bool vibranceSupported = caps.NvidiaColorVibrance == GpuCapabilityStatus.Supported;
                NvidiaVibranceGroup.IsEnabled = vibranceSupported;
                if (!vibranceSupported)
                {
                    NvidiaVibranceGroup.Opacity = 0.55;
                    NvidiaVibranceGroup.ToolTip = caps.NvidiaColorVibrance switch
                    {
                        GpuCapabilityStatus.Unavailable => "NVIDIA driver API not available. Ensure NVIDIA drivers are installed.",
                        GpuCapabilityStatus.ReadOnly => "NVIDIA digital vibrance is read-only on this system.",
                        GpuCapabilityStatus.Unsupported => "NVIDIA digital vibrance not supported on this hardware/driver.",
                        _ => "NVIDIA digital vibrance not available."
                    };
                }
                else
                {
                    NvidiaVibranceGroup.Opacity = 1.0;
                    NvidiaVibranceGroup.ToolTip = null;
                }
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "SettingsWindow.RefreshGpuDriverUI");
            }
        }

        /// <summary>
        /// Updates the Display Settings tab status rows for color management and sync technologies.
        /// </summary>
        private void UpdateDisplaySettingsStatus(GpuCapabilities caps)
        {
            try
            {
                if (DispAmdColorManagement != null)
                    DispAmdColorManagement.Text = CapabilityStatusText(caps.AmdColorManagement);
                if (DispNvidiaColorVibrance != null)
                    DispNvidiaColorVibrance.Text = CapabilityStatusText(caps.NvidiaColorVibrance);
                if (DispNvidiaGSync != null)
                    DispNvidiaGSync.Text = CapabilityStatusText(caps.NvidiaGSync);
                if (DispAmdFreeSync != null)
                    DispAmdFreeSync.Text = CapabilityStatusText(caps.AmdFreeSync);
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "SettingsWindow.UpdateDisplaySettingsStatus");
            }
        }

        private static string CapabilityStatusText(GpuCapabilityStatus status)
        {
            return status switch
            {
                GpuCapabilityStatus.Supported => "✓ Supported",
                GpuCapabilityStatus.ReadOnly => "◎ Read-Only",
                GpuCapabilityStatus.Unavailable => "⚠ Unavailable",
                GpuCapabilityStatus.Unsupported => "✗ Not Supported",
                _ => "? Unknown"
            };
        }

        private void GpuRefreshButton_Click()
        {
            try
            {
                GpuRefreshButton.IsEnabled = false;
                GpuVendorText.Text = "Detecting...";
                GpuDriverApiText.Text = "-";
                GpuDriverStatusText.Text = "Refreshing...";
                InitializeGpuDriverService();
                _hasLoadedNvidiaProfileAudit = false;
                RefreshNvidiaProfileAudit();
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "SettingsWindow.GpuRefreshButton_Click");
            }
            finally
            {
                try { GpuRefreshButton.IsEnabled = true; } catch { }
            }
        }

        private void NvidiaFpsCapApplyButton_Click()
        {
            try
            {
                if (_gpuDriverService == null)
                {
                    NvidiaFpsCapStatusText.Text = "GPU driver service not initialized.";
                    return;
                }

                int targetFps = (int)NvidiaFpsCapSlider.Value;

                // Application-specific profile only (global profile fallback removed).
                // Validate that a target process is configured before proceeding.
                string targetProcess = NormalizeTargetProcessInput(TargetProcessTextBox.Text);
                if (string.IsNullOrWhiteSpace(targetProcess))
                {
                    NvidiaFpsCapStatusText.Text =
                        "Select a target application before applying an NVIDIA driver FPS cap. " +
                        "Go to Display Settings and set a Target Process.";
                    return;
                }

                if (_gpuDriverService.TrySetNvidiaFpsCap(targetFps, targetProcess, out string error))
                {
                    NvidiaFpsCapStatusText.Text = $"FPS cap of {targetFps} applied successfully to '{targetProcess}'.";
                }
                else
                {
                    NvidiaFpsCapStatusText.Text = $"Failed to apply FPS cap: {error}";
                }
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "SettingsWindow.NvidiaFpsCapApplyButton_Click");
                NvidiaFpsCapStatusText.Text = $"Error: {ex.Message}";
            }
        }

        private void NvidiaFpsCapClearButton_Click()
        {
            try
            {
                if (_gpuDriverService == null)
                {
                    NvidiaFpsCapStatusText.Text = "GPU driver service not initialized.";
                    return;
                }

                // Application-specific profile only (global profile fallback removed).
                // Validate that a target process is configured before proceeding.
                string targetProcess = NormalizeTargetProcessInput(TargetProcessTextBox.Text);
                if (string.IsNullOrWhiteSpace(targetProcess))
                {
                    NvidiaFpsCapStatusText.Text =
                        "Select a target application before clearing an NVIDIA driver FPS cap. " +
                        "Go to Display Settings and set a Target Process.";
                    return;
                }

                if (_gpuDriverService.TryClearNvidiaFpsCap(targetProcess, out string error))
                {
                    NvidiaFpsCapStatusText.Text = $"FPS cap cleared for '{targetProcess}'.";
                }
                else
                {
                    NvidiaFpsCapStatusText.Text = $"Failed to clear FPS cap: {error}";
                }
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "SettingsWindow.NvidiaFpsCapClearButton_Click");
                NvidiaFpsCapStatusText.Text = $"Error: {ex.Message}";
            }
        }

        private void RefreshNvidiaProfileAudit()
        {
            try
            {
                _hasLoadedNvidiaProfileAudit = true;

                if (_gpuDriverService == null)
                {
                    SetNvidiaProfileAuditUnavailable("GPU driver service not initialized.");
                    return;
                }

                string targetProcess = NormalizeTargetProcessInput(TargetProcessTextBox.Text);
                NvidiaProfileStatusText.Text = "Auditing selected application profile...";
                var result = _gpuDriverService.AuditNvidiaProfileSettings(targetProcess);
                _lastNvidiaProfileAuditTarget = targetProcess;
                NvidiaProfileStatusText.Text = FormatNvidiaProfileAuditStatus(result);
                NvidiaProfileNameText.Text = string.IsNullOrWhiteSpace(result.ProfileName) ? "-" : result.ProfileName;

                SetNvidiaProfileAuditRow(
                    result,
                    NvidiaProfileSettingCatalog.DriverFpsCapSettingId,
                    NvidiaProfileFpsCapValueText,
                    NvidiaProfileFpsCapStatusText);
                SetNvidiaProfileAuditRow(
                    result,
                    NvidiaProfileSettingCatalog.LowLatencyModeSettingId,
                    NvidiaProfileLowLatencyValueText,
                    NvidiaProfileLowLatencyStatusText);
                SyncWritableComboFromAudit(
                    NvidiaLowLatencyComboBox,
                    FindAuditRawValue(result, NvidiaProfileSettingCatalog.LowLatencyModeSettingId));
                SetNvidiaProfileAuditRow(
                    result,
                    NvidiaProfileSettingCatalog.LowLatencyCplStateSettingId,
                    NvidiaProfileLowLatencyCplValueText,
                    NvidiaProfileLowLatencyCplStatusText);
                SetNvidiaProfileAuditRow(
                    result,
                    NvidiaProfileSettingCatalog.VerticalSyncSettingId,
                    NvidiaProfileVSyncValueText,
                    NvidiaProfileVSyncStatusText);
                SyncWritableComboFromAudit(
                    NvidiaVSyncComboBox,
                    FindAuditRawValue(result, NvidiaProfileSettingCatalog.VerticalSyncSettingId));
                SetGSyncAuditRow(result);

                bool canApply = result.Status is NvidiaProfileAuditStatus.Present or NvidiaProfileAuditStatus.NoProfile;
                SetNvidiaProfileApplyControlsEnabled(canApply && !string.IsNullOrWhiteSpace(targetProcess));
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "SettingsWindow.RefreshNvidiaProfileAudit");
                SetNvidiaProfileAuditUnavailable($"Error: {ex.Message}");
            }
        }

        private void ApplyNvidiaProfileSettingFromUi(uint settingId, System.Windows.Controls.ComboBox comboBox, TextBlock statusText)
        {
            try
            {
                if (_gpuDriverService == null)
                {
                    statusText.Text = "GPU driver service not initialized.";
                    return;
                }

                string targetProcess = NormalizeTargetProcessInput(TargetProcessTextBox.Text);
                if (string.IsNullOrWhiteSpace(targetProcess))
                {
                    statusText.Text = "Select a target application before applying NVIDIA profile controls.";
                    return;
                }

                if (!TryGetSelectedComboRawValue(comboBox, out uint rawValue))
                {
                    statusText.Text = "Select a supported value first.";
                    return;
                }

                var result = _gpuDriverService.ApplyNvidiaProfileSetting(
                    targetProcess,
                    new NvidiaProfileSettingWriteRequest(settingId, rawValue),
                    _currentVersion);
                statusText.Text = result.StatusText;

                if (result.Succeeded)
                {
                    RefreshNvidiaProfileAudit();
                }
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "SettingsWindow.ApplyNvidiaProfileSettingFromUi");
                statusText.Text = $"Error: {ex.Message}";
            }
        }

        private void RestoreNvidiaProfileSettingFromUi(uint settingId, TextBlock statusText)
        {
            try
            {
                if (_gpuDriverService == null)
                {
                    statusText.Text = "GPU driver service not initialized.";
                    return;
                }

                string targetProcess = NormalizeTargetProcessInput(TargetProcessTextBox.Text);
                if (string.IsNullOrWhiteSpace(targetProcess))
                {
                    statusText.Text = "Select a target application before restoring NVIDIA profile controls.";
                    return;
                }

                var result = _gpuDriverService.RestoreNvidiaProfileSetting(targetProcess, settingId);
                statusText.Text = result.StatusText;

                if (result.Succeeded)
                {
                    RefreshNvidiaProfileAudit();
                }
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "SettingsWindow.RestoreNvidiaProfileSettingFromUi");
                statusText.Text = $"Error: {ex.Message}";
            }
        }

        private void MarkNvidiaProfileAuditStale()
        {
            _hasLoadedNvidiaProfileAudit = false;
            _lastNvidiaProfileAuditTarget = string.Empty;
            if (NvidiaProfileStatusText != null)
            {
                NvidiaProfileStatusText.Text = "Target changed. Refresh to audit the selected application profile.";
            }

            SetNvidiaProfileApplyControlsEnabled(!string.IsNullOrWhiteSpace(NormalizeTargetProcessInput(TargetProcessTextBox.Text)));
        }

        private bool ShouldRefreshNvidiaProfileAudit()
        {
            string targetProcess = NormalizeTargetProcessInput(TargetProcessTextBox.Text);
            return !_hasLoadedNvidiaProfileAudit ||
                   !string.Equals(targetProcess, _lastNvidiaProfileAuditTarget, StringComparison.OrdinalIgnoreCase);
        }

        private void SetNvidiaProfileAuditUnavailable(string message)
        {
            NvidiaProfileStatusText.Text = message;
            NvidiaProfileNameText.Text = "-";
            NvidiaProfileFpsCapValueText.Text = "-";
            NvidiaProfileLowLatencyValueText.Text = "-";
            NvidiaProfileLowLatencyCplValueText.Text = "-";
            NvidiaProfileVSyncValueText.Text = "-";
            NvidiaProfileGSyncValueText.Text = "-";
            NvidiaProfileFpsCapStatusText.Text = message;
            NvidiaProfileLowLatencyStatusText.Text = message;
            NvidiaProfileLowLatencyCplStatusText.Text = message;
            NvidiaProfileVSyncStatusText.Text = message;
            NvidiaProfileGSyncStatusText.Text = message;
            SetNvidiaProfileApplyControlsEnabled(false);
        }

        private static string FormatNvidiaProfileAuditStatus(NvidiaProfileAuditResult result)
        {
            string status = result.Status switch
            {
                NvidiaProfileAuditStatus.Present => "Present",
                NvidiaProfileAuditStatus.NoProfile => "No profile",
                NvidiaProfileAuditStatus.InvalidTarget => "Invalid target",
                NvidiaProfileAuditStatus.Unsupported => "Unsupported",
                NvidiaProfileAuditStatus.Error => "Error",
                _ => result.Status.ToString()
            };

            return $"{status}: {result.StatusText}";
        }

        private static void SetNvidiaProfileAuditRow(
            NvidiaProfileAuditResult result,
            uint settingId,
            TextBlock valueText,
            TextBlock statusText)
        {
            var item = result.Settings.FirstOrDefault(setting => setting.SettingId == settingId);
            if (item == null)
            {
                valueText.Text = "-";
                statusText.Text = "Setting is not part of the LightCrosshair catalog.";
                return;
            }

            valueText.Text = item.Status == NvidiaProfileAuditStatus.Present
                ? item.FriendlyValue
                : "Not present";
            statusText.Text = item.Definition.IsReferenceOnly
                ? $"Read-only reference. {item.StatusText}"
                : item.StatusText;
        }

        private static uint? FindAuditRawValue(NvidiaProfileAuditResult result, uint settingId) =>
            result.Settings.FirstOrDefault(setting => setting.SettingId == settingId)?.RawValue;

        private void SetGSyncAuditRow(NvidiaProfileAuditResult result)
        {
            var mode = result.Settings.FirstOrDefault(setting =>
                setting.SettingId == NvidiaProfileSettingCatalog.GSyncApplicationModeSettingId);
            var state = result.Settings.FirstOrDefault(setting =>
                setting.SettingId == NvidiaProfileSettingCatalog.GSyncApplicationStateSettingId);
            var requested = result.Settings.FirstOrDefault(setting =>
                setting.SettingId == NvidiaProfileSettingCatalog.GSyncApplicationRequestedStateSettingId);

            NvidiaProfileGSyncValueText.Text = string.Join(
                " / ",
                new[] { mode, state, requested }
                    .Where(item => item?.Status == NvidiaProfileAuditStatus.Present)
                    .Select(item => item!.FriendlyValue)
                    .DefaultIfEmpty("Not present"));
            NvidiaProfileGSyncStatusText.Text = "Read-only until write mapping is validated.";
        }

        private void SetNvidiaProfileApplyControlsEnabled(bool isEnabled)
        {
            NvidiaLowLatencyComboBox.IsEnabled = isEnabled;
            NvidiaLowLatencyApplyButton.IsEnabled = isEnabled;
            NvidiaLowLatencyRestoreButton.IsEnabled = isEnabled;
            NvidiaVSyncComboBox.IsEnabled = isEnabled;
            NvidiaVSyncApplyButton.IsEnabled = isEnabled;
            NvidiaVSyncRestoreButton.IsEnabled = isEnabled;
        }

        private static void SyncWritableComboFromAudit(System.Windows.Controls.ComboBox comboBox, uint? rawValue)
        {
            if (rawValue == null)
            {
                comboBox.SelectedIndex = -1;
                return;
            }

            foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
            {
                if (TryParseComboTagRawValue(item.Tag, out uint itemValue) && itemValue == rawValue.Value)
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }

            comboBox.SelectedIndex = -1;
        }

        private static bool TryGetSelectedComboRawValue(System.Windows.Controls.ComboBox comboBox, out uint rawValue)
        {
            rawValue = 0;
            return comboBox.SelectedItem is ComboBoxItem item &&
                   TryParseComboTagRawValue(item.Tag, out rawValue);
        }

        private static bool TryParseComboTagRawValue(object? tag, out uint rawValue)
        {
            rawValue = 0;
            string? value = tag?.ToString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? uint.TryParse(value[2..], System.Globalization.NumberStyles.HexNumber, null, out rawValue)
                : uint.TryParse(value, out rawValue);
        }

        private void NvidiaVibranceApplyButton_Click()
        {
            try
            {
                if (_gpuDriverService == null)
                {
                    NvidiaVibranceStatusText.Text = "GPU driver service not initialized.";
                    return;
                }

                int vibrance = (int)NvidiaVibranceSlider.Value;
                if (_gpuDriverService.TrySetNvidiaVibrance(vibrance, out string error))
                {
                    NvidiaVibranceStatusText.Text = $"Digital vibrance set to {vibrance}.";
                }
                else
                {
                    NvidiaVibranceStatusText.Text = $"Failed to set vibrance: {error}";
                }
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "SettingsWindow.NvidiaVibranceApplyButton_Click");
                NvidiaVibranceStatusText.Text = $"Error: {ex.Message}";
            }
        }

        private void NvidiaVibranceResetButton_Click()
        {
            try
            {
                if (_gpuDriverService == null)
                {
                    NvidiaVibranceStatusText.Text = "GPU driver service not initialized.";
                    return;
                }

                if (_gpuDriverService.TrySetNvidiaVibrance(50, out string error))
                {
                    NvidiaVibranceSlider.Value = 50;
                    NvidiaVibranceStatusText.Text = "Digital vibrance reset to default (50).";
                }
                else
                {
                    NvidiaVibranceStatusText.Text = $"Failed to reset vibrance: {error}";
                }
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "SettingsWindow.NvidiaVibranceResetButton_Click");
                NvidiaVibranceStatusText.Text = $"Error: {ex.Message}";
            }
        }

    }
}
