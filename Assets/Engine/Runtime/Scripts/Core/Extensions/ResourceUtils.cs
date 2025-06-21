using System;
using System.Collections.Generic;
using System.Linq;
using ZLinq;

namespace Sinkii09.Engine.Extensions
{
    public static class ResourceUtils
    {
        public static IEnumerable<string> LocateResourcePathsAtFolder(this IEnumerable<string> source, string parentFolderPath)
        {
            parentFolderPath = parentFolderPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(parentFolderPath))
                return source.AsValueEnumerable().Where(p => !p.Contains("/") || string.IsNullOrEmpty(p.GetBeforeLast("/"))).ToList(); // Added ToList() to convert to IEnumerable<string>  

            return source.AsValueEnumerable().Where(p => p.Contains("/") && (p.StartsWithFast($"{parentFolderPath}/") || p.StartsWithFast($"/{parentFolderPath}/"))).ToList(); // Added ToList() to convert to IEnumerable<string>  
        }

        /// <summary>
        /// Given the path to the parent folder, returns all the unique folder paths inside that folder (or any sub-folders).
        /// </summary>
        public static IEnumerable<string> LocateFolderPathsAtFolder(this IEnumerable<string> source, string parentFolderPath)
        {
            parentFolderPath = parentFolderPath ?? string.Empty;

            if (parentFolderPath.StartsWithFast("/"))
                parentFolderPath = parentFolderPath.GetAfterFirst("/") ?? string.Empty;

            if (parentFolderPath.Length > 0 && !parentFolderPath.EndsWithFast("/"))
                parentFolderPath += "/";

            return source.Where(p => p.StartsWithFast(parentFolderPath) && p.GetAfterFirst(parentFolderPath).Contains("/"))
                .Select(p => parentFolderPath + p.GetBetween(parentFolderPath, "/")).Distinct();
        }

        /// <summary>
        /// Attempts to extract content between the specified matches (on first occurence).
        /// </summary>
        public static string GetBetween(this string content, string startMatch, string endMatch, StringComparison comp = StringComparison.Ordinal)
        {
            if (content.Contains(startMatch) && content.Contains(endMatch))
            {
                var startIndex = content.IndexOf(startMatch, comp) + startMatch.Length;
                var endIndex = content.IndexOf(endMatch, startIndex, comp);
                return content.Substring(startIndex, endIndex - startIndex);
            }
            else return null;
        }

    }
}