using System.Text;

namespace DevTools.TestRunner;

public interface IRunInput
{
    Task<string> ReadToEndAsync(CancellationToken cancellationToken);
}

internal sealed class ConsoleRunInput : IRunInput
{
    public async Task<string> ReadToEndAsync(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class BufferedRunInput(TextReader reader) : IRunInput
{
    public Task<string> ReadToEndAsync(CancellationToken cancellationToken) =>
        reader.ReadToEndAsync(cancellationToken);
}
