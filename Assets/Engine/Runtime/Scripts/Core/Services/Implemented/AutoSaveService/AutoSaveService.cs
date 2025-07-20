using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Services.AutoSave
{
    [EngineService(ServiceCategory.Core, ServicePriority.Medium,
        Description = "Manages automatic save triggers and policies for seamless save automation")]
    [ServiceConfiguration(typeof(AutoSaveServiceConfiguration))]
    public class AutoSaveService : IAutoSaveService
    {
        public event Action<AutoSaveEvent> AutoSaveCompleted;
        public event Action<AutoSaveEvent> AutoSaveFailed;

        public bool IsAutoSaveEnabled { get; private set; }

        public bool CanAutoSave =>
            IsAutoSaveEnabled &&
            _isInitialized &&
            !_isShuttingDown &&
            Time.time - _lastSaveTime >= _config.SaveCooldown &&
            _activeSaveCount < _config.MaxConcurrentAutoSaves;

        public DateTime LastAutoSaveTime { get; private set; } = DateTime.MinValue;

        public IReadOnlyList<string> AutoSaveSlots => _autoSaveSlots.AsReadOnly();

        private readonly List<IAutoSaveProvider> _providers = new();
        private readonly List<IAutoSaveTrigger> _triggers = new();
        private readonly List<string> _autoSaveSlots = new();
        private readonly SemaphoreSlim _saveLock = new(1, 1);

        private ISaveLoadService _saveLoadService;
        private AutoSaveServiceConfiguration _config;
        private bool _isInitialized;
        private bool _isShuttingDown;
        private float _lastSaveTime;
        private int _activeSaveCount;
        private CancellationTokenSource _shutdownCts;

        public AutoSaveService(AutoSaveServiceConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }
        public async UniTask<ServiceInitializationResult> InitializeAsync(IServiceProvider provider, CancellationToken cancellationToken)
        {
            try
            {
                if (_isInitialized)
                    return ServiceInitializationResult.Success();

                _saveLoadService = Engine.GetService<ISaveLoadService>();
                if (_saveLoadService == null)
                    return ServiceInitializationResult.Failed("SaveLoadService is required but not found");

                if (_config == null)
                    return ServiceInitializationResult.Failed("AutoSaveServiceConfiguration is required but not found");

                if (!_config.Validate(out var errors))
                {
                    return ServiceInitializationResult.Failed($"Configuration validation failed: {string.Join(", ", errors)}");
                }

                _shutdownCts = new CancellationTokenSource();
                IsAutoSaveEnabled = _config.EnableAutoSave;

                // TODO: Register auto save providers
                //if (provider is ServiceContainer container)
                //{
                //    var providers = container.resol<IAutoSaveProvider>();
                //    foreach (var autoSaveProvider in providers)
                //    {
                //        RegisterProvider(autoSaveProvider);
                //    }
                //}

                // TODO: Register triggers
                // Initialize built-in triggers
                InitializeBuiltInTriggers();

                _isInitialized = true;

                if (_config.EnableAutoSaveLogging)
                    Debug.Log($"[AutoSaveService] Initialized with {_providers.Count} providers and {_triggers.Count} triggers");

                return await UniTask.FromResult(ServiceInitializationResult.Success());
            }
            catch (Exception ex)
            {
                return ServiceInitializationResult.Failed($"Initialization failed: {ex.Message}");
            }
        }

        public async UniTask<ServiceHealthStatus> HealthCheckAsync(CancellationToken cancellationToken)
        {
            if (!_isInitialized)
                return ServiceHealthStatus.Unhealthy("Service not initialized");

            if (_isShuttingDown)
                return ServiceHealthStatus.Degraded("Service shutting down");

            if (_saveLoadService == null)
                return ServiceHealthStatus.Unhealthy("SaveLoadService dependency missing");

            if (!_providers.Any())
                return ServiceHealthStatus.Unknown("No auto-save providers registered");

            try
            {
                var saveLoadHealth = await _saveLoadService.HealthCheckAsync(cancellationToken);
                if (saveLoadHealth.IsHealthy != true)
                    return ServiceHealthStatus.Degraded($"SaveLoadService unhealthy: {saveLoadHealth.ToString()}");

                return ServiceHealthStatus.Healthy($"Active with {_providers.Count} providers, {_triggers.Count} triggers");
            }
            catch (Exception ex)
            {
                return ServiceHealthStatus.Unhealthy($"Health check failed: {ex.Message}");
            }
        }

        public async UniTask<ServiceShutdownResult> ShutdownAsync(CancellationToken cancellationToken)
        {
            try
            {
                _isShuttingDown = true;

                // Stop all triggers
                foreach (var trigger in _triggers)
                {
                    try
                    {
                        trigger.Shutdown();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[AutoSaveService] Error shutting down trigger {trigger.TriggerName}: {ex.Message}");
                    }
                }

                // Wait for active saves to complete
                var timeout = TimeSpan.FromSeconds(_config.LifecycleSettings.QuitSaveTimeout);
                var waitStart = DateTime.UtcNow;

                while (_activeSaveCount > 0 && DateTime.UtcNow - waitStart < timeout)
                {
                    await UniTask.Delay(100, cancellationToken: cancellationToken);
                }

                _shutdownCts?.Cancel();
                _shutdownCts?.Dispose();
                _saveLock?.Dispose();

                if (_config.EnableAutoSaveLogging)
                    Debug.Log("[AutoSaveService] Shutdown completed");

                return ServiceShutdownResult.Success();
            }
            catch (Exception ex)
            {
                return ServiceShutdownResult.Failed($"Shutdown failed: {ex.Message}");
            }
        }

        public async UniTask<SaveResult> TriggerAutoSaveAsync(AutoSaveReason reason = AutoSaveReason.Manual, CancellationToken cancellationToken = default)
        {
            if (!CanAutoSave)
            {
                var message = !IsAutoSaveEnabled ? "Auto-save is disabled" :
                             !_isInitialized ? "Service not initialized" :
                             _isShuttingDown ? "Service shutting down" :
                             Time.time - _lastSaveTime < _config.SaveCooldown ? "Save cooldown active" :
                             "Max concurrent saves reached";

                return SaveResult.CreateFailure(
                    saveId: "auto_save_failed",
                    exception: new InvalidOperationException(message),
                    duration: TimeSpan.Zero
                );
            }

            if (!await ValidateAutoSaveConditionsAsync(cancellationToken))
                return SaveResult.CreateFailure(
                    saveId: "auto_save_failed",
                    exception: new InvalidOperationException("Auto-save conditions not met"),
                    duration: TimeSpan.Zero
                );

            await _saveLock.WaitAsync(cancellationToken);

            try
            {
                _activeSaveCount++;

                var provider = GetBestProvider();
                if (provider == null)
                    return SaveResult.CreateFailure(
                        saveId: "auto_save_failed",
                        exception: new InvalidOperationException("No suitable auto-save provider available"),
                        duration: TimeSpan.Zero
                    );
                
                var saveData = await provider.CreateSaveDataAsync(cancellationToken);
                var slotName = GetNextAutoSaveSlot();

                var startTime = DateTime.UtcNow;
                var result = await _saveLoadService.SaveAsync(slotName, saveData, cancellationToken);
                var duration = (float)(DateTime.UtcNow - startTime).TotalMilliseconds;

                var autoSaveEvent = new AutoSaveEvent
                {
                    SlotName = slotName,
                    Reason = reason,
                    Timestamp = DateTime.UtcNow,
                    Success = result.Success,
                    Error = result.Exception.Message,
                    SaveDurationMs = duration,
                    ProviderName = provider.ProviderName
                };

                if (result.Success)
                {
                    _lastSaveTime = Time.time;
                    LastAutoSaveTime = DateTime.UtcNow;

                    provider.OnAutoSaveCompleted(autoSaveEvent);

                    if (_config.EnableAutoSaveEvents)
                        AutoSaveCompleted?.Invoke(autoSaveEvent);

                    if (_config.EnableAutoSaveLogging)
                        Debug.Log($"[AutoSaveService] Auto-save completed: {slotName} ({reason}) in {duration:F1}ms");
                }
                else
                {
                    provider.OnAutoSaveFailed(autoSaveEvent);

                    if (_config.EnableAutoSaveEvents)
                        AutoSaveFailed?.Invoke(autoSaveEvent);

                    if (_config.EnableAutoSaveLogging)
                        Debug.LogError($"[AutoSaveService] Auto-save failed: {result.Exception.Message}");
                }

                return result;
            }
            finally
            {
                _activeSaveCount--;
                _saveLock.Release();
            }
        }

        public void RegisterProvider(IAutoSaveProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            if (!_providers.Contains(provider))
            {
                _providers.Add(provider);

                if (_config?.EnableAutoSaveLogging == true)
                    Debug.Log($"[AutoSaveService] Registered provider: {provider.ProviderName}");
            }
        }

        public void UnregisterProvider(IAutoSaveProvider provider)
        {
            if (provider == null) return;

            if (_providers.Remove(provider))
            {
                if (_config?.EnableAutoSaveLogging == true)
                    Debug.Log($"[AutoSaveService] Unregistered provider: {provider.ProviderName}");
            }
        }

        public void RegisterTrigger(IAutoSaveTrigger trigger)
        {
            if (trigger == null) throw new ArgumentNullException(nameof(trigger));

            if (!_triggers.Contains(trigger))
            {
                _triggers.Add(trigger);
                trigger.OnTrigger += HandleTriggerEvent;

                if (_isInitialized)
                    trigger.Initialize(_config);

                if (_config?.EnableAutoSaveLogging == true)
                    Debug.Log($"[AutoSaveService] Registered trigger: {trigger.TriggerName}");
            }
        }

        public void UnregisterTrigger(IAutoSaveTrigger trigger)
        {
            if (trigger == null) return;

            if (_triggers.Remove(trigger))
            {
                trigger.OnTrigger -= HandleTriggerEvent;
                trigger.Shutdown();

                if (_config?.EnableAutoSaveLogging == true)
                    Debug.Log($"[AutoSaveService] Unregistered trigger: {trigger.TriggerName}");
            }
        }

        public void SetAutoSaveEnabled(bool enabled)
        {
            IsAutoSaveEnabled = enabled;

            if (_config?.EnableAutoSaveLogging == true)
                Debug.Log($"[AutoSaveService] Auto-save {(enabled ? "enabled" : "disabled")}");
        }

        public void UpdateTimers(float deltaTime)
        {
            if (!_isInitialized || _isShuttingDown) return;

            foreach (var trigger in _triggers)
            {
                try
                {
                    trigger.Update(deltaTime);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[AutoSaveService] Error updating trigger {trigger.TriggerName}: {ex.Message}");
                }
            }
        }

        public void ClearOldAutoSaves(int maxToKeep = -1)
        {
            var keepCount = maxToKeep == -1 ? _config.MaxAutoSaveSlots : maxToKeep;

            while (_autoSaveSlots.Count > keepCount)
            {
                var oldestSlot = _autoSaveSlots[0];
                _autoSaveSlots.RemoveAt(0);

                // Optionally delete the save file
                // This would require additional SaveLoadService API
            }
        }

        public async UniTask<bool> ValidateAutoSaveConditionsAsync(CancellationToken cancellationToken = default)
        {
            if (_providers.Count == 0) return false;

            var provider = GetBestProvider();
            if (provider == null) return false;

            var result = provider.CanAutoSave() && provider.MeetsConditions(_config.Conditions);
            return await UniTask.FromResult(result);
        }

        private void InitializeBuiltInTriggers()
        {
            // Built-in triggers will be registered automatically
            // This method is for future extensibility
        }

        private IAutoSaveProvider GetBestProvider()
        {
            return _providers
                .Where(p => p.CanAutoSave() && p.MeetsConditions(_config.Conditions))
                .OrderByDescending(p => p.GetAutoSavePriority())
                .FirstOrDefault();
        }

        private string GetNextAutoSaveSlot()
        {
            string slotName;

            switch (_config.SlotStrategy)
            {
                case AutoSaveSlotStrategy.Rotating:
                    if (_autoSaveSlots.Count >= _config.MaxAutoSaveSlots)
                    {
                        slotName = _autoSaveSlots[0];
                        _autoSaveSlots.RemoveAt(0);
                    }
                    else
                    {
                        slotName = $"{_config.AutoSavePrefix}_{_autoSaveSlots.Count + 1:D2}";
                    }
                    break;

                case AutoSaveSlotStrategy.Timestamped:
                    slotName = $"{_config.AutoSavePrefix}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
                    break;

                case AutoSaveSlotStrategy.Numbered:
                    var nextNumber = _autoSaveSlots.Count + 1;
                    slotName = $"{_config.AutoSavePrefix}_{nextNumber:D3}";
                    break;

                default:
                    slotName = $"{_config.AutoSavePrefix}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
                    break;
            }

            if (!_autoSaveSlots.Contains(slotName))
                _autoSaveSlots.Add(slotName);

            return slotName;
        }

        private async void HandleTriggerEvent(AutoSaveReason reason)
        {
            try
            {
                if (CanAutoSave)
                {
                    await TriggerAutoSaveAsync(reason, _shutdownCts.Token);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AutoSaveService] Error handling trigger event {reason}: {ex.Message}");
            }
        }
    }
}