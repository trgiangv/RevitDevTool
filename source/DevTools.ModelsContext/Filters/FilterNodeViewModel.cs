using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DevTools.ModelsContext.Filters;

public abstract partial class FilterNodeViewModel : ObservableObject
{
    private protected FilterNodeViewModel(IFilterEditor editor, bool isReadOnly)
    {
        Editor = editor;
        IsReadOnly = isReadOnly;
    }

    public bool IsReadOnly { get; }

    public RuleSetViewModel? Group => Parent;

    public bool ShowsConnector => Parent is { } parent && parent.Children.IndexOf(this) > 0;

    public string ConnectorText => Parent?.MatchText ?? string.Empty;

    internal IFilterEditor Editor { get; }

    internal RuleSetViewModel? Parent { get; set; }

    private protected bool CanEdit => !IsReadOnly;

    private bool CanRemove => Parent is { IsReadOnly: false };

    private bool CanMoveUp => Parent is { IsReadOnly: false } parent
                              && FilterTreeMoves.CanMoveUp(parent.Children.IndexOf(this));

    private bool CanMoveDown => Parent is { IsReadOnly: false } parent
                                && FilterTreeMoves.CanMoveDown(parent.Children.IndexOf(this), parent.Children.Count);

    internal abstract FilterNode? ToNode();

    internal virtual void RefreshScope()
    {
    }

    internal static FilterNodeViewModel From(FilterNode node, IFilterEditor editor, bool isReadOnly)
    {
        return node switch
        {
            FilterSet set => RuleSetViewModel.From(set, editor, isReadOnly),
            FilterRule rule => new RuleViewModel(editor, isReadOnly, rule),
            UnsupportedRule unsupported => new UnsupportedRuleViewModel(editor, unsupported.Description),
            _ => new UnsupportedRuleViewModel(editor, node.ToString() ?? string.Empty)
        };
    }

    public bool CanMoveTo(RuleSetViewModel target)
    {
        return FilterTreeMoves.CanMove<FilterNodeViewModel>(this, target, node => node.Parent, node => node.IsReadOnly);
    }

    public bool CanDropAt(RuleSetViewModel target, int index)
    {
        return CanMoveTo(target)
               && (!ReferenceEquals(Parent, target)
                   || FilterTreeMoves.WithinGroup(target.Children.IndexOf(this), index, target.Children.Count) is not null);
    }

    public bool MoveTo(RuleSetViewModel target, int index)
    {
        if (!CanMoveTo(target))
        {
            return false;
        }

        var source = Parent!;
        var from = source.Children.IndexOf(this);
        if (ReferenceEquals(source, target))
        {
            if (FilterTreeMoves.WithinGroup(from, index, source.Children.Count) is not { } to)
            {
                return false;
            }

            source.Children.Move(from, to);
            Edited();
            return true;
        }

        source.Children.RemoveAt(from);
        Parent = target;
        target.Children.Insert(FilterTreeMoves.IntoGroup(index, target.Children.Count), this);
        RefreshScope();
        Edited();
        return true;
    }

    internal void NotifyConnector()
    {
        OnPropertyChanged(nameof(ShowsConnector));
        OnPropertyChanged(nameof(ConnectorText));
        OnPropertyChanged(nameof(Group));
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
        ToggleConnectorCommand.NotifyCanExecuteChanged();
    }

    private protected void Edited()
    {
        Editor.OnEdited();
    }

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void Remove()
    {
        Parent?.RemoveChild(this);
    }

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp()
    {
        if (Parent is { } parent)
        {
            MoveTo(parent, FilterTreeMoves.Up(parent.Children.IndexOf(this)));
        }
    }

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown()
    {
        if (Parent is { } parent)
        {
            MoveTo(parent, FilterTreeMoves.Down(parent.Children.IndexOf(this)));
        }
    }

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void ToggleConnector()
    {
        Parent?.ToggleMatchCommand.Execute(null);
    }
}
