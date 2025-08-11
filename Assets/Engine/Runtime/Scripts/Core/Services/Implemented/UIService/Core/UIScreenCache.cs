using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// LRU cache for UI screens with automatic eviction and memory pressure response
    /// Simple and efficient implementation specifically for UIScreen caching
    /// </summary>
    public class UIScreenCache : IDisposable
    {
        private readonly Dictionary<UIScreenType, UIScreen> _cache;
        private readonly LinkedList<UIScreenType> _accessOrder;
        private readonly int _maxCacheSize;
        private readonly bool _enableCaching;
        
        // Statistics
        private long _hits;
        private long _misses;
        private long _evictions;

        /// <summary>
        /// Current number of cached screens
        /// </summary>
        public int Count => _cache.Count;

        /// <summary>
        /// Cache hit ratio as a percentage (0-1)
        /// </summary>
        public double HitRatio => _hits + _misses > 0 ? (double)_hits / (_hits + _misses) : 0.0;

        /// <summary>
        /// Total cache evictions performed
        /// </summary>
        public long EvictionCount => _evictions;

        public UIScreenCache(int maxCacheSize, bool enableCaching = true)
        {
            _maxCacheSize = Math.Max(1, maxCacheSize);
            _enableCaching = enableCaching;
            _cache = new Dictionary<UIScreenType, UIScreen>();
            _accessOrder = new LinkedList<UIScreenType>();

            Debug.Log($"[UIScreenCache] Initialized with maxSize={_maxCacheSize}, caching={_enableCaching}");
        }

        /// <summary>
        /// Try to get a cached screen instance
        /// </summary>
        public bool TryGet(UIScreenType screenType, out UIScreen screen)
        {
            if (!_enableCaching)
            {
                screen = null;
                _misses++;
                return false;
            }

            if (_cache.TryGetValue(screenType, out screen))
            {
                // Move to front of access order (most recently used)
                _accessOrder.Remove(screenType);
                _accessOrder.AddFirst(screenType);
                _hits++;
                return true;
            }

            _misses++;
            return false;
        }

        /// <summary>
        /// Cache a screen instance
        /// </summary>
        public void Put(UIScreenType screenType, UIScreen screen)
        {
            if (!_enableCaching || screen == null)
                return;

            // Remove existing entry if present
            if (_cache.ContainsKey(screenType))
            {
                _accessOrder.Remove(screenType);
            }

            // Add new entry
            _cache[screenType] = screen;
            _accessOrder.AddFirst(screenType);

            // Evict least recently used if over capacity
            while (_cache.Count > _maxCacheSize && _accessOrder.Count > 0)
            {
                var lru = _accessOrder.Last.Value;
                _accessOrder.RemoveLast();
                
                if (_cache.TryGetValue(lru, out var lruScreen))
                {
                    _cache.Remove(lru);
                    
                    // Destroy the evicted screen GameObject
                    if (lruScreen != null && lruScreen.gameObject != null)
                    {
                        UnityEngine.Object.Destroy(lruScreen.gameObject);
                    }
                    
                    _evictions++;
                    Debug.Log($"[UIScreenCache] Evicted LRU screen: {lru}");
                }
            }

            Debug.Log($"[UIScreenCache] Cached screen: {screenType}");
        }

        /// <summary>
        /// Remove a specific screen from cache
        /// </summary>
        public bool Remove(UIScreenType screenType)
        {
            if (!_enableCaching)
                return false;

            if (_cache.TryGetValue(screenType, out var screen))
            {
                _cache.Remove(screenType);
                _accessOrder.Remove(screenType);
                
                // Destroy the screen GameObject
                if (screen != null && screen.gameObject != null)
                {
                    UnityEngine.Object.Destroy(screen.gameObject);
                }
                
                Debug.Log($"[UIScreenCache] Removed screen from cache: {screenType}");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Clear all cached screens
        /// </summary>
        public void Clear()
        {
            if (!_enableCaching) return;

            // Destroy all cached screen GameObjects
            foreach (var screen in _cache.Values)
            {
                if (screen != null && screen.gameObject != null)
                {
                    UnityEngine.Object.Destroy(screen.gameObject);
                }
            }

            _cache.Clear();
            _accessOrder.Clear();
            Debug.Log("[UIScreenCache] Cleared all cached screens");
        }

        /// <summary>
        /// Respond to memory pressure by evicting screens
        /// </summary>
        public void RespondToMemoryPressure(float pressureLevel)
        {
            if (!_enableCaching) return;

            int targetSize;
            if (pressureLevel > 0.8f)
            {
                // High pressure - clear 75% of cache
                targetSize = _cache.Count / 4;
            }
            else if (pressureLevel > 0.6f)
            {
                // Medium pressure - clear 50% of cache
                targetSize = _cache.Count / 2;
            }
            else if (pressureLevel > 0.4f)
            {
                // Low pressure - clear 25% of cache
                targetSize = (_cache.Count * 3) / 4;
            }
            else
            {
                return; // No action needed for low pressure
            }

            EvictToSize(targetSize);
            Debug.Log($"[UIScreenCache] Memory pressure eviction completed, size: {_cache.Count}");
        }

        private void EvictToSize(int targetSize)
        {
            while (_cache.Count > targetSize && _accessOrder.Count > 0)
            {
                var lru = _accessOrder.Last.Value;
                Remove(lru);
            }
        }

        public void Dispose()
        {
            Clear();
        }
    }
}