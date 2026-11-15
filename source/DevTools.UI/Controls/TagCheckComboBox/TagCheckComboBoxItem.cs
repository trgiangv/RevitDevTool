using HandyControl.Controls;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;

namespace DevTools.UI.Controls;

public class TagCheckComboBoxItem : CheckComboBoxItem
{
    protected override AutomationPeer OnCreateAutomationPeer() => new TagCheckComboBoxItemWrapperPeer(this);

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (ItemsControl.ItemsControlFromItemContainer(this) is TagCheckComboBox box
            && box.ItemContainerGenerator.ItemFromContainer(this) is var item
            && item != DependencyProperty.UnsetValue)
        {
            box.ToggleItem(item);
            e.Handled = true;
            return;
        }

        base.OnMouseLeftButtonDown(e);
    }

    private sealed class TagCheckComboBoxItemWrapperPeer(TagCheckComboBoxItem owner) : ListBoxItemWrapperAutomationPeer(owner)
    {
        public override object? GetPattern(PatternInterface patternInterface)
        {
            if (patternInterface is PatternInterface.Toggle or PatternInterface.SelectionItem
                && ItemsControl.ItemsControlFromItemContainer(Owner) is TagCheckComboBox box
                && box.ItemContainerGenerator.ItemFromContainer(Owner) is var item
                && item != DependencyProperty.UnsetValue)
            {
                return box.ItemPattern(item, patternInterface);
            }

            return base.GetPattern(patternInterface);
        }
    }
}
