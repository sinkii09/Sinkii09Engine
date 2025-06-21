using System.Collections.Generic;
using UnityEngine;

namespace Sinkii09.Engine
{
    public static class LinqUtils
    {
        public static bool IsIndexValid<T>(this T[] array, int index)
        {
            return array.Length > 0 && index >= 0 && index < array.Length;
        }

        public static bool IsIndexValid<T>(this IList<T> list, int index)
        {
            return list.Count > 0 && index >= 0 && index < list.Count;
        }
    }
}
