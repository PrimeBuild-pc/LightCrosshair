using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LightCrosshair.UI.Localization;
using LightCrosshair.UI.Services;
using LightCrosshair.UI.ViewModels;

namespace LightCrosshair.UI.Controls;

public class NumberSlider : StackPanel
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(NumberSlider),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(NumberSlider), new PropertyMetadata(0d, OnRangeChanged));
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(NumberSlider), new PropertyMetadata(100d, OnRangeChanged));
    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(nameof(Caption), typeof(string), typeof(NumberSlider), new PropertyMetadata("", OnCaptionChanged));
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(nameof(Unit), typeof(string), typeof(NumberSlider), new PropertyMetadata(""));
    public static readonly DependencyProperty IntegerProperty = DependencyProperty.Register(nameof(Integer), typeof(bool), typeof(NumberSlider), new PropertyMetadata(true));
    public static readonly DependencyProperty DragStartedCommandProperty = DependencyProperty.Register(nameof(DragStartedCommand), typeof(ICommand), typeof(NumberSlider));
    public static readonly DependencyProperty DragCompletedCommandProperty = DependencyProperty.Register(nameof(DragCompletedCommand), typeof(ICommand), typeof(NumberSlider));

    private readonly Slider _slider = new() { MinHeight = 32, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox _box = new() { Width = 72, MinHeight = 32 };
    private bool _internal;

    public NumberSlider()
    {
        Margin = new Thickness(0, 6, 0, 6);
        var caption = new TextBlock { Style = TryStyle("LabelText") };
        caption.SetBinding(TextBlock.TextProperty, new Binding(nameof(Caption)) { Source = this });
        var row = new Grid { MinHeight = 32 };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_slider, 0);
        Grid.SetColumn(_box, 1);
        var unit = new TextBlock { Style = TryStyle("LabelText"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), MinWidth = 28 };
        unit.SetBinding(TextBlock.TextProperty, new Binding(nameof(Unit)) { Source = this });
        Grid.SetColumn(unit, 2);
        row.Children.Add(_slider);
        row.Children.Add(_box);
        row.Children.Add(unit);
        Children.Add(caption);
        Children.Add(row);
        _slider.ValueChanged += (_, _) => { if (!_internal) Value = _slider.Value; };
        _slider.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler((_, _) => DragStartedCommand?.Execute(null)), true);
        _slider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler((_, _) => DragCompletedCommand?.Execute(null)), true);
        _box.GotKeyboardFocus += (_, _) => DragStartedCommand?.Execute(null);
        _box.LostFocus += (_, _) => { CommitText(); DragCompletedCommand?.Execute(null); };
        _slider.PreviewMouseLeftButtonDown += (_, _) => DragStartedCommand?.Execute(null);
        _slider.PreviewMouseLeftButtonUp += (_, _) => DragCompletedCommand?.Execute(null);
        _slider.GotKeyboardFocus += (_, _) => DragStartedCommand?.Execute(null);
        _slider.LostKeyboardFocus += (_, _) => DragCompletedCommand?.Execute(null);
        _box.PreviewKeyDown += OnBoxKey;
        Loaded += (_, _) => SyncFromValue();
    }

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public string Caption { get => (string)GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public bool Integer { get => (bool)GetValue(IntegerProperty); set => SetValue(IntegerProperty, value); }
    public ICommand? DragStartedCommand { get => (ICommand?)GetValue(DragStartedCommandProperty); set => SetValue(DragStartedCommandProperty, value); }
    public ICommand? DragCompletedCommand { get => (ICommand?)GetValue(DragCompletedCommandProperty); set => SetValue(DragCompletedCommandProperty, value); }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((NumberSlider)d).SyncFromValue();
    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((NumberSlider)d).SyncFromValue();
    private static void OnCaptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => System.Windows.Automation.AutomationProperties.SetName((NumberSlider)d, e.NewValue as string ?? "");

    private void SyncFromValue()
    {
        _internal = true;
        _slider.Minimum = Minimum;
        _slider.Maximum = Maximum;
        _slider.SmallChange = Integer ? 1 : 0.1;
        _slider.LargeChange = Integer ? 10 : 1;
        _slider.TickFrequency = _slider.SmallChange;
        _slider.IsSnapToTickEnabled = Integer;
        _slider.Value = Math.Clamp(Value, Minimum, Maximum);
        if (!_box.IsKeyboardFocused) _box.Text = Integer ? ((int)Math.Round(_slider.Value)).ToString(CultureInfo.CurrentCulture) : _slider.Value.ToString("0.##", CultureInfo.CurrentCulture);
        System.Windows.Automation.AutomationProperties.SetName(_slider, Caption);
        System.Windows.Automation.AutomationProperties.SetName(_box, Caption);
        _internal = false;
    }

    private void CommitText()
    {
        if (double.TryParse(_box.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsed))
            Value = Math.Clamp(Integer ? Math.Round(parsed) : parsed, Minimum, Maximum);
        SyncFromValue();
    }

    private void OnBoxKey(object sender, KeyEventArgs e)
    {
        var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10d : 1d;
        if (e.Key == Key.Up) { Value = Math.Clamp(Value + step, Minimum, Maximum); e.Handled = true; }
        else if (e.Key == Key.Down) { Value = Math.Clamp(Value - step, Minimum, Maximum); e.Handled = true; }
        else if (e.Key == Key.Enter) { CommitText(); e.Handled = true; }
    }

    private static Style? TryStyle(string key) => Application.Current?.TryFindResource(key) as Style;
}

public class ColorField : StackPanel
{
    public static readonly string[] Presets =
    [
        "#FFFFFF", "#000000", "#FF4D4D", "#3DDC97", "#4D8DFF", "#FFD24D",
        "#00E5FF", "#E85DFF", "#FF8A3D", "#9AA0A6", "#E8EAED", "#F26D6D",
    ];

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(ColorField),
        new FrameworkPropertyMetadata("#FFFFFF", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));
    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(nameof(Caption), typeof(string), typeof(ColorField), new PropertyMetadata(""));

    private readonly Border _swatch = new() { Width = 32, Height = 32, CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1) };
    private readonly TextBox _hex = new() { MinWidth = 96, MinHeight = 32, CharacterCasing = CharacterCasing.Upper };
    private bool _internal;

    public ColorField()
    {
        Margin = new Thickness(0, 6, 0, 6);
        var caption = new TextBlock { Style = Application.Current?.TryFindResource("LabelText") as Style };
        caption.SetBinding(TextBlock.TextProperty, new Binding(nameof(Caption)) { Source = this });
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        _swatch.BorderBrush = Application.Current?.TryFindResource("BorderStrongBrush") as Brush;
        _swatch.Margin = new Thickness(0, 0, 8, 0);
        row.Children.Add(_swatch);
        row.Children.Add(_hex);
        var presets = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        foreach (var preset in Presets)
        {
            var color = preset;
            var swatch = BrushOf(color);
            var button = new Button
            {
                Width = 32,
                Height = 32,
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(0),
                Background = swatch,
                BorderBrush = Application.Current?.TryFindResource("BorderStrongBrush") as Brush,
                BorderThickness = new Thickness(1),
                Content = new Border { Width = 22, Height = 22, Background = swatch, CornerRadius = new CornerRadius(4) },
            };
            System.Windows.Automation.AutomationProperties.SetName(button, color);
            button.Click += (_, _) => Value = color;
            presets.Children.Add(button);
        }
        _hex.LostFocus += (_, _) => Commit();
        _hex.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter) { Commit(); e.Handled = true; } };
        Children.Add(caption);
        Children.Add(row);
        Children.Add(presets);
        Sync();
    }

    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Caption { get => (string)GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ColorField)d).Sync();

    private void Sync()
    {
        if (_swatch == null) return;
        _internal = true;
        _swatch.Background = BrushOf(Value);
        if (!_hex.IsKeyboardFocused) _hex.Text = Value;
        System.Windows.Automation.AutomationProperties.SetName(this, Caption);
        System.Windows.Automation.AutomationProperties.SetName(_hex, Caption);
        _internal = false;
    }

    private void Commit()
    {
        if (_internal) return;
        var hex = StudioViewModel.NormalizeHex(_hex.Text);
        if (hex != null) Value = hex;
        else Sync();
    }

    private static SolidColorBrush BrushOf(string? hex)
    {
        try
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex ?? "#FFFFFF")!);
            brush.Freeze();
            return brush;
        }
        catch (FormatException) { return Brushes.White; }
    }
}

public class BindingCaptureBox : Border
{
    public static readonly DependencyProperty BindingProperty = DependencyProperty.Register(
        nameof(Binding), typeof(LightCrosshair.Contracts.InputBinding), typeof(BindingCaptureBox), new PropertyMetadata(null, OnBindingChanged));
    public static readonly DependencyProperty PromptProperty = DependencyProperty.Register(nameof(Prompt), typeof(string), typeof(BindingCaptureBox), new PropertyMetadata(""));
    public static readonly DependencyProperty CapturedCommandProperty = DependencyProperty.Register(nameof(CapturedCommand), typeof(ICommand), typeof(BindingCaptureBox));
    public static readonly DependencyProperty CaptureChangedCommandProperty = DependencyProperty.Register(nameof(CaptureChangedCommand), typeof(ICommand), typeof(BindingCaptureBox));
    public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(BindingCaptureBox));

    private readonly TextBlock _text = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) };
    private readonly DispatcherTimer _renew = new() { Interval = TimeSpan.FromSeconds(30) };
    private Window? _window;
    private bool _capturing;
    private bool _arming;

    public BindingCaptureBox()
    {
        MinHeight = 32;
        CornerRadius = new CornerRadius(6);
        BorderThickness = new Thickness(1);
        Background = Application.Current?.TryFindResource("SurfaceRaisedBrush") as Brush ?? Brushes.Transparent;
        BorderBrush = Application.Current?.TryFindResource("BorderStrongBrush") as Brush;
        Focusable = true;
        Cursor = Cursors.Hand;
        Child = _text;
        MouseLeftButtonDown += (_, e) => { Start(); e.Handled = true; };
        Loaded += (_, _) =>
        {
            Refresh();
            _window = Window.GetWindow(this);
            if (_window != null) _window.Deactivated += OnWindowDeactivated;
        };
        Unloaded += (_, _) =>
        {
            if (_window != null) _window.Deactivated -= OnWindowDeactivated;
            _window = null;
            Loc.Instance.Changed -= OnLocChanged;
            EndCapture();
        };
        _renew.Tick += (_, _) => NotifyCapture(true);
        LocChanged();
        Loc.Instance.Changed += OnLocChanged;
    }

    private void OnLocChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.CheckAccess()) Refresh();
        else Dispatcher.BeginInvoke(Refresh);
    }

    public LightCrosshair.Contracts.InputBinding? Binding { get => (LightCrosshair.Contracts.InputBinding?)GetValue(BindingProperty); set => SetValue(BindingProperty, value); }
    public string Prompt { get => (string)GetValue(PromptProperty); set => SetValue(PromptProperty, value); }
    public ICommand? CapturedCommand { get => (ICommand?)GetValue(CapturedCommandProperty); set => SetValue(CapturedCommandProperty, value); }
    public ICommand? CaptureChangedCommand { get => (ICommand?)GetValue(CaptureChangedCommandProperty); set => SetValue(CaptureChangedCommandProperty, value); }
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    private void LocChanged() { }

    private static void OnBindingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((BindingCaptureBox)d).Refresh();

    internal void StartCapture(bool openingClickHeld = false)
    {
        if (_capturing) return;
        _capturing = true;
        _arming = openingClickHeld || Mouse.LeftButton == MouseButtonState.Pressed;
        Focus();
        _text.Text = Prompt;
        BorderBrush = Application.Current?.TryFindResource("AccentBrush") as Brush;
        Mouse.Capture(this);
        NotifyCapture(true);
        _renew.Start();
    }

    internal void ReleaseOpeningClick() => _arming = false;

    internal string? MouseBinding(MouseButton button)
    {
        if (!_capturing || _arming) return null;
        return button switch
        {
            MouseButton.Middle => "MiddleClick",
            MouseButton.Right => "RightClick",
            MouseButton.XButton1 => "Mouse4",
            MouseButton.XButton2 => "Mouse5",
            _ => null,
        };
    }

    internal string? WheelBinding(int delta)
    {
        if (!_capturing || _arming || delta == 0) return null;
        return delta > 0 ? "WheelUp" : "WheelDown";
    }

    private void Start() => StartCapture();

    private void EndCapture()
    {
        if (!_capturing) return;
        _capturing = false;
        _renew.Stop();
        if (Mouse.Captured == this) Mouse.Capture(null);
        BorderBrush = Application.Current?.TryFindResource("BorderStrongBrush") as Brush;
        Refresh();
        NotifyCapture(false);
    }

    private void OnWindowDeactivated(object? sender, EventArgs e) => EndCapture();

    private void NotifyCapture(bool active)
    {
        if (CaptureChangedCommand?.CanExecute(active) == true)
            CaptureChangedCommand.Execute(active);
    }

    private void Refresh()
    {
        if (_capturing) return;
        _text.Text = HotkeyRules.Format(Binding);
        System.Windows.Automation.AutomationProperties.SetName(this, _text.Text);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (!_capturing)
        {
            if (e.Key is Key.Enter or Key.Space) { Start(); e.Handled = true; }
            return;
        }
        e.Handled = true;
        if (e.Key == Key.Escape || (e.Key == Key.System && e.SystemKey == Key.Escape))
        {
            EndCapture();
            return;
        }
        var name = KeyCodes.FromKey(e.Key == Key.System ? e.SystemKey : e.Key);
        if (name == null || name == "Escape") return;
        Finish(name, ReadModifiers());
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);
        if (!_capturing) return;
        e.Handled = true;
        if (e.ChangedButton == MouseButton.Left && _arming) return;
        var name = MouseBinding(e.ChangedButton);
        if (name == null) return;
        Finish(name, ReadModifiers());
    }

    protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseUp(e);
        if (!_capturing || e.ChangedButton != MouseButton.Left) return;
        _arming = false;
        e.Handled = true;
    }

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        base.OnPreviewMouseWheel(e);
        if (!_capturing) return;
        e.Handled = true;
        var name = WheelBinding(e.Delta);
        if (name == null) return;
        Finish(name, ReadModifiers());
    }

    private void Finish(string key, KeyModifiers modifiers)
    {
        var binding = new LightCrosshair.Contracts.InputBinding { Key = key, Modifiers = modifiers, Mode = Binding?.Mode ?? LightCrosshair.Contracts.BindingMode.Toggle };
        EndCapture();
        if (CapturedCommand?.CanExecute(CommandParameter) == true)
            CapturedCommand.Execute(new HotkeyCommit { Action = CommandParameter as string ?? "", Binding = binding });
    }

    private static KeyModifiers ReadModifiers()
    {
        var mods = KeyModifiers.None;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) mods |= KeyModifiers.Control;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) mods |= KeyModifiers.Alt;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) mods |= KeyModifiers.Shift;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) mods |= KeyModifiers.Win;
        return mods;
    }
}

public class OffsetDiagram : FrameworkElement
{
    public static readonly DependencyProperty OffsetXProperty = DependencyProperty.Register(nameof(OffsetX), typeof(int), typeof(OffsetDiagram), new PropertyMetadata(0, (d, _) => ((UIElement)d).InvalidateVisual()));
    public static readonly DependencyProperty OffsetYProperty = DependencyProperty.Register(nameof(OffsetY), typeof(int), typeof(OffsetDiagram), new PropertyMetadata(0, (d, _) => ((UIElement)d).InvalidateVisual()));

    public OffsetDiagram()
    {
        Width = 180;
        Height = 110;
        Margin = new Thickness(0, 8, 0, 8);
    }

    public int OffsetX { get => (int)GetValue(OffsetXProperty); set => SetValue(OffsetXProperty, value); }
    public int OffsetY { get => (int)GetValue(OffsetYProperty); set => SetValue(OffsetYProperty, value); }

    protected override void OnRender(DrawingContext context)
    {
        var screen = new Rect(0, 0, ActualWidth, ActualHeight);
        context.DrawRectangle(Brush(TryFind("SurfaceRaisedBrush")), new Pen(Brush(TryFind("BorderStrongBrush")), 1), screen);
        var cx = ActualWidth / 2 + Math.Clamp(OffsetX, -80, 80) * 0.4;
        var cy = ActualHeight / 2 + Math.Clamp(OffsetY, -80, 80) * 0.4;
        var accent = Brush(TryFind("AccentBrush"));
        context.DrawLine(new Pen(accent, 1), new Point(cx - 8, cy), new Point(cx + 8, cy));
        context.DrawLine(new Pen(accent, 1), new Point(cx, cy - 8), new Point(cx, cy + 8));
    }

    private static Brush Brush(object? resource) => resource as Brush ?? System.Windows.Media.Brushes.Gray;
    private object? TryFind(string key) => Application.Current?.TryFindResource(key);
}

public static class ThumbLoader
{
    public static readonly DependencyProperty CardProperty = DependencyProperty.RegisterAttached(
        "Card", typeof(LibraryCard), typeof(ThumbLoader), new PropertyMetadata(null, OnCardChanged));

    public static void SetCard(DependencyObject element, LibraryCard? value) => element.SetValue(CardProperty, value);
    public static LibraryCard? GetCard(DependencyObject element) => (LibraryCard?)element.GetValue(CardProperty);

    private static void OnCardChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not LibraryCard card) return;
        var library = FindLibrary(d);
        if (library != null) _ = library.LoadThumbAsync(card);
    }

    private static LibraryViewModel? FindLibrary(DependencyObject start)
    {
        var node = start;
        while (node != null)
        {
            if (node is FrameworkElement element && element.DataContext is LibraryViewModel library) return library;
            node = VisualTreeHelper.GetParent(node);
        }
        return Application.Current?.MainWindow?.DataContext is MainViewModel main ? main.Library : null;
    }
}
