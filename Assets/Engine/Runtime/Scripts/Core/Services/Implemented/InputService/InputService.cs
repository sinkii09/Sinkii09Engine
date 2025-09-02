using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Advanced input management service with type-safe actions, context management,
    /// and cross-service coordination following CLAUDE.md patterns
    /// </summary>
    [EngineService(ServiceCategory.Core, ServicePriority.High,
        Description = "Advanced input management with action mapping, profiles, and platform support",
        RequiredServices = new[] { typeof(ISaveLoadService) })]
    [ServiceConfiguration(typeof(InputServiceConfiguration))]
    public class InputService : IInputService
    {
        #region Dependencies

        private readonly InputServiceConfiguration _config;

        #endregion

        #region Unity Input System Integration

        private InputSystem_Actions _inputActions;
        private bool _isInitialized;

        #endregion

        #region Configuration Systems

        private InputDeviceTracker _deviceTracker;
        private InputSensitivityProcessor _sensitivityProcessor;
        private InputRebindingManager _rebindingManager;
        private InputBindingPersistence _bindingPersistence;

        #endregion

        #region Context Management

        private readonly Dictionary<Guid, InputContext> _activeContexts = new Dictionary<Guid, InputContext>();
        private readonly object _contextLock = new object();
        private List<InputContext> _sortedContexts = new List<InputContext>();
        private bool _contextsDirty = false;

        #endregion

        #region Events

        public event Action<PlayerAction, UnityEngine.InputSystem.InputActionPhase> OnPlayerActionTriggered;
        public event Action<UIAction, UnityEngine.InputSystem.InputActionPhase> OnUIActionTriggered;
        public event Action<System.Collections.Generic.IReadOnlyList<InputContext>> OnContextsChanged;
        public event Action<InputDeviceType, InputDeviceType> OnDeviceChanged;

        #endregion

        #region Constructor

        public InputService(InputServiceConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        #endregion

        #region IEngineService Implementation

        public async UniTask<ServiceInitializationResult> InitializeAsync(IServiceProvider provider, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                if (_isInitialized)
                {
                    Debug.LogWarning("[InputService] Already initialized, skipping.");
                    return ServiceInitializationResult.Success(DateTime.UtcNow - startTime);
                }
                // Resolve SaveLoadService for binding persistence
                var saveLoadService = Engine.GetService<ISaveLoadService>();
                if (saveLoadService == null)
                {
                    return ServiceInitializationResult.Failed("SaveLoadService is required for InputService binding persistence.");
                }

                // Initialize Unity Input System actions
                await InitializeInputActionsAsync();

                // Initialize action mappings cache
                InitializeActionMappings();

                // Initialize configuration systems
                await InitializeConfigurationSystems(saveLoadService);

                // Subscribe to generated events from InputActionMappings
                SubscribeToGeneratedEvents();

                // Apply input update rate from configuration
                ApplyInputUpdateRate();

                _isInitialized = true;
                var initializationTime = DateTime.UtcNow - startTime;
                
                Debug.Log($"[InputService] Input Service initialized successfully in {initializationTime.TotalMilliseconds:F2}ms.");
                return ServiceInitializationResult.Success(initializationTime);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputService] Failed to initialize: {ex.Message}");
                return ServiceInitializationResult.Failed(ex);
            }
        }

        public async UniTask<ServiceShutdownResult> ShutdownAsync(CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                if (!_isInitialized)
                {
                    Debug.LogWarning("[InputService] Not initialized, skipping shutdown.");
                    return ServiceShutdownResult.Success(DateTime.UtcNow - startTime);
                }

                Debug.Log("[InputService] Shutting down Input Service...");

                // Unsubscribe from generated events
                UnsubscribeFromGeneratedEvents();
                
                // Clean up static event subscriptions in InputActionMappings
                InputActionMappings.Cleanup();

                // Save bindings before shutdown
                await SaveBindingsAsync();

                // Dispose configuration systems
                _deviceTracker?.Dispose();
                _deviceTracker = null;
                _sensitivityProcessor = null;
                _rebindingManager?.Dispose();
                _rebindingManager = null;
                _bindingPersistence = null;

                // Clear active contexts
                lock (_contextLock)
                {
                    _activeContexts.Clear();
                    _sortedContexts.Clear();
                    _contextsDirty = false;
                }

                // Dispose input actions
                if (_inputActions != null)
                {
                    _inputActions.Disable();
                    _inputActions.Dispose();
                    _inputActions = null;
                }

                _isInitialized = false;
                var shutdownTime = DateTime.UtcNow - startTime;
                
                Debug.Log($"[InputService] Input Service shut down successfully in {shutdownTime.TotalMilliseconds:F2}ms.");

                await UniTask.CompletedTask;
                return ServiceShutdownResult.Success(shutdownTime);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputService] Error during shutdown: {ex.Message}");
                return ServiceShutdownResult.Failed(ex);
            }
        }

        public async UniTask<ServiceHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                // Check if service is initialized
                if (!_isInitialized)
                {
                    return ServiceHealthStatus.Unhealthy("InputService is not initialized")
                        .WithDetail("Initialized", false);
                }

                // Check if input actions are available
                if (_inputActions == null)
                {
                    return ServiceHealthStatus.Unhealthy("Input actions are not available")
                        .WithDetail("InputActions", "null")
                        .WithDetail("Initialized", _isInitialized);
                }

                // Check if input actions are enabled
                var playerActionsEnabled = _inputActions.Player.enabled;
                var uiActionsEnabled = _inputActions.UI.enabled;

                if (!playerActionsEnabled || !uiActionsEnabled)
                {
                    return ServiceHealthStatus.Degraded("Some input action maps are disabled")
                        .WithDetail("PlayerActionsEnabled", playerActionsEnabled)
                        .WithDetail("UIActionsEnabled", uiActionsEnabled)
                        .WithDetail("ActiveContextsCount", _activeContexts.Count);
                }

                // Check InputActionMappings initialization
                try
                {
                    // Test a basic action to verify mappings work
                    var testResult = InputActionMappings.IsActionPressed(PlayerAction.Move);
                    // If we get here without exception, mappings are working
                }
                catch (Exception ex)
                {
                    return ServiceHealthStatus.Unhealthy("Input action mappings are not working")
                        .WithDetail("MappingError", ex.Message);
                }

                var responseTime = DateTime.UtcNow - startTime;
                
                // Service is healthy
                var status = ServiceHealthStatus.Healthy("InputService is functioning normally", responseTime)
                    .WithDetail("InputActionsEnabled", true)
                    .WithDetail("ActiveContextsCount", _activeContexts.Count)
                    .WithDetail("HighestPriorityLayer", _sortedContexts.Count > 0 ? _sortedContexts[0].Layer.ToString() : "None")
                    .WithDetail("ConfigurationValid", _config != null)
                    .WithDetail("DetailedLogging", _config?.EnableDetailedLogging ?? false);

                await UniTask.CompletedTask;
                return status;
            }
            catch (Exception ex)
            {
                return ServiceHealthStatus.Unhealthy($"Health check failed: {ex.Message}")
                    .WithDetail("Exception", ex.GetType().Name)
                    .WithDetail("Initialized", _isInitialized);
            }
        }

        #endregion

        #region Device Information

        public InputDeviceType CurrentDeviceType => _deviceTracker?.CurrentDeviceType ?? InputDeviceType.Unknown;

        public bool IsGamepadConnected => _deviceTracker?.IsGamepadConnected ?? false;

        #endregion

        #region Input Rebinding

        public IRebindableAction Rebinding => _rebindingManager;

        #endregion

        #region Player Input State

        public bool IsActionPressed(PlayerAction action)
        {
            if (!_isInitialized || IsActionBlocked(action))
                return false;

            return InputActionMappings.IsActionPressed(action);
        }

        public bool IsActionTriggered(PlayerAction action)
        {
            if (!_isInitialized || IsActionBlocked(action))
                return false;

            return InputActionMappings.IsActionTriggered(action);
        }

        public bool IsActionReleased(PlayerAction action)
        {
            if (!_isInitialized || IsActionBlocked(action))
                return false;

            return InputActionMappings.IsActionReleased(action);
        }

        public float GetFloatValue(PlayerAction action)
        {
            if (!_isInitialized || IsActionBlocked(action))
                return 0f;

            var rawValue = InputActionMappings.GetFloatValue(action);
            return _sensitivityProcessor?.ApplySensitivity(rawValue, action) ?? rawValue;
        }

        public Vector2 GetVector2Value(PlayerAction action)
        {
            if (!_isInitialized || IsActionBlocked(action))
                return Vector2.zero;

            var rawValue = InputActionMappings.GetVector2Value(action);
            return _sensitivityProcessor?.ApplySensitivity(rawValue, action) ?? rawValue;
        }

        #endregion

        #region UI Input State

        public bool IsActionPressed(UIAction action)
        {
            if (!_isInitialized || IsActionBlocked(action))
                return false;

            return InputActionMappings.IsActionPressed(action);
        }

        public bool IsActionTriggered(UIAction action)
        {
            if (!_isInitialized || IsActionBlocked(action))
                return false;

            return InputActionMappings.IsActionTriggered(action);
        }

        public bool IsActionReleased(UIAction action)
        {
            if (!_isInitialized || IsActionBlocked(action))
                return false;

            return InputActionMappings.IsActionReleased(action);
        }

        public float GetFloatValue(UIAction action)
        {
            if (!_isInitialized || IsActionBlocked(action))
                return 0f;

            var rawValue = InputActionMappings.GetFloatValue(action);
            return _sensitivityProcessor?.ApplySensitivity(rawValue, action) ?? rawValue;
        }

        public Vector2 GetVector2Value(UIAction action)
        {
            if (!_isInitialized || IsActionBlocked(action))
                return Vector2.zero;

            var rawValue = InputActionMappings.GetVector2Value(action);
            return _sensitivityProcessor?.ApplySensitivity(rawValue, action) ?? rawValue;
        }

        #endregion

        #region Input Context Management

        public IInputContextHandle RegisterContext(InputContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            lock (_contextLock)
            {
                if (_activeContexts.Count >= _config.MaxContextStackSize)
                {
                    Debug.LogWarning($"[InputService] Maximum contexts limit reached ({_config.MaxContextStackSize}). Cannot register new context.");
                    return null;
                }

                _activeContexts[context.Id] = context;
                _contextsDirty = true;
                
                if (_config.EnableDetailedLogging)
                {
#if DEBUG
                    var debugInfo = !string.IsNullOrEmpty(context.DebugInfo) ? $" ({context.DebugInfo})" : "";
                    Debug.Log($"[InputService] Registered context: {context.Id:D} Layer={context.Layer} Priority={context.EffectivePriority}{debugInfo}");
#else
                    Debug.Log($"[InputService] Registered context: {context.Id:D} Layer={context.Layer} Priority={context.EffectivePriority}");
#endif
                }
            }

            // Create handle for lifecycle management
            var handle = new InputContextHandle(context.Id, context.Layer, context.EffectivePriority, RemoveContext);
            
            // Notify listeners of context changes
            NotifyContextsChanged();
            
            return handle;
        }

        public System.Collections.Generic.IReadOnlyList<InputContext> GetActiveContexts()
        {
            lock (_contextLock)
            {
                if (_contextsDirty)
                {
                    RefreshSortedContexts();
                }
                return _sortedContexts.AsReadOnly();
            }
        }

        public InputContext GetHandlingContext(PlayerAction action)
        {
            lock (_contextLock)
            {
                if (_contextsDirty)
                {
                    RefreshSortedContexts();
                }
                
                foreach (var context in _sortedContexts)
                {
                    if (context.IsAllowed(action))
                    {
                        return context;
                    }
                    if (context.ConsumeInput)
                    {
                        return null; // Context blocks but doesn't allow
                    }
                }
            }
            return null;
        }

        public InputContext GetHandlingContext(UIAction action)
        {
            lock (_contextLock)
            {
                if (_contextsDirty)
                {
                    RefreshSortedContexts();
                }
                
                foreach (var context in _sortedContexts)
                {
                    if (context.IsAllowed(action))
                    {
                        return context;
                    }
                    if (context.ConsumeInput)
                    {
                        return null; // Context blocks but doesn't allow
                    }
                }
            }
            return null;
        }

        #endregion

        #region Private Context Management

        /// <summary>
        /// Remove a context by ID (called by InputContextHandle)
        /// </summary>
        /// <param name="contextId">ID of context to remove</param>
        private void RemoveContext(Guid contextId)
        {
            lock (_contextLock)
            {
                if (_activeContexts.Remove(contextId))
                {
                    _contextsDirty = true;
                    
                    if (_config.EnableDetailedLogging)
                    {
                        Debug.Log($"[InputService] Removed context: {contextId:D}");
                    }
                }
            }
            
            // Notify listeners of context changes
            NotifyContextsChanged();
        }
        
        /// <summary>
        /// Refresh the sorted contexts list (priority order)
        /// </summary>
        private void RefreshSortedContexts()
        {
            _sortedContexts.Clear();
            _sortedContexts.AddRange(_activeContexts.Values);
            _sortedContexts.Sort((a, b) => b.EffectivePriority.CompareTo(a.EffectivePriority)); // Highest first
            _contextsDirty = false;
        }
        
        /// <summary>
        /// Notify listeners that context configuration has changed
        /// </summary>
        private void NotifyContextsChanged()
        {
            try
            {
                OnContextsChanged?.Invoke(GetActiveContexts());
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputService] Error notifying context change listeners: {ex.Message}");
            }
        }

        #endregion

        #region Configuration Management

        /// <summary>
        /// Update configuration at runtime (hot-reload support)
        /// </summary>
        public void UpdateConfiguration(InputServiceConfiguration newConfig)
        {
            if (newConfig == null)
            {
                Debug.LogWarning("[InputService] Cannot update with null configuration");
                return;
            }

            if (!_isInitialized)
            {
                Debug.LogWarning("[InputService] Cannot update configuration before initialization");
                return;
            }

            try
            {
                // Update device tracker multipliers
                _deviceTracker?.UpdateCachedMultipliers(newConfig);
                
                // Update sensitivity processor
                _sensitivityProcessor?.UpdateConfiguration(newConfig);
                
                // Re-apply input update rate
                ApplyInputUpdateRate();
                
                if (_config.EnableDetailedLogging)
                {
                    Debug.Log("[InputService] Configuration hot-reloaded successfully");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputService] Failed to update configuration: {ex.Message}");
            }
        }

        #endregion

        #region Binding Persistence

        /// <summary>
        /// Save current input bindings to persistent storage
        /// </summary>
        private async UniTask SaveBindingsAsync()
        {
            if (_rebindingManager == null || _bindingPersistence == null)
                return;

            try
            {
                var success = await _rebindingManager.SaveBindingsAsync();
                if (_config.EnableDetailedLogging && success)
                {
                    Debug.Log("[InputService] Input bindings saved successfully");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputService] Failed to save input bindings: {ex.Message}");
            }
        }

        /// <summary>
        /// Load input bindings from persistent storage
        /// </summary>
        private async UniTask LoadBindingsAsync()
        {
            if (_rebindingManager == null || _bindingPersistence == null)
                return;

            try
            {
                var success = await _rebindingManager.LoadBindingsAsync();
                if (_config.EnableDetailedLogging && success)
                {
                    Debug.Log("[InputService] Input bindings loaded successfully");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputService] Failed to load input bindings: {ex.Message}");
            }
        }

        #endregion

        #region Utility Methods

        public async UniTask WaitForAction(PlayerAction action)
        {
            while (!IsActionTriggered(action))
            {
                await UniTask.Yield();
            }
        }

        public async UniTask WaitForAction(UIAction action)
        {
            while (!IsActionTriggered(action))
            {
                await UniTask.Yield();
            }
        }

        #endregion

        #region Private Implementation

        private async UniTask InitializeInputActionsAsync()
        {
            try
            {
                // Create new instance of input actions
                _inputActions = new InputSystem_Actions();
                
                // Enable all action maps
                _inputActions.Enable();
                
                Debug.Log("[InputService] Unity Input System actions initialized and enabled.");
                await UniTask.CompletedTask;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputService] Failed to initialize input actions: {ex.Message}");
                throw;
            }
        }

        private void InitializeActionMappings()
        {
            try
            {
                // Initialize the static delegate cache
                InputActionMappings.Initialize(_inputActions);
                Debug.Log("[InputService] Action mappings cache initialized.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputService] Failed to initialize action mappings: {ex.Message}");
                throw;
            }
        }

        private async UniTask InitializeConfigurationSystems(ISaveLoadService saveLoadService)
        {
            try
            {
                // Initialize device tracker
                _deviceTracker = new InputDeviceTracker(_config);
                _deviceTracker.Initialize();
                _deviceTracker.OnDeviceChanged += OnInputDeviceChanged;

                // Initialize sensitivity processor
                _sensitivityProcessor = new InputSensitivityProcessor(_config, _deviceTracker);

                // Initialize binding persistence with SaveLoadService
                _bindingPersistence = new InputBindingPersistence(_config, saveLoadService);

                // Initialize rebinding manager with binding persistence
                _rebindingManager = new InputRebindingManager(_inputActions, _config, _bindingPersistence);

                // Load saved bindings
                await LoadBindingsAsync();

                Debug.Log("[InputService] Configuration systems initialized (device tracker, sensitivity processor, rebinding manager)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputService] Failed to initialize configuration systems: {ex.Message}");
                throw;
            }
        }

        private void ApplyInputUpdateRate()
        {
            try
            {
                // Apply update rate to Unity Input System
                if (_config.InputUpdateRate > 0)
                {
                    var settings = UnityEngine.InputSystem.InputSystem.settings;
                    if (settings != null)
                    {
                        // Set update mode based on configuration
                        float targetInterval = 1.0f / _config.InputUpdateRate;
                        
                        if (_config.InputUpdateRate >= 60)
                        {
                            settings.updateMode = UnityEngine.InputSystem.InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
                        }
                        else
                        {
                            settings.updateMode = UnityEngine.InputSystem.InputSettings.UpdateMode.ProcessEventsInFixedUpdate;
                            Time.fixedDeltaTime = targetInterval;
                        }
                        
                        if (_config.EnableDetailedLogging)
                        {
                            Debug.Log($"[InputService] Input update rate set to {_config.InputUpdateRate} Hz");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InputService] Failed to apply input update rate: {ex.Message}");
            }
        }

        private void OnInputDeviceChanged(InputDeviceType oldDevice, InputDeviceType newDevice)
        {
            if (_config.EnableDetailedLogging)
            {
                Debug.Log($"[InputService] Input device changed from {oldDevice} to {newDevice}");
            }

            // Fire service-level event
            OnDeviceChanged?.Invoke(oldDevice, newDevice);
        }

        private void SubscribeToGeneratedEvents()
        {
            // Subscribe to the auto-generated events from InputActionMappings
            InputActionMappings.OnPlayerActionTriggered += HandlePlayerAction;
            InputActionMappings.OnUIActionTriggered += HandleUIAction;
            
            if (_config.EnableDetailedLogging)
            {
                Debug.Log("[InputService] Subscribed to generated events from InputActionMappings");
            }
        }

        private void UnsubscribeFromGeneratedEvents()
        {
            // Unsubscribe from the auto-generated events
            InputActionMappings.OnPlayerActionTriggered -= HandlePlayerAction;
            InputActionMappings.OnUIActionTriggered -= HandleUIAction;
            
            if (_config.EnableDetailedLogging)
            {
                Debug.Log("[InputService] Unsubscribed from generated events from InputActionMappings");
            }
        }

        private void HandlePlayerAction(PlayerAction action, UnityEngine.InputSystem.InputActionPhase phase)
        {
            try
            {
                // Apply context-aware filtering
                if (!IsActionBlocked(action))
                {
                    if (_config.EnableDetailedLogging)
                    {
                        Debug.Log($"[InputService] Player action '{action}' {phase}");
                    }

                    // Broadcast the event to subscribers
                    OnPlayerActionTriggered?.Invoke(action, phase);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputService] Error handling player action event: {ex.Message}");
            }
        }

        private void HandleUIAction(UIAction action, UnityEngine.InputSystem.InputActionPhase phase)
        {
            try
            {
                // Apply context-aware filtering
                if (!IsActionBlocked(action))
                {
                    if (_config.EnableDetailedLogging)
                    {
                        Debug.Log($"[InputService] UI action '{action}' {phase}");
                    }

                    // Broadcast the event to subscribers
                    OnUIActionTriggered?.Invoke(action, phase);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InputService] Error handling UI action event: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a PlayerAction is blocked by the current context configuration
        /// Uses priority-based context processing
        /// </summary>
        /// <param name="action">PlayerAction to check</param>
        /// <returns>True if blocked, false if allowed</returns>
        private bool IsActionBlocked(PlayerAction action)
        {
            lock (_contextLock)
            {
                if (_contextsDirty)
                {
                    RefreshSortedContexts();
                }
                
                // Process contexts in priority order (highest first)
                foreach (var context in _sortedContexts)
                {
                    if (context.IsAllowed(action))
                    {
                        return false; // Allowed by this context
                    }
                    if (context.ConsumeInput)
                    {
                        return true; // Blocked by consuming context
                    }
                    // Otherwise continue to next context
                }
                
                // No context handled it - allow by default
                return false;
            }
        }

        /// <summary>
        /// Check if a UIAction is blocked by the current context configuration
        /// Uses priority-based context processing
        /// </summary>
        /// <param name="action">UIAction to check</param>
        /// <returns>True if blocked, false if allowed</returns>
        private bool IsActionBlocked(UIAction action)
        {
            lock (_contextLock)
            {
                if (_contextsDirty)
                {
                    RefreshSortedContexts();
                }
                
                // Process contexts in priority order (highest first)
                foreach (var context in _sortedContexts)
                {
                    if (context.IsAllowed(action))
                    {
                        return false; // Allowed by this context
                    }
                    if (context.ConsumeInput)
                    {
                        return true; // Blocked by consuming context
                    }
                    // Otherwise continue to next context
                }
                
                // No context handled it - allow by default
                return false;
            }
        }

        #endregion
    }
}