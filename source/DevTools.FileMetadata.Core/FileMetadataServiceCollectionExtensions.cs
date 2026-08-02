using Microsoft.Extensions.DependencyInjection;

namespace DevTools.FileMetadata.Core;

public static class FileMetadataServiceCollectionExtensions
{
    public static IServiceCollection AddFileMetadataReaders(this IServiceCollection services)
    {
        services.AddSingleton<IFileReaderCatalog, FileReaderCatalog>();
        return services;
    }
}
