using System;
using System.Collections.Generic;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Save data container for input binding configurations
    /// Integrates with SaveLoadService for persistent input customization
    /// </summary>
    [Serializable]
    public class InputBindingsSaveData : SaveData
    {
        #region Fields
        
        public string activeProfileName = "Default";
        public List<InputBindingData> bindings = new List<InputBindingData>();
        public List<InputBindingProfile> profiles = new List<InputBindingProfile>();
        public Dictionary<string, object> settings = new Dictionary<string, object>();
        
        #endregion
        
        #region Constructor
        
        public InputBindingsSaveData() : base()
        {
            // Initialize with default profile
            var defaultProfile = new InputBindingProfile("Default");
            profiles.Add(defaultProfile);
        }
        
        #endregion
        
        #region SaveData Implementation
        
        protected override int GetCurrentVersion()
        {
            return 1;
        }
        
        protected override bool ValidateData()
        {
            // Validate profile consistency
            if (string.IsNullOrEmpty(activeProfileName))
                return false;
                
            if (profiles == null || profiles.Count == 0)
                return false;
                
            // Ensure active profile exists
            bool activeProfileExists = false;
            foreach (var profile in profiles)
            {
                if (profile.profileName == activeProfileName)
                {
                    activeProfileExists = true;
                    break;
                }
            }
            
            if (!activeProfileExists)
                return false;
                
            // Validate bindings structure
            if (bindings != null)
            {
                foreach (var binding in bindings)
                {
                    if (string.IsNullOrEmpty(binding.actionName))
                        return false;
                    if (binding.bindingIndex < 0)
                        return false;
                }
            }
            
            return true;
        }
        
        #endregion
        
        #region Profile Management
        
        /// <summary>
        /// Get the currently active profile
        /// </summary>
        public InputBindingProfile GetActiveProfile()
        {
            foreach (var profile in profiles)
            {
                if (profile.profileName == activeProfileName)
                    return profile;
            }
            
            // Fallback to first profile if active not found
            return profiles.Count > 0 ? profiles[0] : null;
        }
        
        /// <summary>
        /// Set the active profile by name
        /// </summary>
        public bool SetActiveProfile(string profileName)
        {
            foreach (var profile in profiles)
            {
                if (profile.profileName == profileName)
                {
                    activeProfileName = profileName;
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// Add or update a profile
        /// </summary>
        public void AddOrUpdateProfile(InputBindingProfile profile)
        {
            if (profile == null)
                return;
                
            // Remove existing profile with same name
            for (int i = profiles.Count - 1; i >= 0; i--)
            {
                if (profiles[i].profileName == profile.profileName)
                {
                    profiles.RemoveAt(i);
                    break;
                }
            }
            
            // Add new profile
            profiles.Add(profile);
        }
        
        /// <summary>
        /// Remove a profile by name
        /// </summary>
        public bool RemoveProfile(string profileName)
        {
            if (profileName == "Default")
                return false; // Cannot remove default profile
                
            for (int i = 0; i < profiles.Count; i++)
            {
                if (profiles[i].profileName == profileName)
                {
                    profiles.RemoveAt(i);
                    
                    // If removing active profile, switch to Default
                    if (activeProfileName == profileName)
                    {
                        activeProfileName = "Default";
                    }
                    
                    return true;
                }
            }
            return false;
        }
        
        #endregion
        
        #region Binding Management
        
        /// <summary>
        /// Update binding data from Unity's binding override JSON
        /// </summary>
        public void UpdateFromBindingOverrides(string bindingOverridesJson)
        {
            if (string.IsNullOrEmpty(bindingOverridesJson))
                return;
                
            try
            {
                bindings.Clear();
                
                // Parse Unity's binding overrides format and convert to InputBindingData
                var overrides = UnityEngine.JsonUtility.FromJson<BindingOverrideWrapper>("{\"overrides\":" + bindingOverridesJson + "}");
                if (overrides?.overrides != null)
                {
                    foreach (var binding in overrides.overrides)
                    {
                        var bindingData = new InputBindingData
                        {
                            actionName = binding.action ?? "",
                            bindingIndex = binding.bindingIndex,
                            originalPath = binding.originalPath ?? "",
                            overridePath = binding.path ?? "",
                            displayName = binding.path ?? "",
                            lastModified = DateTime.UtcNow
                        };
                        bindings.Add(bindingData);
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[InputBindingsSaveData] Failed to parse binding overrides: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Convert to Unity's binding override JSON format
        /// </summary>
        public string ToBindingOverridesJson()
        {
            try
            {
                var overrideList = new List<BindingOverride>();
                
                foreach (var binding in bindings)
                {
                    if (!string.IsNullOrEmpty(binding.overridePath))
                    {
                        overrideList.Add(new BindingOverride
                        {
                            action = binding.actionName,
                            bindingIndex = binding.bindingIndex,
                            path = binding.overridePath,
                            originalPath = binding.originalPath
                        });
                    }
                }
                
                var wrapper = new BindingOverrideWrapper { overrides = overrideList };
                var json = UnityEngine.JsonUtility.ToJson(wrapper);
                
                // Extract just the overrides array from the wrapper
                if (json.StartsWith("{\"overrides\":"))
                {
                    return json.Substring(12, json.Length - 13); // Remove {"overrides": and }
                }
                
                return "[]";
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[InputBindingsSaveData] Failed to convert to binding overrides JSON: {ex.Message}");
                return "[]";
            }
        }
        
        #endregion
        
        #region Settings Management
        
        /// <summary>
        /// Set a custom setting value
        /// </summary>
        public void SetSetting<T>(string key, T value)
        {
            if (settings == null)
                settings = new Dictionary<string, object>();
                
            settings[key] = value;
        }
        
        /// <summary>
        /// Get a custom setting value
        /// </summary>
        public T GetSetting<T>(string key, T defaultValue = default(T))
        {
            if (settings == null || !settings.ContainsKey(key))
                return defaultValue;
                
            try
            {
                return (T)settings[key];
            }
            catch
            {
                return defaultValue;
            }
        }
        
        #endregion
        
        #region Helper Classes
        
        [Serializable]
        private class BindingOverride
        {
            public string action;
            public int bindingIndex;
            public string path;
            public string originalPath;
        }
        
        [Serializable]
        private class BindingOverrideWrapper
        {
            public List<BindingOverride> overrides;
        }
        
        #endregion
    }
}