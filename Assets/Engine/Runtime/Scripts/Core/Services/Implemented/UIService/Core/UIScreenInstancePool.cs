using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Object pool for multiple-instance UI screens to reduce create/destroy overhead
    /// Manages separate pools per screen type for screens with AllowMultipleInstances = true
    /// </summary>
    public class UIScreenInstancePool : IDisposable
    {
        private readonly Dictionary<UIScreenType, Stack<UIScreen>> _availablePools;
        private readonly Dictionary<UIScreenType, HashSet<UIScreen>> _activePools;
        private readonly Transform _poolParent;
        private readonly int _maxPoolSizePerType;
        private readonly bool _enablePooling;

        // Statistics
        private readonly Dictionary<UIScreenType, int> _totalCreated;
        private readonly Dictionary<UIScreenType, int> _poolHits;
        private readonly Dictionary<UIScreenType, int> _poolMisses;

        /// <summary>
        /// Get current pool statistics for a screen type
        /// </summary>
        public (int available, int active, int totalCreated, double efficiency) GetStats(UIScreenType screenType)
        {
            var available = _availablePools.TryGetValue(screenType, out var availableStack) ? availableStack.Count : 0;
            var active = _activePools.TryGetValue(screenType, out var activeSet) ? activeSet.Count : 0;
            var created = _totalCreated.TryGetValue(screenType, out var total) ? total : 0;
            
            var hits = _poolHits.TryGetValue(screenType, out var h) ? h : 0;
            var misses = _poolMisses.TryGetValue(screenType, out var m) ? m : 0;
            var efficiency = hits + misses > 0 ? (double)hits / (hits + misses) : 0.0;

            return (available, active, created, efficiency);
        }

        /// <summary>
        /// Get total statistics across all screen types
        /// </summary>
        public (int totalAvailable, int totalActive, int totalCreated, double overallEfficiency) GetOverallStats()
        {
            var totalAvailable = 0;
            var totalActive = 0;
            var totalCreated = 0;
            var totalHits = 0;
            var totalMisses = 0;

            foreach (var pool in _availablePools.Values)
                totalAvailable += pool.Count;

            foreach (var activeSet in _activePools.Values)
                totalActive += activeSet.Count;

            foreach (var created in _totalCreated.Values)
                totalCreated += created;

            foreach (var hits in _poolHits.Values)
                totalHits += hits;

            foreach (var misses in _poolMisses.Values)
                totalMisses += misses;

            var efficiency = totalHits + totalMisses > 0 ? (double)totalHits / (totalHits + totalMisses) : 0.0;
            return (totalAvailable, totalActive, totalCreated, efficiency);
        }

        public UIScreenInstancePool(Transform poolParent, int maxPoolSizePerType = 5, bool enablePooling = true)
        {
            _poolParent = poolParent;
            _maxPoolSizePerType = Math.Max(1, maxPoolSizePerType);
            _enablePooling = enablePooling;

            _availablePools = new Dictionary<UIScreenType, Stack<UIScreen>>();
            _activePools = new Dictionary<UIScreenType, HashSet<UIScreen>>();
            _totalCreated = new Dictionary<UIScreenType, int>();
            _poolHits = new Dictionary<UIScreenType, int>();
            _poolMisses = new Dictionary<UIScreenType, int>();

            Debug.Log($"[UIScreenInstancePool] Initialized with maxPoolSizePerType={_maxPoolSizePerType}, pooling={_enablePooling}");
        }

        /// <summary>
        /// Get a screen instance from the pool or mark for creation
        /// Returns null if a new instance should be created
        /// </summary>
        public UIScreen GetPooledInstance(UIScreenType screenType)
        {
            if (!_enablePooling)
            {
                IncrementMisses(screenType);
                return null; // Caller should create new instance
            }

            // Get or create available pool for this screen type
            if (!_availablePools.TryGetValue(screenType, out var availableStack))
            {
                availableStack = new Stack<UIScreen>();
                _availablePools[screenType] = availableStack;
            }

            // Get or create active pool for this screen type
            if (!_activePools.TryGetValue(screenType, out var activeSet))
            {
                activeSet = new HashSet<UIScreen>();
                _activePools[screenType] = activeSet;
            }

            UIScreen screenInstance = null;

            if (availableStack.Count > 0)
            {
                // Reuse from pool
                screenInstance = availableStack.Pop();
                if (screenInstance != null && screenInstance.gameObject != null)
                {
                    screenInstance.gameObject.SetActive(true);
                    activeSet.Add(screenInstance);
                    IncrementHits(screenType);
                    
                    Debug.Log($"[UIScreenInstancePool] Reused {screenType} from pool, available: {availableStack.Count}");
                    return screenInstance;
                }
                else
                {
                    // Screen was destroyed, remove from pool
                    Debug.LogWarning($"[UIScreenInstancePool] Found destroyed screen in {screenType} pool, cleaning up");
                }
            }

            IncrementMisses(screenType);
            return null; // Caller should create new instance
        }

        /// <summary>
        /// Register a newly created screen instance with the pool
        /// </summary>
        public void RegisterNewInstance(UIScreenType screenType, UIScreen screenInstance)
        {
            if (!_enablePooling || screenInstance == null)
                return;

            // Get or create active pool for this screen type
            if (!_activePools.TryGetValue(screenType, out var activeSet))
            {
                activeSet = new HashSet<UIScreen>();
                _activePools[screenType] = activeSet;
            }

            activeSet.Add(screenInstance);
            IncrementCreated(screenType);
            
            Debug.Log($"[UIScreenInstancePool] Registered new {screenType} instance, total created: {_totalCreated[screenType]}");
        }

        /// <summary>
        /// Return a screen instance to the pool for reuse
        /// </summary>
        public void ReturnInstance(UIScreenType screenType, UIScreen screenInstance)
        {
            if (screenInstance == null)
                return;

            // Remove from active pool
            if (_activePools.TryGetValue(screenType, out var activeSet))
            {
                activeSet.Remove(screenInstance);
            }

            if (_enablePooling)
            {
                // Get or create available pool for this screen type
                if (!_availablePools.TryGetValue(screenType, out var availableStack))
                {
                    availableStack = new Stack<UIScreen>();
                    _availablePools[screenType] = availableStack;
                }

                if (availableStack.Count < _maxPoolSizePerType)
                {
                    // Return to pool for reuse
                    screenInstance.gameObject.SetActive(false);
                    screenInstance.transform.SetParent(_poolParent, false);
                    
                    // Reset screen state
                    ResetScreenInstance(screenInstance);
                    
                    availableStack.Push(screenInstance);
                    Debug.Log($"[UIScreenInstancePool] Returned {screenType} to pool, available: {availableStack.Count}");
                    return;
                }
            }

            // Pool is full, pooling disabled, or cleanup - destroy the screen
            if (screenInstance.gameObject != null)
            {
                UnityEngine.Object.Destroy(screenInstance.gameObject);
            }
            Debug.Log($"[UIScreenInstancePool] Destroyed {screenType} instance - pool full or disabled");
        }

        /// <summary>
        /// Clear all instances for a specific screen type
        /// </summary>
        public void ClearScreenType(UIScreenType screenType)
        {
            // Destroy available instances
            if (_availablePools.TryGetValue(screenType, out var availableStack))
            {
                while (availableStack.Count > 0)
                {
                    var screen = availableStack.Pop();
                    if (screen != null && screen.gameObject != null)
                    {
                        UnityEngine.Object.Destroy(screen.gameObject);
                    }
                }
            }

            // Destroy active instances
            if (_activePools.TryGetValue(screenType, out var activeSet))
            {
                var activeArray = new UIScreen[activeSet.Count];
                activeSet.CopyTo(activeArray);
                foreach (var screen in activeArray)
                {
                    if (screen != null && screen.gameObject != null)
                    {
                        UnityEngine.Object.Destroy(screen.gameObject);
                    }
                }
                activeSet.Clear();
            }

            Debug.Log($"[UIScreenInstancePool] Cleared all {screenType} instances");
        }

        /// <summary>
        /// Clear all pooled instances of all screen types
        /// </summary>
        public void Clear()
        {
            // Destroy all available instances
            foreach (var kvp in _availablePools)
            {
                while (kvp.Value.Count > 0)
                {
                    var screen = kvp.Value.Pop();
                    if (screen != null && screen.gameObject != null)
                    {
                        UnityEngine.Object.Destroy(screen.gameObject);
                    }
                }
            }

            // Destroy all active instances  
            foreach (var kvp in _activePools)
            {
                var activeArray = new UIScreen[kvp.Value.Count];
                kvp.Value.CopyTo(activeArray);
                foreach (var screen in activeArray)
                {
                    if (screen != null && screen.gameObject != null)
                    {
                        UnityEngine.Object.Destroy(screen.gameObject);
                    }
                }
                kvp.Value.Clear();
            }

            Debug.Log("[UIScreenInstancePool] Cleared all pooled instances");
        }

        /// <summary>
        /// Respond to memory pressure by reducing pool sizes
        /// </summary>
        public void RespondToMemoryPressure(float pressureLevel)
        {
            if (!_enablePooling) return;

            int reductionFactor;
            if (pressureLevel > 0.8f)
            {
                reductionFactor = 5; // Keep only 20%
            }
            else if (pressureLevel > 0.6f)
            {
                reductionFactor = 2; // Keep only 50%
            }
            else if (pressureLevel > 0.4f)
            {
                reductionFactor = 4; // Keep 75%
                reductionFactor = 3; // Actually keep ~67%
            }
            else
            {
                return; // No action needed for low pressure
            }

            var totalDestroyed = 0;
            foreach (var kvp in _availablePools)
            {
                var screenType = kvp.Key;
                var availableStack = kvp.Value;
                var targetSize = availableStack.Count / reductionFactor;

                while (availableStack.Count > targetSize)
                {
                    var screen = availableStack.Pop();
                    if (screen != null && screen.gameObject != null)
                    {
                        UnityEngine.Object.Destroy(screen.gameObject);
                        totalDestroyed++;
                    }
                }
            }

            Debug.Log($"[UIScreenInstancePool] Memory pressure cleanup - destroyed {totalDestroyed} instances");
        }

        private void ResetScreenInstance(UIScreen screen)
        {
            if (screen == null) return;

            // Reset transform
            screen.transform.localPosition = Vector3.zero;
            screen.transform.localScale = Vector3.one;
            screen.transform.localRotation = Quaternion.identity;

            // Reset screen state
            screen.gameObject.SetActive(false);

            // Additional reset logic can be added here as needed
            // For example: reset screen data, clear animations, etc.
        }

        private void IncrementCreated(UIScreenType screenType)
        {
            _totalCreated[screenType] = _totalCreated.TryGetValue(screenType, out var current) ? current + 1 : 1;
        }

        private void IncrementHits(UIScreenType screenType)
        {
            _poolHits[screenType] = _poolHits.TryGetValue(screenType, out var current) ? current + 1 : 1;
        }

        private void IncrementMisses(UIScreenType screenType)
        {
            _poolMisses[screenType] = _poolMisses.TryGetValue(screenType, out var current) ? current + 1 : 1;
        }

        public void Dispose()
        {
            Clear();
        }
    }
}