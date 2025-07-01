using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Services.Performance
{
    /// <summary>
    /// Manages weak references for optional service dependencies to prevent memory leaks
    /// Automatically cleans up dead references and provides efficient access patterns
    /// </summary>
    public class WeakReferenceManager : IDisposable
    {
        private readonly ConcurrentDictionary<Type, ConcurrentBag<WeakReference<object>>> _weakReferences;
        private readonly ConcurrentDictionary<object, WeakReference<object>> _objectToRef;
        private readonly Timer _cleanupTimer;
        private readonly object _cleanupLock = new object();
        
        // Performance metrics
        private long _totalRegistrations;
        private long _totalLookups;
        private long _successfulLookups;
        private long _cleanupCycles;
        private long _referencesCollected;
        
        /// <summary>
        /// Total number of references registered
        /// </summary>
        public long TotalRegistrations => _totalRegistrations;
        
        /// <summary>
        /// Total number of lookup attempts
        /// </summary>
        public long TotalLookups => _totalLookups;
        
        /// <summary>
        /// Successful lookup ratio
        /// </summary>
        public double SuccessRatio => _totalLookups > 0 ? (double)_successfulLookups / _totalLookups : 0;
        
        /// <summary>
        /// Number of cleanup cycles performed
        /// </summary>
        public long CleanupCycles => _cleanupCycles;
        
        /// <summary>
        /// Number of dead references collected
        /// </summary>
        public long ReferencesCollected => _referencesCollected;
        
        public WeakReferenceManager()
        {
            _weakReferences = new ConcurrentDictionary<Type, ConcurrentBag<WeakReference<object>>>();
            _objectToRef = new ConcurrentDictionary<object, WeakReference<object>>();
            
            // Setup cleanup timer - run every 2 minutes
            _cleanupTimer = new Timer(PerformCleanup, null, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(2));
        }
        
        /// <summary>
        /// Register a weak reference for an optional dependency
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RegisterWeakDependency<T>(T service) where T : class
        {
            RegisterWeakDependency(typeof(T), service);
        }
        
        /// <summary>
        /// Register a weak reference for an optional dependency by type
        /// </summary>
        public void RegisterWeakDependency(Type serviceType, object service)
        {
            if (service == null)
                return;
                
            var weakRef = new WeakReference<object>(service);
            
            // Add to type-based collection
            var bag = _weakReferences.GetOrAdd(serviceType, _ => new ConcurrentBag<WeakReference<object>>());
            bag.Add(weakRef);
            
            // Add to object-based collection for reverse lookup
            _objectToRef.TryAdd(service, weakRef);
            
            Interlocked.Increment(ref _totalRegistrations);
        }
        
        /// <summary>
        /// Try to get a live reference to an optional dependency
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetWeakDependency<T>(out T service) where T : class
        {
            Interlocked.Increment(ref _totalLookups);
            
            if (TryGetWeakDependency(typeof(T), out var obj) && obj is T typedService)
            {
                service = typedService;
                Interlocked.Increment(ref _successfulLookups);
                return true;
            }
            
            service = null;
            return false;
        }
        
        /// <summary>
        /// Try to get a live reference to an optional dependency by type
        /// </summary>
        public bool TryGetWeakDependency(Type serviceType, out object service)
        {
            service = null;
            
            if (!_weakReferences.TryGetValue(serviceType, out var bag))
                return false;
                
            // Try to find a live reference
            foreach (var weakRef in bag)
            {
                if (weakRef.TryGetTarget(out var target))
                {
                    service = target;
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Get all live references of a specific type
        /// </summary>
        public IEnumerable<T> GetAllWeakDependencies<T>() where T : class
        {
            if (!_weakReferences.TryGetValue(typeof(T), out var bag))
                return Enumerable.Empty<T>();
                
            var liveReferences = new List<T>();
            
            foreach (var weakRef in bag)
            {
                if (weakRef.TryGetTarget(out var target) && target is T typedTarget)
                {
                    liveReferences.Add(typedTarget);
                }
            }
            
            return liveReferences;
        }
        
        /// <summary>
        /// Check if a service has been garbage collected
        /// </summary>
        public bool IsServiceAlive(object service)
        {
            if (service == null)
                return false;
                
            if (_objectToRef.TryGetValue(service, out var weakRef))
            {
                return weakRef.TryGetTarget(out _);
            }
            
            return false; // Not tracked
        }
        
        /// <summary>
        /// Remove a specific service from weak reference tracking
        /// </summary>
        public bool RemoveWeakDependency<T>(T service) where T : class
        {
            return RemoveWeakDependency(typeof(T), service);
        }
        
        /// <summary>
        /// Remove a specific service from weak reference tracking by type
        /// </summary>
        public bool RemoveWeakDependency(Type serviceType, object service)
        {
            if (service == null)
                return false;
                
            var removed = _objectToRef.TryRemove(service, out var weakRef);
            
            // Note: We don't remove from the ConcurrentBag as it doesn't support removal
            // Dead references will be cleaned up during the next cleanup cycle
            
            return removed;
        }
        
        /// <summary>
        /// Get count of live references for a specific type
        /// </summary>
        public int GetLiveReferenceCount<T>() where T : class
        {
            return GetLiveReferenceCount(typeof(T));
        }
        
        /// <summary>
        /// Get count of live references for a specific type
        /// </summary>
        public int GetLiveReferenceCount(Type serviceType)
        {
            if (!_weakReferences.TryGetValue(serviceType, out var bag))
                return 0;
                
            var count = 0;
            foreach (var weakRef in bag)
            {
                if (weakRef.TryGetTarget(out _))
                {
                    count++;
                }
            }
            
            return count;
        }
        
        /// <summary>
        /// Get total count of all registered weak references
        /// </summary>
        public int GetTotalReferenceCount()
        {
            return _weakReferences.Values.Sum(bag => bag.Count);
        }
        
        /// <summary>
        /// Get total count of all live weak references
        /// </summary>
        public int GetTotalLiveReferenceCount()
        {
            var count = 0;
            foreach (var bag in _weakReferences.Values)
            {
                foreach (var weakRef in bag)
                {
                    if (weakRef.TryGetTarget(out _))
                    {
                        count++;
                    }
                }
            }
            return count;
        }
        
        /// <summary>
        /// Perform manual cleanup of dead references
        /// </summary>
        public void PerformManualCleanup()
        {
            PerformCleanup(null);
        }
        
        /// <summary>
        /// Setup automatic resurrection handling for critical services
        /// </summary>
        public void SetupResurrectionHandling<T>(Func<T> resurrector) where T : class
        {
            // This could be extended to automatically recreate services when they're collected
            // For now, it's a placeholder for future resurrection patterns
        }
        
        /// <summary>
        /// Periodic cleanup of dead weak references
        /// </summary>
        private void PerformCleanup(object state)
        {
            lock (_cleanupLock)
            {
                try
                {
                    Interlocked.Increment(ref _cleanupCycles);
                    var collectedCount = 0;
                    
                    // Clean up object-to-reference mappings
                    var deadObjects = new List<object>();
                    foreach (var kvp in _objectToRef)
                    {
                        if (!kvp.Value.TryGetTarget(out _))
                        {
                            deadObjects.Add(kvp.Key);
                        }
                    }
                    
                    foreach (var deadObj in deadObjects)
                    {
                        _objectToRef.TryRemove(deadObj, out _);
                        collectedCount++;
                    }
                    
                    // Clean up type-based collections
                    var typesToClean = new List<Type>();
                    foreach (var kvp in _weakReferences)
                    {
                        var liveBag = new ConcurrentBag<WeakReference<object>>();
                        var deadCount = 0;
                        
                        foreach (var weakRef in kvp.Value)
                        {
                            if (weakRef.TryGetTarget(out _))
                            {
                                liveBag.Add(weakRef);
                            }
                            else
                            {
                                deadCount++;
                            }
                        }
                        
                        if (deadCount > 0)
                        {
                            _weakReferences.TryUpdate(kvp.Key, liveBag, kvp.Value);
                            collectedCount += deadCount;
                        }
                        
                        // Mark empty collections for removal
                        if (liveBag.IsEmpty)
                        {
                            typesToClean.Add(kvp.Key);
                        }
                    }
                    
                    // Remove empty type collections
                    foreach (var type in typesToClean)
                    {
                        _weakReferences.TryRemove(type, out _);
                    }
                    
                    Interlocked.Add(ref _referencesCollected, collectedCount);
                    
                    if (collectedCount > 0)
                    {
                        Debug.Log($"WeakReferenceManager: Collected {collectedCount} dead references, {GetTotalLiveReferenceCount()} live references remaining");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error during weak reference cleanup: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Get weak reference manager statistics
        /// </summary>
        public WeakReferenceStatistics GetStatistics()
        {
            return new WeakReferenceStatistics
            {
                TotalRegistrations = _totalRegistrations,
                TotalLookups = _totalLookups,
                SuccessfulLookups = _successfulLookups,
                SuccessRatio = SuccessRatio,
                CleanupCycles = _cleanupCycles,
                ReferencesCollected = _referencesCollected,
                CurrentTotalReferences = GetTotalReferenceCount(),
                CurrentLiveReferences = GetTotalLiveReferenceCount(),
                TrackedTypes = _weakReferences.Keys.Count
            };
        }
        
        /// <summary>
        /// Dispose resources and stop cleanup timer
        /// </summary>
        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            
            // Perform final cleanup
            PerformCleanup(null);
            
            _weakReferences.Clear();
            _objectToRef.Clear();
        }
    }
    
    /// <summary>
    /// Weak reference manager statistics
    /// </summary>
    public struct WeakReferenceStatistics
    {
        public long TotalRegistrations { get; set; }
        public long TotalLookups { get; set; }
        public long SuccessfulLookups { get; set; }
        public double SuccessRatio { get; set; }
        public long CleanupCycles { get; set; }
        public long ReferencesCollected { get; set; }
        public int CurrentTotalReferences { get; set; }
        public int CurrentLiveReferences { get; set; }
        public int TrackedTypes { get; set; }
        
        public override string ToString()
        {
            return $"WeakRefManager: {CurrentLiveReferences}/{CurrentTotalReferences} live refs, " +
                   $"{SuccessRatio:P1} success ratio, {TrackedTypes} types tracked";
        }
    }
    
    /// <summary>
    /// Utility for working with weak reference collections
    /// </summary>
    public static class WeakReferenceUtils
    {
        /// <summary>
        /// Create a weak reference collection
        /// </summary>
        public static WeakReferenceCollection<T> CreateCollection<T>() where T : class
        {
            return new WeakReferenceCollection<T>();
        }
        
        /// <summary>
        /// Check if an object is eligible for weak referencing
        /// </summary>
        public static bool IsEligibleForWeakReference(object obj)
        {
            if (obj == null)
                return false;
                
            var type = obj.GetType();
            
            // Skip value types (they can't be weakly referenced)
            if (type.IsValueType)
                return false;
                
            // Skip strings (they're interned)
            if (type == typeof(string))
                return false;
                
            return true;
        }
    }
    
    /// <summary>
    /// Collection specifically for weak references
    /// </summary>
    public class WeakReferenceCollection<T> where T : class
    {
        private readonly List<WeakReference<T>> _references = new List<WeakReference<T>>();
        private readonly object _lock = new object();
        
        public void Add(T item)
        {
            if (item == null)
                return;
                
            lock (_lock)
            {
                _references.Add(new WeakReference<T>(item));
            }
        }
        
        public IEnumerable<T> GetLiveItems()
        {
            lock (_lock)
            {
                var liveItems = new List<T>();
                for (int i = _references.Count - 1; i >= 0; i--)
                {
                    if (_references[i].TryGetTarget(out var target))
                    {
                        liveItems.Add(target);
                    }
                    else
                    {
                        _references.RemoveAt(i); // Clean up dead reference
                    }
                }
                return liveItems;
            }
        }
        
        public int Count => GetLiveItems().Count();
        
        public void Clear()
        {
            lock (_lock)
            {
                _references.Clear();
            }
        }
    }
}