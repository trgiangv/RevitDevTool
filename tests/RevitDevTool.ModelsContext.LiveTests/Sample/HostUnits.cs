namespace RevitDevTool.ModelsContext.LiveTests;

internal sealed class HostUnits : IDisposable
{
    private readonly Transaction? _transaction;

    private HostUnits(Transaction? transaction)
    {
        _transaction = transaction;
    }

    public static HostUnits Set(string? lengthUnit)
    {
        if (lengthUnit is null)
        {
            return new HostUnits(null);
        }

        var unit = (ForgeTypeId)typeof(UnitTypeId).GetProperty(lengthUnit)!.GetValue(null)!;
        var host = SampleModel.Host;
        var transaction = new Transaction(host, "Model Context test units");
        transaction.Start();
        var units = host.GetUnits();
        units.SetFormatOptions(SpecTypeId.Length, new FormatOptions(unit));
        host.SetUnits(units);
        return new HostUnits(transaction);
    }

    public void Dispose()
    {
        if (_transaction is { } transaction)
        {
            transaction.RollBack();
            transaction.Dispose();
        }
    }
}
