using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Services.Performance
{
    /// <summary>
    /// Object pooling for frequently created service-related objects
    /// Reduces allocation pressure and improves memory performance
    /// </summary>
    public class ServiceObjectPool<T> where T : class, new()
    {
        private readonly ConcurrentQueue<T> _pool;
        private readonly Func<T> _factory;
        private readonly Action<T> _resetAction;
        private readonly int _maxPoolSize;
        private readonly bool _autoScale;
        private int _currentSize;
        private int _totalCreated;
        private int _totalReused;
        private long _memoryPressureThreshold;
        
        /// <summary>
        /// Current number of objects in the pool
        /// </summary>
        public int Count => _currentSize;
        
        /// <summary>
        /// Maximum pool size
        /// </summary>
        public int MaxPoolSize => _maxPoolSize;
        
        /// <summary>
        /// Total objects created
        /// </summary>
        public int TotalCreated => _totalCreated;
        
        /// <summary>
        /// Total objects reused from pool
        /// </summary>
        public int TotalReused => _totalReused;
        
        /// <summary>
        /// Pool efficiency ratio (reused / created)
        /// </summary>
        public double EfficiencyRatio => _totalCreated > 0 ? (double)_totalReused / _totalCreated : 0;
        
        public ServiceObjectPool(int maxPoolSize = 100, bool autoScale = true, Func<T> factory = null, Action<T> resetAction = null)
        {
            _maxPoolSize = maxPoolSize;
            _autoScale = autoScale;
            _factory = factory ?? (() => new T());
            _resetAction = resetAction;
            _pool = new ConcurrentQueue<T>();
            _memoryPressureThreshold = (long)(GC.GetTotalMemory(false) * 0.8); // 80% threshold
        }
        
        /// <summary>
        /// Get an object from the pool or create a new one
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get()
        {
            if (_pool.TryDequeue(out var item))
            {
                Interlocked.Decrement(ref _currentSize);
                Interlocked.Increment(ref _totalReused);
                return item;
            }
            
            // Create new object
            var newItem = _factory();
            Interlocked.Increment(ref _totalCreated);
            return newItem;
        }
        
        /// <summary>
        /// Return an object to the pool
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Return(T item)
        {
            if (item == null)
                return;
                
            // Check memory pressure
            if (IsUnderMemoryPressure())
            {
                // Don't pool during high memory pressure
                return;
            }
            
            // Check pool size limits
            if (_currentSize >= _maxPoolSize && !_autoScale)
            {
                return; // Pool is full
            }
            
            // Auto-scale check
            if (_autoScale && _currentSize >= _maxPoolSize)
            {
                var newLimit = Math.Min(_maxPoolSize * 2, 1000); // Cap at 1000
                if (_currentSize >= newLimit)
                    return;
            }
            
            // Reset object if reset action provided
            try
            {
                _resetAction?.Invoke(item);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error resetting pooled object: {ex.Message}");
                return; // Don't pool if reset fails
            }
            
            _pool.Enqueue(item);
            Interlocked.Increment(ref _currentSize);
        }
        
        /// <summary>
        /// Prewarm the pool with objects
        /// </summary>
        public void Prewarm(int count)
        {
            count = Math.Min(count, _maxPoolSize);
            
            for (int i = 0; i < count; i++)
            {
                var item = _factory();
                _resetAction?.Invoke(item);
                _pool.Enqueue(item);
                Interlocked.Increment(ref _currentSize);
            }
        }
        
        /// <summary>
        /// Clear all objects from the pool
        /// </summary>
        public void Clear()
        {
            while (_pool.TryDequeue(out var item))
            {
                if (item is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error disposing pooled object: {ex.Message}");
                    }
                }
            }
            
            Interlocked.Exchange(ref _currentSize, 0);
        }
        
        /// <summary>
        /// Trim pool to target size
        /// </summary>
        public void Trim(int targetSize = -1)
        {
            if (targetSize < 0)
                targetSize = _maxPoolSize / 2; // Default to half capacity
                
            while (_currentSize > targetSize && _pool.TryDequeue(out var item))
            {
                Interlocked.Decrement(ref _currentSize);
                
                if (item is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error disposing trimmed object: {ex.Message}");
                    }
                }
            }
        }
        
        /// <summary>
        /// Check if system is under memory pressure
        /// </summary>
        private bool IsUnderMemoryPressure()
        {
            var currentMemory = GC.GetTotalMemory(false);
            return currentMemory > _memoryPressureThreshold;
        }
        
        /// <summary>
        /// Get pool statistics
        /// </summary>
        public PoolStatistics GetStatistics()
        {
            return new PoolStatistics
            {
                CurrentSize = _currentSize,
                MaxPoolSize = _maxPoolSize,
                TotalCreated = _totalCreated,
                TotalReused = _totalReused,
                EfficiencyRatio = EfficiencyRatio,
                EstimatedMemoryUsage = EstimateMemoryUsage(),
                IsAutoScaling = _autoScale
            };
        }
        
        /// <summary>
        /// Estimate memory usage of the pool
        /// </summary>
        private long EstimateMemoryUsage()
        {
            if (_currentSize == 0)
                return 0;
                
            // Rough estimation - object header + fields
            var estimatedObjectSize = IntPtr.Size == 8 ? 24 : 12; // 64-bit vs 32-bit
            return _currentSize * estimatedObjectSize;
        }
    }
    
    /// <summary>
    /// Generic object pool manager for multiple types
    /// </summary>
    public static class ServiceObjectPoolManager
    {
        private static readonly ConcurrentDictionary<Type, object> _pools = new ConcurrentDictionary<Type, object>();
        private static Timer _cleanupTimer;
        private static readonly object _timerLock = new object();
        
        static ServiceObjectPoolManager()
        {
            InitializeCleanupTimer();
        }
        
        /// <summary>
        /// Get or create a pool for the specified type
        /// </summary>
        public static ServiceObjectPool<T> GetPool<T>() where T : class, new()
        {
            return (ServiceObjectPool<T>)_pools.GetOrAdd(typeof(T), _ => new ServiceObjectPool<T>());
        }
        
        /// <summary>
        /// Get or create a pool with custom configuration
        /// </summary>
        public static ServiceObjectPool<T> GetPool<T>(int maxSize, bool autoScale = true, Func<T> factory = null, Action<T> resetAction = null) where T : class, new()
        {
            return (ServiceObjectPool<T>)_pools.GetOrAdd(typeof(T), _ => new ServiceObjectPool<T>(maxSize, autoScale, factory, resetAction));
        }
        
        /// <summary>
        /// Get an object from the pool
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Get<T>() where T : class, new()
        {
            return GetPool<T>().Get();
        }
        
        /// <summary>
        /// Return an object to the pool
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Return<T>(T item) where T : class, new()
        {
            GetPool<T>().Return(item);
        }
        
        /// <summary>
        /// Clear all pools
        /// </summary>
        public static void ClearAllPools()
        {
            foreach (var pool in _pools.Values)
            {
                if (pool is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                else
                {
                    // Use reflection to call Clear method
                    var clearMethod = pool.GetType().GetMethod("Clear");
                    clearMethod?.Invoke(pool, null);
                }
            }
            
            _pools.Clear();
        }
        
        /// <summary>
        /// Get statistics for all pools
        /// </summary>
        public static Dictionary<Type, PoolStatistics> GetAllStatistics()
        {
            var stats = new Dictionary<Type, PoolStatistics>();
            
            foreach (var kvp in _pools)
            {
                try
                {
                    var getStatsMethod = kvp.Value.GetType().GetMethod("GetStatistics");
                    if (getStatsMethod != null)
                    {
                        var poolStats = (PoolStatistics)getStatsMethod.Invoke(kvp.Value, null);
                        stats[kvp.Key] = poolStats;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error getting statistics for pool {kvp.Key.Name}: {ex.Message}");
                }
            }
            
            return stats;
        }
        
        /// <summary>
        /// Trim all pools to reduce memory usage
        /// </summary>
        public static void TrimAllPools()
        {
            foreach (var pool in _pools.Values)
            {
                try
                {
                    var trimMethod = pool.GetType().GetMethod("Trim", new Type[] { typeof(int) });
                    trimMethod?.Invoke(pool, new object[] { -1 }); // Use default trim size
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error trimming pool: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Initialize cleanup timer for periodic maintenance
        /// </summary>
        private static void InitializeCleanupTimer()
        {
            lock (_timerLock)
            {
                if (_cleanupTimer == null)
                {
                    _cleanupTimer = new Timer(PerformCleanup, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
                }
            }
        }
        
        /// <summary>
        /// Periodic cleanup callback
        /// </summary>
        private static void PerformCleanup(object state)
        {
            try
            {
                // Check memory pressure and trim if needed
                var totalMemory = GC.GetTotalMemory(false);
                var memoryThreshold = totalMemory * 0.8; // 80% threshold
                
                if (totalMemory > memoryThreshold)
                {
                    TrimAllPools();
                    GC.Collect(0, GCCollectionMode.Optimized);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during pool cleanup: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Dispose cleanup timer
        /// </summary>
        public static void Dispose()
        {
            lock (_timerLock)
            {
                _cleanupTimer?.Dispose();
                _cleanupTimer = null;
            }
            
            ClearAllPools();
        }
    }
    
    /// <summary>
    /// Pool statistics information
    /// </summary>
    public struct PoolStatistics
    {
        public int CurrentSize { get; set; }
        public int MaxPoolSize { get; set; }
        public int TotalCreated { get; set; }
        public int TotalReused { get; set; }
        public double EfficiencyRatio { get; set; }
        public long EstimatedMemoryUsage { get; set; }
        public bool IsAutoScaling { get; set; }
        
        public override string ToString()
        {
            return $"Pool: {CurrentSize}/{MaxPoolSize} objects, {EfficiencyRatio:P1} efficiency, " +
                   $"{EstimatedMemoryUsage / 1024.0:F1}KB memory";
        }
    }
    
    /// <summary>
    /// Pooled object wrapper that automatically returns to pool when disposed
    /// </summary>
    public struct PooledObject<T> : IDisposable where T : class, new()
    {
        private readonly T _object;
        private readonly ServiceObjectPool<T> _pool;
        private bool _disposed;
        
        public T Object => _disposed ? null : _object;
        
        public PooledObject(T obj, ServiceObjectPool<T> pool)
        {
            _object = obj;
            _pool = pool;
            _disposed = false;
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _pool?.Return(_object);
                _disposed = true;
            }
        }
        
        public static implicit operator T(PooledObject<T> pooled) => pooled.Object;
    }
}