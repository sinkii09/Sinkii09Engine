using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Sinkii09.Engine.Services.Performance
{
    /// <summary>
    /// Memory-efficient resolution caching with LRU eviction policy
    /// Targets 50% improvement in service resolution performance (<0.5ms for cached dependencies)
    /// </summary>
    public class ServiceResolutionCache
    {
        private readonly struct CacheEntry
        {
            public readonly object Instance;
            public readonly long AccessTick;
            public readonly int AccessCount;

            public CacheEntry(object instance, long accessTick, int accessCount)
            {
                Instance = instance;
                AccessTick = accessTick;
                AccessCount = accessCount;
            }

            public CacheEntry WithAccess(long tick) => new CacheEntry(Instance, tick, AccessCount + 1);
        }

        private readonly ConcurrentDictionary<Type, CacheEntry> _cache;
        private readonly LinkedList<Type> _accessOrder;
        private readonly object _evictionLock = new object();
        private readonly int _maxCacheSize;
        private readonly bool _enableMetrics;
        private long _currentTick;

        // Performance metrics
        private long _cacheHits;
        private long _cacheMisses;
        private long _evictions;
        private long _memoryUsageBytes;

        /// <summary>
        /// Cache hit ratio as a percentage (0-1)
        /// </summary>
        public double HitRatio => _cacheHits + _cacheMisses > 0 ? (double)_cacheHits / (_cacheHits + _cacheMisses) : 0;

        /// <summary>
        /// Current number of cached entries
        /// </summary>
        public int Count => _cache.Count;

        /// <summary>
        /// Current estimated memory usage in bytes
        /// </summary>
        public long MemoryUsageBytes => _memoryUsageBytes;

        /// <summary>
        /// Total cache evictions performed
        /// </summary>
        public long EvictionCount => _evictions;

        public ServiceResolutionCache(int maxCacheSize = 1000, bool enableMetrics = true)
        {
            _maxCacheSize = maxCacheSize;
            _enableMetrics = enableMetrics;
            _cache = new ConcurrentDictionary<Type, CacheEntry>(Environment.ProcessorCount * 2, maxCacheSize);
            _accessOrder = new LinkedList<Type>();
            _currentTick = 0;
        }

        /// <summary>
        /// Try to get a cached service instance
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet<T>(out T instance) where T : class
        {
            if (TryGet(typeof(T), out var obj))
            {
                instance = obj as T;
                return instance != null;
            }

            instance = null;
            return false;
        }

        /// <summary>
        /// Try to get a cached service instance by type
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet(Type serviceType, out object instance)
        {
            if (_cache.TryGetValue(serviceType, out var entry))
            {
                // Update access information
                var newTick = System.Threading.Interlocked.Increment(ref _currentTick);
                var updatedEntry = entry.WithAccess(newTick);
                _cache.TryUpdate(serviceType, updatedEntry, entry);

                // Update LRU order
                UpdateAccessOrder(serviceType);

                instance = entry.Instance;

                if (_enableMetrics)
                {
                    System.Threading.Interlocked.Increment(ref _cacheHits);
                }

                return true;
            }

            instance = null;
            if (_enableMetrics)
            {
                System.Threading.Interlocked.Increment(ref _cacheMisses);
            }

            return false;
        }

        /// <summary>
        /// Cache a service instance
        /// </summary>
        public void Set<T>(T instance) where T : class
        {
            Set(typeof(T), instance);
        }

        /// <summary>
        /// Cache a service instance by type
        /// </summary>
        public void Set(Type serviceType, object instance)
        {
            if (instance == null)
                return;

            var tick = System.Threading.Interlocked.Increment(ref _currentTick);
            var entry = new CacheEntry(instance, tick, 1);

            // Check if we need to evict before adding
            if (_cache.Count >= _maxCacheSize)
            {
                EvictLeastRecentlyUsed();
            }

            _cache.AddOrUpdate(serviceType, entry, (key, existing) => entry);

            // Update memory tracking
            if (_enableMetrics)
            {
                UpdateMemoryUsage(serviceType, instance, true);
            }

            // Update access order
            UpdateAccessOrder(serviceType);
        }

        /// <summary>
        /// Remove a specific service from cache
        /// </summary>
        public bool Remove<T>()
        {
            return Remove(typeof(T));
        }

        /// <summary>
        /// Remove a specific service from cache by type
        /// </summary>
        public bool Remove(Type serviceType)
        {
            if (_cache.TryRemove(serviceType, out var entry))
            {
                lock (_evictionLock)
                {
                    _accessOrder.Remove(serviceType);
                }

                if (_enableMetrics)
                {
                    UpdateMemoryUsage(serviceType, entry.Instance, false);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Clear all cached entries
        /// </summary>
        public void Clear()
        {
            _cache.Clear();

            lock (_evictionLock)
            {
                _accessOrder.Clear();
            }

            if (_enableMetrics)
            {
                System.Threading.Interlocked.Exchange(ref _memoryUsageBytes, 0);
            }
        }

        /// <summary>
        /// Preload critical services for optimal performance
        /// </summary>
        public void PrewarmCache(IEnumerable<Type> criticalServices, IServiceContainer container)
        {
            foreach (var serviceType in criticalServices)
            {
                try
                {
                    if (container.TryResolve(serviceType, out var instance))
                    {
                        Set(serviceType, instance);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to prewarm cache for {serviceType.Name}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Get cache performance statistics
        /// </summary>
        public ServiceResolutionCacheStatistics GetStatistics()
        {
            return new ServiceResolutionCacheStatistics
            {
                HitRatio = HitRatio,
                TotalHits = _cacheHits,
                TotalMisses = _cacheMisses,
                CacheSize = Count,
                MaxCacheSize = _maxCacheSize,
                MemoryUsageBytes = _memoryUsageBytes,
                EvictionCount = _evictions
            };
        }

        /// <summary>
        /// Evict least recently used entries to make room
        /// </summary>
        private void EvictLeastRecentlyUsed()
        {
            lock (_evictionLock)
            {
                // Evict 10% of cache when full to avoid frequent evictions
                var evictCount = Math.Max(1, _maxCacheSize / 10);
                var evicted = 0;

                var node = _accessOrder.First;
                while (node != null && evicted < evictCount)
                {
                    var next = node.Next;
                    var typeToEvict = node.Value;

                    if (_cache.TryRemove(typeToEvict, out var entry))
                    {
                        _accessOrder.Remove(node);
                        evicted++;

                        if (_enableMetrics)
                        {
                            UpdateMemoryUsage(typeToEvict, entry.Instance, false);
                            System.Threading.Interlocked.Increment(ref _evictions);
                        }
                    }

                    node = next;
                }
            }
        }

        /// <summary>
        /// Update the LRU access order
        /// </summary>
        private void UpdateAccessOrder(Type serviceType)
        {
            lock (_evictionLock)
            {
                // Remove from current position
                _accessOrder.Remove(serviceType);
                // Add to end (most recently used)
                _accessOrder.AddLast(serviceType);
            }
        }

        /// <summary>
        /// Update memory usage tracking
        /// </summary>
        private void UpdateMemoryUsage(Type serviceType, object instance, bool adding)
        {
            if (!_enableMetrics)
                return;

            // Rough estimation of memory usage
            var estimatedSize = EstimateObjectSize(instance);

            if (adding)
            {
                System.Threading.Interlocked.Add(ref _memoryUsageBytes, estimatedSize);
            }
            else
            {
                System.Threading.Interlocked.Add(ref _memoryUsageBytes, -estimatedSize);
            }
        }

        /// <summary>
        /// Estimate the memory size of an object (rough approximation)
        /// </summary>
        private static long EstimateObjectSize(object instance)
        {
            if (instance == null)
                return 0;

            var type = instance.GetType();

            // Base object overhead
            long size = IntPtr.Size == 8 ? 24 : 12; // 64-bit vs 32-bit

            // Add estimated field sizes
            var fields = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            foreach (var field in fields)
            {
                size += GetTypeSize(field.FieldType);
            }

            return size;
        }

        /// <summary>
        /// Get estimated size of a type
        /// </summary>
        private static long GetTypeSize(Type type)
        {
            if (type == typeof(bool) || type == typeof(byte) || type == typeof(sbyte))
                return 1;
            if (type == typeof(short) || type == typeof(ushort) || type == typeof(char))
                return 2;
            if (type == typeof(int) || type == typeof(uint) || type == typeof(float))
                return 4;
            if (type == typeof(long) || type == typeof(ulong) || type == typeof(double))
                return 8;
            if (type == typeof(decimal))
                return 16;

            // Reference types
            return IntPtr.Size;
        }
    }

    /// <summary>
    /// Cache performance statistics
    /// </summary>
    public struct ServiceResolutionCacheStatistics
    {
        public double HitRatio { get; set; }
        public long TotalHits { get; set; }
        public long TotalMisses { get; set; }
        public int CacheSize { get; set; }
        public int MaxCacheSize { get; set; }
        public long MemoryUsageBytes { get; set; }
        public long EvictionCount { get; set; }

        public TimeSpan AverageResolutionTime { get; set; }
        public TimeSpan MaxResolutionTime { get; set; }
        public DateTime LastCacheOptimization { get; set; }
        public override string ToString()
        {
            return $"Resolution Cache: {HitRatio:P1} hit ratio, {CacheSize}/{MaxCacheSize} entries, " +
                   $"{MemoryUsageBytes / 1024.0:F1}KB memory, {EvictionCount} evictions";
        }
    }
}