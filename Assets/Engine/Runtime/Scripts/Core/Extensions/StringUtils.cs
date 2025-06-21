using System;

namespace Sinkii09.Engine.Extensions
{
    public static class StringUtils
    {
        public static string GetAfter(this string content, string matchString, StringComparison comp = StringComparison.Ordinal)
        {
            if (content.Contains(matchString))
            {
                var startIndex = content.LastIndexOf(matchString, comp) + matchString.Length;
                if (content.Length <= startIndex) return string.Empty;
                return content.Substring(startIndex);
            }
            else return null;
        }

        /// <summary>
        /// Attempts to extract content after the specified match (on first occurence).
        /// </summary>
        public static string GetAfterFirst(this string content, string matchString, StringComparison comp = StringComparison.Ordinal)
        {
            if (content.Contains(matchString))
            {
                var startIndex = content.IndexOf(matchString, comp) + matchString.Length;
                if (content.Length <= startIndex) return string.Empty;
                return content.Substring(startIndex);
            }
            else return null;
        }
        public static string GetBefore(this string content, string matchString, StringComparison comp = StringComparison.Ordinal)
        {
            if (content.Contains(matchString))
            {
                var endIndex = content.IndexOf(matchString, comp);
                return content.Substring(0, endIndex);
            }
            else return null;
        }

        /// <summary>
        /// Attempts to extract content before the specified match (on last occurence).
        /// </summary>
        public static string GetBeforeLast(this string content, string matchString, StringComparison comp = StringComparison.Ordinal)
        {
            if (content.Contains(matchString))
            {
                var endIndex = content.LastIndexOf(matchString, comp);
                return content.Substring(0, endIndex);
            }
            else return null;
        }

        /// <summary>
        /// Performs <see cref="string.StartsWith(string, StringComparison)"/> with <see cref="StringComparison.Ordinal"/>.
        /// </summary>
        public static bool StartsWithFast(this string content, string match)
        {
            return content.StartsWith(match, StringComparison.Ordinal);
        }

        /// <summary>
        /// Performs <see cref="string.EndsWith(string, StringComparison)"/> with <see cref="StringComparison.Ordinal"/>.
        /// </summary>
        public static bool EndsWithFast(this string content, string match)
        {
            return content.EndsWith(match, StringComparison.Ordinal);
        }

        /// <summary>
        /// Performs <see cref="string.Equals(string, string, StringComparison)"/> with <see cref="StringComparison.Ordinal"/>.
        /// </summary>
        public static bool EqualsFast(this string content, string comparedString)
        {
            return content.Equals(comparedString, StringComparison.Ordinal);
        }


        public static string[] SplitByNewLine(this string content, StringSplitOptions splitOptions = StringSplitOptions.None)
        {
            if (string.IsNullOrEmpty(content)) return null;

            // "\r\n"   (\u000D\u000A)  Windows
            // "\n"     (\u000A)        Unix
            // "\r"     (\u000D)        Mac
            // Not using Environment.NewLine here, as content could've been produced 
            // in not the same environment we running the program in.
            return content.Split(new string[] { "\r\n", "\n", "\r" }, splitOptions);
        }

        /// <summary>
        /// Performs <see cref="string.Equals(string, string, StringComparison)"/> with <see cref="StringComparison.OrdinalIgnoreCase"/>.
        /// </summary>
        public static bool EqualsFastIgnoreCase(this string content, string comparedString)
        {
            return content.Equals(comparedString, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Performs <see cref="string.StartsWith(string)"/> and <see cref="string.EndsWith(string)"/> with the provided match.
        /// </summary>
        public static bool WrappedIn(this string content, string match, StringComparison comp = StringComparison.Ordinal)
        {
            return content.StartsWith(match, comp) && content.EndsWith(match, comp);
        }
    }
}