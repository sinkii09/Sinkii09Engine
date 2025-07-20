using Sinkii09.Engine.Services.AutoSave;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sinkii09.Engine.Core.Services.AutoSave
{
    public class SceneChangeAutoSaveTrigger : AutoSaveTriggerBase
    {
        public override string TriggerName => "SceneChange";
        
        private string[] _ignoredScenes;
        private bool _saveBeforeLoad;
        private bool _saveAfterLoad;
        private string _currentSceneName;
        
        protected override void OnInitialize()
        {
            if (_config?.SceneChangeSettings?.Enabled == true)
            {
                _ignoredScenes = _config.SceneChangeSettings.IgnoredScenes ?? new string[0];
                _saveBeforeLoad = _config.SceneChangeSettings.SaveBeforeLoad;
                _saveAfterLoad = _config.SceneChangeSettings.SaveAfterLoad;
                _currentSceneName = SceneManager.GetActiveScene().name;
                
                // Subscribe to scene events
                SceneManager.sceneLoaded += OnSceneLoaded;
                SceneManager.sceneUnloaded += OnSceneUnloaded;
                
                IsEnabled = true;
                
                Debug.Log($"[SceneChangeAutoSaveTrigger] Initialized - Before: {_saveBeforeLoad}, After: {_saveAfterLoad}");
                Debug.Log($"[SceneChangeAutoSaveTrigger] Ignored scenes: {string.Join(", ", _ignoredScenes)}");
            }
            else
            {
                IsEnabled = false;
                Debug.Log("[SceneChangeAutoSaveTrigger] Disabled via configuration");
            }
        }
        
        protected override void OnShutdown()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            
            Debug.Log("[SceneChangeAutoSaveTrigger] Shutdown");
        }
        
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsEnabled) return;
            
            var sceneName = scene.name;
            
            // Skip ignored scenes
            if (IsSceneIgnored(sceneName))
            {
                Debug.Log($"[SceneChangeAutoSaveTrigger] Skipping ignored scene: {sceneName}");
                return;
            }
            
            // Skip if we're loading the same scene (reload)
            if (sceneName == _currentSceneName)
            {
                Debug.Log($"[SceneChangeAutoSaveTrigger] Scene reload detected, skipping: {sceneName}");
                return;
            }
            
            _currentSceneName = sceneName;
            
            if (_saveAfterLoad)
            {
                Debug.Log($"[SceneChangeAutoSaveTrigger] Scene loaded: {sceneName}");
                TriggerAutoSave(AutoSaveReason.SceneChange);
            }
        }
        
        private void OnSceneUnloaded(Scene scene)
        {
            if (!IsEnabled) return;
            
            var sceneName = scene.name;
            
            // Skip ignored scenes
            if (IsSceneIgnored(sceneName))
                return;
            
            if (_saveBeforeLoad)
            {
                Debug.Log($"[SceneChangeAutoSaveTrigger] Scene unloading: {sceneName}");
                TriggerAutoSave(AutoSaveReason.SceneChange);
            }
        }
        
        private bool IsSceneIgnored(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || _ignoredScenes == null)
                return false;
            
            return _ignoredScenes.Contains(sceneName);
        }
        
        public void AddIgnoredScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            
            if (_ignoredScenes?.Contains(sceneName) != true)
            {
                var newIgnoredScenes = _ignoredScenes?.ToList() ?? new System.Collections.Generic.List<string>();
                newIgnoredScenes.Add(sceneName);
                _ignoredScenes = newIgnoredScenes.ToArray();
                
                Debug.Log($"[SceneChangeAutoSaveTrigger] Added ignored scene: {sceneName}");
            }
        }
        
        public void RemoveIgnoredScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || _ignoredScenes == null) return;
            
            var ignoredList = _ignoredScenes.ToList();
            if (ignoredList.Remove(sceneName))
            {
                _ignoredScenes = ignoredList.ToArray();
                Debug.Log($"[SceneChangeAutoSaveTrigger] Removed ignored scene: {sceneName}");
            }
        }
    }
}