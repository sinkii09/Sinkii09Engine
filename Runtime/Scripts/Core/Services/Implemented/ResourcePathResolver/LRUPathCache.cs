using Sinkii09.Engine.Services.Performance;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// High-performance LRU cache for resource path resolution with thread-safe operations
    /// Implements efficient O(1) access, insertion, and eviction
    /// </summary>
    internal class LRUPathCache : IDisposable
    {
        private readonly ConcurrentDictionary<string, CachedPathEntry> _cache;
        private readonly ReaderWriterLockSlim _lruLock;
        private readonly int _maxSize;
        private readonly float _cacheEntryLifetime;
        private readonly bool _enableLRUEviction;
        
        // LRU doubly-linked list
        private CachedPathEntry _head;
        private CachedPathEntry _tail;
        private int _currentSize;
        
        // Statistics
        private long _hits;
        private long _misses;
        private long _evictions;
        private long _expirations;
        private DateTime _lastCleanup;
        
        // Cleanup configuration
        private const int CLEANUP_INTERVAL_SECONDS = 60;
        private const double CLEANUP_THRESHOLD_RATIO = 0.8;
        
        public LRUPathCache(int maxSize, float cacheEntryLifetime, bool enableLRUEviction)
        {
            _maxSize = Math.Max(1, maxSize);
            _cacheEntryLifetime = cacheEntryLifetime;
            _enableLRUEviction = enableLRUEviction;
            
            _cache = new ConcurrentDictionary<string, CachedPathEntry>();
            _lruLock = new ReaderWriterLockSlim();
            _lastCleanup = DateTime.UtcNow;
            
            // Initialize LRU sentinel nodes
            _head = new CachedPathEntry("HEAD_SENTINEL", PathPriority.Normal, TimeSpan.Zero, "HEAD");
            _tail = new CachedPathEntry("TAIL_SENTINEL", PathPriority.Normal, TimeSpan.Zero, "TAIL");
            _head.Next = _tail;
            _tail.Previous = _head;
            
            Debug.Log($"[LRUPathCache] Initialized with maxSize={_maxSize}, lifetime={_cacheEntryLifetime}s, LRU={_enableLRUEviction}");
        }
        
        #region Public API
        
        /// <summary>
        /// Attempts to get a cached path entry
        /// </summary>
        public bool TryGet(string key, out CachedPathEntry entry)
        {
            if (_cache.TryGetValue(key, out entry))
            {
                // Check if expired
                if (entry.IsExpired(_cacheEntryLifetime))
                {
                    Remove(key);
                    Interlocked.Increment(ref _expirations);
                    Interlocked.Increment(ref _misses);
                    entry = null;
                    return false;
                }
                
                // Update access tracking
                entry.UpdateAccess();
                
                // Move to front of LRU list if LRU is enabled
                if (_enableLRUEviction)
                {
                    MoveToFront(entry);
                }
                
                Interlocked.Increment(ref _hits);
                return true;
            }
            
            Interlocked.Increment(ref _misses);
            return false;
        }
        
        /// <summary>
        /// Adds or updates a cache entry
        /// </summary>
        public void Put(string key, string resolvedPath, PathPriority priority, TimeSpan resolutionTime)
        {
            var entry = new CachedPathEntry(resolvedPath, priority, resolutionTime, key);
            
            // Remove existing entry if present
            if (_cache.ContainsKey(key))
            {
                Remove(key);
            }
            
            // Add new entry
            _cache[key] = entry;
            
            if (_enableLRUEviction)
            {
                AddToFront(entry);
                
                // Evict if necessary
                if (_currentSize > _maxSize)
                {
                    EvictLeastRecentlyUsed();
                }
            }
            
            // Periodic cleanup
            TryPeriodicCleanup();
        }
        
        /// <summary>
        /// Removes a specific cache entry
        /// </summary>
        public bool Remove(string key)
        {
            if (_cache.TryRemove(key, out var entry))
            {
                if (_enableLRUEviction)
                {
                    RemoveFromLRUList(entry);
                }
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Clears all cache entries
        /// </summary>
        public void Clear()
        {
            _lruLock.EnterWriteLock();
            try
            {
                _cache.Clear();
                
                if (_enableLRUEviction)
                {
                    // Reset LRU list
                    _head.Next = _tail;
                    _tail.Previous = _head;
                    _currentSize = 0;
                }
            }
            finally
            {
                _lruLock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Gets cache statistics
        /// </summary>
        public ServiceResolutionCacheStatistics GetStatistics()
        {
            var totalRequests = _hits + _misses;
            var hitRate = totalRequests > 0 ? (double)_hits / totalRequests : 0.0;

            return new ServiceResolutionCacheStatistics
            {
                TotalHits = _hits,
                TotalMisses = _misses,
                EvictionCount = _evictions,
                HitRatio = hitRate,
                CacheSize = _cache.Count,
                MaxCacheSize = _maxSize,
                AverageResolutionTime = TimeSpan.FromSeconds(CalculateAverageEntryAge()),
                LastCacheOptimization = DateTime.UtcNow - TimeSpan.FromSeconds(GetOldestEntryAge()),
                MemoryUsageBytes = EstimateMemoryUsage()
            };
        }
        
        #endregion
        
        #region LRU Management
        
        private void MoveToFront(CachedPathEntry entry)
        {
            _lruLock.EnterWriteLock();
            try
            {
                // Remove from current position
                if (entry.Previous != null && entry.Next != null)
                {
                    entry.Previous.Next = entry.Next;
                    entry.Next.Previous = entry.Previous;
                }
                
                // Add to front
                entry.Previous = _head;
                entry.Next = _head.Next;
                _head.Next.Previous = entry;
                _head.Next = entry;
            }
            finally
            {
                _lruLock.ExitWriteLock();
            }
        }
        
        private void AddToFront(CachedPathEntry entry)
        {
            _lruLock.EnterWriteLock();
            try
            {
                entry.Previous = _head;
                entry.Next = _head.Next;
                _head.Next.Previous = entry;
                _head.Next = entry;
                _currentSize++;
            }
            finally
            {
                _lruLock.ExitWriteLock();
            }
        }
        
        private void RemoveFromLRUList(CachedPathEntry entry)
        {
            _lruLock.EnterWriteLock();
            try
            {
                if (entry.Previous != null && entry.Next != null)
                {
                    entry.Previous.Next = entry.Next;
                    entry.Next.Previous = entry.Previous;
                    _currentSize--;
                }
            }
            finally
            {
                _lruLock.ExitWriteLock();
            }
        }
        
        private void EvictLeastRecentlyUsed()
        {
            _lruLock.EnterWriteLock();
            try
            {
                while (_currentSize > _maxSize && _tail.Previous != _head)
                {
                    var lru = _tail.Previous;
                    
                    // Remove from cache
                    _cache.TryRemove(lru.CacheKey, out _);
                    
                    // Remove from LRU list
                    lru.Previous.Next = _tail;
                    _tail.Previous = lru.Previous;
                    _currentSize--;
                    
                    Interlocked.Increment(ref _evictions);
                    
                    Debug.Log($"[LRUPathCache] Evicted LRU entry: {lru.CacheKey}");
                }
            }
            finally
            {
                _lruLock.ExitWriteLock();
            }
        }
        
        #endregion
        
        #region Cleanup and Maintenance
        
        private void TryPeriodicCleanup()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastCleanup).TotalSeconds >= CLEANUP_INTERVAL_SECONDS)
            {
                _lastCleanup = now;
                PerformCleanup();
            }
        }
        
        private void PerformCleanup()
        {
            var expiredKeys = new List<string>();
            
            // Find expired entries
            foreach (var kvp in _cache)
            {
                if (kvp.Value.IsExpired(_cacheEntryLifetime))
                {
                    expiredKeys.Add(kvp.Key);
                }
            }
            
            // Remove expired entries
            foreach (var key in expiredKeys)
            {
                Remove(key);
                Interlocked.Increment(ref _expirations);
            }
            
            if (expiredKeys.Count > 0)
            {
                Debug.Log($"[LRUPathCache] Cleaned up {expiredKeys.Count} expired entries");
            }
        }
        
        /// <summary>
        /// Responds to memory pressure by evicting entries
        /// </summary>
        public void RespondToMemoryPressure(float pressureLevel)
        {
            if (pressureLevel > 0.8f)
            {
                // High pressure - clear 50% of cache
                var targetSize = _cache.Count / 2;
                EvictToSize(targetSize);
            }
            else if (pressureLevel > 0.6f)
            {
                // Medium pressure - clear 25% of cache
                var targetSize = (_cache.Count * 3) / 4;
                EvictToSize(targetSize);
            }
            else if (pressureLevel > 0.4f)
            {
                // Low pressure - just clean expired entries
                PerformCleanup();
            }
        }
        
        private void EvictToSize(int targetSize)
        {
            while (_cache.Count > targetSize && _enableLRUEviction)
            {
                EvictLeastRecentlyUsed();
            }
            
            Debug.Log($"[LRUPathCache] Memory pressure eviction completed, size: {_cache.Count}");
        }
        
        #endregion
        
        #region Statistics Helpers
        
        private double CalculateAverageEntryAge()
        {
            if (_cache.IsEmpty) return 0.0;
            
            var totalAge = _cache.Values.Sum(entry => entry.AgeInSeconds);
            return totalAge / _cache.Count;
        }
        
        private double GetOldestEntryAge()
        {
            if (_cache.IsEmpty) return 0.0;
            
            return _cache.Values.Max(entry => entry.AgeInSeconds);
        }
        
        private long EstimateMemoryUsage()
        {
            // Rough estimate: 100 bytes per entry (strings + overhead)
            return _cache.Count * 100L;
        }
        
        #endregion
        
        #region IDisposable
        
        public void Dispose()
        {
            _lruLock?.Dispose();
            _cache?.Clear();
        }
        
        #endregion
    }
}