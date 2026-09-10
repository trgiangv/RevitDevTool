using System.Text;

namespace DevTools.TestRunner;

public interface IMachineRunInput
{
    Task<string> ReadToEndAsync(CancellationToken cancellationToken);
}

internal sealed class ConsoleMachineRunInput : IMachineRunInput
{
    public async Task<string> ReadToEndAsync(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class BufferedMachineRunInput(TextReader reader) : IMachineRunInput
{
    public Task<string> ReadToEndAsync(CancellationToken cancellationToken) =>
        reader.ReadToEndAsync(cancellationToken);
}
