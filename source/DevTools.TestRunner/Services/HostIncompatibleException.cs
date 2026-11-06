namespace DevTools.TestRunner.Services;

public sealed class HostIncompatibleException(string sentence) : Exception(sentence);
