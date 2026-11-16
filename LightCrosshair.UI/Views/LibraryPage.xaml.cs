using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LightCrosshair.UI.Localization;
using LightCrosshair.UI.ViewModels;

namespace LightCrosshair.UI.Views;

public partial class LibraryPage : UserControl
{
    public LibraryPage()
    {
        InitializeComponent();
        MineList.PreviewMouseRightButtonUp += OnRightClick;
        PresetList.PreviewMouseRightButtonUp += OnRightClick;
        MineList.PreviewKeyDown += OnListKey;
        PresetList.PreviewKeyDown += OnListKey;
    }

    private void OnRightClick(object sender, MouseButtonEventArgs e)
    {
        if (FindCard(e.OriginalSource as DependencyObject) is not LibraryCard card) return;
        if (sender is not FrameworkElement owner) return;
        OpenMenu(owner, card);
        e.Handled = true;
    }

    private void OnListKey(object sender, KeyEventArgs e)
    {
        var open = e.Key == Key.Apps || (e.Key == Key.F10 && Keyboard.Modifiers == ModifierKeys.Shift);
        if (!open || sender is not ListBox list || list.SelectedItem is not LibraryCard card) return;
        OpenMenu(list, card);
        e.Handled = true;
    }

    private void OpenMenu(FrameworkElement owner, LibraryCard card)
    {
        if (DataContext is not LibraryViewModel library) return;
        var menu = new ContextMenu { PlacementTarget = owner };
        AutomationProperties.SetName(menu, Loc.Instance["Library_Actions"]);
        if (!card.IsPreset)
            Add(menu, "Library_ApplyDefault", library.ApplyDefaultCommand, card);
        Add(menu, card.IsPreset ? "Library_EditCopy" : "Edit", library.EditCommand, card);
        Add(menu, "Duplicate", library.DuplicateCommand, card);
        if (!card.IsPreset)
        {
            Add(menu, "Export", library.ExportCommand, card);
            Add(menu, "Delete", library.DeleteCommand, card);
        }
        menu.IsOpen = true;
    }

    private static void Add(ContextMenu menu, string key, ICommand command, LibraryCard card)
    {
        var item = new MenuItem { Header = Loc.Instance[key], Command = command, CommandParameter = card };
        AutomationProperties.SetName(item, Loc.Instance[key]);
        menu.Items.Add(item);
    }

    private static LibraryCard? FindCard(DependencyObject? node)
    {
        while (node != null)
        {
            if (node is FrameworkElement element && element.DataContext is LibraryCard card) return card;
            node = VisualTreeHelper.GetParent(node);
        }
        return null;
    }
}
