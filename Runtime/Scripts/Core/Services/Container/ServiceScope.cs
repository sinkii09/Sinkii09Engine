using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Implementation of service scope for scoped lifetime services
    /// </summary>
    public class ServiceScope : IServiceScope
    {
        private readonly IServiceContainer _parentContainer;
        private readonly ConcurrentDictionary<Type, object> _scopedServices;
        private readonly object _lock = new object();
        private bool _disposed;
        
        public IServiceProvider ServiceProvider { get; }
        
        internal ServiceScope(IServiceContainer parentContainer)
        {
            _parentContainer = parentContainer ?? throw new ArgumentNullException(nameof(parentContainer));
            _scopedServices = new ConcurrentDictionary<Type, object>();
            ServiceProvider = new ScopedServiceProvider(this, parentContainer);
        }
        
        /// <summary>
        /// Get or create a scoped service instance
        /// </summary>
        internal T GetOrCreateScopedService<T>() where T : class
        {
            return (T)GetOrCreateScopedService(typeof(T));
        }
        
        /// <summary>
        /// Get or create a scoped service instance
        /// </summary>
        internal object GetOrCreateScopedService(Type serviceType)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ServiceScope));
            
            return _scopedServices.GetOrAdd(serviceType, type =>
            {
                lock (_lock)
                {
                    if (_disposed)
                        throw new ObjectDisposedException(nameof(ServiceScope));
                    
                    // Create the instance using the parent container's resolution logic
                    return CreateScopedInstance(type);
                }
            });
        }
        
        /// <summary>
        /// Check if a service is already created in this scope
        /// </summary>
        internal bool HasScopedService(Type serviceType)
        {
            return _scopedServices.ContainsKey(serviceType);
        }
        
        /// <summary>
        /// Get all scoped services created in this scope
        /// </summary>
        internal IEnumerable<object> GetScopedServices()
        {
            return _scopedServices.Values;
        }
        
        private object CreateScopedInstance(Type serviceType)
        {
            try
            {
                // For now, delegate to parent container's resolution
                // In a full implementation, this would use the scoped service provider
                return _parentContainer.Resolve(serviceType);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to create scoped instance of {serviceType.Name}: {ex.Message}");
                throw;
            }
        }
        
        public void Dispose()
        {
            if (_disposed)
                return;
            
            lock (_lock)
            {
                if (_disposed)
                    return;
                
                // Dispose all scoped services that implement IDisposable
                foreach (var service in _scopedServices.Values)
                {
                    if (service is IDisposable disposable)
                    {
                        try
                        {
                            disposable.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Error disposing scoped service {service.GetType().Name}: {ex.Message}");
                        }
                    }
                }
                
                _scopedServices.Clear();
                _disposed = true;
            }
        }
    }
    
    /// <summary>
    /// Service provider implementation for scoped services
    /// </summary>
    internal class ScopedServiceProvider : IServiceProvider
    {
        private readonly ServiceScope _scope;
        private readonly IServiceContainer _parentContainer;
        
        public ScopedServiceProvider(ServiceScope scope, IServiceContainer parentContainer)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            _parentContainer = parentContainer ?? throw new ArgumentNullException(nameof(parentContainer));
        }
        
        public object GetService(Type serviceType)
        {
            // Check if it's registered as scoped in parent container
            if (IsRegisteredAsScoped(serviceType))
            {
                return _scope.GetOrCreateScopedService(serviceType);
            }
            
            // For non-scoped services, delegate to parent container
            return _parentContainer.TryResolve(serviceType, out var service) ? service : null;
        }
        
        private bool IsRegisteredAsScoped(Type serviceType)
        {
            // This would check the registration information from the parent container
            // For now, we'll assume any registered service could be scoped
            return _parentContainer.IsRegistered(serviceType);
        }
    }
    
    /// <summary>
    /// Scope manager for handling nested scopes and scope lifecycle
    /// </summary>
    public class ServiceScopeManager : IDisposable
    {
        private readonly IServiceContainer _container;
        private readonly AsyncLocal<ServiceScope> _currentScope;
        private readonly List<ServiceScope> _allScopes;
        private readonly object _lock = new object();
        private bool _disposed;
        
        public ServiceScopeManager(IServiceContainer container)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _currentScope = new AsyncLocal<ServiceScope>();
            _allScopes = new List<ServiceScope>();
        }
        
        /// <summary>
        /// Current scope for the current async context
        /// </summary>
        public ServiceScope CurrentScope => _currentScope.Value;
        
        /// <summary>
        /// Create a new service scope
        /// </summary>
        public ServiceScope CreateScope()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ServiceScopeManager));
            
            var scope = new ServiceScope(_container);
            
            lock (_lock)
            {
                _allScopes.Add(scope);
            }
            
            return scope;
        }
        
        /// <summary>
        /// Enter a scope for the current async context
        /// </summary>
        public IDisposable EnterScope(ServiceScope scope)
        {
            if (scope == null)
                throw new ArgumentNullException(nameof(scope));
            
            var previousScope = _currentScope.Value;
            _currentScope.Value = scope;
            
            return new ScopeContext(this, previousScope);
        }
        
        /// <summary>
        /// Get statistics about all active scopes
        /// </summary>
        public ScopeStatistics GetStatistics()
        {
            lock (_lock)
            {
                var activeScopes = _allScopes.Count;
                var totalScopedServices = 0;
                
                foreach (var scope in _allScopes)
                {
                    totalScopedServices += scope.GetScopedServices().Count();
                }
                
                return new ScopeStatistics
                {
                    ActiveScopes = activeScopes,
                    TotalScopedServices = totalScopedServices,
                    HasCurrentScope = _currentScope.Value != null
                };
            }
        }
        
        /// <summary>
        /// Dispose all scopes
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            
            lock (_lock)
            {
                foreach (var scope in _allScopes)
                {
                    try
                    {
                        scope.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error disposing scope: {ex.Message}");
                    }
                }
                
                _allScopes.Clear();
                _disposed = true;
            }
        }
        
        private void RestoreScope(ServiceScope previousScope)
        {
            _currentScope.Value = previousScope;
        }
        
        /// <summary>
        /// Context for managing scope entry/exit
        /// </summary>
        private class ScopeContext : IDisposable
        {
            private readonly ServiceScopeManager _manager;
            private readonly ServiceScope _previousScope;
            private bool _disposed;
            
            public ScopeContext(ServiceScopeManager manager, ServiceScope previousScope)
            {
                _manager = manager;
                _previousScope = previousScope;
            }
            
            public void Dispose()
            {
                if (!_disposed)
                {
                    _manager.RestoreScope(_previousScope);
                    _disposed = true;
                }
            }
        }
    }
    
    /// <summary>
    /// Statistics about service scopes
    /// </summary>
    public class ScopeStatistics
    {
        public int ActiveScopes { get; set; }
        public int TotalScopedServices { get; set; }
        public bool HasCurrentScope { get; set; }
        
        public override string ToString()
        {
            return $"Scope Statistics: {ActiveScopes} active scopes, {TotalScopedServices} scoped services" +
                   (HasCurrentScope ? ", current scope active" : ", no current scope");
        }
    }
    
    /// <summary>
    /// Extensions for working with service scopes
    /// </summary>
    public static class ServiceScopeExtensions
    {
        /// <summary>
        /// Execute an action within a new scope
        /// </summary>
        public static void WithScope(this IServiceContainer container, Action<IServiceScope> action)
        {
            using (var scope = container.CreateScope())
            {
                action(scope);
            }
        }
        
        /// <summary>
        /// Execute a function within a new scope
        /// </summary>
        public static T WithScope<T>(this IServiceContainer container, Func<IServiceScope, T> func)
        {
            using (var scope = container.CreateScope())
            {
                return func(scope);
            }
        }
        
        /// <summary>
        /// Resolve a service within the current scope if available, otherwise use container
        /// </summary>
        public static T ResolveScoped<T>(this IServiceContainer container, ServiceScopeManager scopeManager = null)
            where T : class
        {
            if (scopeManager?.CurrentScope != null)
            {
                return scopeManager.CurrentScope.ServiceProvider.GetService(typeof(T)) as T;
            }
            
            return container.Resolve<T>();
        }
    }
}