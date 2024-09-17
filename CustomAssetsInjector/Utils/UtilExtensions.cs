using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;

namespace CustomAssetsInjector.Utils;

public static class UtilExtensions
{
    /// <summary>
    /// Sets <see cref="InputElement.IsEnabled"/> and <see cref="InputElement.IsVisible"/> of element to active.
    /// </summary>
    /// <param name="element">The element to use.</param>
    /// <param name="active">If the element activates or deactivates.</param>
    public static void SetActive(this InputElement element, bool active)
    {
        element.IsEnabled = active;
        element.IsVisible = active;
    }

    public static bool IsEqualTo<T>(this List<T> list1, List<T> list2)
    {
        if (list1.Count != list2.Count)
            return false;
        
        return list1.ToHashSet().SetEquals(list2);
    }
}