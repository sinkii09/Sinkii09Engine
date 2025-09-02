using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Data structure for storing input binding information
    /// </summary>
    [Serializable]
    public class InputBindingData
    {
        public string actionName;
        public int bindingIndex;
        public string originalPath;
        public string overridePath;
        public string displayName;
        public DateTime lastModified;
        
        public InputBindingData() { }
        
        public InputBindingData(string actionName, int bindingIndex, string originalPath, string overridePath, string displayName = null)
        {
            this.actionName = actionName;
            this.bindingIndex = bindingIndex;
            this.originalPath = originalPath;
            this.overridePath = overridePath;
            this.displayName = displayName;
            this.lastModified = DateTime.UtcNow;
        }
    }
    
    /// <summary>
    /// Container for all input bindings for a profile
    /// </summary>
    [Serializable]
    public class InputBindingProfile
    {
        public string profileName = "Default";
        public string version = "1.0";
        public DateTime created;
        public DateTime lastModified;
        public List<InputBindingData> bindings = new List<InputBindingData>();
        
        public InputBindingProfile()
        {
            created = DateTime.UtcNow;
            lastModified = DateTime.UtcNow;
        }
        
        public InputBindingProfile(string profileName) : this()
        {
            this.profileName = profileName;
        }
    }
    
    /// <summary>
    /// Handles persistent storage and management of input binding configurations
    /// Integrates with SaveLoadService for unified save system
    /// </summary>
    public class InputBindingPersistence
    {
        #region Fields
        
        private readonly InputServiceConfiguration _config;
        private readonly ISaveLoadService _saveLoadService;
        private readonly string _saveKey = "InputBindings";
        
        #endregion
        
        #region Constructor
        
        public InputBindingPersistence(InputServiceConfiguration config, ISaveLoadService saveLoadService)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _saveLoadService = saveLoadService ?? throw new ArgumentNullException(nameof(saveLoadService));
        }
        
        #endregion
        
        #region Profile Management
        
        /// <summary>
        /// Save a binding profile to persistent storage via SaveLoadService
        /// </summary>
        public async UniTask<bool> SaveProfileAsync(InputBindingProfile profile)
        {
            if (profile == null)
            {
                Debug.LogWarning("[InputBindingPersistence] Cannot save null profile");
                return false;
            }
            
            try
            {
                profile.lastModified = DateTime.UtcNow;
                
                // Load existing save data or create new
                var saveData = await LoadSaveDataAsync() ?? new InputBindingsSaveData();
                
                // Update profile in save data
                saveData.AddOrUpdateProfile(profile);
                
                // Save via SaveLoadService
                var result = await _saveLoadService.SaveAsync(_saveKey, saveData);
                
                if (result.Success)
                {
                    // Update RuntimeData for other services
                    RuntimeData.LoadCustomData(saveData);
                    
                    if (_config.EnableDetailedLogging)
                    {
                        Debug.Log($"[InputBindingPersistence] Saved profile '{profile.profileName}' with {profile.bindings.Count} bindings");
                    }
                }
                
                return result.Success;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputBindingPersistence] Failed to save profile '{profile.profileName}': {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Load a binding profile from persistent storage via SaveLoadService
        /// </summary>
        public async UniTask<InputBindingProfile> LoadProfileAsync(string profileName)
        {
            if (string.IsNullOrEmpty(profileName))
            {
                Debug.LogWarning("[InputBindingPersistence] Cannot load profile with null/empty name");
                return null;
            }
            
            try
            {
                var saveData = await LoadSaveDataAsync();
                if (saveData != null)
                {
                    foreach (var profile in saveData.profiles)
                    {
                        if (profile.profileName == profileName)
                        {
                            if (_config.EnableDetailedLogging)
                            {
                                Debug.Log($"[InputBindingPersistence] Loaded profile '{profileName}' with {profile.bindings.Count} bindings");
                            }
                            return profile;
                        }
                    }
                }
                
                if (_config.EnableDetailedLogging)
                {
                    Debug.Log($"[InputBindingPersistence] No saved profile found for '{profileName}'");
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputBindingPersistence] Failed to load profile '{profileName}': {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Delete a binding profile from persistent storage via SaveLoadService
        /// </summary>
        public async UniTask<bool> DeleteProfileAsync(string profileName)
        {
            if (string.IsNullOrEmpty(profileName))
                return false;
                
            try
            {
                var saveData = await LoadSaveDataAsync();
                if (saveData != null)
                {
                    bool removed = saveData.RemoveProfile(profileName);
                    if (removed)
                    {
                        var result = await _saveLoadService.SaveAsync(_saveKey, saveData);
                        
                        if (result.Success)
                        {
                            RuntimeData.LoadCustomData(saveData);
                            
                            if (_config.EnableDetailedLogging)
                            {
                                Debug.Log($"[InputBindingPersistence] Deleted profile '{profileName}'");
                            }
                        }
                        
                        return result.Success;
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputBindingPersistence] Failed to delete profile '{profileName}': {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Get list of available profile names from SaveLoadService
        /// </summary>
        public async UniTask<List<string>> GetProfileNamesAsync()
        {
            try
            {
                var saveData = await LoadSaveDataAsync();
                if (saveData?.profiles != null)
                {
                    var names = new List<string>();
                    foreach (var profile in saveData.profiles)
                    {
                        names.Add(profile.profileName);
                    }
                    return names;
                }
                
                // Return default profile if no data exists
                return new List<string> { "Default" };
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputBindingPersistence] Failed to get profile names: {ex.Message}");
                return new List<string> { "Default" };
            }
        }
        
        #endregion
        
        #region Quick Save/Load (Default Profile)
        
        /// <summary>
        /// Quick save current bindings via SaveLoadService
        /// </summary>
        public async UniTask<bool> SaveCurrentBindingsAsync(string bindingOverridesJson)
        {
            try
            {
                // Load existing save data or create new
                var saveData = await LoadSaveDataAsync() ?? new InputBindingsSaveData();
                
                // Update bindings from override JSON
                if (!string.IsNullOrEmpty(bindingOverridesJson))
                {
                    saveData.UpdateFromBindingOverrides(bindingOverridesJson);
                }
                
                // Save via SaveLoadService
                var result = await _saveLoadService.SaveAsync(_saveKey, saveData);
                
                if (result.Success)
                {
                    // Update RuntimeData for other services
                    RuntimeData.LoadCustomData(saveData);
                    
                    if (_config.EnableDetailedLogging)
                    {
                        Debug.Log("[InputBindingPersistence] Saved current binding overrides");
                    }
                }
                
                return result.Success;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputBindingPersistence] Failed to save current bindings: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Quick load current bindings from SaveLoadService
        /// </summary>
        public async UniTask<string> LoadCurrentBindingsAsync()
        {
            try
            {
                var saveData = await LoadSaveDataAsync();
                if (saveData != null)
                {
                    var bindingOverrides = saveData.ToBindingOverridesJson();
                    
                    // Update RuntimeData for other services
                    RuntimeData.LoadCustomData(saveData);
                    
                    if (_config.EnableDetailedLogging && !string.IsNullOrEmpty(bindingOverrides))
                    {
                        Debug.Log("[InputBindingPersistence] Loaded current binding overrides");
                    }
                    
                    return bindingOverrides;
                }
                
                return "";
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputBindingPersistence] Failed to load current bindings: {ex.Message}");
                return "";
            }
        }
        
        #endregion
        
        #region Import/Export
        
        /// <summary>
        /// Export a profile as a shareable string
        /// </summary>
        public string ExportProfile(InputBindingProfile profile)
        {
            if (profile == null)
                return null;
                
            try
            {
                return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(profile)));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputBindingPersistence] Failed to export profile: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Import a profile from a shareable string
        /// </summary>
        public InputBindingProfile ImportProfile(string exportedData)
        {
            if (string.IsNullOrEmpty(exportedData))
                return null;
                
            try
            {
                var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(exportedData));
                return JsonUtility.FromJson<InputBindingProfile>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputBindingPersistence] Failed to import profile: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Create a backup of current bindings using SaveLoadService backup functionality
        /// </summary>
        public async UniTask<bool> CreateBackupAsync(string backupName = null)
        {
            try
            {
                backupName = backupName ?? $"Backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
                
                // Use SaveLoadService backup functionality
                var result = await _saveLoadService.CreateBackupAsync(_saveKey);
                
                if (result && _config.EnableDetailedLogging)
                {
                    Debug.Log($"[InputBindingPersistence] Created backup '{backupName}'");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputBindingPersistence] Failed to create backup: {ex.Message}");
                return false;
            }
        }
        
        #endregion
        
        #region Private Helpers
        
        /// <summary>
        /// Load InputBindingsSaveData from SaveLoadService
        /// </summary>
        private async UniTask<InputBindingsSaveData> LoadSaveDataAsync()
        {
            try
            {
                var result = await _saveLoadService.LoadAsync<InputBindingsSaveData>(_saveKey);
                return result.Success ? result.Data : null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputBindingPersistence] Failed to load save data: {ex.Message}");
                return null;
            }
        }
        
        #endregion
    }
}