using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Optimized folder locator for Addressable assets using catalog-based discovery with caching
    /// </summary>
    public class AddressableFolderLocator : LocateFolderRunner
    {
        private readonly Action<string> _logAction;
        private bool _isCancelled = false;

        // Static caching for performance - shared across all instances
        private static readonly ConcurrentDictionary<string, List<Folder>> _folderCache = new();
        private static readonly ConcurrentDictionary<string, DateTime> _cacheTimestamps = new();
        private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5);
        private static readonly object _catalogLock = new object();

        public AddressableFolderLocator(
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
                _logAction?.Invoke($"[Addressable] Locating folders for path: '{Path}'");

                if (_isCancelled)
                {
                    SetResult(new List<Folder>());
                    return;
                }

                // Use optimized cached discovery
                var folders = await GetOptimizedFoldersAsync();

                if (_isCancelled)
                {
                    SetResult(new List<Folder>());
                    return;
                }

                _logAction?.Invoke($"[Addressable] Located {folders.Count} folders for '{Path}' (cached: {IsCached()})");
                SetResult(folders);
            }
            catch (Exception ex)
            {
                _logAction?.Invoke($"[Addressable] Failed to locate folders for '{Path}': {ex.Message}");
                SetResult(new List<Folder>());
                throw;
            }
        }

        /// <summary>
        /// Get folders using optimized caching strategy
        /// </summary>
        private async UniTask<List<Folder>> GetOptimizedFoldersAsync()
        {
            var cacheKey = Path ?? "root";
            
            // Check cache first
            if (IsCacheValid(cacheKey))
            {
                return new List<Folder>(_folderCache[cacheKey]);
            }

            // Discover folders efficiently
            var folders = await DiscoverFoldersFromCatalogAsync();
            
            // Cache results
            _folderCache[cacheKey] = folders;
            _cacheTimestamps[cacheKey] = DateTime.UtcNow;
            
            return new List<Folder>(folders);
        }

        /// <summary>
        /// Fast folder discovery using Addressables catalog without loading assets
        /// </summary>
        private async UniTask<List<Folder>> DiscoverFoldersFromCatalogAsync()
        {
            var folders = new HashSet<Folder>(); // Use HashSet to avoid duplicates

            try
            {
                await UniTask.SwitchToMainThread(); // Ensure we're on main thread for Addressables

                lock (_catalogLock)
                {
                    // Get all resource locators (catalogs)
                    foreach (var locator in Addressables.ResourceLocators)
                    {
                        if (_isCancelled) break;

                        // Extract folder structure from catalog keys
                        ExtractFoldersFromKeys(locator.Keys, folders);
                    }
                }

                _logAction?.Invoke($"[Addressable] Discovered {folders.Count} unique folders from catalog");
            }
            catch (Exception ex)
            {
                _logAction?.Invoke($"[Addressable] Catalog-based discovery failed: {ex.Message}");
                
                // Fallback: create minimal virtual folders
                CreateFallbackFolders(folders);
            }

            return folders.ToList();
        }

        /// <summary>
        /// Extract folder structure from catalog keys efficiently
        /// </summary>
        private void ExtractFoldersFromKeys(IEnumerable<object> keys, HashSet<Folder> folders)
        {
            var processedPaths = new HashSet<string>();

            foreach (var key in keys)
            {
                if (_isCancelled) break;

                var keyString = key?.ToString();
                if (string.IsNullOrEmpty(keyString)) continue;

                // Skip if this is a label (labels are usually short and don't contain '/')
                if (!keyString.Contains("/")) continue;

                // Extract hierarchical folder structure
                ExtractHierarchicalFolders(keyString, folders, processedPaths);
            }
        }

        /// <summary>
        /// Extract folder hierarchy from a single address path
        /// </summary>
        private void ExtractHierarchicalFolders(string address, HashSet<Folder> folders, HashSet<string> processedPaths)
        {
            var parts = address.Split('/');
            
            // Create folder for each level of hierarchy (excluding the asset name)
            for (int i = 0; i < parts.Length - 1; i++)
            {
                var folderPath = string.Join("/", parts, 0, i + 1);
                
                // Skip if already processed or doesn't match our path filter
                if (processedPaths.Contains(folderPath)) continue;
                if (!MatchesPathFilter(folderPath)) continue;

                processedPaths.Add(folderPath);
                folders.Add(new Folder(folderPath));
            }
        }

        /// <summary>
        /// Check if a folder path matches our search criteria
        /// </summary>
        private bool MatchesPathFilter(string folderPath)
        {
            if (string.IsNullOrEmpty(Path)) return true; // Show all if no filter
            
            // Match if the folder path starts with our search path
            // or if our search path is contained within the folder path
            return folderPath.StartsWith(Path, StringComparison.OrdinalIgnoreCase) ||
                   (Path.Length < folderPath.Length && folderPath.Contains(Path));
        }

        /// <summary>
        /// Create fallback virtual folders if catalog discovery fails
        /// </summary>
        private void CreateFallbackFolders(HashSet<Folder> folders)
        {
            var fallbackFolders = new[]
            {
                "UI", "Audio", "Textures", "Prefabs", "Materials", 
                "Scripts", "Scenes", "Animations", "Data"
            };

            foreach (var folderName in fallbackFolders)
            {
                var folderPath = string.IsNullOrEmpty(Path) ? folderName : $"{Path}/{folderName}";
                if (MatchesPathFilter(folderPath))
                {
                    folders.Add(new Folder(folderPath));
                }
            }

            _logAction?.Invoke($"[Addressable] Created {folders.Count} fallback folders");
        }

        /// <summary>
        /// Check if cache entry is valid
        /// </summary>
        private bool IsCacheValid(string cacheKey)
        {
            return _folderCache.ContainsKey(cacheKey) &&
                   _cacheTimestamps.TryGetValue(cacheKey, out var timestamp) &&
                   DateTime.UtcNow - timestamp < CacheExpiry;
        }

        /// <summary>
        /// Check if current request used cache
        /// </summary>
        private bool IsCached()
        {
            var cacheKey = Path ?? "root";
            return IsCacheValid(cacheKey);
        }

        /// <summary>
        /// Clear the static cache (useful for testing or when Addressables catalog changes)
        /// </summary>
        public static void ClearCache()
        {
            _folderCache.Clear();
            _cacheTimestamps.Clear();
        }


        public override void Cancel()
        {
            _isCancelled = true;
            _logAction?.Invoke($"[Addressable] Cancelling folder locate operation for '{Path}'");
            base.Cancel();
        }
    }
}