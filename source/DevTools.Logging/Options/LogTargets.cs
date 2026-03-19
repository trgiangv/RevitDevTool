namespace DevTools.Logging.Options;

[Flags]
public enum LogTargets
{
    None    = 0,
    Monitor = 1,
    File    = 2,
    Http    = 4,
    All     = Monitor | File | Http
}
