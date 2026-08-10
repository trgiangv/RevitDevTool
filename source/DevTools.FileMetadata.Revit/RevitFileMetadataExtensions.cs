using DevTools.FileMetadata.Core;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.FileMetadata.Revit;

public static class RevitFileMetadataExtensions
{
    public static IServiceCollection AddRevitFileMetadataReader(this IServiceCollection services)
    {
        services.AddSingleton<IFileReader, RevitFileMetadataReader>();
        return services;
    }
}
