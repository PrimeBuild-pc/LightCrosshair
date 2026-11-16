using System.Windows.Controls;
using LightCrosshair.UI.ViewModels;

namespace LightCrosshair.UI.Views;

public partial class HotkeysPage : UserControl
{
    public HotkeysPage() => InitializeComponent();

    private void OnModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox box || (!box.IsDropDownOpen && !box.IsKeyboardFocusWithin)) return;
        if (box.DataContext is not HotkeyRow row || box.SelectedValue is not BindingMode mode) return;
        if (row.Binding.Mode == mode) return;
        if (DataContext is HotkeysViewModel viewModel)
            viewModel.SetModeCommand.Execute(new ModeChange { Action = row.Action, Mode = mode });
    }

    private void OnMouseChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox box || (!box.IsDropDownOpen && !box.IsKeyboardFocusWithin)) return;
        if (box.DataContext is not HotkeyRow row || box.SelectedValue is not string key || key.Length == 0) return;
        if (row.MouseKey == key) return;
        if (DataContext is HotkeysViewModel viewModel)
            viewModel.SetMouseCommand.Execute(new MousePick { Action = row.Action, Key = key });
    }
}
