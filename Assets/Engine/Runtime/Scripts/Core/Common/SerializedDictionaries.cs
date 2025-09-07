using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Unity-serializable dictionary for Type -> float mappings.
    /// Used for command timeout overrides in ScriptPlayerConfiguration.
    /// </summary>
    [System.Serializable]
    public class SerializedTypeFloatDictionary
    {
        [SerializeField]
        private List<TypeFloatPair> _pairs = new List<TypeFloatPair>();

        public Dictionary<Type, float> ToDictionary()
        {
            var result = new Dictionary<Type, float>();
            
            foreach (var pair in _pairs)
            {
                if (pair.Type != null && !result.ContainsKey(pair.Type))
                {
                    result[pair.Type] = pair.Value;
                }
            }
            
            return result;
        }

        public void FromDictionary(Dictionary<Type, float> dictionary)
        {
            _pairs.Clear();
            
            if (dictionary != null)
            {
                foreach (var kvp in dictionary)
                {
                    _pairs.Add(new TypeFloatPair { Type = kvp.Key, Value = kvp.Value });
                }
            }
        }

        public void SetTimeout(Type commandType, float timeout)
        {
            var existing = _pairs.FirstOrDefault(p => p.Type == commandType);
            if (existing != null)
            {
                existing.Value = timeout;
            }
            else
            {
                _pairs.Add(new TypeFloatPair { Type = commandType, Value = timeout });
            }
        }

        public bool RemoveTimeout(Type commandType)
        {
            return _pairs.RemoveAll(p => p.Type == commandType) > 0;
        }

        public float? GetTimeout(Type commandType)
        {
            var pair = _pairs.FirstOrDefault(p => p.Type == commandType);
            return pair?.Value;
        }

        public int Count => _pairs.Count;

        [System.Serializable]
        private class TypeFloatPair
        {
            [SerializeField] 
            private string _typeAssemblyQualifiedName;
            
            [SerializeField] 
            private float _value;

            public Type Type
            {
                get
                {
                    if (string.IsNullOrEmpty(_typeAssemblyQualifiedName))
                        return null;
                    
                    try
                    {
                        return Type.GetType(_typeAssemblyQualifiedName);
                    }
                    catch
                    {
                        return null;
                    }
                }
                set
                {
                    _typeAssemblyQualifiedName = value?.AssemblyQualifiedName;
                }
            }

            public float Value
            {
                get => _value;
                set => _value = value;
            }
        }
    }

    /// <summary>
    /// Unity-serializable dictionary for string -> object mappings.
    /// Used for error context information.
    /// </summary>
    [System.Serializable]
    public class SerializedStringObjectDictionary
    {
        [SerializeField]
        private List<StringObjectPair> _pairs = new List<StringObjectPair>();

        public Dictionary<string, object> ToDictionary()
        {
            var result = new Dictionary<string, object>();
            
            foreach (var pair in _pairs)
            {
                if (!string.IsNullOrEmpty(pair.Key) && !result.ContainsKey(pair.Key))
                {
                    result[pair.Key] = pair.Value;
                }
            }
            
            return result;
        }

        public void FromDictionary(Dictionary<string, object> dictionary)
        {
            _pairs.Clear();
            
            if (dictionary != null)
            {
                foreach (var kvp in dictionary)
                {
                    _pairs.Add(new StringObjectPair { Key = kvp.Key, Value = kvp.Value });
                }
            }
        }

        public void Set(string key, object value)
        {
            var existing = _pairs.FirstOrDefault(p => p.Key == key);
            if (existing != null)
            {
                existing.Value = value;
            }
            else
            {
                _pairs.Add(new StringObjectPair { Key = key, Value = value });
            }
        }

        public bool Remove(string key)
        {
            return _pairs.RemoveAll(p => p.Key == key) > 0;
        }

        public object Get(string key)
        {
            var pair = _pairs.FirstOrDefault(p => p.Key == key);
            return pair?.Value;
        }

        public int Count => _pairs.Count;

        [System.Serializable]
        private class StringObjectPair
        {
            [SerializeField] 
            private string _key;
            
            [SerializeField] 
            private UnityEngine.Object _objectValue;
            
            [SerializeField] 
            private string _stringValue;
            
            [SerializeField] 
            private float _floatValue;
            
            [SerializeField] 
            private int _intValue;
            
            [SerializeField] 
            private bool _boolValue;
            
            [SerializeField]
            private ValueType _valueType;

            public string Key
            {
                get => _key;
                set => _key = value;
            }

            public object Value
            {
                get
                {
                    return _valueType switch
                    {
                        ValueType.String => _stringValue,
                        ValueType.Float => _floatValue,
                        ValueType.Int => _intValue,
                        ValueType.Bool => _boolValue,
                        ValueType.UnityObject => _objectValue,
                        _ => null
                    };
                }
                set
                {
                    // Reset all values
                    _stringValue = null;
                    _objectValue = null;
                    _floatValue = 0f;
                    _intValue = 0;
                    _boolValue = false;

                    // Set appropriate value and type
                    switch (value)
                    {
                        case string s:
                            _stringValue = s;
                            _valueType = ValueType.String;
                            break;
                        case float f:
                            _floatValue = f;
                            _valueType = ValueType.Float;
                            break;
                        case double d:
                            _floatValue = (float)d;
                            _valueType = ValueType.Float;
                            break;
                        case int i:
                            _intValue = i;
                            _valueType = ValueType.Int;
                            break;
                        case bool b:
                            _boolValue = b;
                            _valueType = ValueType.Bool;
                            break;
                        case UnityEngine.Object obj:
                            _objectValue = obj;
                            _valueType = ValueType.UnityObject;
                            break;
                        default:
                            _stringValue = value?.ToString();
                            _valueType = ValueType.String;
                            break;
                    }
                }
            }

            private enum ValueType
            {
                String,
                Float,
                Int,
                Bool,
                UnityObject
            }
        }
    }

    /// <summary>
    /// Unity-serializable dictionary for string -> int mappings.
    /// Used for label name to line index mappings.
    /// </summary>
    [System.Serializable]
    public class SerializedStringIntDictionary
    {
        [SerializeField]
        private List<StringIntPair> _pairs = new List<StringIntPair>();

        public Dictionary<string, int> ToDictionary()
        {
            var result = new Dictionary<string, int>();
            
            foreach (var pair in _pairs)
            {
                if (!string.IsNullOrEmpty(pair.Key) && !result.ContainsKey(pair.Key))
                {
                    result[pair.Key] = pair.Value;
                }
            }
            
            return result;
        }

        public void FromDictionary(Dictionary<string, int> dictionary)
        {
            _pairs.Clear();
            
            if (dictionary != null)
            {
                foreach (var kvp in dictionary)
                {
                    _pairs.Add(new StringIntPair { Key = kvp.Key, Value = kvp.Value });
                }
            }
        }

        public void Set(string key, int value)
        {
            var existing = _pairs.FirstOrDefault(p => p.Key == key);
            if (existing != null)
            {
                existing.Value = value;
            }
            else
            {
                _pairs.Add(new StringIntPair { Key = key, Value = value });
            }
        }

        public bool Remove(string key)
        {
            return _pairs.RemoveAll(p => p.Key == key) > 0;
        }

        public int? Get(string key)
        {
            var pair = _pairs.FirstOrDefault(p => p.Key == key);
            return pair?.Value;
        }

        public int Count => _pairs.Count;

        [System.Serializable]
        private class StringIntPair
        {
            [SerializeField] 
            private string _key;
            
            [SerializeField] 
            private int _value;

            public string Key
            {
                get => _key;
                set => _key = value;
            }

            public int Value
            {
                get => _value;
                set => _value = value;
            }
        }
    }

    /// <summary>
    /// Unity-serializable list for int collections.
    /// Used for breakpoint line indices.
    /// </summary>
    [System.Serializable]
    public class SerializedIntList
    {
        [SerializeField]
        private List<int> _values = new List<int>();

        public HashSet<int> ToHashSet()
        {
            return new HashSet<int>(_values);
        }

        public List<int> ToList()
        {
            return new List<int>(_values);
        }

        public void FromHashSet(HashSet<int> hashSet)
        {
            _values.Clear();
            if (hashSet != null)
            {
                _values.AddRange(hashSet);
            }
        }

        public void FromList(List<int> list)
        {
            _values.Clear();
            if (list != null)
            {
                _values.AddRange(list);
            }
        }

        public void Add(int value)
        {
            if (!_values.Contains(value))
            {
                _values.Add(value);
            }
        }

        public bool Remove(int value)
        {
            return _values.Remove(value);
        }

        public void Clear()
        {
            _values.Clear();
        }

        public bool Contains(int value)
        {
            return _values.Contains(value);
        }

        public int Count => _values.Count;

        public int[] ToArray()
        {
            return _values.ToArray();
        }
    }
}