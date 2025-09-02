using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using UnityEngine;
using Sinkii09.Engine.Services.Performance;
using Debug = UnityEngine.Debug;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Manages the lifecycle of all services including initialization, shutdown, and health monitoring
    /// Enhanced with performance optimizations: parallel initialization, memory management, and intelligent health monitoring
    /// </summary>
    public class ServiceLifecycleManager : IDisposable
    {
        private readonly IServiceContainer _container;
        private readonly Dictionary<Type, ServiceLifecycleInfo> _lifecycleInfo;
        private readonly CancellationTokenSource _healthCheckCts;
        private readonly object _lock = new object();
        private bool _disposed;
        private bool _isInitialized;
        
        // Performance optimization components
        private readonly ParallelServiceInitializer _parallelInitializer;
        private readonly ServiceMetadataCache _metadataCache;
        private readonly ServiceObjectPool<List<string>> _stringListPool;
        private readonly ServiceObjectPool<List<Type>> _typeListPool;
        
        // Performance settings
        private readonly bool _enableParallelInitialization;
        private readonly bool _enablePerformanceOptimizations;
        
        /// <summary>
        /// Whether all services have been successfully initialized
        /// </summary>
        public bool IsInitialized => _isInitialized;
        
        public ServiceLifecycleManager(IServiceContainer container, bool enablePerformanceOptimizations = true)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _lifecycleInfo = new Dictionary<Type, ServiceLifecycleInfo>();
            _healthCheckCts = new CancellationTokenSource();
            _enablePerformanceOptimizations = enablePerformanceOptimizations;
            _enableParallelInitialization = enablePerformanceOptimizations;
            
            if (enablePerformanceOptimizations)
            {
                _parallelInitializer = new ParallelServiceInitializer(container);
                _metadataCache = new ServiceMetadataCache();
                
                // Initialize object pools for frequently created objects
                _stringListPool = new ServiceObjectPool<List<string>>(
                    factory: () => new List<string>(),
                    resetAction: list => list.Clear(),
                    maxPoolSize: 20,
                    autoScale: true
                );
                _typeListPool = new ServiceObjectPool<List<Type>>(
                    factory: () => new List<Type>(),
                    resetAction: list => list.Clear(),
                    maxPoolSize: 15,
                    autoScale: true
                );
                
                Debug.Log("ServiceLifecycleManager: Performance optimizations enabled with object pooling");
            }
        }
        
        
        #region Initialization
        
        /// <summary>
        /// Initialize all services in dependency order with performance optimizations
        /// </summary>
        public async UniTask<ServiceInitializationReport> InitializeAllAsync(CancellationToken cancellationToken = default)
        {
            var report = new ServiceInitializationReport();
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Get dependency graph (cached in ServiceContainer)
                var dependencyGraph = _container.BuildDependencyGraph();
                
                // Generate and log dependency analysis report
                var dependencyReport = dependencyGraph.GenerateReport();
                LogDependencyReport(dependencyReport);
                
                // Check for circular dependencies
                if (dependencyGraph.HasCircularDependencies)
                {
                    report.Success = false;
                    report.FailureReason = "Circular dependencies detected";
                    report.CircularDependencies = dependencyGraph.CircularDependencies
                        .Select(cycle => string.Join(" -> ", cycle.Select(t => t.Name)))
                        .ToList();
                    return report;
                }
                
                // Get initialization order (use async version for better performance with large graphs)
                var initOrder = await dependencyGraph.GetInitializationOrderAsync();
                report.TotalServices = initOrder.Count;
                
                // Use parallel initialization if enabled and beneficial
                if (_enableParallelInitialization && _parallelInitializer != null && initOrder.Count > 3)
                {
                    var parallelResult = await InitializeServicesInParallelAsync(initOrder.ToArray(), cancellationToken);
                    
                    report.InitializedServices = parallelResult.InitializedServices;
                    report.FailedServices = parallelResult.FailedServices;
                    report.Success = parallelResult.Success;
                    report.FailureReason = parallelResult.ErrorMessage;
                    report.ParallelInitializationResult = parallelResult;
                    
                    // Update lifecycle info for all services
                    foreach (var serviceType in parallelResult.InitializedServices)
                    {
                        var info = GetOrCreateLifecycleInfo(serviceType);
                        info.State = ServiceState.Running;
                        // Parallel timing is handled by ParallelServiceInitializer
                    }
                    
                    foreach (var serviceType in parallelResult.FailedServices)
                    {
                        var info = GetOrCreateLifecycleInfo(serviceType);
                        info.State = ServiceState.Error;
                    }
                }
                else
                {
                    // Fall back to sequential initialization
                    await InitializeServicesSequentiallyAsync(initOrder, report, cancellationToken);
                }
                
                report.TotalTime = stopwatch.Elapsed;
                
                // Start health monitoring if initialization succeeded
                if (report.Success)
                {
                    _isInitialized = true;
                    StartHealthMonitoring();
                    
                    // Precompile critical services if performance optimizations are enabled
                    if (_enablePerformanceOptimizations && _container is ServiceContainer serviceContainer)
                    {
                        var criticalServices = GetCriticalServices(initOrder);
                        if (criticalServices.Length > 0)
                        {
                            _ = serviceContainer.PrecompileCriticalServicesAsync(criticalServices);
                        }
                    }
                }
                
                return report;
            }
            catch (Exception ex)
            {
                report.Success = false;
                report.FailureReason = $"Unexpected error: {ex.Message}";
                report.TotalTime = stopwatch.Elapsed;
                return report;
            }
        }
        
        /// <summary>
        /// Initialize a specific service
        /// </summary>
        private async UniTask<bool> InitializeServiceAsync(Type serviceType, CancellationToken cancellationToken)
        {
            if (!_container.TryResolve(serviceType, out var service))
            {
                Debug.LogError($"Service {serviceType.Name} not found in container");
                return false;
            }
            
            if (service is IEngineService engineService)
            {
                // Set initializing state
                var info = GetOrCreateLifecycleInfo(serviceType);
                info.State = ServiceState.Initializing;
                
                // Update optimized state tracking
                if (_metadataCache != null)
                {
                    _metadataCache.UpdateOptimizedServiceState(serviceType, ServiceState.Initializing, TimeSpan.Zero, false);
                }
                
                // Check for initialization timeout
                var serviceAttribute = serviceType.GetEngineServiceAttribute();
                CancellationTokenSource timeoutCts = null;
                CancellationToken effectiveToken = cancellationToken;
                
                if (serviceAttribute.InitializationTimeout > 0)
                {
                    timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutCts.CancelAfter(serviceAttribute.InitializationTimeout);
                    effectiveToken = timeoutCts.Token;
                }
                
                try
                {
                    var result = await engineService.InitializeAsync(_container as IServiceProvider, effectiveToken);
                    return result.IsSuccess;
                }
                catch (OperationCanceledException) when (timeoutCts?.Token.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested)
                {
                    Debug.LogError($"Service {serviceType.Name} initialization timed out after {serviceAttribute.InitializationTimeout}ms");
                    info.State = ServiceState.Error;
                    return false;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Service {serviceType.Name} initialization failed: {ex.Message}");
                    info.State = ServiceState.Error;
                    return false;
                }
                finally
                {
                    timeoutCts?.Dispose();
                }
            }
            
            // Non-engine services are considered initialized
            return true;
        }
        
        #endregion
        
        #region Shutdown
        
        /// <summary>
        /// Shutdown all services in reverse dependency order
        /// </summary>
        public async UniTask<ServiceShutdownReport> ShutdownAllAsync(CancellationToken cancellationToken = default)
        {
            var report = new ServiceShutdownReport();
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Mark as not initialized since we're shutting down
                _isInitialized = false;
                
                // Stop health monitoring
                _healthCheckCts.Cancel();
                
                // Get shutdown order (reverse of initialization)
                var dependencyGraph = _container.BuildDependencyGraph();
                var shutdownOrder = await dependencyGraph.GetInitializationOrderAsync();
                shutdownOrder.Reverse();
                
                report.TotalServices = shutdownOrder.Count;
                
                // Shutdown services
                foreach (var serviceType in shutdownOrder)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        report.FailureReason = "Shutdown cancelled";
                        break;
                    }
                    
                    try
                    {
                        var success = await ShutdownServiceAsync(serviceType, cancellationToken);
                        
                        if (success)
                        {
                            report.ShutdownServices.Add(serviceType);
                            var info = GetOrCreateLifecycleInfo(serviceType);
                            info.State = ServiceState.Shutdown;
                        }
                        else
                        {
                            report.FailedServices.Add(serviceType);
                            report.Warnings.Add($"Failed to shutdown {serviceType.Name} gracefully");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Exception shutting down {serviceType.Name}: {ex.Message}");
                        report.FailedServices.Add(serviceType);
                        report.Errors.Add($"{serviceType.Name}: {ex.Message}");
                    }
                }
                
                report.Success = report.FailedServices.Count == 0;
                report.TotalTime = stopwatch.Elapsed;
                
                return report;
            }
            catch (Exception ex)
            {
                report.Success = false;
                report.FailureReason = $"Unexpected error: {ex.Message}";
                report.TotalTime = stopwatch.Elapsed;
                return report;
            }
        }
        
        /// <summary>
        /// Shutdown a specific service
        /// </summary>
        private async UniTask<bool> ShutdownServiceAsync(Type serviceType, CancellationToken cancellationToken)
        {
            if (!_container.TryResolve(serviceType, out var service))
            {
                return true; // Service not found, consider it shutdown
            }
            
            if (service is IEngineService engineService)
            {
                // Set shutting down state
                var info = GetOrCreateLifecycleInfo(serviceType);
                info.State = ServiceState.ShuttingDown;
                
                // Update optimized state tracking
                if (_metadataCache != null)
                {
                    _metadataCache.UpdateOptimizedServiceState(serviceType, ServiceState.ShuttingDown, TimeSpan.Zero, false);
                }
                
                try
                {
                    var result = await engineService.ShutdownAsync(cancellationToken);
                    return result.IsSuccess;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Service {serviceType.Name} shutdown failed: {ex.Message}");
                    info.State = ServiceState.Error;
                    return false;
                }
            }
            
            // Non-engine services
            if (service is IDisposable disposable)
            {
                disposable.Dispose();
            }
            
            return true;
        }
        
        #endregion
        
        #region Health Monitoring
        
        /// <summary>
        /// Start background health monitoring
        /// </summary>
        private void StartHealthMonitoring()
        {
            HealthMonitoringTask(_healthCheckCts.Token).Forget();
        }
        
        /// <summary>
        /// Background task for health monitoring
        /// </summary>
        private async UniTaskVoid HealthMonitoringTask(CancellationToken cancellationToken)
        {
            var healthCheckInterval = TimeSpan.FromSeconds(30); // Configurable
            
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await UniTask.Delay(healthCheckInterval, cancellationToken: cancellationToken);
                    
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    
                    await PerformHealthChecksAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error in health monitoring: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Perform health checks on all services
        /// </summary>
        public async UniTask<HealthCheckReport> PerformHealthChecksAsync()
        {
            var report = new HealthCheckReport();
            var services = _container.GetRegisteredServices()
                .Select(t => _container.TryResolve(t, out var s) ? s : null)
                .Where(s => s is IEngineService)
                .Cast<IEngineService>()
                .ToList();
            
            foreach (var service in services)
            {
                try
                {
                    var result = await service.HealthCheckAsync();
                    report.Results[service.GetType()] = result;
                    
                    var info = GetOrCreateLifecycleInfo(service.GetType());
                    info.LastHealthCheck = DateTime.UtcNow;
                    info.HealthStatus = result;
                    
                    if (!result.IsHealthy)
                    {
                        report.UnhealthyServices.Add(service.GetType());
                        Debug.LogWarning($"Service {service.GetType().Name} is unhealthy: {result.StatusMessage}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Health check failed for {service.GetType().Name}: {ex.Message}");
                    report.Results[service.GetType()] = ServiceHealthStatus.Unknown($"Health check failed: {ex.Message}");
                    report.FailedHealthChecks.Add(service.GetType());
                }
            }
            
            report.TotalServices = services.Count;
            report.HealthyServices = report.Results.Count(r => r.Value.IsHealthy);
            
            return report;
        }
        
        #endregion
        
        #region Service Restart
        
        /// <summary>
        /// Restart a specific service
        /// </summary>
        public async UniTask<bool> RestartServiceAsync(Type serviceType, CancellationToken cancellationToken = default)
        {
            Debug.Log($"Restarting service {serviceType.Name}");
            
            // Get dependent services that need to be stopped
            var dependencyGraph = _container.BuildDependencyGraph();
            var dependents = dependencyGraph.GetAllDependents(serviceType);
            
            // Shutdown dependent services
            foreach (var dependent in dependents.Reverse())
            {
                await ShutdownServiceAsync(dependent, cancellationToken);
            }
            
            // Shutdown the service
            await ShutdownServiceAsync(serviceType, cancellationToken);
            
            // Reinitialize the service
            var initSuccess = await InitializeServiceAsync(serviceType, cancellationToken);
            
            if (initSuccess)
            {
                // Reinitialize dependent services
                foreach (var dependent in dependents)
                {
                    await InitializeServiceAsync(dependent, cancellationToken);
                }
            }
            
            return initSuccess;
        }
        
        /// <summary>
        /// Attempt to recover failed services
        /// </summary>
        public async UniTask<ServiceRecoveryReport> RecoverFailedServicesAsync(CancellationToken cancellationToken = default)
        {
            var report = new ServiceRecoveryReport();
            
            var failedServices = _lifecycleInfo
                .Where(kvp => kvp.Value.State == ServiceState.Error)
                .Select(kvp => kvp.Key)
                .ToList();
            
            report.TotalFailedServices = failedServices.Count;
            
            foreach (var serviceType in failedServices)
            {
                try
                {
                    var success = await RestartServiceAsync(serviceType, cancellationToken);
                    
                    if (success)
                    {
                        report.RecoveredServices.Add(serviceType);
                    }
                    else
                    {
                        report.UnrecoverableServices.Add(serviceType);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to recover {serviceType.Name}: {ex.Message}");
                    report.UnrecoverableServices.Add(serviceType);
                }
            }
            
            report.Success = report.UnrecoverableServices.Count == 0;
            
            return report;
        }
        
        #endregion
        
        #region Lifecycle Information
        
        /// <summary>
        /// Get lifecycle information for a service
        /// </summary>
        public ServiceLifecycleInfo GetServiceLifecycleInfo(Type serviceType)
        {
            lock (_lock)
            {
                return _lifecycleInfo.TryGetValue(serviceType, out var info) ? info : null;
            }
        }
        
        /// <summary>
        /// Get all lifecycle information
        /// </summary>
        public IReadOnlyDictionary<Type, ServiceLifecycleInfo> GetAllLifecycleInfo()
        {
            lock (_lock)
            {
                return new Dictionary<Type, ServiceLifecycleInfo>(_lifecycleInfo);
            }
        }
        
        /// <summary>
        /// Get the current state of a service
        /// </summary>
        public ServiceState GetServiceState<T>() where T : class, IEngineService
        {
            return GetServiceState(typeof(T));
        }
        
        /// <summary>
        /// Get the current state of a service by type
        /// </summary>
        public ServiceState GetServiceState(Type serviceType)
        {
            var info = GetServiceLifecycleInfo(serviceType);
            return info?.State ?? ServiceState.Uninitialized;
        }
        
        /// <summary>
        /// Check if a service is in a specific state
        /// </summary>
        public bool IsServiceInState<T>(ServiceState state) where T : class, IEngineService
        {
            return GetServiceState<T>() == state;
        }
        
        /// <summary>
        /// Check if a service is in a specific state by type
        /// </summary>
        public bool IsServiceInState(Type serviceType, ServiceState state)
        {
            return GetServiceState(serviceType) == state;
        }
        
        private ServiceLifecycleInfo GetOrCreateLifecycleInfo(Type serviceType)
        {
            lock (_lock)
            {
                if (!_lifecycleInfo.TryGetValue(serviceType, out var info))
                {
                    info = new ServiceLifecycleInfo { ServiceType = serviceType };
                    _lifecycleInfo[serviceType] = info;
                }
                return info;
            }
        }
        
        /// <summary>
        /// Log dependency analysis report for diagnostics
        /// </summary>
        private void LogDependencyReport(ServiceDependencyReport report)
        {
            Debug.Log($"📊 Service Dependency Analysis: {report.TotalServices} services, max depth {report.MaxDepth}");
            
            if (report.CircularDependencies.Count > 0)
            {
                Debug.LogError($"⚠️ Found {report.CircularDependencies.Count} circular dependencies!");
                foreach (var cycle in report.CircularDependencies)
                {
                    var cycleText = string.Join(" → ", cycle.Select(t => t.Name));
                    Debug.LogError($"   Circular: {cycleText}");
                }
            }
            
            if (report.MaxDepth > 5)
            {
                Debug.LogWarning($"🔗 Deep dependency chain detected (depth: {report.MaxDepth})");
            }
            
            Debug.Log($"📈 Dependency Stats: {report.AverageDependencies:F1} avg deps, " +
                     $"{report.ServicesWithNoDependencies} roots, {report.ServicesWithNoDependents} leaves");
        }
        
        
        #endregion
        
        #region IDisposable
        
        public void Dispose()
        {
            if (_disposed)
                return;
            
            _healthCheckCts?.Cancel();
            _healthCheckCts?.Dispose();
            
            // Dispose performance optimization components
            // Memory monitor is owned by ServiceContainer, don't dispose here
            _metadataCache?.Clear();
            
            _disposed = true;
        }
        
        #endregion
        
        #region Performance Optimization Methods
        
        /// <summary>
        /// Initialize services using parallel initialization for better performance
        /// </summary>
        private async UniTask<ParallelInitializationResult> InitializeServicesInParallelAsync(Type[] serviceTypes, CancellationToken cancellationToken)
        {
            var options = new ParallelInitializationOptions
            {
                InitializationTimeout = TimeSpan.FromSeconds(30),
                ContinueOnFailure = false,
                MaxConcurrency = Environment.ProcessorCount
            };
            
            return await _parallelInitializer.InitializeServicesAsync(serviceTypes, cancellationToken, options);
        }
        
        /// <summary>
        /// Fall back to sequential initialization
        /// </summary>
        private async UniTask InitializeServicesSequentiallyAsync(List<Type> initOrder, ServiceInitializationReport report, CancellationToken cancellationToken)
        {
            foreach (var serviceType in initOrder)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    report.Success = false;
                    report.FailureReason = "Initialization cancelled";
                    break;
                }
                
                var serviceStopwatch = Stopwatch.StartNew();
                
                try
                {
                    var success = await InitializeServiceAsync(serviceType, cancellationToken);
                    
                    if (success)
                    {
                        report.InitializedServices.Add(serviceType);
                        var info = GetOrCreateLifecycleInfo(serviceType);
                        info.InitializationTime = serviceStopwatch.Elapsed;
                        info.State = ServiceState.Running;
                        
                        // Update optimized state tracking
                        if (_metadataCache != null)
                        {
                            _metadataCache.UpdateOptimizedServiceState(serviceType, ServiceState.Running, serviceStopwatch.Elapsed, true);
                        }
                    }
                    else
                    {
                        report.FailedServices.Add(serviceType);
                        report.Success = false;
                        report.FailureReason = $"Failed to initialize {serviceType.Name}";
                        var failedInfo = GetOrCreateLifecycleInfo(serviceType);
                        failedInfo.State = ServiceState.Error;
                        
                        // Update optimized state tracking
                        if (_metadataCache != null)
                        {
                            _metadataCache.UpdateOptimizedServiceState(serviceType, ServiceState.Error, TimeSpan.Zero, false);
                        }
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Exception initializing {serviceType.Name}: {ex.Message}");
                    report.FailedServices.Add(serviceType);
                    report.Errors.Add($"{serviceType.Name}: {ex.Message}");
                    report.Success = false;
                    report.FailureReason = $"Exception during initialization of {serviceType.Name}";
                    var errorInfo = GetOrCreateLifecycleInfo(serviceType);
                    errorInfo.State = ServiceState.Error;
                    
                    // Update optimized state tracking
                    if (_metadataCache != null)
                    {
                        _metadataCache.UpdateOptimizedServiceState(serviceType, ServiceState.Error, TimeSpan.Zero, false);
                    }
                    break;
                }
            }
        }
        
        /// <summary>
        /// Identify critical services that should be precompiled for optimal performance
        /// </summary>
        private Type[] GetCriticalServices(List<Type> allServices)
        {
            // For now, consider services with high priority as critical
            var criticalServices = new List<Type>();
            
            foreach (var serviceType in allServices)
            {
                if (_metadataCache != null)
                {
                    var attribute = _metadataCache.GetCachedServiceAttribute(serviceType, _container);
                    if (attribute?.Priority == ServicePriority.Critical)
                    {
                        criticalServices.Add(serviceType);
                    }
                }
            }
            
            return criticalServices.ToArray();
        }
        
        #endregion
    }
    
    #region Supporting Classes
    
    /// <summary>
    /// Information about a service's lifecycle
    /// </summary>
    public class ServiceLifecycleInfo
    {
        public Type ServiceType { get; set; }
        public ServiceState State { get; set; }
        public TimeSpan InitializationTime { get; set; }
        public DateTime LastHealthCheck { get; set; }
        public ServiceHealthStatus HealthStatus { get; set; }
        public int RestartCount { get; set; }
        public DateTime LastRestart { get; set; }
    }
    
    /// <summary>
    /// Report of service initialization
    /// </summary>
    public class ServiceInitializationReport
    {
        public bool Success { get; set; } = true;
        public string FailureReason { get; set; }
        public int TotalServices { get; set; }
        public List<Type> InitializedServices { get; set; } = new List<Type>();
        public List<Type> FailedServices { get; set; } = new List<Type>();
        public List<string> CircularDependencies { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
        public TimeSpan TotalTime { get; set; }
        public ParallelInitializationResult ParallelInitializationResult { get; set; }
    }
    
    /// <summary>
    /// Report of service shutdown
    /// </summary>
    public class ServiceShutdownReport
    {
        public bool Success { get; set; } = true;
        public string FailureReason { get; set; }
        public int TotalServices { get; set; }
        public List<Type> ShutdownServices { get; set; } = new List<Type>();
        public List<Type> FailedServices { get; set; } = new List<Type>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
        public TimeSpan TotalTime { get; set; }
    }
    
    /// <summary>
    /// Report of health checks
    /// </summary>
    public class HealthCheckReport
    {
        public int TotalServices { get; set; }
        public int HealthyServices { get; set; }
        public Dictionary<Type, ServiceHealthStatus> Results { get; set; } = new Dictionary<Type, ServiceHealthStatus>();
        public List<Type> UnhealthyServices { get; set; } = new List<Type>();
        public List<Type> FailedHealthChecks { get; set; } = new List<Type>();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Report of service recovery
    /// </summary>
    public class ServiceRecoveryReport
    {
        public bool Success { get; set; }
        public int TotalFailedServices { get; set; }
        public List<Type> RecoveredServices { get; set; } = new List<Type>();
        public List<Type> UnrecoverableServices { get; set; } = new List<Type>();
    }
    
    #endregion
}