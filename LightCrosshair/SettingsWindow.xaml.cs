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
            ShapeCombo.SelectionChanged += (_, __) => ApplyChange(p =>
            {
                var item = (ShapeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Cross";
                p.Shape = item;
                p.EnumShape = MapShape(item);
            });
            CompositeCombo.SelectionChanged += (_, __) => ApplyChange(p =>
            {
                var item = (CompositeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
                if (!string.IsNullOrEmpty(item))
                {
                    p.Shape = item;
                    p.EnumShape = MapShape(item);
                }
            });

            SizeSlider.ValueChanged += (_, __) => { SizeValue.Text = ((int)SizeSlider.Value).ToString(); ApplyChange(p => p.Size = (int)SizeSlider.Value); };
            ThicknessSlider.ValueChanged += (_, __) => { ThicknessValue.Text = ((int)ThicknessSlider.Value).ToString(); ApplyChange(p => p.Thickness = (int)ThicknessSlider.Value); };
            GapSlider.ValueChanged += (_, __) => { GapValue.Text = ((int)GapSlider.Value).ToString(); ApplyChange(p => p.GapSize = (int)GapSlider.Value); };

            InnerSizeSlider.ValueChanged += (_, __) => { InnerSizeValue.Text = ((int)InnerSizeSlider.Value).ToString(); ApplyChange(p => p.InnerSize = (int)InnerSizeSlider.Value); };
            InnerThicknessSlider.ValueChanged += (_, __) => { InnerThicknessValue.Text = ((int)InnerThicknessSlider.Value).ToString(); ApplyChange(p => p.InnerThickness = (int)InnerThicknessSlider.Value); };
            InnerGapSlider.ValueChanged += (_, __) => { InnerGapValue.Text = ((int)InnerGapSlider.Value).ToString(); ApplyChange(p => p.InnerGapSize = (int)InnerGapSlider.Value); };

            EdgeColorBtn.Click += (_, __) => PickColor(c => ApplyChange(p => p.EdgeColor = System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B)));
            InnerColorBtn.Click += (_, __) => PickColor(c => ApplyChange(p => p.InnerColor = System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B)));
            InnerShapeEdgeColorBtn.Click += (_, __) => PickColor(c => ApplyChange(p => p.InnerShapeEdgeColor = System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B)));

            ThemeToggle.Click += (_, __) => { _prefs.Theme = _prefs.Theme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark; ApplyTheme(_prefs.Theme); UpdateThemeButtonIcon(); SavePrefs(); };

            // Reflect current profile
            LoadFromProfile(_profiles.Current);
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
            // Shape
            foreach (var item in ShapeCombo.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Content?.ToString(), p.Shape, StringComparison.OrdinalIgnoreCase))
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

            InnerSizeSlider.Value = p.InnerSize;
            InnerSizeValue.Text = p.InnerSize.ToString();
            InnerThicknessSlider.Value = p.InnerThickness;
            InnerThicknessValue.Text = p.InnerThickness.ToString();
            InnerGapSlider.Value = p.InnerGapSize;
            InnerGapValue.Text = p.InnerGapSize.ToString();
        }

        private void ApplyChange(Action<CrosshairProfile> change)
        {
            var cur = _profiles.Current.Clone();
            change(cur);
            _profiles.Update(cur);
        }

        private void ApplyTheme(AppTheme theme)
        {
            // Use clean ResourceDictionary replacement to avoid mixed states after repeated toggles
            Resources.MergedDictionaries.Clear();
            var dict = new ResourceDictionary();
            if (theme == AppTheme.Dark)
            {
                dict["WindowBg"] = new SolidColorBrush(ColorFromRgb(32, 32, 36)); // #202024
                dict["TextFg"] = new SolidColorBrush(Colors.WhiteSmoke);
                dict["PanelBg"] = new SolidColorBrush(ColorFromRgb(40, 40, 48)); // #282830
                dict["AccentBrush"] = new SolidColorBrush(Colors.Orange);
            }
            else
            {
                dict["WindowBg"] = new SolidColorBrush(Colors.White);
                dict["TextFg"] = new SolidColorBrush(Colors.Black);
                dict["PanelBg"] = new SolidColorBrush(Colors.WhiteSmoke);
                dict["AccentBrush"] = new SolidColorBrush(Colors.Orange);
            }
            Resources.MergedDictionaries.Add(dict);

            Background = (System.Windows.Media.Brush)dict["WindowBg"];
            RootPanel.Background = (System.Windows.Media.Brush)dict["PanelBg"];
            Foreground = (System.Windows.Media.Brush)dict["TextFg"];
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

