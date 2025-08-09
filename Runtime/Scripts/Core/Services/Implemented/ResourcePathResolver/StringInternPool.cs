using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// High-performance string interning pool for resource path resolution
    /// Reduces memory allocation for frequently used path components
    /// </summary>
    internal class StringInternPool : IDisposable
    {
        private readonly ConcurrentDictionary<string, string> _internPool;
        private readonly int _maxPoolSize;
        private long _totalInternRequests;
        private long _cacheHits;
        
        // Common path components that benefit from interning
        private static readonly string[] COMMON_COMPONENTS = new[]
        {
            "Actors", "Scripts", "Audio", "UI", "Configs", "Prefabs",
            "Sprites", "Animations", "Textures", "Effects", "Music",
            "Character", "Background", "Default", "Primary", "Fallback",
            "Development", "Production", "High", "Normal", "Low"
        };
        
        public StringInternPool(int maxPoolSize = 10000)
        {
            _maxPoolSize = maxPoolSize;
            _internPool = new ConcurrentDictionary<string, string>();
            
            // Pre-intern common components
            PreInternCommonStrings();
            
            Debug.Log($"[StringInternPool] Initialized with {_internPool.Count} pre-interned strings");
        }
        
        /// <summary>
        /// Interns a string to reduce memory usage for repeated strings
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string Intern(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;
            
            _totalInternRequests++;
            
            // Try to get from our pool first
            if (_internPool.TryGetValue(value, out var internedValue))
            {
                _cacheHits++;
                return internedValue;
            }
            
            // Check if we should add to pool (avoid memory bloat)
            if (_internPool.Count < _maxPoolSize)
            {
                // Use system string interning for longer strings, our pool for shorter ones
                if (value.Length > 50)
                {
                    internedValue = string.Intern(value);
                }
                else
                {
                    internedValue = value;
                    _internPool.TryAdd(value, internedValue);
                }
                
                return internedValue;
            }
            
            // Pool is full, just return the original string
            return value;
        }
        
        /// <summary>
        /// Interns multiple path components efficiently
        /// </summary>
        public string[] InternPathComponents(string[] components)
        {
            if (components == null || components.Length == 0)
                return components;
            
            var internedComponents = new string[components.Length];
            for (int i = 0; i < components.Length; i++)
            {
                internedComponents[i] = Intern(components[i]);
            }
            
            return internedComponents;
        }
        
        /// <summary>
        /// Pre-interns commonly used strings to maximize hit rate
        /// </summary>
        private void PreInternCommonStrings()
        {
            foreach (var component in COMMON_COMPONENTS)
            {
                _internPool.TryAdd(component, component);
            }
            
            // Pre-intern parameter names
            var parameterFields = typeof(PathParameterNames).GetFields();
            foreach (var field in parameterFields)
            {
                if (field.IsStatic && field.FieldType == typeof(string))
                {
                    var value = (string)field.GetValue(null);
                    if (!string.IsNullOrEmpty(value))
                    {
                        _internPool.TryAdd(value, value);
                    }
                }
            }
        }
        
        /// <summary>
        /// Gets interning statistics
        /// </summary>
        public InternPoolStatistics GetStatistics()
        {
            var hitRate = _totalInternRequests > 0 ? (double)_cacheHits / _totalInternRequests : 0.0;
            
            return new InternPoolStatistics
            {
                TotalRequests = _totalInternRequests,
                CacheHits = _cacheHits,
                HitRate = hitRate,
                PoolSize = _internPool.Count,
                MaxPoolSize = _maxPoolSize,
                EstimatedMemorySavings = EstimateMemorySavings()
            };
        }
        
        private long EstimateMemorySavings()
        {
            // Rough estimate: assume average 10 character strings saved 5 times each
            return _cacheHits * 10 * 2; // 2 bytes per character in .NET
        }
        
        /// <summary>
        /// Clears the intern pool (use carefully)
        /// </summary>
        public void Clear()
        {
            _internPool.Clear();
            PreInternCommonStrings();
            _totalInternRequests = 0;
            _cacheHits = 0;
            
            Debug.Log("[StringInternPool] Pool cleared and re-initialized");
        }
        
        public void Dispose()
        {
            _internPool?.Clear();
        }
    }
    
    /// <summary>
    /// Statistics for string interning performance
    /// </summary>
    public struct InternPoolStatistics
    {
        public long TotalRequests { get; set; }
        public long CacheHits { get; set; }
        public double HitRate { get; set; }
        public int PoolSize { get; set; }
        public int MaxPoolSize { get; set; }
        public long EstimatedMemorySavings { get; set; }
        
        public override string ToString()
        {
            return $"StringIntern: {HitRate:P1} hit rate ({CacheHits}/{TotalRequests}), " +
                   $"Pool: {PoolSize}/{MaxPoolSize}, Memory saved: ~{EstimatedMemorySavings}B";
        }
    }
}