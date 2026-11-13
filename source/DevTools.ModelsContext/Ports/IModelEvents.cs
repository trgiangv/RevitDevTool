using DevTools.ModelsContext.Model;

namespace DevTools.ModelsContext.Ports;

public interface IModelEvents
{
    bool IsBusy { get; }

    event EventHandler? DocumentsChanged;

    event EventHandler<ElementDelta>? ElementsChanged;

    event EventHandler<IReadOnlyCollection<ElementRef>>? SelectionChanged;

    event EventHandler<ViewInfo>? ViewActivated;

    event EventHandler<DocumentKey>? DocumentClosing;

    event EventHandler<DocumentKey>? ActiveModelChanged;

    event EventHandler<DocumentKey>? ModelSynced;

    event EventHandler<LinkStatusChange>? LinkStatusChanged;

    event EventHandler<bool>? BusyChanged;

    event EventHandler<SourceId>? ParameterDefinitionsChanged;
}
