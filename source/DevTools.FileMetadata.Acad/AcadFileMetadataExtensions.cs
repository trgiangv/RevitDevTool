using DevTools.FileMetadata.Core;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.FileMetadata.Acad;

public static class AcadFileMetadataExtensions
{
    public static IServiceCollection AddAcadFileMetadataReader(this IServiceCollection services)
    {
        services.AddSingleton<IFileReader, AcadFileMetadataReader>();
        return services;
    }
}
