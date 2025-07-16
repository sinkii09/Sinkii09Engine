using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Sinkii09.Engine.Services.Performance
{
    /// <summary>
    /// Optimized resolution algorithms using compiled expressions for ultra-fast service resolution
    /// Target: <0.5ms resolution time with compiled expression caching
    /// </summary>
    public class FastServiceResolver
    {
        private readonly IServiceContainer _container;
        private readonly ServiceResolutionCache _resolutionCache;
        private readonly ConcurrentDictionary<Type, Func<IServiceProvider, object>> _compiledResolvers;
        private readonly ConcurrentDictionary<Type, Func<IServiceProvider, object[], object>> _compiledConstructors;
        private readonly ServiceMetadataCache _metadataCache;
        
        // Performance metrics
        private long _fastResolutions;
        private long _fallbackResolutions;
        private long _compilationTime;
        
        /// <summary>
        /// Number of resolutions using compiled expressions
        /// </summary>
        public long FastResolutionCount => _fastResolutions;
        
        /// <summary>
        /// Number of resolutions falling back to reflection
        /// </summary>
        public long FallbackResolutionCount => _fallbackResolutions;
        
        /// <summary>
        /// Total time spent compiling expressions (ticks)
        /// </summary>
        public long CompilationTimeTicks => _compilationTime;
        
        public FastServiceResolver(IServiceContainer container, ServiceResolutionCache resolutionCache = null)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _resolutionCache = resolutionCache ?? new ServiceResolutionCache();
            _compiledResolvers = new ConcurrentDictionary<Type, Func<IServiceProvider, object>>();
            _compiledConstructors = new ConcurrentDictionary<Type, Func<IServiceProvider, object[], object>>();
            _metadataCache = new ServiceMetadataCache();
        }
        
        /// <summary>
        /// Fast resolution with caching and compiled expressions
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async UniTask<T> ResolveAsync<T>() where T : class
        {
            var serviceType = typeof(T);
            
            // First check cache
            if (_resolutionCache.TryGet<T>(out var cachedInstance))
            {
                return cachedInstance;
            }
            
            // Use compiled resolver if available
            if (_compiledResolvers.TryGetValue(serviceType, out var compiledResolver))
            {
                var instance = (T)compiledResolver(_container as IServiceProvider);
                _resolutionCache.Set(instance);
                System.Threading.Interlocked.Increment(ref _fastResolutions);
                return instance;
            }
            
            // Compile resolver for this type
            var resolver = await CompileResolverAsync<T>();
            if (resolver != null)
            {
                var instance = (T)resolver(_container as IServiceProvider);
                _resolutionCache.Set(instance);
                System.Threading.Interlocked.Increment(ref _fastResolutions);
                return instance;
            }
            
            // Fallback to container resolution
            System.Threading.Interlocked.Increment(ref _fallbackResolutions);
            var fallbackInstance = await _container.ResolveAsync<T>();
            _resolutionCache.Set(fallbackInstance);
            return fallbackInstance;
        }
        
        /// <summary>
        /// Fast synchronous resolution
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Resolve<T>() where T : class
        {
            var serviceType = typeof(T);
            
            // First check cache
            if (_resolutionCache.TryGet<T>(out var cachedInstance))
            {
                return cachedInstance;
            }
            
            // Use compiled resolver if available
            if (_compiledResolvers.TryGetValue(serviceType, out var compiledResolver))
            {
                var instance = (T)compiledResolver(_container as IServiceProvider);
                _resolutionCache.Set(instance);
                System.Threading.Interlocked.Increment(ref _fastResolutions);
                return instance;
            }
            
            // Compile resolver for this type (synchronous)
            var resolver = CompileResolver<T>();
            if (resolver != null)
            {
                var instance = (T)resolver(_container as IServiceProvider);
                _resolutionCache.Set(instance);
                System.Threading.Interlocked.Increment(ref _fastResolutions);
                return instance;
            }
            
            // Fallback to container resolution
            System.Threading.Interlocked.Increment(ref _fallbackResolutions);
            var fallbackInstance = _container.Resolve<T>();
            _resolutionCache.Set(fallbackInstance);
            return fallbackInstance;
        }
        
        /// <summary>
        /// Batch resolution for multiple services
        /// </summary>
        public async UniTask<object[]> ResolveBatchAsync(Type[] serviceTypes)
        {
            var results = new object[serviceTypes.Length];
            var tasks = new UniTask[serviceTypes.Length];
            
            for (int i = 0; i < serviceTypes.Length; i++)
            {
                var index = i;
                var serviceType = serviceTypes[i];
                
                tasks[i] = ResolveSingleServiceAsync(serviceType, index, results);
            }
            
            await UniTask.WhenAll(tasks);
            return results;
        }
        
        /// <summary>
        /// Resolve a single service asynchronously for batch operations
        /// </summary>
        private async UniTask ResolveSingleServiceAsync(Type serviceType, int index, object[] results)
        {
            // Check cache first
            if (_resolutionCache.TryGet(serviceType, out var cached))
            {
                results[index] = cached;
                return;
            }

            // Use compiled resolver
            if (_compiledResolvers.TryGetValue(serviceType, out var compiled))
            {
                var instance = compiled(_container as IServiceProvider);
                _resolutionCache.Set(serviceType, instance);
                results[index] = instance;
                System.Threading.Interlocked.Increment(ref _fastResolutions);
                return;
            }
            
            // Fallback to container resolution
            var fallback = _container.Resolve(serviceType);
            _resolutionCache.Set(serviceType, fallback);
            results[index] = fallback;
            System.Threading.Interlocked.Increment(ref _fallbackResolutions);
            
            await UniTask.Yield(); // Ensure async contract
                
        }
        
        /// <summary>
        /// Precompile resolvers for critical services
        /// </summary>
        public async UniTask PrecompileResolversAsync(Type[] criticalServices)
        {
            var compilationTasks = criticalServices.Select(async serviceType =>
            {
                try
                {
                    await CompileResolverAsync(serviceType);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to precompile resolver for {serviceType.Name}: {ex.Message}");
                }
            });
            
            await UniTask.WhenAll(compilationTasks);
            Debug.Log($"Precompiled {criticalServices.Length} service resolvers");
            foreach (var serviceType in criticalServices)
            {
                if (_compiledResolvers.TryGetValue(serviceType, out var resolver))
                {
                    Debug.Log($"Compiled resolver for {serviceType.Name}");
                }
                else
                {
                    Debug.LogWarning($"Failed to compile resolver for {serviceType.Name}");
                }
            }
        }
        
        /// <summary>
        /// Compile async resolver for a specific type
        /// </summary>
        private async UniTask<Func<IServiceProvider, object>> CompileResolverAsync<T>()
        {
            return await CompileResolverAsync(typeof(T));
        }
        
        /// <summary>
        /// Compile async resolver for a specific type
        /// </summary>
        private async UniTask<Func<IServiceProvider, object>> CompileResolverAsync(Type serviceType)
        {
            return await UniTask.RunOnThreadPool(() => CompileResolver(serviceType));
        }
        
        /// <summary>
        /// Compile synchronous resolver for a specific type
        /// </summary>
        private Func<IServiceProvider, object> CompileResolver<T>()
        {
            return CompileResolver(typeof(T));
        }
        
        /// <summary>
        /// Compile synchronous resolver for a specific type
        /// </summary>
        private Func<IServiceProvider, object> CompileResolver(Type serviceType)
        {
            var startTicks = DateTime.UtcNow.Ticks;
            
            try
            {
                // Check if already compiled
                if (_compiledResolvers.TryGetValue(serviceType, out var existing))
                {
                    return existing;
                }
                
                // Get service metadata
                var metadata = _metadataCache.GetOrCreateMetadata(serviceType, _container);
                if (metadata?.ImplementationType == null)
                {
                    return null;
                }
                
                // Create expression tree for service resolution
                var providerParam = Expression.Parameter(typeof(IServiceProvider), "provider");
                var resolver = BuildResolverExpression(metadata, providerParam);
                
                if (resolver == null)
                {
                    return null;
                }
                
                // Compile the expression
                var lambda = Expression.Lambda<Func<IServiceProvider, object>>(resolver, providerParam);
                var compiledResolver = lambda.Compile();
                
                // Cache the compiled resolver
                _compiledResolvers.TryAdd(serviceType, compiledResolver);
                
                return compiledResolver;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to compile resolver for {serviceType.Name}: {ex.Message}");
                return null;
            }
            finally
            {
                var compilationTime = DateTime.UtcNow.Ticks - startTicks;
                System.Threading.Interlocked.Add(ref _compilationTime, compilationTime);
            }
        }
        
        /// <summary>
        /// Build expression tree for service resolution
        /// </summary>
        private Expression BuildResolverExpression(ServiceMetadata metadata, ParameterExpression providerParam)
        {
            var implementationType = metadata.ImplementationType;
            
            // Handle singleton instances
            if (metadata.SingletonInstance != null)
            {
                return Expression.Constant(metadata.SingletonInstance);
            }
            
            // Handle factory methods
            if (metadata.Factory != null)
            {
                return Expression.Invoke(Expression.Constant(metadata.Factory), providerParam);
            }
            
            // Find best constructor
            var constructors = implementationType.GetConstructors();
            if (constructors.Length == 0)
            {
                return null;
            }
            
            var bestConstructor = constructors.OrderByDescending(c => c.GetParameters().Length).First();
            var parameters = bestConstructor.GetParameters();
            
            // Build parameter expressions
            var parameterExpressions = new Expression[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;
                
                // Build expression to resolve parameter
                var resolveMethod = typeof(IServiceProvider).GetMethod("GetService");
                var getServiceCall = Expression.Call(providerParam, resolveMethod, Expression.Constant(paramType));
                var castToType = Expression.Convert(getServiceCall, paramType);
                
                parameterExpressions[i] = castToType;
            }
            
            // Build constructor call
            var constructorCall = Expression.New(bestConstructor, parameterExpressions);
            
            // Convert to object if needed
            if (implementationType.IsValueType)
            {
                return Expression.Convert(constructorCall, typeof(object));
            }
            
            return constructorCall;
        }
        
        /// <summary>
        /// Get performance statistics
        /// </summary>
        public FastResolverStatistics GetStatistics()
        {
            return new FastResolverStatistics
            {
                CompiledResolverCount = _compiledResolvers.Count,
                FastResolutionCount = _fastResolutions,
                FallbackResolutionCount = _fallbackResolutions,
                CompilationTimeMs = _compilationTime / TimeSpan.TicksPerMillisecond,
                CacheStatistics = _resolutionCache.GetStatistics()
            };
        }
        
        /// <summary>
        /// Clear all compiled resolvers and reset cache
        /// </summary>
        public void Reset()
        {
            _compiledResolvers.Clear();
            _compiledConstructors.Clear();
            _resolutionCache.Clear();
            _metadataCache.Clear();
            
            System.Threading.Interlocked.Exchange(ref _fastResolutions, 0);
            System.Threading.Interlocked.Exchange(ref _fallbackResolutions, 0);
            System.Threading.Interlocked.Exchange(ref _compilationTime, 0);
        }
    }
    
    /// <summary>
    /// Fast resolver performance statistics
    /// </summary>
    public struct FastResolverStatistics
    {
        public int CompiledResolverCount { get; set; }
        public long FastResolutionCount { get; set; }
        public long FallbackResolutionCount { get; set; }
        public long CompilationTimeMs { get; set; }
        public CacheStatistics CacheStatistics { get; set; }
        
        public double FastResolutionRatio => 
            FastResolutionCount + FallbackResolutionCount > 0 ? 
            (double)FastResolutionCount / (FastResolutionCount + FallbackResolutionCount) : 0;
        
        public override string ToString()
        {
            return $"FastResolver: {CompiledResolverCount} compiled, {FastResolutionRatio:P1} fast ratio, " +
                   $"{CompilationTimeMs}ms compilation time, {CacheStatistics}";
        }
    }
}