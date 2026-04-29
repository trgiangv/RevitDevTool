namespace DevTools.Presentation.Interfaces;

public interface ILogEnricherProvider
{
    IReadOnlyList<object> AvailableEnrichers { get; }
    IList<object> SelectedEnrichers { get; set; }
}
