using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Commands;
using Sinkii09.Engine.Common.Script;
using Sinkii09.Engine.Services.Performance;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Service implementation for executing and controlling script playback
    /// </summary>
    [EngineService(ServiceCategory.Core, ServicePriority.High,
        Description = "Manages script execution and playback control with comprehensive command processing",
        RequiredServices = new[] { typeof(IScriptService), typeof(IResourceService), typeof(ISaveLoadService) })]
    [ServiceConfiguration(typeof(ScriptPlayerConfiguration))]
    public class ScriptPlayerService : IScriptPlayerService
    {
        #region Private Fields
        private readonly ScriptPlayerConfiguration _config;
        private IScriptService _scriptService;
        private IResourceService _resourceService;
        private ISaveLoadService _saveloadService;

        private ScriptExecutionContext _executionContext;
        private readonly Dictionary<string, ICommand> _commandCache;
        private readonly Queue<string> _executionHistory;
        
        private CancellationTokenSource _executionCancellationTokenSource;
        private readonly SemaphoreSlim _executionSemaphore;
        private readonly object _stateLock = new object();

        // Enhanced Error Handling Components
        private TimeoutManager _timeoutManager;
        private ErrorRecoveryManager _errorRecoveryManager;
        private ServicePerformanceMonitor _performanceMonitor;
        
        // Extracted Component Architecture
        private ScriptExecutionEngine _executionEngine;
        private PlaybackStateManager _stateManager;
        private ScriptMetricsCollector _metricsCollector;
        private ResourcePreloader _resourcePreloader;
        private CommandResultPool _commandResultPool;

        // Event handler references for proper unsubscription
        private Action<PlaybackState, PlaybackState> _stateChangedHandler;
        private Action<ICommand> _commandExecutingHandler;
        private Action<ICommand> _commandExecutedHandler;
        private Action<ICommand, Exception> _commandFailedHandler;

        private bool _isInitialized;
        private bool _isDisposed;
        private bool _isFastForwarding;
        private float _currentPlaybackSpeed;

        private Stopwatch _executionStopwatch;
        private DateTime _lastAutoSaveTime;
        #endregion

        #region Properties
        public PlaybackState State => _stateManager?.State ?? PlaybackState.Idle;

        public Script CurrentScript => _executionContext?.Script;
        public int CurrentLineIndex => _executionContext?.CurrentLineIndex ?? -1;
        public ICommand CurrentCommand => _executionEngine?.CurrentCommand;
        
        public float PlaybackSpeed 
        { 
            get => _stateManager?.PlaybackSpeed ?? _currentPlaybackSpeed;
            set => _stateManager?.SetPlaybackSpeed(value);
        }

        public bool IsPlaying => State == PlaybackState.Playing;
        public bool IsPaused => State == PlaybackState.Paused;
        public bool IsWaiting => State == PlaybackState.Waiting;
        public float Progress => _executionContext?.GetProgress() ?? 0f;
        #endregion

        #region Events
        public event Action<Script> ScriptStarted;
        public event Action<Script, ScriptExecutionResult> ScriptCompleted;
        public event Action<Script, Exception> ScriptFailed;
        public event Action<ScriptLine, int> LineExecuting;
        public event Action<ScriptLine, int> LineExecuted;
        public event Action<ICommand> CommandExecuting;
        public event Action<ICommand> CommandExecuted;
        public event Action<PlaybackState, PlaybackState> StateChanged;
        public event Action<float> ProgressChanged;
        public event Action<int> BreakpointHit;
        public event Action<string, object> VariableChanged;
        #endregion

        #region Constructor
        public ScriptPlayerService(
            ScriptPlayerConfiguration config,
            IScriptService scriptService,
            IResourceService resourceService,
            IServiceProvider serviceProvider)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _executionContext = new ScriptExecutionContext
            {
                MaxCallStackDepth = _config.MaxExecutionStackDepth
            };

            _commandCache = new Dictionary<string, ICommand>(_config.CommandCacheSize);
            _executionHistory = new Queue<string>(_config.MaxExecutionHistorySize);
            _executionSemaphore = new SemaphoreSlim(1, 1);
            
            _currentPlaybackSpeed = _config.DefaultPlaybackSpeed;
            _executionStopwatch = new Stopwatch();
            _lastAutoSaveTime = DateTime.UtcNow;

            // Initialize enhanced error handling components
            _timeoutManager = new TimeoutManager(_config);
            _errorRecoveryManager = new ErrorRecoveryManager(_config);
        }
        #endregion

        #region IEngineService Implementation
        public async UniTask<ServiceInitializationResult> InitializeAsync(IServiceProvider provider, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_isInitialized)
                {
                    return ServiceInitializationResult.Success();
                }

                Debug.Log($"Initializing ScriptPlayerService with configuration: {_config.GetConfigurationSummary()}");

                _resourceService = Engine.GetService<IResourceService>() ;
                _saveloadService = Engine.GetService<ISaveLoadService>();
                _scriptService = Engine.GetService<IScriptService>();
                _performanceMonitor = Engine.GetService<ServicePerformanceMonitor>();

                // Validate dependencies
                if (_scriptService == null)
                {
                    return ServiceInitializationResult.Failed(new InvalidOperationException("ScriptService dependency is required"));
                }

                if (_resourceService == null)
                {
                    return ServiceInitializationResult.Failed(new InvalidOperationException("ResourceService dependency is required"));
                }

                if (_saveloadService == null)
                {
                    return ServiceInitializationResult.Failed(new InvalidOperationException("SaveLoadService dependency is required"));
                }

                // Initialize execution context
                _executionContext.Reset();

                // Initialize extracted components first
                InitializeExtractedComponents();

                // Set initial state through state manager
                _stateManager.TransitionToIdle();

                // Connect performance monitor to enhanced error handling components
                if (_performanceMonitor != null)
                {
                    _timeoutManager.PerformanceMonitor = _performanceMonitor;
                    _errorRecoveryManager.PerformanceMonitor = _performanceMonitor;
                }

                _isInitialized = true;
                
                await UniTask.Yield();

                return ServiceInitializationResult.Success();
            }
            catch (Exception ex)
            {
                Debug.LogError($"ScriptPlayerService initialization failed: {ex.Message}");
                return ServiceInitializationResult.Failed(ex);
            }
        }

        public UniTask<ServiceHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_isInitialized)
                {
                    return UniTask.FromResult(ServiceHealthStatus.Unhealthy("Service not initialized"));
                }

                if (_isDisposed)
                {
                    return UniTask.FromResult(ServiceHealthStatus.Unhealthy("Service disposed"));
                }

                // Gather enhanced health information
                var timeoutStats = _timeoutManager.GetStats();
                var recoveryStats = _errorRecoveryManager.GetStatistics();

                var healthInfo = $"State: {State}, " +
                                $"Script: {CurrentScript?.Name ?? "None"}, " +
                                $"Line: {CurrentLineIndex}, " +
                                $"Cache: {_commandCache.Count}/{_config.CommandCacheSize}, " +
                                $"Timeouts: {timeoutStats.ActiveTimeouts} active / {timeoutStats.TrackedCommandTypes} types, " +
                                $"Recovery: {recoveryStats.SuccessRate:P1} success rate ({recoveryStats.SuccessfulRecoveries}/{recoveryStats.TotalAttempts})";

                return UniTask.FromResult(ServiceHealthStatus.Healthy(healthInfo));
            }
            catch (Exception ex)
            {
                return UniTask.FromResult(ServiceHealthStatus.Unhealthy($"Health check failed: {ex.Message}"));
            }
        }

        public async UniTask<ServiceShutdownResult> ShutdownAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_isDisposed)
                {
                    return ServiceShutdownResult.Success();
                }

                Debug.Log("Shutting down ScriptPlayerService...");

                // Stop any running execution
                await StopAsync();

                // Clean up resources
                _executionCancellationTokenSource?.Cancel();
                _executionCancellationTokenSource?.Dispose();
                _executionSemaphore?.Dispose();

                _commandCache.Clear();
                _executionHistory.Clear();
                _executionContext.Reset();

                // Unsubscribe from events to prevent memory leaks
                UnsubscribeFromEvents();
                
                // Dispose all IDisposable components in reverse order of initialization
                _resourcePreloader?.Dispose();
                _executionEngine?.Dispose();
                _stateManager?.Dispose();
                _timeoutManager?.Dispose();
                _commandResultPool?.Dispose();
                _executionContext?.Dispose();
                
                // Cleanup remaining components that don't implement IDisposable
                _errorRecoveryManager?.CleanupHistory();
                _metricsCollector?.ResetMetrics();
                
                // Log final statistics for debugging
                if (_config.EnablePerformanceMonitoring && Application.isEditor)
                {
                    var finalTimeoutStats = _timeoutManager?.GetStats();
                    var finalRecoveryStats = _errorRecoveryManager?.GetStatistics();
                    
                    Debug.Log($"[ScriptPlayer] Final Statistics - " +
                             $"Timeouts: {finalTimeoutStats?.TrackedCommandTypes ?? 0} types tracked, " +
                             $"Recovery: {finalRecoveryStats?.SuccessRate:P1} success rate");
                }

                _isDisposed = true;
                Debug.Log("ScriptPlayerService shutdown completed");

                return ServiceShutdownResult.Success();
            }
            catch (Exception ex)
            {
                Debug.LogError($"ScriptPlayerService shutdown failed: {ex.Message}");
                return ServiceShutdownResult.Failed(ex);
            }
        }
        #endregion

        #region Execution Control
        public async UniTask<ScriptExecutionResult> PlayScriptAsync(Script script, CancellationToken cancellationToken = default)
        {
            if (script == null)
                throw new ArgumentNullException(nameof(script));

            if (!_isInitialized)
                throw new InvalidOperationException("ScriptPlayerService is not initialized");

            await _executionSemaphore.WaitAsync(cancellationToken);
            try
            {
                // Stop any current execution
                if (State != PlaybackState.Idle)
                {
                    await StopAsync();
                }

                // Initialize execution context
                _executionContext.Reset();
                _executionContext.Script = script;
                _executionContext.CurrentLineIndex = 0;
                _executionContext.StartTime = DateTime.UtcNow;
                _executionContext.CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                _stateManager.TransitionToLoading();
                _executionStopwatch.Restart();

                // Preprocess the script
                await PreprocessScriptAsync(script, cancellationToken);

                // Preload script resources for better performance
                if (_config.EnablePerformanceMonitoring)
                {
                    try
                    {
                        await _resourcePreloader.PreloadScriptResourcesAsync(cancellationToken);
                        if (_config.LogExecutionFlow)
                        {
                            var stats = _resourcePreloader.GetPreloadStats();
                            Debug.Log($"[ScriptPlayer] Resource preloading completed: {stats}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ScriptPlayer] Resource preloading failed: {ex.Message}");
                        // Continue execution even if preloading fails
                    }
                }

                // Fire script started event
                ScriptStarted?.Invoke(script);

                // Start execution
                _stateManager.TransitionToPlaying();
                var result = await ExecuteScriptAsync(_executionContext.CancellationTokenSource.Token);

                // Fire completion event
                ScriptCompleted?.Invoke(script, result);

                return result;
            }
            finally
            {
                _executionSemaphore.Release();
            }
        }

        public async UniTask<ScriptExecutionResult> PlayScriptAsync(string scriptName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(scriptName))
                throw new ArgumentException("Script name cannot be null or empty", nameof(scriptName));

            if (!_isInitialized)
                throw new InvalidOperationException("ScriptPlayerService is not initialized");

            try
            {
                _stateManager.TransitionToLoading();
                var script = await _scriptService.LoadScriptAsync(scriptName, cancellationToken);
                
                if (script == null)
                {
                    throw new InvalidOperationException($"Failed to load script: {scriptName}");
                }

                return await PlayScriptAsync(script, cancellationToken);
            }
            catch (Exception ex)
            {
                _stateManager.TransitionToFailed();
                ScriptFailed?.Invoke(null, ex);
                return ScriptExecutionResult.CreateFailure($"Failed to load script '{scriptName}': {ex.Message}", ex);
            }
        }

        public async UniTask<ScriptExecutionResult> PlayFromLineAsync(int lineIndex, CancellationToken cancellationToken = default)
        {
            if (CurrentScript == null)
                throw new InvalidOperationException("No script is loaded");

            if (lineIndex < 0 || lineIndex >= CurrentScript.Lines.Count)
                throw new ArgumentOutOfRangeException(nameof(lineIndex));

            _executionContext.CurrentLineIndex = lineIndex;
            _stateManager.TransitionToPlaying();

            return await ExecuteScriptAsync(cancellationToken);
        }

        public async UniTask PauseAsync()
        {
            if (State == PlaybackState.Playing || State == PlaybackState.Waiting)
            {
                _stateManager.TransitionToPausedAsync().Forget();
                
                if (_config.LogExecutionFlow)
                {
                    Debug.Log($"Script execution paused at line {CurrentLineIndex}");
                }
                
                // Trigger GC optimization during pause for memory cleanup opportunity
                await _metricsCollector.TriggerGCOptimizationAsync("Script paused");
                
                await UniTask.Yield();
            }
        }

        public async UniTask ResumeAsync()
        {
            if (State == PlaybackState.Paused)
            {
                _stateManager.TransitionToPlaying();
                
                if (_config.LogExecutionFlow)
                {
                    Debug.Log($"Script execution resumed from line {CurrentLineIndex}");
                }
                
                await UniTask.Yield();
            }
        }

        public async UniTask StopAsync()
        {
            if (State != PlaybackState.Idle && State != PlaybackState.Stopped)
            {
                _executionCancellationTokenSource?.Cancel();
                _stateManager.TransitionToStopped();
                
                if (_config.LogExecutionFlow)
                {
                    Debug.Log($"Script execution stopped at line {CurrentLineIndex}");
                }
                
                await UniTask.Yield();
            }
        }

        public async UniTask ResetAsync()
        {
            await StopAsync();
            _executionContext.Reset();
            _commandCache.Clear();
            _stateManager.TransitionToIdle();
        }
        #endregion

        #region Navigation Control
        public async UniTask<bool> StepForwardAsync()
        {
            if (CurrentScript == null || _executionContext.IsAtEnd())
                return false;

            var previousState = State;
            
            try
            {
                // Enable step mode
                _executionContext.IsStepMode = true;
                _stateManager.TransitionToPlaying();

                // Get the current line and execute it
                var currentLine = CurrentScript.Lines[CurrentLineIndex];
                
                // Fire line executing event
                LineExecuting?.Invoke(currentLine, CurrentLineIndex);

                // Execute the current line
                var result = await ExecuteLineAsync(currentLine, CancellationToken.None);

                // Fire line executed event
                LineExecuted?.Invoke(currentLine, CurrentLineIndex);

                // Handle flow control
                if (!HandleFlowControl(result))
                {
                    // Move to next line normally if no flow control
                    _executionContext.CurrentLineIndex++;
                }
                
                _executionContext.LinesExecuted++;
                
                // Update progress
                ProgressChanged?.Invoke(Progress);

                if (_config.LogExecutionFlow)
                {
                    Debug.Log($"Step forward executed line {CurrentLineIndex - 1}, now at line {CurrentLineIndex}");
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Step forward failed at line {CurrentLineIndex}: {ex.Message}");
                return false;
            }
            finally
            {
                // Return to previous state
                if (previousState == PlaybackState.Playing)
                    _stateManager.TransitionToPausedAsync().Forget();
                else if (previousState == PlaybackState.Paused)
                    _stateManager.TransitionToPausedAsync().Forget();
                else
                    _stateManager.TransitionToIdle();
            }
        }

        public async UniTask<bool> StepBackwardAsync()
        {
            if (CurrentScript == null || CurrentLineIndex <= 0)
                return false;

            try
            {
                // Move to previous line (don't execute it, just position)
                _executionContext.CurrentLineIndex--;
                
                // Update progress
                ProgressChanged?.Invoke(Progress);

                if (_config.LogExecutionFlow)
                {
                    Debug.Log($"Step backward to line {CurrentLineIndex}");
                }

                await UniTask.Yield();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Step backward failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Execute a single step in step mode
        /// </summary>
        public async UniTask<bool> ExecuteStepAsync()
        {
            if (CurrentScript == null || _executionContext.IsAtEnd())
                return false;

            if (!_executionContext.IsStepMode)
            {
                _executionContext.IsStepMode = true;
            }

            return await StepForwardAsync();
        }

        public async UniTask SkipToLineAsync(int lineIndex)
        {
            if (CurrentScript == null)
                throw new InvalidOperationException("No script is loaded");

            if (lineIndex < 0 || lineIndex >= CurrentScript.Lines.Count)
                throw new ArgumentOutOfRangeException(nameof(lineIndex));

            _executionContext.CurrentLineIndex = lineIndex;
            ProgressChanged?.Invoke(Progress);
            
            await UniTask.Yield();
        }

        public async UniTask SkipToLabelAsync(string label)
        {
            if (string.IsNullOrEmpty(label))
                throw new ArgumentException("Label cannot be null or empty", nameof(label));

            var lineIndex = _executionContext.GetLabelLineIndex(label);
            if (lineIndex < 0)
                throw new InvalidOperationException($"Label '{label}' not found");

            await SkipToLineAsync(lineIndex);
        }

        public async UniTask FastForwardAsync(float speed = 2.0f)
        {
            if (CurrentScript == null)
                throw new InvalidOperationException("No script is loaded");

            var clampedSpeed = Mathf.Clamp(speed, 1.0f, _config.MaxFastForwardSpeed);
            
            _isFastForwarding = true;
            var previousSpeed = PlaybackSpeed;
            PlaybackSpeed = clampedSpeed;
            
            if (_config.LogExecutionFlow)
            {
                Debug.Log($"Fast forward enabled at {clampedSpeed}x speed (was {previousSpeed}x)");
            }
            
            await UniTask.Yield();
        }

        /// <summary>
        /// Stop fast forward mode and return to normal speed
        /// </summary>
        public async UniTask StopFastForwardAsync()
        {
            if (_isFastForwarding)
            {
                _isFastForwarding = false;
                PlaybackSpeed = _config.DefaultPlaybackSpeed;
                
                if (_config.LogExecutionFlow)
                {
                    Debug.Log($"Fast forward disabled, returned to normal speed ({PlaybackSpeed}x)");
                }
            }
            
            await UniTask.Yield();
        }

        /// <summary>
        /// Toggle fast forward mode on/off
        /// </summary>
        public async UniTask ToggleFastForwardAsync(float fastSpeed = 2.0f)
        {
            if (_isFastForwarding)
            {
                await StopFastForwardAsync();
            }
            else
            {
                await FastForwardAsync(fastSpeed);
            }
        }

        /// <summary>
        /// Set playback speed with validation
        /// </summary>
        public async UniTask SetPlaybackSpeedAsync(float speed)
        {
            var clampedSpeed = Mathf.Clamp(speed, 0.1f, _config.MaxFastForwardSpeed);
            var previousSpeed = PlaybackSpeed;
            
            PlaybackSpeed = clampedSpeed;
            _isFastForwarding = clampedSpeed > _config.DefaultPlaybackSpeed;
            
            if (_config.LogExecutionFlow)
            {
                Debug.Log($"Playback speed changed from {previousSpeed}x to {clampedSpeed}x");
            }
            
            await UniTask.Yield();
        }
        #endregion

        #region Execution Context
        public void SetVariable(string name, object value)
        {
            _executionContext.SetVariable(name, value);
            VariableChanged?.Invoke(name, value);
        }

        public object GetVariable(string name)
        {
            return _executionContext.GetVariable(name);
        }

        public bool HasVariable(string name)
        {
            return _executionContext.HasVariable(name);
        }

        public void ClearVariables()
        {
            _executionContext.ClearVariables();
        }

        public ScriptExecutionContext GetExecutionContext()
        {
            return _executionContext;
        }
        #endregion

        #region Breakpoint Management
        public void AddBreakpoint(int lineIndex)
        {
            _executionContext.AddBreakpoint(lineIndex);
        }

        public void RemoveBreakpoint(int lineIndex)
        {
            _executionContext.RemoveBreakpoint(lineIndex);
        }

        public void ClearBreakpoints()
        {
            _executionContext.ClearBreakpoints();
        }

        public bool HasBreakpoint(int lineIndex)
        {
            return _executionContext.HasBreakpoint(lineIndex);
        }
        #endregion

        #region Save/Load State
        public async UniTask<string> SaveExecutionStateAsync()
        {
            if (!_config.EnableStateSaving)
                throw new InvalidOperationException("State saving is disabled in configuration");

            if (_saveloadService == null)
                throw new InvalidOperationException("SaveLoadService is not available");

            var snapshot = _executionContext.CreateSnapshot();
            
            // Generate a unique key for this execution state
            var stateKey = $"ScriptPlayer_ExecutionState_{DateTime.UtcNow.Ticks}";
            
            // Save using cached SaveLoadService - snapshot now inherits from SaveData
            var saveResult = await _saveloadService.SaveAsync(stateKey, snapshot);
            if (!saveResult.Success)
            {
                throw new InvalidOperationException($"Failed to save execution state: {saveResult.Message}");
            }
            
            return stateKey;
        }

        public async UniTask LoadExecutionStateAsync(string stateKey)
        {
            if (!_config.EnableStateSaving)
                throw new InvalidOperationException("State saving is disabled in configuration");

            if (string.IsNullOrEmpty(stateKey))
                throw new ArgumentException("State key cannot be null or empty", nameof(stateKey));

            if (_saveloadService == null)
                throw new InvalidOperationException("SaveLoadService is not available");

            // Load using cached SaveLoadService - directly load the snapshot
            var loadResult = await _saveloadService.LoadAsync<ScriptExecutionContextSnapshot>(stateKey);
            if (!loadResult.Success || loadResult.Data == null)
            {
                throw new InvalidOperationException($"Failed to load execution state with key '{stateKey}': {loadResult.Message}");
            }

            var snapshot = loadResult.Data;
            _executionContext.RestoreFromSnapshot(snapshot);
            
            // Reload the script if needed
            if (!string.IsNullOrEmpty(snapshot.ScriptName) && CurrentScript == null)
            {
                var script = await _scriptService.LoadScriptAsync(snapshot.ScriptName);
                _executionContext.Script = script;
            }
            
            _stateManager.TransitionToPausedAsync().Forget();
            ProgressChanged?.Invoke(Progress);
        }
        #endregion

        #region Script Preprocessing
        /// <summary>
        /// Preprocesses a script before execution to validate, scan labels, and prepare commands
        /// </summary>
        private async UniTask PreprocessScriptAsync(Script script, CancellationToken cancellationToken)
        {
            if (script?.Lines == null)
            {
                throw new InvalidOperationException("Script or script lines are null");
            }

            if (_config.LogExecutionFlow)
            {
                Debug.Log($"Preprocessing script '{script.Name}' with {script.Lines.Count} lines");
            }

            var preprocessingErrors = new List<string>();
            var labelCount = 0;
            var commandCount = 0;

            // First pass: Scan for labels and validate script structure
            for (int i = 0; i < script.Lines.Count; i++)
            {
                var line = script.Lines[i];
                if (line == null)
                {
                    preprocessingErrors.Add($"Line {i} is null");
                    continue;
                }

                try
                {
                    // Handle different line types during preprocessing
                    switch (line)
                    {
                        case CommandScriptLine commandLine:
                            await PreprocessCommandLineAsync(commandLine, i, preprocessingErrors, cancellationToken);
                            commandCount++;
                            break;

                        case GenericTextLine textLine:
                            PreprocessTextLine(textLine, i);
                            
                            // Check for labels during preprocessing
                            var lineText = GetLineText(textLine);
                            if (!string.IsNullOrEmpty(lineText) && lineText.StartsWith("#"))
                            {
                                var labelName = lineText.Substring(1).Trim();
                                if (!string.IsNullOrEmpty(labelName))
                                {
                                    if (_executionContext.HasLabel(labelName))
                                    {
                                        preprocessingErrors.Add($"Duplicate label '{labelName}' at line {i}");
                                    }
                                    else
                                    {
                                        _executionContext.RegisterLabel(labelName, i);
                                        labelCount++;
                                        
                                        if (_config.LogExecutionFlow)
                                        {
                                            Debug.Log($"Registered label '{labelName}' at line {i}");
                                        }
                                    }
                                }
                            }
                            break;

                        default:
                            if (_config.LogExecutionFlow)
                            {
                                Debug.LogWarning($"Unknown line type {line.GetType().Name} at line {i}");
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    preprocessingErrors.Add($"Error preprocessing line {i}: {ex.Message}");
                }

                // Yield occasionally during preprocessing to avoid blocking
                if (i % 50 == 0)
                {
                    await UniTask.Yield();
                }
            }

            // Report preprocessing results
            if (_config.LogExecutionFlow)
            {
                Debug.Log($"Script preprocessing completed: {labelCount} labels, {commandCount} commands found");
            }

            // Handle preprocessing errors
            if (preprocessingErrors.Count > 0)
            {
                var errorMessage = $"Script preprocessing found {preprocessingErrors.Count} errors:\n" +
                                   string.Join("\n", preprocessingErrors.Take(10)); // Limit to first 10 errors

                if (_config.ContinueOnError)
                {
                    Debug.LogWarning($"Script preprocessing warnings for '{script.Name}':\n{errorMessage}");
                }
                else
                {
                    throw new InvalidOperationException($"Script preprocessing failed for '{script.Name}':\n{errorMessage}");
                }
            }
        }

        private async UniTask PreprocessCommandLineAsync(CommandScriptLine commandLine, int lineIndex, List<string> errors, CancellationToken cancellationToken)
        {
            if (commandLine.Command == null)
            {
                errors.Add($"Command line {lineIndex} has null command");
                return;
            }

            // Validate command if it has validation methods
            try
            {
                // Here you could add command-specific validation
                // For example, checking required parameters, etc.
                
                if (_config.LogCommandExecution)
                {
                    Debug.Log($"Validated command {commandLine.Command.GetType().Name} at line {lineIndex}");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Command validation failed at line {lineIndex}: {ex.Message}");
            }

            await UniTask.Yield();
        }

        private void PreprocessTextLine(GenericTextLine textLine, int lineIndex)
        {
            // Text line preprocessing - could validate dialog syntax, check for variables, etc.
            var lineText = GetLineText(textLine);
            
            // Example: Check for variable references like {variableName}
            if (!string.IsNullOrEmpty(lineText) && lineText.Contains("{") && lineText.Contains("}"))
            {
                if (_config.LogExecutionFlow)
                {
                    Debug.Log($"Text line {lineIndex} contains variable references: {lineText}");
                }
            }
        }
        #endregion

        #region Private Execution Methods
        private async UniTask<ScriptExecutionResult> ExecuteScriptAsync(CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            var linesExecuted = 0;
            var commandsExecuted = 0;

            try
            {
                while (_executionContext.CanContinue() && !cancellationToken.IsCancellationRequested)
                {
                    // Handle pause state
                    while (State == PlaybackState.Paused && !cancellationToken.IsCancellationRequested)
                    {
                        await UniTask.Delay(100, cancellationToken: cancellationToken);
                    }

                    // Check for stop
                    if (State == PlaybackState.Stopped)
                    {
                        return ScriptExecutionResult.CreateStopped(linesExecuted, 
                            (float)(DateTime.UtcNow - startTime).TotalSeconds, commandsExecuted);
                    }

                    // Get current line
                    var currentLine = CurrentScript.Lines[CurrentLineIndex];
                    
                    // Check for breakpoint
                    if (_config.PauseOnBreakpoints && HasBreakpoint(CurrentLineIndex))
                    {
                        BreakpointHit?.Invoke(CurrentLineIndex);
                        _stateManager.TransitionToPausedAsync().Forget();
                        continue;
                    }

                    // Fire line executing event
                    LineExecuting?.Invoke(currentLine, CurrentLineIndex);

                    // Execute the line
                    var result = await ExecuteLineAsync(currentLine, cancellationToken);
                    
                    linesExecuted++;
                    if (currentLine is CommandScriptLine)
                    {
                        commandsExecuted++;
                    }

                    // Fire line executed event
                    LineExecuted?.Invoke(currentLine, CurrentLineIndex);

                    // Handle flow control
                    if (HandleFlowControl(result))
                    {
                        // Flow control handled the line advancement
                        continue;
                    }

                    // Update progress
                    _executionContext.CurrentLineIndex++;
                    _executionContext.LinesExecuted = linesExecuted;
                    _executionContext.CommandsExecuted = commandsExecuted;
                    ProgressChanged?.Invoke(Progress);

                    // Add line delay if configured
                    if (_config.DefaultLineDelay > 0 && !_isFastForwarding)
                    {
                        var delay = (int)(_config.DefaultLineDelay * 1000 / PlaybackSpeed);
                        await UniTask.Delay(delay, cancellationToken: cancellationToken);
                    }

                    // Check for auto-save
                    if (_config.EnableAutoSave && 
                        (DateTime.UtcNow - _lastAutoSaveTime).TotalSeconds >= _config.AutoSaveInterval)
                    {
                        await SaveExecutionStateAsync();
                        _lastAutoSaveTime = DateTime.UtcNow;
                    }
                }

                // Script completed successfully
                _stateManager.TransitionToCompleted();
                var executionTime = (float)(DateTime.UtcNow - startTime).TotalSeconds;
                
                // Trigger GC optimization after script completion for memory cleanup
                await _metricsCollector.TriggerGCOptimizationAsync("Script completion");
                
                return ScriptExecutionResult.CreateSuccess(linesExecuted, executionTime, commandsExecuted);
            }
            catch (OperationCanceledException)
            {
                _stateManager.TransitionToStopped();
                var executionTime = (float)(DateTime.UtcNow - startTime).TotalSeconds;
                return ScriptExecutionResult.CreateCancelled(linesExecuted, executionTime, commandsExecuted);
            }
            catch (Exception ex)
            {
                _stateManager.TransitionToFailed();
                ScriptFailed?.Invoke(CurrentScript, ex);
                return ScriptExecutionResult.CreateFailure(ex.Message, ex, CurrentLineIndex);
            }
        }

        private async UniTask<CommandResult> ExecuteLineAsync(ScriptLine line, CancellationToken cancellationToken)
        {
            if (line == null)
                return CommandResult.Success();

            // Handle different line types
            switch (line)
            {
                case CommandScriptLine commandLine:
                    return await ExecuteCommandLineAsync(commandLine, cancellationToken);
                    
                case GenericTextLine textLine:
                    await ExecuteTextLineAsync(textLine, cancellationToken);
                    return CommandResult.Success();
                    
                default:
                    await ExecuteUnknownLineAsync(line, cancellationToken);
                    return CommandResult.Success();
            }
        }

        private async UniTask<CommandResult> ExecuteCommandLineAsync(CommandScriptLine commandLine, CancellationToken cancellationToken)
        {
            if (commandLine?.Command == null)
            {
                if (_config.LogExecutionFlow)
                {
                    Debug.LogWarning($"Command line {CurrentLineIndex} has null command, skipping");
                }
                return CommandResult.Success();
            }

            return await _executionEngine.ExecuteCommandAsync(commandLine.Command, cancellationToken);
        }

        private async UniTask ExecuteTextLineAsync(GenericTextLine textLine, CancellationToken cancellationToken)
        {
            if (_config.LogExecutionFlow)
            {
                Debug.Log($"Executing text line {CurrentLineIndex}: {textLine}");
            }

            // Check if this is a label line (starts with #)
            var lineText = GetLineText(textLine);
            if (!string.IsNullOrEmpty(lineText))
            {
                if (lineText.StartsWith("#"))
                {
                    // This is a label, register it if not already registered
                    var labelName = lineText.Substring(1).Trim();
                    if (!string.IsNullOrEmpty(labelName))
                    {
                        _executionContext.RegisterLabel(labelName, CurrentLineIndex);
                        
                        if (_config.LogExecutionFlow)
                        {
                            Debug.Log($"Registered label '{labelName}' at line {CurrentLineIndex}");
                        }
                    }
                }
                else if (lineText.StartsWith("//") || lineText.StartsWith(";"))
                {
                    // This is a comment, just log it if needed
                    if (_config.LogExecutionFlow)
                    {
                        Debug.Log($"Comment at line {CurrentLineIndex}: {lineText}");
                    }
                }
                else
                {
                    // Regular text line - could be dialog or narrative
                    await ProcessTextContentAsync(lineText, cancellationToken);
                }
            }

            // Text lines typically don't need async processing unless they trigger events
            await UniTask.Yield();
        }

        private async UniTask ExecuteUnknownLineAsync(ScriptLine line, CancellationToken cancellationToken)
        {
            if (_config.LogExecutionFlow)
            {
                Debug.LogWarning($"Unknown line type {line.GetType().Name} at line {CurrentLineIndex}: {line}");
            }
            
            await UniTask.Yield();
        }

        private async UniTask ProcessTextContentAsync(string textContent, CancellationToken cancellationToken)
        {
            // TODO: Implement text content processing logic
            // This is where you could integrate with dialog systems, UI display, etc.
            // For now, just log the text content
            if (_config.LogExecutionFlow)
            {
                Debug.Log($"Text content at line {CurrentLineIndex}: {textContent}");
            }
            
            // Apply line delay for text content if configured
            if (_config.DefaultLineDelay > 0 && !_isFastForwarding)
            {
                var delay = (int)(_config.DefaultLineDelay * 1000 / PlaybackSpeed);
                await UniTask.Delay(delay, cancellationToken: cancellationToken);
            }
        }

        private string GetLineText(ScriptLine line)
        {
            // This is a helper method to extract the original line text
            // Since we don't store the original text in ScriptLine, we'll need to work with what we have
            // In a full implementation, you might want to store the original text in ScriptLine
            // TODO: For now, just return a placeholder
            return line?.ToString() ?? string.Empty;
        }

        private async UniTask<CommandResult> ExecuteCommandAsync(ICommand command, CancellationToken cancellationToken)
        {
            if (command == null)
                return CommandResult.Success();

            _stateManager.TransitionToWaiting();

            var commandType = command.GetType();
            var metadata = CommandMetadataCache.GetMetadata(commandType);
            var retryCount = 0;
            CommandResult result = null;
            var startTime = DateTime.Now;

            try
            {
                // Fire command executing event
                CommandExecuting?.Invoke(command);

                if (_config.LogCommandExecution)
                {
                    Debug.Log($"[ScriptPlayer] Executing {metadata.Alias} command at line {CurrentLineIndex} (timeout: {metadata.Timeout}s, retries: {metadata.MaxRetries})");
                }

                // Execute command with enhanced error handling
                while (retryCount <= metadata.MaxRetries)
                {
                    try
                    {
                        // Execute command with type-based timeout management
                        result = await _timeoutManager.ExecuteWithTimeoutAsync(
                            commandType,
                            async (token) =>
                            {
                                // Check if command supports CommandResult pattern
                                if (command is IFlowControlCommand flowCommand)
                                {
                                    return await flowCommand.ExecuteWithResultAsync(token);
                                }
                                else
                                {
                                    // Execute traditional command
                                    await command.ExecuteAsync(token);
                                    return CommandResult.Success();
                                }
                            },
                            cancellationToken);

                        // Success - record performance and cache if needed
                        var executionTime = DateTime.Now - startTime;
                        var memoryUsage = GC.GetTotalMemory(false);
                        
                        _timeoutManager.RecordCommandCompletion(commandType, executionTime, true);
                        _performanceMonitor?.RecordCommandMetrics(commandType, memoryUsage, executionTime, false, retryCount > 0);
                        _executionContext.PerformanceMetrics?.RecordCommandExecution(commandType, (float)executionTime.TotalSeconds);

                        if (_config.EnableCommandCaching)
                        {
                            CacheCommand(GetCommandCacheKey(command), command);
                        }

                        // Fire command executed event
                        CommandExecuted?.Invoke(command);
                        break; // Success, exit retry loop
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        // Main cancellation - don't retry, just propagate
                        var executionTime = DateTime.Now - startTime;
                        var memoryUsage = GC.GetTotalMemory(false);
                        
                        _timeoutManager.RecordCommandCompletion(commandType, executionTime, false);
                        _performanceMonitor?.RecordCommandMetrics(commandType, memoryUsage, executionTime, true, retryCount > 0);
                        _executionContext.PerformanceMetrics?.RecordCommandExecution(commandType, (float)executionTime.TotalSeconds);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        var executionTime = DateTime.Now - startTime;
                        var memoryUsage = GC.GetTotalMemory(false);
                        
                        _timeoutManager.RecordCommandCompletion(commandType, executionTime, false, ex);
                        _performanceMonitor?.RecordCommandMetrics(commandType, memoryUsage, executionTime, false, retryCount > 0);
                        _executionContext.PerformanceMetrics?.RecordCommandExecution(commandType, (float)executionTime.TotalSeconds);

                        // Create enhanced script execution error
                        var scriptError = CreateScriptExecutionError(ex, commandType, retryCount);
                        
                        // Check if we should retry
                        if (metadata.ShouldRetry(retryCount, ex) && retryCount < metadata.MaxRetries)
                        {
                            retryCount++;
                            scriptError = scriptError.WithRetryAttempt(retryCount);

                            Debug.LogWarning($"[ScriptPlayer] {scriptError.GetSummary()} - Retrying ({retryCount}/{metadata.MaxRetries})");

                            // Apply retry delay strategy
                            var retryDelay = metadata.CalculateRetryDelay(retryCount);
                            if (retryDelay > 0)
                            {
                                await UniTask.Delay((int)(retryDelay * 1000), cancellationToken: cancellationToken);
                            }
                            
                            startTime = DateTime.Now; // Reset timer for retry
                            continue;
                        }

                        // Retry exhausted or not retryable - attempt recovery
                        result = await AttemptCommandRecovery(scriptError, cancellationToken);
                        break;
                    }
                }
            }
            finally
            {
                _stateManager.TransitionToPlaying();
            }

            return result ?? CommandResult.Success();
        }

        /// <summary>
        /// Handle flow control actions from command results
        /// </summary>
        /// <param name="result">The command result to process</param>
        /// <returns>True if flow control was handled, false if normal execution should continue</returns>
        private bool HandleFlowControl(CommandResult result)
        {
            if (result == null || !result.IsSuccess)
            {
                return false; // Continue normal execution
            }

            switch (result.FlowAction)
            {
                case FlowControlAction.Continue:
                    return false; // Normal execution

                case FlowControlAction.JumpToLine:
                    if (result.TargetLineIndex >= 0 && result.TargetLineIndex < CurrentScript.Lines.Count)
                    {
                        _executionContext.CurrentLineIndex = result.TargetLineIndex;
                        if (_config.LogExecutionFlow)
                        {
                            Debug.Log($"[ScriptPlayer] Flow control: Jump to line {result.TargetLineIndex}");
                        }
                        return true; // Flow control handled
                    }
                    else
                    {
                        Debug.LogError($"[ScriptPlayer] Invalid jump target line index: {result.TargetLineIndex}");
                        return false;
                    }

                case FlowControlAction.JumpToLabel:
                    if (!string.IsNullOrEmpty(result.TargetLabel))
                    {
                        var targetLine = _executionContext.GetLabelLineIndex(result.TargetLabel);
                        if (targetLine >= 0)
                        {
                            _executionContext.CurrentLineIndex = targetLine;
                            if (_config.LogExecutionFlow)
                            {
                                Debug.Log($"[ScriptPlayer] Flow control: Jump to label '{result.TargetLabel}' at line {targetLine}");
                            }
                            return true; // Flow control handled
                        }
                        else
                        {
                            Debug.LogError($"[ScriptPlayer] Label not found: {result.TargetLabel}");
                            return false;
                        }
                    }
                    return false;

                case FlowControlAction.Stop:
                    _stateManager.TransitionToStopped();
                    if (_config.LogExecutionFlow)
                    {
                        Debug.Log($"[ScriptPlayer] Flow control: Stop execution - {result.ErrorMessage}");
                    }
                    return true; // Flow control handled

                case FlowControlAction.Return:
                    // Handle return from subroutine/nested call
                    if (_executionContext.CallStack.Count > 0)
                    {
                        var frame = _executionContext.CallStack.Pop();
                        _executionContext.CurrentLineIndex = frame.ReturnLineIndex;
                        if (_config.LogExecutionFlow)
                        {
                            Debug.Log($"[ScriptPlayer] Flow control: Return to line {frame.ReturnLineIndex}");
                        }
                        return true;
                    }
                    else
                    {
                        // No call stack, end script
                        _stateManager.TransitionToCompleted();
                        return true;
                    }

                case FlowControlAction.CallScript:
                    // TODO: Implement script calling
                    Debug.LogWarning($"[ScriptPlayer] Script calling not yet implemented: {result.TargetLabel}");
                    return false;

                default:
                    Debug.LogWarning($"[ScriptPlayer] Unknown flow control action: {result.FlowAction}");
                    return false;
            }
        }

        /// <summary>
        /// Creates a ScriptExecutionError from an exception with enhanced context
        /// </summary>
        private ScriptExecutionError CreateScriptExecutionError(Exception ex, Type commandType, int retryCount)
        {
            var metadata = CommandMetadataCache.GetMetadata(commandType);
            
            // Determine error category and severity based on exception type
            var (category, severity) = ClassifyError(ex);
            
            var error = new ScriptExecutionError(
                ex.Message,
                severity,
                category,
                commandType,
                CurrentLineIndex,
                CurrentScript?.Name,
                isRetryable: metadata.ShouldRetry(retryCount, ex),
                recoveryAction: GetRecoveryActionSuggestion(ex, metadata),
                innerException: ex)
                .WithRetryAttempt(retryCount)
                .WithContext("CommandAlias", metadata.Alias)
                .WithContext("CommandCategory", metadata.Category.ToString())
                .WithContext("ExecutionTime", DateTime.Now);

            return error;
        }

        /// <summary>
        /// Attempts to recover from a command execution error using the recovery manager
        /// </summary>
        private async UniTask<CommandResult> AttemptCommandRecovery(ScriptExecutionError error, CancellationToken cancellationToken)
        {
            try
            {
                var recoveryResult = await _errorRecoveryManager.AttemptRecoveryAsync(error, _executionContext, cancellationToken);
                
                if (recoveryResult.Success)
                {
                    Debug.Log($"[ScriptPlayer] Error recovery successful: {recoveryResult.Message}");
                    
                    return recoveryResult.Action switch
                    {
                        RecoveryAction.Skip => CommandResult.Success(), // Continue execution
                        RecoveryAction.Jump => CommandResult.JumpToLine(recoveryResult.TargetLine ?? CurrentLineIndex),
                        RecoveryAction.Stop => CommandResult.Stop("Recovery requested execution stop"),
                        RecoveryAction.Rollback => CommandResult.Failed($"Rollback recovery: {recoveryResult.Message}", error),
                        RecoveryAction.Retry => CommandResult.Failed($"Retry recovery: {recoveryResult.Message}", error), // Will be handled by caller
                        _ => CommandResult.Success()
                    };
                }
                else
                {
                    Debug.LogError($"[ScriptPlayer] Error recovery failed: {recoveryResult.Message}");
                    return HandleUnrecoverableError(error);
                }
            }
            catch (Exception recoveryException)
            {
                Debug.LogError($"[ScriptPlayer] Recovery mechanism failed: {recoveryException.Message}");
                return HandleUnrecoverableError(error);
            }
        }

        /// <summary>
        /// Handles errors that cannot be recovered from
        /// </summary>
        private CommandResult HandleUnrecoverableError(ScriptExecutionError error)
        {
            var errorMessage = error.GetDetailedMessage();
            
            if (_config.LogErrorsToFile)
            {
                Debug.LogError($"[ScriptPlayer] Unrecoverable Error:\n{errorMessage}");
            }

            // Fire script failed event
            ScriptFailed?.Invoke(CurrentScript, error);

            if (_config.ContinueOnError || _config.SkipErrorLines)
            {
                Debug.LogWarning($"[ScriptPlayer] Continuing execution despite error: {error.GetSummary()}");
                return CommandResult.Success(); // Continue execution
            }
            else
            {
                return CommandResult.Stop($"Critical error: {error.Message}");
            }
        }

        /// <summary>
        /// Classifies an exception into error category and severity
        /// </summary>
        private (ErrorCategory category, ErrorSeverity severity) ClassifyError(Exception ex)
        {
            return ex switch
            {
                OperationCanceledException => (ErrorCategory.Timeout, ErrorSeverity.Recoverable),
                ArgumentException or ArgumentNullException => (ErrorCategory.Validation, ErrorSeverity.Critical),
                InvalidOperationException => (ErrorCategory.StateManagement, ErrorSeverity.Critical),
                UnauthorizedAccessException => (ErrorCategory.Security, ErrorSeverity.Critical),
                System.IO.FileNotFoundException or System.IO.DirectoryNotFoundException => (ErrorCategory.ResourceLoading, ErrorSeverity.Critical),
                NetworkInformationException => (ErrorCategory.Network, ErrorSeverity.Recoverable),
                NotImplementedException => (ErrorCategory.Configuration, ErrorSeverity.Fatal),
                OutOfMemoryException => (ErrorCategory.Unknown, ErrorSeverity.Fatal),
                _ => (ErrorCategory.Unknown, ErrorSeverity.Critical)
            };
        }

        /// <summary>
        /// Provides recovery action suggestions based on error type and command metadata
        /// </summary>
        private string GetRecoveryActionSuggestion(Exception ex, CommandMetadata metadata)
        {
            return ex switch
            {
                OperationCanceledException => "Increase timeout or check for infinite loops",
                ArgumentException => "Validate command parameters and syntax",
                InvalidOperationException => "Check service dependencies and system state",
                System.IO.FileNotFoundException => "Verify resource paths and file availability",
                NetworkInformationException => "Check network connectivity",
                _ when metadata.Category == CommandCategory.ResourceLoading => "Verify resource exists and is accessible",
                _ when metadata.Category == CommandCategory.Network => "Check network settings and endpoints", 
                _ when metadata.Category == CommandCategory.Audio => "Check audio system and device availability",
                _ => "Review command usage and system configuration"
            };
        }

        private string GetCommandCacheKey(ICommand command)
        {
            if (command == null)
                return null;

            return $"{command.GetType().Name}_{CurrentScript?.Name}_{CurrentLineIndex}";
        }

        private void CacheCommand(string key, ICommand command)
        {
            if (_commandCache.Count >= _config.CommandCacheSize)
            {
                var firstKey = _commandCache.Keys.First();
                _commandCache.Remove(firstKey);
            }

            _commandCache[key] = command;
            
            if (_config.LogCommandExecution)
            {
                Debug.Log($"Cached command: {key}");
            }
        }

        /// <summary>
        /// Trigger GC optimization through performance monitor if available
        /// </summary>
        private async UniTask TriggerGCOptimizationAsync(string reason)
        {
            if (_performanceMonitor == null)
                return;

            try
            {
                // Record GC event in context performance metrics
                var gcStartTime = DateTime.Now;
                
                // Request incremental GC through performance monitor
                await RequestGCOptimizationAsync();
                
                var gcTime = (float)(DateTime.Now - gcStartTime).TotalSeconds;
                _executionContext.PerformanceMetrics?.RecordGCEvent(gcTime);
                
                if (_config.LogExecutionFlow)
                {
                    Debug.Log($"GC optimization triggered: {reason} (took {gcTime:F3}s)");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"GC optimization failed for {reason}: {ex.Message}");
            }
        }

        /// <summary>
        /// Initialize the extracted component architecture
        /// </summary>
        private void InitializeExtractedComponents()
        {
            // Initialize event handlers
            _stateChangedHandler = (oldState, newState) => StateChanged?.Invoke(oldState, newState);
            _commandExecutingHandler = (cmd) => CommandExecuting?.Invoke(cmd);
            _commandExecutedHandler = (cmd) => CommandExecuted?.Invoke(cmd);
            _commandFailedHandler = (cmd, ex) => 
            {
                if (_config.LogCommandExecution)
                {
                    Debug.LogError($"[ScriptPlayer] Command {cmd.GetType().Name} failed: {ex.Message}");
                }
            };

            // Initialize state manager
            _stateManager = new PlaybackStateManager(_config, _executionContext);
            _stateManager.StateChanged += _stateChangedHandler;

            // Initialize metrics collector
            _metricsCollector = new ScriptMetricsCollector(_config, _executionContext, _performanceMonitor);

            // Initialize resource preloader
            _resourcePreloader = new ResourcePreloader(_config, _executionContext);

            // Initialize command result pool
            _commandResultPool = new CommandResultPool(_config);

            // Initialize execution engine
            _executionEngine = new ScriptExecutionEngine(
                _config, 
                _timeoutManager, 
                _errorRecoveryManager, 
                _performanceMonitor, 
                _executionContext);

            // Wire up execution engine events
            _executionEngine.CommandExecuting += _commandExecutingHandler;
            _executionEngine.CommandExecuted += _commandExecutedHandler;
            _executionEngine.CommandFailed += _commandFailedHandler;

            if (_config.LogExecutionFlow)
            {
                Debug.Log("[ScriptPlayer] Extracted component architecture initialized");
            }
        }

        /// <summary>
        /// Unsubscribe from all events to prevent memory leaks
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (_stateManager != null && _stateChangedHandler != null)
            {
                _stateManager.StateChanged -= _stateChangedHandler;
            }

            if (_executionEngine != null)
            {
                if (_commandExecutingHandler != null)
                    _executionEngine.CommandExecuting -= _commandExecutingHandler;
                    
                if (_commandExecutedHandler != null)
                    _executionEngine.CommandExecuted -= _commandExecutedHandler;
                    
                if (_commandFailedHandler != null)
                    _executionEngine.CommandFailed -= _commandFailedHandler;
            }
        }

        /// <summary>
        /// Request GC optimization from performance monitor
        /// </summary>
        private async UniTask RequestGCOptimizationAsync()
        {
            // Use reflection to call internal GC optimization method if available
            var gcManagerField = _performanceMonitor.GetType().GetField("_gcManager", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (gcManagerField?.GetValue(_performanceMonitor) is object gcManager)
            {
                var requestMethod = gcManager.GetType().GetMethod("RequestIncrementalGCAsync");
                if (requestMethod != null)
                {
                    var task = requestMethod.Invoke(gcManager, new object[] { 0 });
                    if (task is UniTask gcTask)
                    {
                        await gcTask;
                    }
                }
            }
        }
        #endregion
    }
}