using System.Collections.ObjectModel;
using System.Windows;
// ReSharper disable ConvertToExtensionBlock

namespace RevitDevTool.Theme;

/// <summary>
/// Extension methods for working with MergedDictionaries.
/// </summary>
public static class MergedDictionariesExtension
{
    public static void RemoveIfNotNull(this Collection<ResourceDictionary> mergedDictionaries, ResourceDictionary? item)
    {
        if (item != null)
        {
            mergedDictionaries.Remove(item);
        }
    }

    public static void InsertOrReplace(this Collection<ResourceDictionary> mergedDictionaries, int index, ResourceDictionary item)
    {
        if (mergedDictionaries.Count > index)
        {
            mergedDictionaries[index] = item;
        }
        else
        {
            mergedDictionaries.Insert(index, item);
        }
    }
}
