// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices;

#if NETSTANDARD2_0 || NETSTANDARD2_1 || NET48
/// <summary>
/// Polyfill to allow C# 9 <c>init</c> accessors and <c>record</c> types on older TFMs.
/// </summary>
// ReSharper disable once UnusedType.Global
internal static class IsExternalInit;
#endif
