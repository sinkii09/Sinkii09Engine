using System;
using System.Collections.Generic;
using System.Linq;

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

        /// <summary>
        /// Returns the elements of the source in topological order according to the dependencies selector.
        /// </summary>
        public static IEnumerable<T> TopologicalOrder<T>(this IEnumerable<T> source, Func<T, IEnumerable<T>> dependencies)
        {
            var sorted = new List<T>();
            var visited = new Dictionary<T, bool>();

            foreach (var item in source)
                Visit(item, visited, sorted, dependencies);

            return sorted;
        }

        private static void Visit<T>(T item, Dictionary<T, bool> visited, List<T> sorted, Func<T, IEnumerable<T>> dependencies)
        {
            if (visited.TryGetValue(item, out var inProcess))
            {
                if (inProcess)
                    throw new InvalidOperationException("Cyclic dependency detected.");
                return;
            }

            visited[item] = true;

            foreach (var dep in dependencies(item) ?? Enumerable.Empty<T>())
                Visit(dep, visited, sorted, dependencies);

            visited[item] = false;
            if (!sorted.Contains(item))
                sorted.Add(item);
        }
    }
}
