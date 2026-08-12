namespace DevTools.NUnit.Runtime;

internal static class Guard
{
    public static T NotNull<T>(T? value, string paramName) where T : class
    {
        if (value is null)
            throw new ArgumentNullException(paramName);
        return value;
    }

    public static string NotNullOrWhiteSpace(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be null or whitespace.", paramName);
        return value!;
    }

    public static void NotDisposed(bool disposed, object instance)
    {
        if (disposed)
            throw new ObjectDisposedException(instance.GetType().FullName);
    }
}
