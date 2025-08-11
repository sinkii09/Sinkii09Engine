using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Object pool for modal backdrops to reduce create/destroy overhead and memory fragmentation
    /// Provides efficient reuse of backdrop GameObjects for better performance
    /// </summary>
    public class UIModalBackdropPool : IDisposable
    {
        private readonly Stack<UIModalBackdrop> _availableBackdrops;
        private readonly HashSet<UIModalBackdrop> _activeBackdrops;
        private readonly Transform _poolParent;
        private readonly int _maxPoolSize;
        private readonly bool _enablePooling;

        // Statistics
        private int _totalCreated;
        private int _poolHits;
        private int _poolMisses;

        /// <summary>
        /// Current number of backdrops in the pool
        /// </summary>
        public int AvailableCount => _availableBackdrops.Count;

        /// <summary>
        /// Current number of active backdrops
        /// </summary>
        public int ActiveCount => _activeBackdrops.Count;

        /// <summary>
        /// Pool efficiency ratio (0-1, higher is better)
        /// </summary>
        public double PoolEfficiency => _poolHits + _poolMisses > 0 ? (double)_poolHits / (_poolHits + _poolMisses) : 0.0;

        public UIModalBackdropPool(Transform poolParent, int maxPoolSize = 10, bool enablePooling = true)
        {
            _poolParent = poolParent;
            _maxPoolSize = Math.Max(1, maxPoolSize);
            _enablePooling = enablePooling;

            _availableBackdrops = new Stack<UIModalBackdrop>();
            _activeBackdrops = new HashSet<UIModalBackdrop>();

            Debug.Log($"[UIModalBackdropPool] Initialized with maxSize={_maxPoolSize}, pooling={_enablePooling}");
        }

        /// <summary>
        /// Get a backdrop instance from the pool or create a new one
        /// </summary>
        public UIModalBackdrop GetBackdrop()
        {
            UIModalBackdrop backdrop;

            if (_enablePooling && _availableBackdrops.Count > 0)
            {
                // Reuse from pool
                backdrop = _availableBackdrops.Pop();
                backdrop.gameObject.SetActive(true);
                _poolHits++;
                
                Debug.Log($"[UIModalBackdropPool] Reused backdrop from pool, available: {_availableBackdrops.Count}");
            }
            else
            {
                // Create new backdrop
                backdrop = CreateNewBackdrop();
                _totalCreated++;
                _poolMisses++;
                
                Debug.Log($"[UIModalBackdropPool] Created new backdrop, total created: {_totalCreated}");
            }

            _activeBackdrops.Add(backdrop);
            return backdrop;
        }

        /// <summary>
        /// Return a backdrop to the pool for reuse
        /// </summary>
        public void ReturnBackdrop(UIModalBackdrop backdrop)
        {
            if (backdrop == null || !_activeBackdrops.Contains(backdrop))
                return;

            _activeBackdrops.Remove(backdrop);

            if (_enablePooling && _availableBackdrops.Count < _maxPoolSize)
            {
                // Return to pool for reuse
                backdrop.gameObject.SetActive(false);
                backdrop.transform.SetParent(_poolParent, false);
                
                // Reset backdrop state
                ResetBackdrop(backdrop);
                
                _availableBackdrops.Push(backdrop);
                Debug.Log($"[UIModalBackdropPool] Returned backdrop to pool, available: {_availableBackdrops.Count}");
            }
            else
            {
                // Pool is full or pooling disabled - destroy the backdrop
                if (backdrop.gameObject != null)
                {
                    UnityEngine.Object.Destroy(backdrop.gameObject);
                }
                Debug.Log("[UIModalBackdropPool] Destroyed backdrop - pool full or disabled");
            }
        }

        /// <summary>
        /// Clear all backdrops from the pool
        /// </summary>
        public void Clear()
        {
            // Destroy all available backdrops
            while (_availableBackdrops.Count > 0)
            {
                var backdrop = _availableBackdrops.Pop();
                if (backdrop != null && backdrop.gameObject != null)
                {
                    UnityEngine.Object.Destroy(backdrop.gameObject);
                }
            }

            // Destroy all active backdrops
            var activeArray = new UIModalBackdrop[_activeBackdrops.Count];
            _activeBackdrops.CopyTo(activeArray);
            foreach (var backdrop in activeArray)
            {
                if (backdrop != null && backdrop.gameObject != null)
                {
                    UnityEngine.Object.Destroy(backdrop.gameObject);
                }
            }
            _activeBackdrops.Clear();

            Debug.Log("[UIModalBackdropPool] Cleared all backdrops");
        }

        /// <summary>
        /// Respond to memory pressure by reducing pool size
        /// </summary>
        public void RespondToMemoryPressure(float pressureLevel)
        {
            if (!_enablePooling) return;

            int targetPoolSize;
            if (pressureLevel > 0.8f)
            {
                // High pressure - clear 80% of pool
                targetPoolSize = _availableBackdrops.Count / 5;
            }
            else if (pressureLevel > 0.6f)
            {
                // Medium pressure - clear 50% of pool
                targetPoolSize = _availableBackdrops.Count / 2;
            }
            else if (pressureLevel > 0.4f)
            {
                // Low pressure - clear 25% of pool
                targetPoolSize = (_availableBackdrops.Count * 3) / 4;
            }
            else
            {
                return; // No action needed for low pressure
            }

            while (_availableBackdrops.Count > targetPoolSize)
            {
                var backdrop = _availableBackdrops.Pop();
                if (backdrop != null && backdrop.gameObject != null)
                {
                    UnityEngine.Object.Destroy(backdrop.gameObject);
                }
            }

            Debug.Log($"[UIModalBackdropPool] Memory pressure cleanup - pool size: {_availableBackdrops.Count}");
        }

        private UIModalBackdrop CreateNewBackdrop()
        {
            var backdropGameObject = new GameObject("ModalBackdrop_Pooled");
            backdropGameObject.transform.SetParent(_poolParent, false);

            // Add required components
            var backdrop = backdropGameObject.AddComponent<UIModalBackdrop>();

            return backdrop;
        }

        private void ResetBackdrop(UIModalBackdrop backdrop)
        {
            if (backdrop == null) return;

            // Reset position and scale
            backdrop.transform.localPosition = Vector3.zero;
            backdrop.transform.localScale = Vector3.one;
            backdrop.transform.localRotation = Quaternion.identity;

            // Reset canvas properties to defaults
            var canvas = backdrop.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = -1;
            }

            // Additional reset logic can be added here as needed
        }

        public void Dispose()
        {
            Clear();
        }
    }
}