using Sinkii09.Engine.Services.AutoSave;
using System.Collections.Generic;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    [CreateAssetMenu(fileName = "AutoSaveServiceConfiguration", menuName = "Engine/Services/AutoSaveServiceConfiguration")]
    public class AutoSaveServiceConfiguration : ServiceConfigurationBase
    {
        [Header("General Settings")]
        [SerializeField] private bool _enableAutoSave = true;
        [SerializeField] private bool _useAsyncSave = true;
        [SerializeField] private float _saveCooldown = 5f;
        
        [Header("Slot Management")]
        [SerializeField] private int _maxAutoSaveSlots = 3;
        [SerializeField] private AutoSaveSlotStrategy _slotStrategy = AutoSaveSlotStrategy.Rotating;
        [SerializeField] private string _autoSavePrefix = "autosave";
        
        [Header("Timer Settings")]
        [SerializeField] private TimerSettings _timerSettings = new TimerSettings();
        
        [Header("Scene Change Settings")]
        [SerializeField] private SceneChangeSettings _sceneChangeSettings = new SceneChangeSettings();
        
        [Header("Application Lifecycle Settings")]
        [SerializeField] private ApplicationLifecycleSettings _lifecycleSettings = new ApplicationLifecycleSettings();
        
        [Header("Conditions")]
        [SerializeField] private AutoSaveConditions _conditions = new AutoSaveConditions();
        
        [Header("Performance")]
        [SerializeField] private int _maxConcurrentAutoSaves = 1;
        [SerializeField] private bool _enableAutoSaveLogging = true;
        [SerializeField] private bool _enableAutoSaveEvents = true;
        
        public bool EnableAutoSave => _enableAutoSave;
        public bool UseAsyncSave => _useAsyncSave;
        public float SaveCooldown => _saveCooldown;
        
        public int MaxAutoSaveSlots => _maxAutoSaveSlots;
        public AutoSaveSlotStrategy SlotStrategy => _slotStrategy;
        public string AutoSavePrefix => _autoSavePrefix;
        
        public TimerSettings TimerSettings => _timerSettings;
        public SceneChangeSettings SceneChangeSettings => _sceneChangeSettings;
        public ApplicationLifecycleSettings LifecycleSettings => _lifecycleSettings;
        
        public AutoSaveConditions Conditions => _conditions;
        
        public int MaxConcurrentAutoSaves => _maxConcurrentAutoSaves;
        public bool EnableAutoSaveLogging => _enableAutoSaveLogging;
        public bool EnableAutoSaveEvents => _enableAutoSaveEvents;


        protected override bool OnCustomValidate(List<string> errors)
        {
            if (_saveCooldown < 0)
                errors.Add("Save cooldown cannot be negative");

            if (_maxAutoSaveSlots <= 0)
                errors.Add("Max auto-save slots must be greater than 0");

            if (_maxAutoSaveSlots > 50)
                errors.Add("Max auto-save slots should not exceed 50 for performance reasons");

            if (string.IsNullOrWhiteSpace(_autoSavePrefix))
                errors.Add("Auto-save prefix cannot be empty");

            if (_timerSettings != null)
            {
                if (_timerSettings.Interval <= 0)
                    errors.Add("Timer interval must be greater than 0");

                if (_timerSettings.MinimumInterval <= 0)
                    errors.Add("Timer minimum interval must be greater than 0");

                if (_timerSettings.Interval < _timerSettings.MinimumInterval)
                    errors.Add("Timer interval cannot be less than minimum interval");
            }

            if (_lifecycleSettings != null && _lifecycleSettings.QuitSaveTimeout <= 0)
                errors.Add("Quit save timeout must be greater than 0");

            if (_maxConcurrentAutoSaves <= 0)
                errors.Add("Max concurrent auto-saves must be greater than 0");

            if (_maxConcurrentAutoSaves > 10)
                errors.Add("Max concurrent auto-saves should not exceed 10 for performance reasons");

            return errors.Count == 0;
        }
        
        public override void ResetToDefaults()
        {
            _enableAutoSave = true;
            _useAsyncSave = true;
            _saveCooldown = 5f;
            _maxAutoSaveSlots = 3;
            _slotStrategy = AutoSaveSlotStrategy.Rotating;
            _autoSavePrefix = "autosave";
            _maxConcurrentAutoSaves = 1;
            _enableAutoSaveLogging = true;
            _enableAutoSaveEvents = true;
            
            _timerSettings = new TimerSettings
            {
                Enabled = true,
                Interval = 300f,
                MinimumInterval = 30f
            };
            
            _sceneChangeSettings = new SceneChangeSettings
            {
                Enabled = true,
                SaveBeforeLoad = true,
                SaveAfterLoad = false,
                IgnoredScenes = new[] { "MainMenu", "LoadingScreen", "Splash" }
            };
            
            _lifecycleSettings = new ApplicationLifecycleSettings
            {
                SaveOnPause = true,
                SaveOnFocusLost = true,
                SaveOnQuit = true,
                QuitSaveTimeout = 5f
            };
            
            _conditions = new AutoSaveConditions
            {
                RequirePlayerAlive = true,
                RequireNotInCombat = true,
                RequireNotInCutscene = true,
                RequireNotInMainMenu = true,
                RequireNotLoading = true,
                RequireUnsavedChanges = false
            };
        }

    }
}