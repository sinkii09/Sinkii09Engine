using Cysharp.Threading.Tasks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;
using ZLinq;
using Sinkii09.Engine.Services.Performance;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Main service container implementation with dependency injection support
    /// Enhanced with performance optimizations: caching, fast resolution, object pooling, and memory management
    /// </summary>
    public class ServiceContainer : IServiceContainer, IServiceProvider, IMemoryPressureResponder
    {
        private readonly ConcurrentDictionary<Type, ServiceRegistration> _registrations;
        private readonly ConcurrentDictionary<Type, object> _resolvedSingletons;
        private readonly HashSet<Type> _currentlyResolving;
        private readonly object _resolutionLock = new object();
        private readonly object _dependencyGraphLock = new object();
        private ServiceDependencyGraph _cachedDependencyGraph;
        private bool _disposed;
        
        // Performance optimization components
        private readonly ServiceResolutionCache _resolutionCache;
        private readonly FastServiceResolver _fastResolver;
        private readonly ServiceObjectPool<List<object>> _listPool;
        private readonly ServiceObjectPool<HashSet<Type>> _hashSetPool;
        private readonly ServiceObjectPool<List<Type>> _typeListPool;
        private readonly ServiceMetadataCache _metadataCache;
        private readonly ResolutionPathOptimizer _pathOptimizer;
        private readonly WeakReferenceManager _weakReferenceManager;
        private readonly MemoryPressureMonitor _memoryMonitor;
        
        // Performance metrics
        private long _totalResolutions;
        private long _cacheHits;
        private long _fastResolutions;
        
        public ServiceContainer(bool enablePerformanceOptimizations = true)
        {
            _registrations = new ConcurrentDictionary<Type, ServiceRegistration>();
            _resolvedSingletons = new ConcurrentDictionary<Type, object>();
            _currentlyResolving = new HashSet<Type>();
            
            if (enablePerformanceOptimizations)
            {
                // Initialize performance optimization components
                _resolutionCache = new ServiceResolutionCache(maxCacheSize: 1000, enableMetrics: true);
                _metadataCache = new ServiceMetadataCache();
                _pathOptimizer = new ResolutionPathOptimizer(_metadataCache);
                _fastResolver = new FastServiceResolver(this, _resolutionCache);
                _weakReferenceManager = new WeakReferenceManager();
                
                // Initialize object pools for frequently created objects
                _listPool = new ServiceObjectPool<List<object>>(
                    factory: () => new List<object>(),
                    resetAction: list => list.Clear(),
                    maxPoolSize: 50,
                    autoScale: true
                );
                _hashSetPool = new ServiceObjectPool<HashSet<Type>>(
                    factory: () => new HashSet<Type>(),
                    resetAction: set => set.Clear(),
                    maxPoolSize: 30,
                    autoScale: true
                );
                _typeListPool = new ServiceObjectPool<List<Type>>(
                    factory: () => new List<Type>(),
                    resetAction: list => list.Clear(),
                    maxPoolSize: 25,
                    autoScale: true
                );
                
                // Initialize memory monitor with GC optimization settings
                var gcSettings = GCOptimizationSettings.GetDefaultSettings();
                _memoryMonitor = new MemoryPressureMonitor(null, gcSettings);
                
                // Register this container as a memory pressure responder
                _memoryMonitor.RegisterResponder(this);
                
                Debug.Log("ServiceContainer: Performance optimizations enabled with frame-aware GC");
            }
            
            // Register self as IServiceProvider
            RegisterSingleton<IServiceProvider>(this);
            RegisterSingleton<IServiceContainer>(this);
        }
        
        #region Registration Methods
        
        public void RegisterService<TService, TImplementation>(ServiceLifetime lifetime = ServiceLifetime.Singleton) 
            where TImplementation : class, TService
        {
            RegisterService(typeof(TService), typeof(TImplementation), lifetime);
        }
        
        public void RegisterService(Type serviceType, Type implementationType, ServiceLifetime lifetime = ServiceLifetime.Singleton)
        {
            ValidateTypes(serviceType, implementationType);
            
            var registration = new ServiceRegistration
            {
                ServiceType = serviceType,
                ImplementationType = implementationType,
                Lifetime = lifetime,
                IsEngineService = typeof(IEngineService).IsAssignableFrom(implementationType)
            };
            
            // Discover dependencies from attributes
            if (registration.IsEngineService)
            {
                DiscoverDependencies(registration);
            }
            
            if (!_registrations.TryAdd(serviceType, registration))
            {
                Debug.LogWarning($"Service {serviceType.Name} is already registered. Overwriting registration.");
                _registrations[serviceType] = registration;
            }
            
            // Invalidate dependency graph cache since registrations changed
            InvalidateDependencyGraphCache();
        }
        
        public void RegisterSingleton<TService>(TService instance) where TService : class
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
                
            var registration = new ServiceRegistration
            {
                ServiceType = typeof(TService),
                ImplementationType = instance.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                SingletonInstance = instance,
                IsEngineService = instance is IEngineService
            };
            
            _registrations[typeof(TService)] = registration;
            _resolvedSingletons[typeof(TService)] = instance;
            
            // Invalidate dependency graph cache since registrations changed
            InvalidateDependencyGraphCache();
        }
        
        public void RegisterSingleton<TService>(Func<IServiceProvider, TService> factory) where TService : class
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));
                
            var registration = new ServiceRegistration
            {
                ServiceType = typeof(TService),
                ImplementationType = typeof(TService),
                Lifetime = ServiceLifetime.Singleton,
                Factory = provider => factory(provider),
                IsEngineService = typeof(IEngineService).IsAssignableFrom(typeof(TService))
            };
            
            _registrations[typeof(TService)] = registration;
            
            // Invalidate dependency graph cache since registrations changed
            InvalidateDependencyGraphCache();
        }
        
        public void RegisterTransient<TService, TImplementation>() where TImplementation : class, TService
        {
            RegisterService<TService, TImplementation>(ServiceLifetime.Transient);
        }
        
        public void RegisterScoped<TService, TImplementation>() where TImplementation : class, TService
        {
            RegisterService<TService, TImplementation>(ServiceLifetime.Scoped);
        }
        
        #endregion
        
        #region Resolution Methods
        
        public TService Resolve<TService>() where TService : class
        {
            System.Threading.Interlocked.Increment(ref _totalResolutions);
            
            // Try fast resolver first if available
            if (_fastResolver != null)
            {
                try
                {
                    var result = _fastResolver.Resolve<TService>();
                    System.Threading.Interlocked.Increment(ref _fastResolutions);
                    return result;
                }
                catch
                {
                    // Fall back to standard resolution
                }
            }
            
            return (TService)Resolve(typeof(TService));
        }
        
        public object Resolve(Type serviceType)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ServiceContainer));
                
            // Check for circular dependencies
            lock (_resolutionLock)
            {
                if (_currentlyResolving.Contains(serviceType))
                {
                    throw new InvalidOperationException($"Circular dependency detected when resolving {serviceType.Name}");
                }
                
                _currentlyResolving.Add(serviceType);
                try
                {
                    return ResolveInternal(serviceType);
                }
                finally
                {
                    _currentlyResolving.Remove(serviceType);
                }
            }
        }
        
        public async UniTask<TService> ResolveAsync<TService>() where TService : class
        {
            System.Threading.Interlocked.Increment(ref _totalResolutions);
            
            // Try fast resolver first if available
            if (_fastResolver != null)
            {
                try
                {
                    var result = await _fastResolver.ResolveAsync<TService>();
                    System.Threading.Interlocked.Increment(ref _fastResolutions);
                    return result;
                }
                catch
                {
                    // Fall back to standard resolution
                }
            }
            
            // Note: Service initialization is now handled by ServiceLifecycleManager
            // ResolveAsync simply returns the service instance
            await UniTask.Yield(); // Keep async signature for future extensibility
            return Resolve<TService>();
        }
        
        public bool TryResolve<TService>(out TService service) where TService : class
        {
            System.Threading.Interlocked.Increment(ref _totalResolutions);
            
            // Check resolution cache first
            if (_resolutionCache?.TryGet<TService>(out service) == true)
            {
                System.Threading.Interlocked.Increment(ref _cacheHits);
                return true;
            }
            
            try
            {
                service = Resolve<TService>();
                
                // Cache successful resolution
                _resolutionCache?.Set(service);
                
                return true;
            }
            catch
            {
                service = null;
                return false;
            }
        }
        
        public bool TryResolve(Type serviceType, out object service)
        {
            try
            {
                service = Resolve(serviceType);
                return true;
            }
            catch
            {
                service = null;
                return false;
            }
        }
        
        #endregion
        
        #region Query Methods
        
        public bool IsRegistered<TService>()
        {
            return IsRegistered(typeof(TService));
        }
        
        public bool IsRegistered(Type serviceType)
        {
            return _registrations.ContainsKey(serviceType);
        }
        
        public IEnumerable<Type> GetRegisteredServices()
        {
            // Use pooled type list if available for better performance
            if (_typeListPool != null)
            {
                var pooledList = _typeListPool.Get();
                try
                {
                    pooledList.AddRange(_registrations.Keys);
                    // Return a copy since we need to return the pooled list
                    return new List<Type>(pooledList);
                }
                finally
                {
                    _typeListPool.Return(pooledList);
                }
            }
            
            return _registrations.Keys.ToList();
        }
        
        #endregion
        
        #region Dependency Graph
        
        public ServiceDependencyGraph BuildDependencyGraph()
        {
            // Return cached graph if available
            if (_cachedDependencyGraph != null)
                return _cachedDependencyGraph;
                
            lock (_dependencyGraphLock)
            {
                // Double-check pattern for thread safety
                if (_cachedDependencyGraph != null)
                    return _cachedDependencyGraph;
                
                Debug.Log("Building dependency graph cache in ServiceContainer...");
                var graph = new ServiceDependencyGraph();
                graph.Build(_registrations);
                _cachedDependencyGraph = graph;
                return graph;
            }
        }
        
        public bool ValidateDependencies()
        {
            // Simple validation for now
            foreach (var registration in _registrations.Values)
            {
                foreach (var dependency in registration.RequiredDependencies)
                {
                    if (!IsRegistered(dependency))
                    {
                        Debug.LogError($"Service {registration.ServiceType.Name} requires {dependency.Name} but it's not registered");
                        return false;
                    }
                }
            }
            return true;
        }
        
        public IEnumerable<string> GetCircularDependencies()
        {
            var graph = BuildDependencyGraph();
            return graph.CircularDependencies.Select(cycle => 
                string.Join(" -> ", cycle.Select(t => t.Name)));
        }
        
        /// <summary>
        /// Internal method to get all service registrations for lifecycle management
        /// </summary>
        internal Dictionary<Type, ServiceRegistration> GetServiceRegistrations()
        {
            return new Dictionary<Type, ServiceRegistration>(_registrations);
        }
        
        /// <summary>
        /// Invalidate cached dependency graph when registrations change
        /// </summary>
        private void InvalidateDependencyGraphCache()
        {
            lock (_dependencyGraphLock)
            {
                _cachedDependencyGraph = null;
            }
            
            // Also invalidate performance optimization caches
            _resolutionCache?.Clear();
            _pathOptimizer?.Clear();
        }
        
        #endregion
        
        #region IServiceProvider Implementation
        
        object IServiceProvider.GetService(Type serviceType)
        {
            return TryResolve(serviceType, out var service) ? service : null;
        }
        
        #endregion
        
        #region Private Methods
        
        private object ResolveInternal(Type serviceType)
        {
            if (!_registrations.TryGetValue(serviceType, out var registration))
            {
                throw new InvalidOperationException($"Service {serviceType.Name} is not registered");
            }
            
            // Handle singleton lifetime
            if (registration.Lifetime == ServiceLifetime.Singleton)
            {
                if (registration.SingletonInstance != null)
                    return registration.SingletonInstance;
                    
                if (_resolvedSingletons.TryGetValue(serviceType, out var cached))
                    return cached;
                    
                lock (registration.SingletonLock)
                {
                    if (registration.SingletonInstance != null)
                        return registration.SingletonInstance;
                        
                    var instance = CreateInstance(registration);
                    registration.SingletonInstance = instance;
                    _resolvedSingletons[serviceType] = instance;
                    return instance;
                }
            }
            
            // Handle transient lifetime
            if (registration.Lifetime == ServiceLifetime.Transient)
            {
                return CreateInstance(registration);
            }
            
            // Handle scoped lifetime (not implemented yet)
            throw new NotImplementedException($"Scoped lifetime is not yet implemented");
        }
        
        private object CreateInstance(ServiceRegistration registration)
        {
            // Use factory if available
            if (registration.Factory != null)
            {
                return registration.Factory(this);
            }
            
            // Check if service needs configuration
            var configurationType = registration.ImplementationType.GetConfigurationType();
            object configuration = null;
            
            if (configurationType != null)
            {
                configuration = LoadConfiguration(configurationType, registration.ImplementationType);
            }
            
            // Create instance using reflection
            var constructors = registration.ImplementationType.GetConstructors();
            if (constructors.Length == 0)
            {
                throw new InvalidOperationException($"No public constructors found for {registration.ImplementationType.Name}");
            }
            
            // Try to find constructor we can satisfy
            foreach (var constructor in constructors.OrderByDescending(c => c.GetParameters().Length))
            {
                var parameters = constructor.GetParameters();
                var parameterInstances = new object[parameters.Length];
                
                bool canResolve = true;
                for (int i = 0; i < parameters.Length; i++)
                {
                    var paramType = parameters[i].ParameterType;
                    
                    // Check if this parameter is the configuration
                    if (configuration != null && paramType.IsAssignableFrom(configurationType))
                    {
                        parameterInstances[i] = configuration;
                    }
                    else if (TryResolve(paramType, out var paramInstance))
                    {
                        parameterInstances[i] = paramInstance;
                    }
                    else if (parameters[i].HasDefaultValue)
                    {
                        parameterInstances[i] = parameters[i].DefaultValue;
                    }
                    else
                    {
                        canResolve = false;
                        break;
                    }
                }
                
                if (canResolve)
                {
                    return Activator.CreateInstance(registration.ImplementationType, parameterInstances);
                }
            }
            
            // Try parameterless constructor
            try
            {
                return Activator.CreateInstance(registration.ImplementationType);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Cannot create instance of {registration.ImplementationType.Name}", ex);
            }
        }
        
        private void DiscoverDependencies(ServiceRegistration registration)
        {
            try
            {
                var type = registration.ImplementationType;
                
                // Add debug logging to track which type is being processed
                Debug.Log($"Discovering dependencies for type: {type.FullName}");
                
                // Use the unified EngineServiceAttribute or fall back to legacy attributes
                var engineAttr = type.GetEngineServiceAttribute();
                
                // Defensive null checks
                registration.RequiredDependencies = engineAttr?.RequiredServices ?? Array.Empty<Type>();
                registration.OptionalDependencies = engineAttr?.OptionalServices ?? Array.Empty<Type>();
                registration.Priority = engineAttr?.Priority ?? ServicePriority.Medium;
                
                // Validate dependencies to prevent circular references
                var allDependencies = registration.RequiredDependencies.Concat(registration.OptionalDependencies);
                foreach (var dep in allDependencies)
                {
                    if (dep == type)
                    {
                        Debug.LogError($"Service {type.Name} has self-dependency. Removing it.");
                        registration.RequiredDependencies = registration.RequiredDependencies.Where(d => d != type).ToArray();
                        registration.OptionalDependencies = registration.OptionalDependencies.Where(d => d != type).ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error discovering dependencies for {registration.ImplementationType.Name}: {ex.Message}");
                // Set safe defaults on error
                registration.RequiredDependencies = Array.Empty<Type>();
                registration.OptionalDependencies = Array.Empty<Type>();
                registration.Priority = ServicePriority.Medium;
            }
        }
        
        private object LoadConfiguration(Type configurationType, Type serviceType)
        {
            try
            {
                // Get the configuration path for this service
                var configPath = serviceType.GetConfigurationPath();
                
                // Try to load from Resources
                var config = Resources.Load(configPath, configurationType);
                
                if (config != null)
                {
                    // Validate the configuration
                    if (config is IServiceConfiguration serviceConfig)
                    {
                        if (!serviceConfig.Validate(out var errors))
                        {
                            Debug.LogWarning($"Configuration validation failed for {serviceType.Name}: {string.Join(", ", errors)}");
                        }
                    }
                    
                    return config;
                }
                
                // If required configuration is missing, throw exception
                if (serviceType.IsConfigurationRequired())
                {
                    throw new InvalidOperationException($"Required configuration of type {configurationType.Name} not found at path {configPath} for service {serviceType.Name}");
                }
                
                // Try to create default instance
                if (configurationType.IsSubclassOf(typeof(ScriptableObject)))
                {
                    var defaultConfig = ScriptableObject.CreateInstance(configurationType);
                    Debug.LogWarning($"Using default configuration for {serviceType.Name} as no configuration asset was found");
                    return defaultConfig;
                }
                
                // Create instance using Activator
                return Activator.CreateInstance(configurationType);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load configuration {configurationType.Name} for service {serviceType.Name}: {ex.Message}");
                
                if (serviceType.IsConfigurationRequired())
                {
                    throw;
                }
                
                return null;
            }
        }
        
        private void ValidateTypes(Type serviceType, Type implementationType)
        {
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));
            if (implementationType == null)
                throw new ArgumentNullException(nameof(implementationType));
            if (!serviceType.IsAssignableFrom(implementationType))
                throw new ArgumentException($"{implementationType.Name} does not implement {serviceType.Name}");
        }
        
        #endregion
        
        #region Scope Management
        
        public IServiceScope CreateScope()
        {
            return new ServiceScope(this);
        }
        
        #endregion
        
        #region IDisposable
        
        public void Dispose()
        {
            if (_disposed) return;
            
            // Dispose performance optimization components
            _memoryMonitor?.Dispose();
            _weakReferenceManager?.Dispose();
            _resolutionCache?.Clear();
            _fastResolver?.Reset();
            _metadataCache?.Clear();
            
            // Clear object pools
            _listPool?.Clear();
            _hashSetPool?.Clear();
            _typeListPool?.Clear();
            
            // Dispose all singleton instances that implement IDisposable
            foreach (var registration in _registrations.Values)
            {
                if (registration.SingletonInstance is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error disposing service: {ex.Message}");
                    }
                }
            }
            
            _registrations.Clear();
            _resolvedSingletons.Clear();
            _currentlyResolving.Clear();
            
            // Clear cached dependency graph
            InvalidateDependencyGraphCache();
            
            _disposed = true;
        }
        
        #endregion
        
        #region Performance Optimization Methods
        
        /// <summary>
        /// Get object pool statistics for monitoring
        /// </summary>
        public string GetObjectPoolStatistics()
        {
            if (_listPool == null || _hashSetPool == null || _typeListPool == null)
                return "Object pooling not enabled";
                
            return $"Object Pool Stats - Lists: {_listPool.Count}/{_listPool.MaxPoolSize} " +
                   $"(Efficiency: {_listPool.EfficiencyRatio:P1}), " +
                   $"HashSets: {_hashSetPool.Count}/{_hashSetPool.MaxPoolSize} " +
                   $"(Efficiency: {_hashSetPool.EfficiencyRatio:P1}), " +
                   $"TypeLists: {_typeListPool.Count}/{_typeListPool.MaxPoolSize} " +
                   $"(Efficiency: {_typeListPool.EfficiencyRatio:P1})";
        }
        
        /// <summary>
        /// Precompile resolvers for critical services to improve performance
        /// </summary>
        public async UniTask PrecompileCriticalServicesAsync(Type[] criticalServices)
        {
            if (_fastResolver != null && criticalServices?.Length > 0)
            {
                await _fastResolver.PrecompileResolversAsync(criticalServices);
                _resolutionCache?.PrewarmCache(criticalServices, this);
                _metadataCache?.PreloadMetadata(criticalServices, this);
                
                Debug.Log($"Precompiled {criticalServices.Length} critical services for optimal performance");
            }
        }
        
        /// <summary>
        /// Register a weak reference for optional dependencies
        /// </summary>
        public void RegisterWeakDependency<T>(T service) where T : class
        {
            _weakReferenceManager?.RegisterWeakDependency(service);
        }
        
        /// <summary>
        /// Try to get a weak reference to an optional dependency
        /// </summary>
        public bool TryGetWeakDependency<T>(out T service) where T : class
        {
            if (_weakReferenceManager != null)
            {
                return _weakReferenceManager.TryGetWeakDependency(out service);
            }
            
            service = null;
            return false;
        }
        
        /// <summary>
        /// Get comprehensive performance statistics
        /// </summary>
        public ServiceContainerPerformanceStats GetPerformanceStatistics()
        {
            return new ServiceContainerPerformanceStats
            {
                TotalResolutions = _totalResolutions,
                CacheHits = _cacheHits,
                FastResolutions = _fastResolutions,
                CacheHitRatio = _totalResolutions > 0 ? (double)_cacheHits / _totalResolutions : 0,
                FastResolutionRatio = _totalResolutions > 0 ? (double)_fastResolutions / _totalResolutions : 0,
                RegisteredServices = _registrations.Count,
                ResolvedSingletons = _resolvedSingletons.Count,
                ResolutionCacheStats = _resolutionCache?.GetStatistics(),
                FastResolverStats = _fastResolver?.GetStatistics(),
                MetadataCacheStats = _metadataCache?.GetStatistics(),
                WeakReferenceStats = _weakReferenceManager?.GetStatistics(),
                MemoryPressureStats = _memoryMonitor?.GetStatistics()
            };
        }
        
        /// <summary>
        /// Get optimized service metadata for memory-efficient access
        /// </summary>
        public bool TryGetOptimizedMetadata<T>(out ServiceMetadataOptimization.OptimizedServiceMetadata metadata) where T : class
        {
            if (_metadataCache != null)
            {
                return _metadataCache.TryGetOptimizedMetadata(typeof(T), out metadata);
            }
            
            metadata = default;
            return false;
        }
        
        /// <summary>
        /// Force memory cleanup and optimization
        /// </summary>
        public async UniTask<MemoryCleanupResult> OptimizeMemoryUsageAsync()
        {
            if (_memoryMonitor != null)
            {
                return await _memoryMonitor.ForceCleanupAsync(MemoryPressureMonitor.CleanupStrategy.Moderate);
            }
            
            return new MemoryCleanupResult { Success = false, ErrorMessage = "Memory monitor not available" };
        }
        
        #endregion
        
        #region IMemoryPressureResponder Implementation
        
        /// <summary>
        /// Respond to memory pressure by cleaning up caches and non-essential data
        /// </summary>
        public async UniTask RespondToMemoryPressureAsync(MemoryPressureMonitor.MemoryPressureLevel pressureLevel, MemoryPressureMonitor.CleanupStrategy strategy)
        {
            try
            {
                switch (strategy)
                {
                    case MemoryPressureMonitor.CleanupStrategy.Conservative:
                        // Light cleanup - clear metadata cache and trim object pools
                        _metadataCache?.Clear();
                        _listPool?.Trim(25); // Reduce to 50% of max size
                        _hashSetPool?.Trim(15);
                        _typeListPool?.Trim(12);
                        break;
                        
                    case MemoryPressureMonitor.CleanupStrategy.Moderate:
                        // Moderate cleanup - clear caches but keep compiled resolvers
                        _metadataCache?.Clear();
                        _resolutionCache?.Clear();
                        _weakReferenceManager?.PerformManualCleanup();
                        _listPool?.Trim(10); // Reduce to 20% of max size
                        _hashSetPool?.Trim(6);
                        _typeListPool?.Trim(5);
                        break;
                        
                    case MemoryPressureMonitor.CleanupStrategy.Aggressive:
                        // Aggressive cleanup - clear everything including compiled resolvers
                        _metadataCache?.Clear();
                        _resolutionCache?.Clear();
                        _fastResolver?.Reset();
                        _weakReferenceManager?.PerformManualCleanup();
                        _listPool?.Clear(); // Clear all pooled objects
                        _hashSetPool?.Clear();
                        _typeListPool?.Clear();
                        
                        // Skip GC operations - they require main thread access
                        // Memory cleanup will be handled by regular .NET GC cycles
                        break;
                }
                
                await UniTask.Yield(); // Ensure async contract
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during memory pressure response: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Handle memory pressure level changes
        /// </summary>
        public void OnMemoryPressureLevelChanged(MemoryPressureMonitor.MemoryPressureLevel previousLevel, MemoryPressureMonitor.MemoryPressureLevel newLevel)
        {
            if (newLevel > previousLevel)
            {
                Debug.LogWarning($"ServiceContainer: Memory pressure increased to {newLevel}");
            }
            else if (newLevel < previousLevel)
            {
                Debug.Log($"ServiceContainer: Memory pressure decreased to {newLevel}");
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Performance statistics for ServiceContainer
    /// </summary>
    public struct ServiceContainerPerformanceStats
    {
        public long TotalResolutions { get; set; }
        public long CacheHits { get; set; }
        public long FastResolutions { get; set; }
        public double CacheHitRatio { get; set; }
        public double FastResolutionRatio { get; set; }
        public int RegisteredServices { get; set; }
        public int ResolvedSingletons { get; set; }
        public ServiceResolutionCacheStatistics? ResolutionCacheStats { get; set; }
        public FastResolverStatistics? FastResolverStats { get; set; }
        public MetadataCacheStatistics? MetadataCacheStats { get; set; }
        public WeakReferenceStatistics? WeakReferenceStats { get; set; }
        public MemoryPressureStatistics? MemoryPressureStats { get; set; }
        
        public override string ToString()
        {
            return $"ServiceContainer Performance: {TotalResolutions} resolutions, " +
                   $"{CacheHitRatio:P1} cache hit ratio, {FastResolutionRatio:P1} fast resolution ratio, " +
                   $"{RegisteredServices} registered services";
        }
    }
}