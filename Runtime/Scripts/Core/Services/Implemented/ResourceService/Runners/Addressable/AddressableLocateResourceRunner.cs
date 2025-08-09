using Cysharp.Threading.Tasks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Optimized resource runner for locating Addressable assets with intelligent caching and efficient search strategies
    /// </summary>
    public class AddressableLocateResourceRunner<T> : LocateResourceRunner<T> where T : UnityEngine.Object
    {
        private readonly Action<string> _logAction;
        private bool _isCancelled = false;

        // Static caching system for location results
        private static readonly ConcurrentDictionary<string, List<string>> _locationCache = new();
        private static readonly ConcurrentDictionary<string, DateTime> _cacheTimestamps = new();
        private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(3);

        // Compiled regex cache for wildcard patterns
        private static readonly ConcurrentDictionary<string, Regex> _regexCache = new();
        
        // Catalog cache for efficient access
        private static readonly ConcurrentDictionary<Type, IList<IResourceLocation>> _catalogCache = new();
        private static DateTime _lastCatalogUpdate = DateTime.MinValue;
        private static readonly TimeSpan CatalogCacheExpiry = TimeSpan.FromMinutes(10);

        public AddressableLocateResourceRunner(
            IResourceProvider provider, 
            string path, 
            Action<string> logAction) : base(provider, path)
        {
            _logAction = logAction;
        }

        public override async UniTask RunAsync()
        {
            try
            {
                _logAction?.Invoke($"[Addressable] Locating resources for '{Path}' of type '{typeof(T).Name}'");

                if (_isCancelled)
                {
                    SetResult(new List<string>());
                    return;
                }

                // Use optimized unified search strategy
                var resourcePaths = await GetResourcePathsOptimizedAsync();

                if (_isCancelled)
                {
                    SetResult(new List<string>());
                    return;
                }

                _logAction?.Invoke($"[Addressable] Located {resourcePaths.Count} resources for '{Path}' (cached: {WasCacheHit()})");
                SetResult(resourcePaths);
            }
            catch (Exception ex)
            {
                _logAction?.Invoke($"[Addressable] Failed to locate resources for '{Path}': {ex.Message}");
                SetResult(new List<string>());
                // Don't throw - return empty list instead
            }
        }

        /// <summary>
        /// Optimized unified search using cached catalog data
        /// </summary>
        private async UniTask<List<string>> GetResourcePathsOptimizedAsync()
        {
            var cacheKey = GenerateCacheKey();

            // Check cache first
            if (IsResultCached(cacheKey))
            {
                return new List<string>(_locationCache[cacheKey]);
            }

            var results = new HashSet<string>(); // Use HashSet to avoid duplicates

            // Strategy 1: Direct address lookup (fastest)
            if (!IsWildcardPattern(Path))
            {
                await SearchDirectAddress(results);
            }

            // Strategy 2: Catalog-based search (for wildcards and empty paths)
            if (results.Count == 0 || IsWildcardPattern(Path) || string.IsNullOrEmpty(Path))
            {
                await SearchFromCatalog(results);
            }

            var resultList = results.ToList();

            // Cache the results
            _locationCache[cacheKey] = resultList;
            _cacheTimestamps[cacheKey] = DateTime.UtcNow;

            return resultList;
        }

        /// <summary>
        /// Fast direct address search
        /// </summary>
        private async UniTask SearchDirectAddress(HashSet<string> results)
        {
            try
            {
                var locations = await Addressables.LoadResourceLocationsAsync(Path, typeof(T)).ToUniTask();
                
                if (locations != null)
                {
                    foreach (var location in locations)
                    {
                        if (_isCancelled) break;

                        var address = GetLocationAddress(location);
                        if (!string.IsNullOrEmpty(address))
                        {
                            results.Add(address);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logAction?.Invoke($"[Addressable] Direct address search failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Efficient catalog-based search with caching
        /// </summary>
        private async UniTask SearchFromCatalog(HashSet<string> results)
        {
            try
            {
                var catalogLocations = await GetCachedCatalogLocationsAsync();
                var searchPattern = CreateSearchPattern();

                await UniTask.SwitchToThreadPool(); // Move regex matching to background thread

                foreach (var location in catalogLocations)
                {
                    if (_isCancelled) break;

                    var address = GetLocationAddress(location);
                    if (string.IsNullOrEmpty(address)) continue;

                    if (MatchesSearchCriteria(address, searchPattern))
                    {
                        results.Add(address);
                    }
                }

                await UniTask.SwitchToMainThread(); // Switch back for Unity operations
            }
            catch (Exception ex)
            {
                _logAction?.Invoke($"[Addressable] Catalog search failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Get cached catalog locations for the specified type
        /// </summary>
        private async UniTask<IList<IResourceLocation>> GetCachedCatalogLocationsAsync()
        {
            var cacheKey = typeof(T);

            // Check cache validity
            if (_catalogCache.TryGetValue(cacheKey, out var cachedLocations) &&
                DateTime.UtcNow - _lastCatalogUpdate < CatalogCacheExpiry)
            {
                return cachedLocations;
            }

            try
            {
                // Load fresh catalog data
                var locations = await Addressables.LoadResourceLocationsAsync(
                    Addressables.MergeMode.Union, 
                    typeof(T)).ToUniTask();

                // Update cache
                _catalogCache[cacheKey] = locations ?? new List<IResourceLocation>();
                _lastCatalogUpdate = DateTime.UtcNow;

                return _catalogCache[cacheKey];
            }
            catch
            {
                // Return empty list if catalog loading fails
                return new List<IResourceLocation>();
            }
        }

        /// <summary>
        /// Create optimized search pattern based on path
        /// </summary>
        private SearchPattern CreateSearchPattern()
        {
            if (string.IsNullOrEmpty(Path))
            {
                return new SearchPattern { MatchAll = true };
            }

            if (IsWildcardPattern(Path))
            {
                var regex = GetCachedRegex(Path);
                return new SearchPattern { Regex = regex, IsWildcard = true };
            }

            return new SearchPattern { ExactMatch = Path, IsExact = true };
        }

        /// <summary>
        /// Check if address matches search criteria efficiently
        /// </summary>
        private bool MatchesSearchCriteria(string address, SearchPattern pattern)
        {
            if (pattern.MatchAll) return true;
            if (pattern.IsExact) return address.IndexOf(pattern.ExactMatch, StringComparison.OrdinalIgnoreCase) >= 0;
            if (pattern.IsWildcard) return pattern.Regex?.IsMatch(address) ?? false;
            
            return false;
        }

        /// <summary>
        /// Get cached compiled regex for wildcard patterns
        /// </summary>
        private Regex GetCachedRegex(string wildcardPattern)
        {
            if (_regexCache.TryGetValue(wildcardPattern, out var cachedRegex))
            {
                return cachedRegex;
            }

            try
            {
                var regexPattern = ConvertWildcardToRegex(wildcardPattern);
                var regex = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                _regexCache[wildcardPattern] = regex;
                return regex;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Convert wildcard pattern to optimized regex
        /// </summary>
        private string ConvertWildcardToRegex(string wildcard)
        {
            var escaped = Regex.Escape(wildcard);
            escaped = escaped.Replace(@"\*", ".*");
            escaped = escaped.Replace(@"\?", ".");
            return "^" + escaped + "$";
        }

        /// <summary>
        /// Extract address from location efficiently
        /// </summary>
        private string GetLocationAddress(IResourceLocation location)
        {
            return !string.IsNullOrEmpty(location.PrimaryKey) 
                ? location.PrimaryKey 
                : location.InternalId;
        }

        /// <summary>
        /// Check if path contains wildcard characters
        /// </summary>
        private bool IsWildcardPattern(string path)
        {
            return !string.IsNullOrEmpty(path) && (path.Contains("*") || path.Contains("?"));
        }

        /// <summary>
        /// Generate cache key for current search
        /// </summary>
        private string GenerateCacheKey()
        {
            return $"{Path ?? "empty"}:{typeof(T).Name}";
        }

        /// <summary>
        /// Check if result is already cached and valid
        /// </summary>
        private bool IsResultCached(string cacheKey)
        {
            return _locationCache.TryGetValue(cacheKey, out _) &&
                   _cacheTimestamps.TryGetValue(cacheKey, out var timestamp) &&
                   DateTime.UtcNow - timestamp < CacheExpiry;
        }

        /// <summary>
        /// Check if current result came from cache
        /// </summary>
        private bool WasCacheHit()
        {
            var cacheKey = GenerateCacheKey();
            return IsResultCached(cacheKey);
        }

        /// <summary>
        /// Clear all static caches
        /// </summary>
        public static void ClearAllCaches()
        {
            _locationCache.Clear();
            _cacheTimestamps.Clear();
            _regexCache.Clear();
            _catalogCache.Clear();
        }

        public override void Cancel()
        {
            _isCancelled = true;
            _logAction?.Invoke($"[Addressable] Cancelling locate operation for '{Path}'");
            base.Cancel();
        }

        /// <summary>
        /// Internal search pattern structure for efficient matching
        /// </summary>
        private struct SearchPattern
        {
            public bool MatchAll;
            public bool IsExact;
            public bool IsWildcard;
            public string ExactMatch;
            public Regex Regex;
        }
    }
}