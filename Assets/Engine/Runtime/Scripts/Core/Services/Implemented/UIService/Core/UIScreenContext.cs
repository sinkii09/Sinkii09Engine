using System;
using System.Collections.Generic;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Type-safe context for passing data to UI screens
    /// </summary>
    public class UIScreenContext
    {
        private readonly Dictionary<Type, object> _data = new();
        
        /// <summary>
        /// Set data of a specific type in the context
        /// </summary>
        /// <typeparam name="T">Type of data to store</typeparam>
        /// <param name="value">Data value to store</param>
        /// <returns>This context for method chaining</returns>
        public UIScreenContext Set<T>(T value)
        {
            if (value != null)
            {
                _data[typeof(T)] = value;
            }
            else
            {
                _data.Remove(typeof(T));
            }
            return this;
        }
        
        /// <summary>
        /// Get data of a specific type from the context
        /// </summary>
        /// <typeparam name="T">Type of data to retrieve</typeparam>
        /// <returns>Data value or default if not found</returns>
        public T Get<T>()
        {
            if (_data.TryGetValue(typeof(T), out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return default(T);
        }
        
        /// <summary>
        /// Try to get data of a specific type from the context
        /// </summary>
        /// <typeparam name="T">Type of data to retrieve</typeparam>
        /// <param name="value">Output parameter for the retrieved value</param>
        /// <returns>True if data was found and retrieved successfully</returns>
        public bool TryGet<T>(out T value)
        {
            if (_data.TryGetValue(typeof(T), out var obj) && obj is T typedValue)
            {
                value = typedValue;
                return true;
            }
            value = default(T);
            return false;
        }
        
        /// <summary>
        /// Check if context contains data of a specific type
        /// </summary>
        /// <typeparam name="T">Type to check for</typeparam>
        /// <returns>True if context contains data of the specified type</returns>
        public bool Has<T>()
        {
            return _data.ContainsKey(typeof(T)) && _data[typeof(T)] is T;
        }
        
        /// <summary>
        /// Remove data of a specific type from the context
        /// </summary>
        /// <typeparam name="T">Type of data to remove</typeparam>
        /// <returns>True if data was removed, false if not found</returns>
        public bool Remove<T>()
        {
            return _data.Remove(typeof(T));
        }
        
        /// <summary>
        /// Clear all data from the context
        /// </summary>
        public void Clear()
        {
            _data.Clear();
        }
        
        /// <summary>
        /// Get the number of data items in the context
        /// </summary>
        public int Count => _data.Count;
        
        /// <summary>
        /// Check if the context is empty
        /// </summary>
        public bool IsEmpty => _data.Count == 0;
        
        /// <summary>
        /// Get all types currently stored in the context
        /// </summary>
        /// <returns>Array of types with data in the context</returns>
        public Type[] GetStoredTypes()
        {
            var types = new Type[_data.Count];
            _data.Keys.CopyTo(types, 0);
            return types;
        }
        
        /// <summary>
        /// Create a copy of this context
        /// </summary>
        /// <returns>New context with the same data</returns>
        public UIScreenContext Clone()
        {
            var clone = new UIScreenContext();
            foreach (var kvp in _data)
            {
                clone._data[kvp.Key] = kvp.Value;
            }
            return clone;
        }
        
        /// <summary>
        /// Merge another context into this one
        /// </summary>
        /// <param name="other">Context to merge from</param>
        /// <param name="overwrite">Whether to overwrite existing data</param>
        /// <returns>This context for method chaining</returns>
        public UIScreenContext Merge(UIScreenContext other, bool overwrite = true)
        {
            if (other == null) return this;
            
            foreach (var kvp in other._data)
            {
                if (overwrite || !_data.ContainsKey(kvp.Key))
                {
                    _data[kvp.Key] = kvp.Value;
                }
            }
            return this;
        }
        
        /// <summary>
        /// Get a string representation of the context for debugging
        /// </summary>
        public override string ToString()
        {
            if (_data.Count == 0)
                return "UIScreenContext: Empty";
                
            var types = string.Join(", ", _data.Keys);
            return $"UIScreenContext: [{types}]";
        }
    }
}