namespace RevitDevTool.Logging.Linkify;

public abstract class SearchMatch
{
    private SearchMatch()
    {
    }

    public sealed class HostElements : SearchMatch
    {
        public HostElements(ICollection<ElementId> ids)
        {
            Ids = ids;
        }

        public ICollection<ElementId> Ids { get; }
    }

    public sealed class LinkedElements : SearchMatch
    {
        public LinkedElements(RevitLinkInstance instance, ICollection<ElementId> ids)
        {
            Instance = instance;
            Ids = ids;
        }

        public RevitLinkInstance Instance { get; }

        public ICollection<ElementId> Ids { get; }
    }
}
