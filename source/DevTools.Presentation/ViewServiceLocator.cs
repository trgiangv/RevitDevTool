using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Presentation;

public static class ViewServiceLocator
{
    public static IServiceProvider? Services { get; set; }

    public static T GetRequired<T>() where T : notnull
        => (Services ?? throw new InvalidOperationException("ViewServiceLocator not initialized"))
            .GetRequiredService<T>();
}
