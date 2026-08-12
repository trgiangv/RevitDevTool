namespace DevTools.NUnit.Runtime.Fixtures;

public static class GenerationMarker
{
#if NUNIT_GENERATION_TWO
    public const string Value = "generation-two";
#else
    public const string Value = "generation-one";
#endif

    public static string GetValue() => Value;
}
