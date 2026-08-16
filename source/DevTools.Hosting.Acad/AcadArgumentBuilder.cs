namespace DevTools.Hosting.Acad;

public sealed class AcadArgumentBuilder : IHostArgumentBuilder
{
    private static readonly Dictionary<HostApp, string> ProductCodes = new()
    {
        [HostApp.AutoCad] = "ACAD",
        [HostApp.Civil3D] = "C3D",
        [HostApp.Plant3D] = "PLNT3D",
        [HostApp.AcadMap3D] = "MAP",
        [HostApp.AcadArch] = "ACA",
        [HostApp.AcadMech] = "ACADM",
        [HostApp.AcadMep] = "MEP",
        [HostApp.AcadElec] = "ACADE",
    };

    public bool Supports(HostApp hostApp) => hostApp.IsAcadFamily();

    public IReadOnlyList<string> Build(HostLaunchRequest request, string executablePath)
    {
        if (!ProductCodes.TryGetValue(request.HostApp, out var productCode))
            throw new InvalidOperationException($"Launch not yet supported for {request.HostApp}.");

        var arguments = new List<string>();
        if (request.HostApp == HostApp.Civil3D)
        {
            var installDir = Path.GetDirectoryName(executablePath)
                ?? throw new InvalidOperationException("Civil3D install directory could not be resolved.");
            var dbxPath = Path.Combine(installDir, "AecBase.dbx");
            if (!File.Exists(dbxPath))
                throw new InvalidOperationException($"Civil3D launch failed: missing '{dbxPath}'.");

            arguments.Add("/ld");
            arguments.Add(dbxPath);
            arguments.Add("/p");
            arguments.Add("<<C3D_Metric>>");
        }

        arguments.Add("/product");
        arguments.Add(productCode);
        arguments.Add("/language");
        arguments.Add(request.LanguageCulture);

        if (!string.IsNullOrWhiteSpace(request.FilePath))
            arguments.Add(request.FilePath!);

        return arguments;
    }
}
