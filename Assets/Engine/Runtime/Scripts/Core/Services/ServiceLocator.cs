using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    public class ServiceLocator
    {
        private static Dictionary<Type, IService> _services = new();
        public static void RegisterService(Type type, IService service)
        {
            if (service == null)
            {
                Debug.LogError("Cannot register a null service or type.");
                return;
            }
            //TODO: Find a better way to handle the service type
            var interfaceType = service.GetType()
                        .GetInterfaces()
                        .FirstOrDefault(i => typeof(IService).IsAssignableFrom(i) && i != typeof(IService));

            Debug.Log($"Registering service of type {interfaceType}.");
            if (!typeof(IService).IsAssignableFrom(interfaceType))
            {
                Debug.LogError($"Type {interfaceType} does not implement IService interface.");
                return;
            }
            Debug.Log($"Service {interfaceType.Name} registered successfully.");
            _services.TryAdd(interfaceType, service);
        }
        public static async UniTask<bool> InitializeAllServices()
        {
            foreach (var service in _services.Values)
            {
                if (!Engine.Initializing) return false;
                if (!await service.Initialize())
                {
                    Debug.LogError($"Failed to initialize service {service.GetType().Name}.");
                    return false;
                }
            }
            return true;
        }
        public static T GetService<T>() where T : class, IService
        {
            var type = typeof(T);
            Debug.Log($"Requesting service of type {type}.");
            Debug.Log($"Available services: {string.Join(", ", _services.Keys.Select(k => k.Name))}");
            if (_services.TryGetValue(type, out var service))
            {
                return service as T;
            }
            Debug.LogError($"Service of type {type} is not registered.");
            return null;
        }
        public static void Terminate()
        {
            Debug.Log("Terminating all services...");
            foreach (var service in _services.Values)
            {
                service.Terminate();
                Debug.Log($"Service {service.GetType().Name} terminated.");
            }
            _services.Clear();
        }

        public static void ResetService(params Type[] exclude)
        {
            if (_services is null || _services.Count == 0) return;

            foreach (var service in _services.Values)
            {
                if (exclude is null || exclude.Length == 0 || !exclude.Any(t => t.IsAssignableFrom(service.GetType())))
                {
                    service.Reset();
                    Debug.Log($"Service {service.GetType().Name} has been reset.");
                }
            }
        }
    }
}