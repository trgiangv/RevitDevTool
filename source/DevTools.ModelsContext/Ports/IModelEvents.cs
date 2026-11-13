using DevTools.ModelsContext.Model;

namespace DevTools.ModelsContext.Ports;

public interface IModelEvents
{
    event EventHandler? DocumentsChanged;

    event EventHandler<ElementDelta>? ElementsChanged;

    event EventHandler<IReadOnlyCollection<ElementRef>>? SelectionChanged;

    event EventHandler<ViewInfo>? ViewActivated;

    event EventHandler<DocumentKey>? DocumentClosing;
}
