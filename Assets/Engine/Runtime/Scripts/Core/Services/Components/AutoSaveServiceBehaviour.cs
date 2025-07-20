using Sinkii09.Engine.Core.Services.AutoSave;
using Sinkii09.Engine.Services.AutoSave;
using System;
using UnityEngine;

namespace Sinkii09.Engine.Services.Components
{
    [AddComponentMenu("Engine/Services/AutoSave Service Behaviour")]
    public class AutoSaveServiceBehaviour : EngineBehaviour
    {
        [Header("Auto-Save Service Integration")]
        [SerializeField] private bool _enableUpdateLoop = true;
        [SerializeField] private bool _enableDebugUI = false;
        [SerializeField] private bool _registerBuiltInTriggers = true;

        [Header("Built-in Triggers")]
        [SerializeField] private bool _enableTimerTrigger = true;
        [SerializeField] private bool _enableSceneChangeTrigger = true;
        [SerializeField] private bool _enableLifecycleTrigger = true;

        [Header("Debug Information")]
        [SerializeField, Space] private bool _showDebugInfo = false;
        [SerializeField] private int _autoSaveCount = 0;
        [SerializeField] private string _lastAutoSaveTime = "Never";
        [SerializeField] private string _currentStatus = "Unknown";

        private IAutoSaveService _autoSaveService;
        private TimerAutoSaveTrigger _timerTrigger;
        private SceneChangeAutoSaveTrigger _sceneChangeTrigger;
        private ApplicationLifecycleAutoSaveTrigger _lifecycleTrigger;

        protected override void OnEngineInitialized(ServiceInitializationReport report)
        {
            _autoSaveService = Engine.GetService<IAutoSaveService>();

            if (_autoSaveService == null)
            {
                Debug.LogError("[AutoSaveServiceBehaviour] AutoSaveService not found in engine!");
                enabled = false;
                return;
            }

            // Subscribe to events for debug info
            _autoSaveService.AutoSaveCompleted += OnAutoSaveCompleted;
            _autoSaveService.AutoSaveFailed += OnAutoSaveFailed;

            // Register built-in triggers if enabled
            if (_registerBuiltInTriggers)
            {
                RegisterBuiltInTriggers();
            }


            UpdateDebugInfo();

            Debug.Log("[AutoSaveServiceBehaviour] Initialized successfully");
        }

        protected override void OnEngineShuttingDown()
        {
            if (_autoSaveService != null)
            {
                _autoSaveService.AutoSaveCompleted -= OnAutoSaveCompleted;
                _autoSaveService.AutoSaveFailed -= OnAutoSaveFailed;

                // Unregister triggers
                if (_timerTrigger != null)
                    _autoSaveService.UnregisterTrigger(_timerTrigger);
                if (_sceneChangeTrigger != null)
                    _autoSaveService.UnregisterTrigger(_sceneChangeTrigger);
                if (_lifecycleTrigger != null)
                    _autoSaveService.UnregisterTrigger(_lifecycleTrigger);
            }
        }

        private void Update()
        {
            if (!_enableUpdateLoop || _autoSaveService == null) return;

            // Update auto-save timers
            _autoSaveService.UpdateTimers(Time.deltaTime);

            // Update debug info periodically
            if (_showDebugInfo && Time.frameCount % 60 == 0) // Every ~1 second at 60fps
            {
                UpdateDebugInfo();
            }
        }
        private void RegisterBuiltInTriggers()
        {
            if (_enableTimerTrigger)
            {
                _timerTrigger = new TimerAutoSaveTrigger();
                _autoSaveService.RegisterTrigger(_timerTrigger);
                Debug.Log("[AutoSaveServiceBehaviour] Registered TimerAutoSaveTrigger");
            }

            if (_enableSceneChangeTrigger)
            {
                _sceneChangeTrigger = new SceneChangeAutoSaveTrigger();
                _autoSaveService.RegisterTrigger(_sceneChangeTrigger);
                Debug.Log("[AutoSaveServiceBehaviour] Registered SceneChangeAutoSaveTrigger");
            }

            if (_enableLifecycleTrigger)
            {
                _lifecycleTrigger = new ApplicationLifecycleAutoSaveTrigger();
                _autoSaveService.RegisterTrigger(_lifecycleTrigger);
                Debug.Log("[AutoSaveServiceBehaviour] Registered ApplicationLifecycleAutoSaveTrigger");
            }
        }

        private void OnAutoSaveCompleted(AutoSaveEvent autoSaveEvent)
        {
            _autoSaveCount++;

            if (_enableDebugUI)
            {
                Debug.Log($"[AutoSaveServiceBehaviour] Auto-save completed: {autoSaveEvent.SlotName} " +
                         $"({autoSaveEvent.Reason}) in {autoSaveEvent.SaveDurationMs:F1}ms");
            }

            UpdateDebugInfo();
        }

        private void OnAutoSaveFailed(AutoSaveEvent autoSaveEvent)
        {
            if (_enableDebugUI)
            {
                Debug.LogError($"[AutoSaveServiceBehaviour] Auto-save failed: {autoSaveEvent.Error}");
            }

            UpdateDebugInfo();
        }

        private void UpdateDebugInfo()
        {
            if (_autoSaveService == null) return;

            _lastAutoSaveTime = _autoSaveService.LastAutoSaveTime == System.DateTime.MinValue
                ? "Never"
                : _autoSaveService.LastAutoSaveTime.ToString("HH:mm:ss");

            _currentStatus = _autoSaveService.IsAutoSaveEnabled
                ? _autoSaveService.CanAutoSave ? "Ready" : "Waiting"
                : "Disabled";
        }

        // Public methods for external control
        [ContextMenu("Trigger Manual Auto-Save")]
        public async void TriggerManualAutoSave()
        {
            if (_autoSaveService == null)
            {
                Debug.LogError("[AutoSaveServiceBehaviour] AutoSaveService not available");
                return;
            }

            var result = await _autoSaveService.TriggerAutoSaveAsync(AutoSaveReason.Manual);

            if (result.Success)
            {
                Debug.Log($"[AutoSaveServiceBehaviour] Manual auto-save completed successfully");
            }
            else
            {
                Debug.LogError($"[AutoSaveServiceBehaviour] Manual auto-save failed: {result.Exception.Message}");
            }
        }

        [ContextMenu("Toggle Auto-Save")]
        public void ToggleAutoSave()
        {
            if (_autoSaveService == null) return;

            var newState = !_autoSaveService.IsAutoSaveEnabled;
            _autoSaveService.SetAutoSaveEnabled(newState);

            Debug.Log($"[AutoSaveServiceBehaviour] Auto-save {(newState ? "enabled" : "disabled")}");
        }

        [ContextMenu("Clear Old Auto-Saves")]
        public void ClearOldAutoSaves()
        {
            if (_autoSaveService == null) return;

            _autoSaveService.ClearOldAutoSaves();
            Debug.Log("[AutoSaveServiceBehaviour] Cleared old auto-saves");
        }

        // Properties for external access
        public IAutoSaveService AutoSaveService => _autoSaveService;
        public bool IsAutoSaveReady => _autoSaveService?.CanAutoSave == true;
        public System.DateTime LastAutoSaveTime => _autoSaveService?.LastAutoSaveTime ?? System.DateTime.MinValue;
        public int AutoSaveCount => _autoSaveCount;

        public void ResetTimerTrigger()
        {
            (_timerTrigger as TimerAutoSaveTrigger)?.ResetTimer();
        }

        public float GetTimeUntilNextAutoSave()
        {
            return (_timerTrigger as TimerAutoSaveTrigger)?.GetTimeUntilNextSave() ?? -1f;
        }
    }
}