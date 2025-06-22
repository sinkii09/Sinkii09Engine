using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Configs;
using Sinkii09.Engine.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine
{
    public class Engine
    {
        /// <summary>  
        /// Whether the engine is initialized and ready.  
        /// </summary>  
        public static bool Initialized => initializeTCS != null && initializeTCS.Task.Status == UniTaskStatus.Succeeded;
        /// <summary>  
        /// Whether the engine is currently being initialized.  
        /// </summary>  
        public static bool Initializing => initializeTCS != null && initializeTCS.Task.Status == UniTaskStatus.Pending;
        public static IEgineBehaviour Behaviour { get; private set; }
        public static IConfigProvider ConfigProvider { get; private set; }

        private static UniTaskCompletionSource<object> initializeTCS;
        public static T GetService<T>() where T : class, IService
        {
            return ServiceLocator.GetService<T>();
        }
        public static T GetConfig<T>() where T : Configuration
        {
            if (ConfigProvider == null)
            {
                Debug.LogError("ConfigProvider is not initialized. Please initialize the Engine first.");
                return null;
            }
            return ConfigProvider.GetConfiguration(typeof(T)) as T;
        }
        public static void Reset(params Type[] exclude)
        {
            ServiceLocator.ResetService(exclude);
        }
        public static void Terminate()
        {
            if (initializeTCS != null && initializeTCS.Task.Status == UniTaskStatus.Pending)
            {
                initializeTCS.TrySetCanceled();
            }
            if (initializeTCS != null && initializeTCS.Task.Status == UniTaskStatus.Faulted)
            {
                Debug.LogError("Engine initialization failed. Please check the logs for more details.");
            }
            if (initializeTCS != null)
            {
                initializeTCS.TrySetResult(null);
                initializeTCS = null;
            }
            if (ConfigProvider != null)
            {
                ConfigProvider = null;
            }

            ServiceLocator.Terminate();

            if (Behaviour != null)
            {
                Behaviour.OnBehaviourDestroy -= Terminate;
                Behaviour.Destroy();
                Behaviour = null;
            }

        }

        public static async UniTask InitializeAsync(IConfigProvider configProvider, IEgineBehaviour behaviour, List<IService> services)
        {
            if (Initialized) return;
            if (Initializing) { await initializeTCS.Task; return; }

            initializeTCS = new UniTaskCompletionSource<object>();

            Behaviour = behaviour ?? throw new ArgumentNullException(nameof(behaviour), "EngineBehaviour cannot be null. Please provide a valid instance.");
            Behaviour.OnBehaviourDestroy += Terminate;

            ConfigProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider), "ConfigProvider cannot be null. Please provide a valid instance.");

            RegisterAllServices(services);

            await ServiceLocator.InitializeAllServices();

            initializeTCS?.TrySetResult(null);
        }
        private static void RegisterAllServices(List<IService> services)
        {
            ServiceLocator.Terminate();
            foreach (var service in services)
            {
                if (service == null)
                {
                    Debug.LogError("Cannot register a null service.");
                    continue;
                }
                ServiceLocator.RegisterService(service.GetType(), service);
            }
        }
    }
}