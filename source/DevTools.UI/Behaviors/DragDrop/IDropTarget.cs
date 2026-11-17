namespace DevTools.UI.Behaviors.DragDrop;

public interface IDropTarget
{
    void DragOver(DropInfo info);

    void Drop(DropInfo info);
}
