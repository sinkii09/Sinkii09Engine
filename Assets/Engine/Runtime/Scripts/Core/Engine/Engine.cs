using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Configs;
using Sinkii09.Engine.Extensions;
using Sinkii09.Engine.Services;
using Sinkii09.Engine.Services.Performance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine
{
    public class Engine
    {
        /// <summary>  
        /// Whether the engine is initialized and ready.  
        /// </summary>  
        public static bool Initialized => _lifecycleManager?.IsInitialized ?? false;
        
        /// <summary>  
        /// Whether the engine is currently being initialized.  
        /// </summary>  
        public static bool Initializing => initializeTCS != null && initializeTCS.Task.Status == UniTaskStatus.Pending;
        
        public static IEgineBehaviour Behaviour { get; private set; }
        public static IConfigProvider ConfigProvider { get; private set; }
        
        private static ServiceContainer _serviceContainer;
        private static ServiceLifecycleManager _lifecycleManager;
        private static ServiceInitializationReport _lastInitializationReport;
        private static GCOptimizationManager _gcOptimizationManager;
        private static UniTaskCompletionSource<object> initializeTCS;
        private static CancellationTokenSource terminationCTS;
        
        /// <summary>
        /// Get a service instance from the container
        /// </summary>
        public static T GetService<T>() where T : class, IEngineService
        {
            if (_serviceContainer == null)
            {
                Debug.LogError("Engine is not initialized. Call InitializeAsync() first.");
                return null;
            }
            
            return _serviceContainer.Resolve<T>();
        }
        
        /// <summary>
        /// Try to get a service instance without exceptions
        /// </summary>
        public static bool TryGetService<T>(out T service) where T : class, IEngineService
        {
            service = null;
            if (_serviceContainer == null)
                return false;
                
            return _serviceContainer.TryResolve(out service);
        }
        
        /// <summary>
        /// Check if a service is registered
        /// </summary>
        public static bool IsServiceRegistered<T>() where T : class, IEngineService
        {
            return _serviceContainer?.IsRegistered<T>() ?? false;
        }
        
        /// <summary>
        /// Get configuration instance
        /// </summary>
        public static T GetConfig<T>() where T : Configuration
        {
            if (ConfigProvider == null)
            {
                Debug.LogError("ConfigProvider is not initialized. Please initialize the Engine first.");
                return null;
            }
            return ConfigProvider.GetConfiguration(typeof(T)) as T;
        }
        
        /// <summary>
        /// Get GC optimization statistics
        /// </summary>
        public static GCOptimizationManager.GCStatistics GetGCStatistics()
        {
            if (_gcOptimizationManager == null)
            {
                return default;
            }
            return _gcOptimizationManager.GetStatistics();
        }
        
        /// <summary>
        /// Request incremental garbage collection
        /// </summary>
        public static async UniTask RequestGCAsync(int generation = 0)
        {
            if (_gcOptimizationManager != null)
            {
                await _gcOptimizationManager.RequestIncrementalGCAsync(generation);
            }
        }
        
        /// <summary>
        /// Reset specific services (keeping others running)
        /// </summary>
        public static async UniTask ResetAsync(params Type[] exclude)
        {
            if (_lifecycleManager == null)
            {
                Debug.LogWarning("Engine is not initialized. Nothing to reset.");
                return;
            }
            
            try
            {
                var cancellationToken = terminationCTS?.Token ?? CancellationToken.None;
                
                // Get all registered service types
                var allServices = _serviceContainer.GetRegisteredServices().ToList();
                
                // Filter out excluded services
                var servicesToReset = allServices.Where(serviceType => 
                    exclude == null || exclude.Length == 0 || !exclude.Any(excludeType => excludeType.IsAssignableFrom(serviceType))
                ).ToList();
                
                Debug.Log($"Resetting {servicesToReset.Count} services...");
                
                // Restart each service that's not excluded
                foreach (var serviceType in servicesToReset)
                {
                    try
                    {
                        var success = await _lifecycleManager.RestartServiceAsync(serviceType, cancellationToken);
                        if (success)
                        {
                            Debug.Log($"Service {serviceType.Name} reset successfully");
                        }
                        else
                        {
                            Debug.LogWarning($"Failed to reset service {serviceType.Name}");
                        }
                    }
                    catch (Exception serviceEx)
                    {
                        Debug.LogError($"Error resetting service {serviceType.Name}: {serviceEx.Message}");
                    }
                }
                
                Debug.Log("Service reset completed");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during service reset: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Terminate the engine and all services
        /// </summary>
        public static async UniTask TerminateAsync()
        {
            Debug.Log("Terminating Engine...");
            
            // Cancel any ongoing operations
            terminationCTS?.Cancel();
            
            // Cancel initialization if it's in progress
            if (initializeTCS != null && initializeTCS.Task.Status == UniTaskStatus.Pending)
            {
                initializeTCS.TrySetCanceled();
            }
            
            // Clean up behaviour
            if (Behaviour != null)
            {
                try
                {
                    Behaviour.OnBehaviourDestroy -= () => TerminateAsync().Forget();
                    Behaviour.Destroy();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error destroying behaviour: {ex.Message}");
                }
                finally
                {
                    Behaviour = null;
                }
            }
            
            // Shutdown services
            if (_lifecycleManager != null)
            {
                try
                {
                    await _lifecycleManager.ShutdownAllAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error shutting down services: {ex.Message}");
                }
            }
            
            // Clean up resources
            ConfigProvider = null;
            _serviceContainer = null;
            _lifecycleManager = null;
            _lastInitializationReport = null;
            
            // Clean up GC optimization manager
            if (_gcOptimizationManager != null)
            {
                if (_gcOptimizationManager.gameObject != null)
                {
                    UnityEngine.Object.Destroy(_gcOptimizationManager.gameObject);
                }
                _gcOptimizationManager = null;
            }
            
            initializeTCS = null;
            terminationCTS?.Dispose();
            terminationCTS = null;
            
            Debug.Log("Engine termination completed");
        }
        
        /// <summary>
        /// Synchronous termination for compatibility
        /// </summary>
        public static void Terminate()
        {
            TerminateAsync().Forget();
        }

        /// <summary>
        /// Initialize the engine with automatic service discovery
        /// </summary>
        public static async UniTask InitializeAsync(IConfigProvider configProvider, IEgineBehaviour behaviour, CancellationToken cancellationToken = default)
        {
            if (Initialized) 
            {
                Debug.LogWarning("Engine is already initialized");
                return;
            }
            
            if (Initializing) 
            { 
                await initializeTCS.Task; 
                return; 
            }

            initializeTCS = new UniTaskCompletionSource<object>();
            terminationCTS = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                Debug.Log("Starting Engine initialization...");
                
                // Set core components
                Behaviour = behaviour ?? throw new ArgumentNullException(nameof(behaviour));
                ConfigProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));

                // Initialize GC optimization first (before any service allocations)
                InitializeGCOptimization();

                // Setup termination callback
                Behaviour.OnBehaviourDestroy += () => TerminateAsync().Forget();

                // Create service infrastructure with performance optimizations
                _serviceContainer = new ServiceContainer(enablePerformanceOptimizations: true);
                _lifecycleManager = new ServiceLifecycleManager(_serviceContainer, enablePerformanceOptimizations: true);

                // Discover and register services
                DiscoverAndRegisterServices();

                // Initialize all services
                var report = await _lifecycleManager.InitializeAllAsync(terminationCTS.Token);
                _lastInitializationReport = report;
                
                if (report.Success)
                {
                    Debug.Log($"Engine initialized successfully. {report.InitializedServices.Count} services started.");
                    Debug.Log($"Initialize Time: {report.TotalTime.TotalMilliseconds} ms");
                    initializeTCS?.TrySetResult(null);
                }
                else
                {
                    throw new InvalidOperationException($"Service initialization failed: {report.FailureReason}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Engine initialization failed: {ex.Message}");
                initializeTCS?.TrySetException(ex);
                
                // Clean up on failure
                await TerminateAsync();
                throw;
            }
        }
        
        /// <summary>
        /// Get service health status for diagnostics
        /// </summary>
        public static ServiceHealthStatus GetServiceHealth<T>() where T : class, IEngineService
        {
            if (_lifecycleManager == null)
                return ServiceHealthStatus.Unknown("Lifecycle manager not initialized");
                
            var info = _lifecycleManager.GetServiceLifecycleInfo(typeof(T));
            return info?.HealthStatus ?? ServiceHealthStatus.Unknown("Service lifecycle info not found");
        }
        
        /// <summary>
        /// Get service lifecycle information for diagnostics
        /// </summary>
        public static ServiceLifecycleInfo GetServiceInfo<T>() where T : class, IEngineService
        {
            return _lifecycleManager?.GetServiceLifecycleInfo(typeof(T));
        }
        
        /// <summary>
        /// Get all service lifecycle information for diagnostics
        /// </summary>
        public static IReadOnlyDictionary<Type, ServiceLifecycleInfo> GetAllServiceInfo()
        {
            return _lifecycleManager?.GetAllLifecycleInfo() ?? new Dictionary<Type, ServiceLifecycleInfo>();
        }
        
        /// <summary>
        /// Perform health checks on all services
        /// </summary>
        public static async UniTask<HealthCheckReport> PerformHealthChecksAsync()
        {
            if (_lifecycleManager == null)
            {
                return new HealthCheckReport();
            }
            
            return await _lifecycleManager.PerformHealthChecksAsync();
        }
        
        /// <summary>
        /// Get the last initialization report for diagnostics
        /// </summary>
        public static ServiceInitializationReport GetInitializationReport()
        {
            return _lastInitializationReport;
        }

        /// <summary>
        /// Discover and register all services using EngineServiceAttribute
        /// </summary>
        private static void DiscoverAndRegisterServices()
        {
            Debug.Log("Discovering services with EngineServiceAttribute...");
            int serviceCount = 0;
            int testServicesSkipped = 0;
            int typesProcessed = 0;

            try
            {
                // Log test service configuration
                ServiceTestUtils.LogTestServiceConfiguration();
                bool includeTestServices = ServiceTestUtils.ShouldIncludeTestServices();
                
                var exportedTypes = ReflectionUtils.ExportedDomainTypes;
                Debug.Log($"Processing {exportedTypes.Count} exported types...");
                
                foreach (var type in exportedTypes)
                {
                    typesProcessed++;
                    
                    try
                    {
                        // Add detailed logging for debugging
                        if (typesProcessed % 100 == 0)
                        {
                            Debug.Log($"Processed {typesProcessed}/{exportedTypes.Count} types...");
                        }
                        
                        // Use consistent attribute reading with ServiceContainer
                        var serviceAttribute = type.GetEngineServiceAttribute();
                        if (!serviceAttribute.InitializeAtRuntime)
                            continue;
                        
                        // Check if this is a test service and should be excluded
                        if (ServiceTestUtils.IsTestService(type) && !includeTestServices)
                        {
                            testServicesSkipped++;
                            Debug.Log($"Skipping test service: {type.Name} (Category: {serviceAttribute.Category})");
                            continue;
                        }

                        // Validate that it implements IEngineService
                        if (!typeof(IEngineService).IsAssignableFrom(type))
                        {
                            Debug.LogWarning($"Type {type.Name} has EngineServiceAttribute but doesn't implement IEngineService");
                            continue;
                        }

                        // Must be a concrete class
                        if (type.IsAbstract || type.IsInterface)
                        {
                            Debug.LogWarning($"Type {type.Name} has EngineServiceAttribute but is abstract or interface");
                            continue;
                        }

                        // Determine service interface
                        var serviceInterface = GetServiceInterface(type);

                        // Validate service before registration
                        if (!ValidateServiceForRegistration(type, serviceAttribute))
                        {
                            Debug.LogError($"Service {type.Name} failed validation and will not be registered");
                            continue;
                        }

                        // Register service using the available method
                        _serviceContainer.RegisterService(serviceInterface, type, serviceAttribute.Lifetime);
                        serviceCount++;

                        Debug.Log($"Registered service: {serviceInterface.Name} -> {type.Name} " +
                                 $"(Category: {serviceAttribute.Category}, Priority: {serviceAttribute.Priority}, Lifetime: {serviceAttribute.Lifetime})");
                    }
                    catch (Exception typeEx)
                    {
                        Debug.LogError($"Error processing type {type.FullName}: {typeEx.Message}");
                        // Continue processing other types
                    }
                }

                Debug.Log($"Service discovery completed. Processed {typesProcessed} types, registered {serviceCount} services, skipped {testServicesSkipped} test services.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Critical error during service discovery: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Validate service before registration
        /// </summary>
        private static bool ValidateServiceForRegistration(Type serviceType, EngineServiceAttribute attribute)
        {
            // Validate constructors exist
            var constructors = serviceType.GetConstructors();
            if (constructors.Length == 0)
            {
                Debug.LogError($"Service {serviceType.Name} has no public constructors");
                return false;
            }

            // Validate required dependencies will be resolvable (basic check)
            if (attribute.RequiredServices != null)
            {
                foreach (var dependency in attribute.RequiredServices)
                {
                    if (!typeof(IEngineService).IsAssignableFrom(dependency))
                    {
                        Debug.LogError($"Service {serviceType.Name} requires {dependency.Name} which is not an IEngineService");
                        return false;
                    }
                }
            }

            // Validate timeout is reasonable
            if (attribute.InitializationTimeout > 0 && attribute.InitializationTimeout < 100)
            {
                Debug.LogWarning($"Service {serviceType.Name} has very short initialization timeout: {attribute.InitializationTimeout}ms");
            }

            return true;
        }

        private static Type GetServiceInterface(Type serviceType)
        {
            // Look for a specific service interface (not IEngineService)
            var serviceInterfaces = serviceType.GetInterfaces()
                .Where(i => typeof(IEngineService).IsAssignableFrom(i) && i != typeof(IEngineService))
                .ToArray();

            if (serviceInterfaces.Length == 1)
                return serviceInterfaces[0];

            if (serviceInterfaces.Length > 1)
            {
                Debug.LogWarning($"Service {serviceType.Name} implements multiple service interfaces. Using first: {serviceInterfaces[0].Name}");
                return serviceInterfaces[0];
            }

            // If no specific interface, use the concrete type
            Debug.LogWarning($"Service {serviceType.Name} doesn't implement a specific service interface. Using concrete type.");
            return serviceType;
        }
        
        /// <summary>
        /// Initialize GC optimization system
        /// </summary>
        private static void InitializeGCOptimization()
        {
            try
            {
                // Initialize the GC optimization manager early
                _gcOptimizationManager = GCOptimizationManager.Instance;
                
                // Try to load custom settings from Resources
                var customSettings = Resources.Load<GCOptimizationSettings>("GCOptimizationSettings");
                if (customSettings != null)
                {
                    _gcOptimizationManager.UpdateSettings(customSettings);
                    Debug.Log("Loaded custom GC optimization settings from Resources");
                }
                else
                {
                    Debug.Log("Using default GC optimization settings");
                }
                
                var stats = _gcOptimizationManager.GetStatistics();
                Debug.Log($"GC Optimization initialized: {stats}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize GC optimization: {ex.Message}");
            }
        }
    }
}