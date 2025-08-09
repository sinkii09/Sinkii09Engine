using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Base class for all service configurations
    /// </summary>
    public abstract class ServiceConfigurationBase : ScriptableObject, IServiceConfiguration
    {
        [SerializeField]
        private int _version = 1;
        
        [SerializeField]
        [TextArea(2, 4)]
        private string _description = "Service configuration";
        
        /// <summary>
        /// Event fired when configuration changes
        /// </summary>
        public event Action<IServiceConfiguration> ConfigurationChanged;
        
        /// <summary>
        /// Validate the configuration
        /// </summary>
        public virtual bool Validate(out List<string> errors)
        {
            errors = new List<string>();
            return OnCustomValidate(errors);
        }
        
        /// <summary>
        /// Get display name for this configuration
        /// </summary>
        public virtual string GetDisplayName()
        {
            return !string.IsNullOrEmpty(name) ? name : GetType().Name;
        }
        
        /// <summary>
        /// Get configuration version
        /// </summary>
        public virtual int GetVersion()
        {
            return _version;
        }
        
        /// <summary>
        /// Get configuration description
        /// </summary>
        public string GetDescription()
        {
            return _description;
        }
        
        /// <summary>
        /// Override this method to provide custom validation logic
        /// </summary>
        /// <param name="errors">List to add validation errors to</param>
        /// <returns>True if validation passes</returns>
        protected virtual bool OnCustomValidate(List<string> errors)
        {
            return true; // Default: valid
        }
        
        /// <summary>
        /// Called when configuration values change in the inspector
        /// </summary>
        protected virtual void OnValidate()
        {
            // Increment version when configuration changes
            _version++;
            
            // Validate configuration
            if (Validate(out var errors) && errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    Debug.LogWarning($"Configuration validation warning in {GetDisplayName()}: {error}", this);
                }
            }
            
            // Notify listeners of changes (in editor)
            if (Application.isPlaying)
            {
                ConfigurationChanged?.Invoke(this);
            }
        }
        
        /// <summary>
        /// Reset configuration to default values
        /// </summary>
        public virtual void ResetToDefaults()
        {
            OnResetToDefaults();
            _version++;
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }
        
        /// <summary>
        /// Override this method to implement custom reset logic
        /// </summary>
        protected virtual void OnResetToDefaults()
        {
            // Default implementation does nothing
        }
        
        /// <summary>
        /// Clone this configuration
        /// </summary>
        public virtual T Clone<T>() where T : ServiceConfigurationBase
        {
            var clone = Instantiate(this) as T;
            clone.name = name + "_Clone";
            return clone;
        }
        
        /// <summary>
        /// Apply changes from another configuration
        /// </summary>
        public virtual void ApplyFrom(ServiceConfigurationBase other)
        {
            if (other == null || other.GetType() != GetType())
                return;
            
            // Copy serialized data
            var json = JsonUtility.ToJson(other);
            JsonUtility.FromJsonOverwrite(json, this);
            
            _version++;
            ConfigurationChanged?.Invoke(this);
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }
        
        /// <summary>
        /// Get configuration as JSON string
        /// </summary>
        public virtual string ToJson()
        {
            return JsonUtility.ToJson(this, true);
        }
        
        /// <summary>
        /// Load configuration from JSON string
        /// </summary>
        public virtual void FromJson(string json)
        {
            JsonUtility.FromJsonOverwrite(json, this);
            _version++;
            ConfigurationChanged?.Invoke(this);
        }
        
        #if UNITY_EDITOR
        /// <summary>
        /// Create asset menu path for this configuration type
        /// </summary>
        protected virtual string GetCreateAssetMenuPath()
        {
            var typeName = GetType().Name;
            return $"Engine/Services/{typeName}";
        }
        #endif
    }
}