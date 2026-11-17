using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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
        MineList.PreviewMouseLeftButtonUp += OnMineClick;
        PresetList.PreviewMouseLeftButtonUp += OnPresetClick;
        MineList.MouseDoubleClick += OnMineDoubleClick;
        MineList.PreviewKeyDown += OnListKey;
        PresetList.PreviewKeyDown += OnListKey;
    }

    public void ReloadVisible()
    {
        if (DataContext is not LibraryViewModel library) return;
        foreach (var card in FindCards(MineList).Concat(FindCards(PresetList)))
            _ = library.LoadThumbAsync(card);
    }

    private void OnRightClick(object sender, MouseButtonEventArgs e)
    {
        if (FindCard(e.OriginalSource as DependencyObject) is not LibraryCard card) return;
        if (sender is not FrameworkElement owner) return;
        OpenMenu(owner, card);
        e.Handled = true;
    }

    private void OnMineClick(object sender, MouseButtonEventArgs e)
    {
        if (IsInsideButton(e.OriginalSource as DependencyObject)) return;
        if (FindCard(e.OriginalSource as DependencyObject) is not LibraryCard card || card.IsPreset || card.Renaming) return;
        if (DataContext is LibraryViewModel library)
            library.ApplyDefaultCommand.Execute(card);
    }

    private void OnPresetClick(object sender, MouseButtonEventArgs e)
    {
        if (IsInsideButton(e.OriginalSource as DependencyObject)) return;
        if (FindCard(e.OriginalSource as DependencyObject) is not LibraryCard card || !card.IsPreset) return;
        if (DataContext is not LibraryViewModel library) return;
        var anchor = FindAnchor(e.OriginalSource as DependencyObject) ?? PresetList;
        if (Window.GetWindow(this) is MainWindow window)
            window.ShowUsePopup(anchor);
        library.BeginUse(card, null);
        e.Handled = true;
    }

    private void OnMineDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindCard(e.OriginalSource as DependencyObject) is not LibraryCard card || card.IsPreset) return;
        if (DataContext is LibraryViewModel library)
            library.EditCommand.Execute(card);
        e.Handled = true;
    }

    private void OnRenameKey(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box || box.DataContext is not LibraryCard card) return;
        if (e.Key == Key.Escape)
        {
            card.Renaming = false;
            e.Handled = true;
            return;
        }
        if (e.Key != Key.Enter || DataContext is not LibraryViewModel library) return;
        library.CommitRenameCommand.Execute(card);
        e.Handled = true;
    }

    private void OnRenameLost(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box || box.DataContext is not LibraryCard card || !card.Renaming) return;
        if (DataContext is LibraryViewModel library)
            library.CommitRenameCommand.Execute(card);
    }

    private void OnListKey(object sender, KeyEventArgs e)
    {
        if (sender is not ListBox list || list.SelectedItem is not LibraryCard card) return;
        if (DataContext is not LibraryViewModel library) return;
        var open = e.Key == Key.Apps || (e.Key == Key.F10 && Keyboard.Modifiers == ModifierKeys.Shift);
        if (open)
        {
            OpenMenu(list, card);
            e.Handled = true;
            return;
        }
        if (card.IsPreset || card.Renaming) return;
        if (e.Key == Key.Enter)
        {
            library.ApplyDefaultCommand.Execute(card);
            e.Handled = true;
        }
        else if (e.Key == Key.F2)
        {
            card.BeginRename();
            e.Handled = true;
            list.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                var box = FindBoxes(list).FirstOrDefault(item => ReferenceEquals(item.DataContext, card) && item.IsVisible);
                box?.Focus();
                box?.SelectAll();
            });
        }
        else if (e.Key == Key.Delete)
        {
            library.DeleteCommand.Execute(card);
            e.Handled = true;
        }
    }

    private void OpenMenu(FrameworkElement owner, LibraryCard card)
    {
        if (DataContext is not LibraryViewModel library) return;
        var menu = new ContextMenu { PlacementTarget = owner };
        AutomationProperties.SetName(menu, Loc.Instance["Library_Actions"]);
        if (card.IsPreset)
        {
            var use = new MenuItem { Header = Loc.Instance["Library_UseMenu"], CommandParameter = card };
            AutomationProperties.SetName(use, Loc.Instance["Library_UseMenu"]);
            use.Click += (_, _) =>
            {
                if (Window.GetWindow(this) is MainWindow window)
                    window.ShowUsePopup(owner);
                library.BeginUse(card, null);
            };
            menu.Items.Add(use);
            Add(menu, "Library_EditCopy", library.EditCommand, card);
        }
        else
        {
            Add(menu, "Library_Use", library.ApplyDefaultCommand, card);
            Add(menu, "Library_SetDefaultAll", library.SetDefaultForAllAppsCommand, card);
            Add(menu, "Edit", library.EditCommand, card);
            Add(menu, "Duplicate", library.DuplicateCommand, card);
            Add(menu, card.Design.Favorite ? "Library_Unfavorite" : "Library_Favorite", library.ToggleFavoriteCommand, card);
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

    private static FrameworkElement? FindAnchor(DependencyObject? node)
    {
        FrameworkElement? anchor = null;
        while (node != null)
        {
            if (node is ListBox) break;
            if (node is FrameworkElement element && element.DataContext is LibraryCard)
                anchor = element;
            node = VisualTreeHelper.GetParent(node);
        }
        return anchor;
    }

    private static bool IsInsideButton(DependencyObject? node)
    {
        while (node != null)
        {
            if (node is Button) return true;
            if (node is ListBox) return false;
            node = VisualTreeHelper.GetParent(node);
        }
        return false;
    }

    private static IEnumerable<TextBox> FindBoxes(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBox box) yield return box;
            foreach (var nested in FindBoxes(child)) yield return nested;
        }
    }

    private static IEnumerable<LibraryCard> FindCards(DependencyObject root)
    {
        var seen = new HashSet<LibraryCard>();
        var pending = new Stack<DependencyObject>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node is FrameworkElement element && element.DataContext is LibraryCard card && seen.Add(card))
                yield return card;
            var count = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < count; i++)
                pending.Push(VisualTreeHelper.GetChild(node, i));
        }
    }
}
