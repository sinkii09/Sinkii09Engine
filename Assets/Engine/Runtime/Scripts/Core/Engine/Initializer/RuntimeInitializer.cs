using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Configs;
using Sinkii09.Engine.Extensions;
using Sinkii09.Engine.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace Sinkii09.Engine.Initializer
{
    public class RuntimeInitializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnApplicationLoaded()
        {
            ConfigProvider configProvider = new ConfigProvider();

            InitializeAsync(configProvider).Forget();
        }

        private static async UniTask InitializeAsync(ConfigProvider configProvider)
        {
            var initData = new List<ServiceInitData>();

            foreach (var type in ReflectionUtils.ExportedDomainTypes)
            {
                var attribute = type.GetCustomAttribute<InitializeAtRuntimeAttribute>();
                if (attribute == null)
                    continue;

                initData.Add(new ServiceInitData(type, attribute));
            }

            initData = initData.OrderBy(d => d.Priority)
                               .TopologicalOrder(d => GetDependencies(d, initData))
                               .ToList();

            IEgineBehaviour behaviour = EngineBehaviour.Create();
            List<IService> services = new List<IService>();
            List<object> ctorParams = new List<object>();

            foreach (var data in initData)
            {
                foreach (var argType in data.CtorArgs)
                {
                    if (IsBehaviour(argType))
                    {
                        ctorParams.Add(behaviour);
                    }
                    else if (IsConfig(argType))
                    {
                        var config = configProvider.GetConfiguration(argType);
                        ctorParams.Add(config ?? throw new InvalidOperationException($"Configuration for {argType.Name} not found."));
                    }
                    else if (IsService(argType))
                    {
                        var dependencyService = services.FirstOrDefault(s => argType.IsAssignableFrom(s.GetType()));
                        ctorParams.Add(dependencyService ?? throw new InvalidOperationException($"Service of type {argType.Name} not found."));
                    }
                    else
                    {
                        throw new InvalidOperationException($"Unsupported constructor argument type: {argType.Name}");
                    }
                }
                var service = Activator.CreateInstance(data.Type, ctorParams.ToArray()) as IService;
                if (service == null)
                {
                    throw new InvalidOperationException($"Failed to create instance of service {data.Type.Name}. Ensure it implements IService and has a valid constructor.");
                }
                services.Add(service);
                ctorParams.Clear();
            }

            await Engine.InitializeAsync(configProvider, behaviour, services);
            if (!Application.isPlaying) return;

            // TODO: Add any additional initialization logic here if needed
        }

        private static bool IsBehaviour(Type type)
        {
            return typeof(IEgineBehaviour).IsAssignableFrom(type);
        }
        private static bool IsConfig(Type type)
        {
            return typeof(Configuration).IsAssignableFrom(type);
        }
        private static bool IsService(Type type)
        {
            return typeof(IService).IsAssignableFrom(type);
        }

        /// <summary>
        /// Finds all ServiceInitData dependencies for the given service initialization data.
        /// A dependency is any ServiceInitData in initData whose type matches a service-type constructor argument of d.
        /// </summary>
        private static IEnumerable<ServiceInitData> GetDependencies(ServiceInitData serviceData, List<ServiceInitData> allInitData)
        {
            // Iterate over each constructor argument type of the service
            foreach (var ctorArgType in serviceData.CtorArgs)
            {
                // Only consider arguments that are services
                if (!IsService(ctorArgType))
                    continue;

                // Find all ServiceInitData in the list that are not the current one and whose type is assignable from the argument type
                foreach (var candidate in allInitData)
                {
                    if (serviceData.Equals(candidate))
                        continue;

                    if (ctorArgType.IsAssignableFrom(candidate.Type))
                        yield return candidate;
                }
            }
        }
    }
}
