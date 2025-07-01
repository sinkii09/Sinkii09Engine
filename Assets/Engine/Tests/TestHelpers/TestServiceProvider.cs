using System;
using System.Collections.Generic;

namespace Sinkii09.Engine.Tests.TestHelpers
{
    public class TestServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public void RegisterService<T>(T service)
        {
            _services[typeof(T)] = service;
        }

        public void RegisterService(Type serviceType, object service)
        {
            _services[serviceType] = service;
        }

        public object GetService(Type serviceType)
        {
            _services.TryGetValue(serviceType, out var service);
            return service;
        }

        public T GetService<T>()
        {
            var service = GetService(typeof(T));
            return service != null ? (T)service : default(T);
        }

        public bool HasService<T>()
        {
            return _services.ContainsKey(typeof(T));
        }

        public bool HasService(Type serviceType)
        {
            return _services.ContainsKey(serviceType);
        }

        public void Clear()
        {
            _services.Clear();
        }

        public IEnumerable<Type> GetRegisteredServiceTypes()
        {
            return _services.Keys;
        }
    }
}