using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LightCrosshair.UI.ViewModels;

namespace LightCrosshair.UI.Views;

public partial class GamesPage : UserControl
{
    public GamesPage() => InitializeComponent();

    private void OnToggleEnabled(object sender, MouseButtonEventArgs e)
    {
        Toggle(sender);
        e.Handled = true;
    }

    private void OnToggleEnabledKey(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Space or Key.Enter)) return;
        Toggle(sender);
        e.Handled = true;
    }

    private void Toggle(object sender)
    {
        if (sender is not CheckBox box || box.DataContext is not GameProfile profile) return;
        if (DataContext is GamesViewModel games) games.ToggleEnabledCommand.Execute(profile);
    }

    private void OnPresetPicked(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox box || box.SelectedItem is not LibraryCard card) return;
        if (DataContext is not GamesViewModel games) return;
        if (Window.GetWindow(this) is not MainWindow window || window.DataContext is not MainViewModel main) return;
        box.SelectedItem = null;
        window.ShowUsePopup(box);
        main.Library.BeginUse(card, games.Selected?.Id);
    }
}
