using UnityEngine;

namespace Sinkii09.Engine.Services.AutoSave
{
    public class ApplicationLifecycleAutoSaveTrigger : AutoSaveTriggerBase
    {
        public override string TriggerName => "ApplicationLifecycle";
        
        private bool _saveOnPause;
        private bool _saveOnFocusLost;
        private bool _saveOnQuit;
        private bool _hasFocus = true;
        private bool _isPaused = false;
        
        protected override void OnInitialize()
        {
            if (_config?.LifecycleSettings != null)
            {
                _saveOnPause = _config.LifecycleSettings.SaveOnPause;
                _saveOnFocusLost = _config.LifecycleSettings.SaveOnFocusLost;
                _saveOnQuit = _config.LifecycleSettings.SaveOnQuit;
                
                // Subscribe to Unity application events
                Application.focusChanged += OnApplicationFocusChanged;
                Application.quitting += OnApplicationQuitting;
                
                IsEnabled = _saveOnPause || _saveOnFocusLost || _saveOnQuit;
                
                Debug.Log($"[ApplicationLifecycleAutoSaveTrigger] Initialized - Pause: {_saveOnPause}, Focus: {_saveOnFocusLost}, Quit: {_saveOnQuit}");
            }
            else
            {
                IsEnabled = false;
                Debug.Log("[ApplicationLifecycleAutoSaveTrigger] Disabled via configuration");
            }
        }
        
        protected override void OnShutdown()
        {
            Application.focusChanged -= OnApplicationFocusChanged;
            Application.quitting -= OnApplicationQuitting;
            
            Debug.Log("[ApplicationLifecycleAutoSaveTrigger] Shutdown");
        }
        
        private void OnApplicationFocusChanged(bool hasFocus)
        {
            if (!IsEnabled) return;
            
            var previousFocus = _hasFocus;
            _hasFocus = hasFocus;
            
            // Trigger save when losing focus
            if (_saveOnFocusLost && previousFocus && !hasFocus)
            {
                Debug.Log("[ApplicationLifecycleAutoSaveTrigger] Application lost focus");
                TriggerAutoSave(AutoSaveReason.ApplicationFocusLost);
            }
        }
        
        private void OnApplicationPauseChanged(bool pauseStatus)
        {
            if (!IsEnabled) return;
            
            var previousPause = _isPaused;
            _isPaused = pauseStatus;
            
            // Trigger save when being paused
            if (_saveOnPause && !previousPause && pauseStatus)
            {
                Debug.Log("[ApplicationLifecycleAutoSaveTrigger] Application paused");
                TriggerAutoSave(AutoSaveReason.ApplicationPause);
            }
        }
        
        private void OnApplicationQuitting()
        {
            if (!IsEnabled) return;
            
            if (_saveOnQuit)
            {
                Debug.Log("[ApplicationLifecycleAutoSaveTrigger] Application quitting");
                TriggerAutoSave(AutoSaveReason.ApplicationQuit);
            }
        }
        
        public bool HasFocus => _hasFocus;
        public bool IsPaused => _isPaused;
    }
}