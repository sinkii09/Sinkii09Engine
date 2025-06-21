using Cysharp.Threading.Tasks;
using System;
using UnityEngine;
using Sinkii09.Engine.Services;
using Sinkii09.Engine.Configs;

namespace Sinkii09.Engine
{
    public class Engine
    {
        public static bool IsInitialized { get; private set; } = false;
        public static IEgineBehaviour Behaviour { get; private set; }
        public static IConfigProvider ConfigProvider { get; private set; }
        private static void RegisterAllServices()
        {
            ServiceConfig config = UnityEngine.Resources.Load<ServiceConfig>("Configs/ServiceConfig");
            if (config == null)
            {
                UnityEngine.Debug.LogError("ServiceConfig not found. Please ensure it is placed in the Resources/Configs folder.");
                return;
            }
            foreach (var service in config.Services)
            {
                // Ensure the service is instantiated before registering
                Type serviceType = service.GetType();
                IService instantiatedService = Activator.CreateInstance(serviceType) as IService;
                ServiceLocator.RegisterService(serviceType, instantiatedService);
            }
        }

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
            if (Behaviour != null)
            {
                Behaviour.OnBehaviourDestroy -= Terminate;
                Behaviour.Destroy();
                Behaviour = null;
            }

            ServiceLocator.Terminate();
            IsInitialized = false;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnApplicationLoaded()
        {
            ConfigProvider configProvider = new ConfigProvider();

            IEgineBehaviour behaviour = EngineBehaviour.Create();
            InitializeAsync(configProvider,behaviour).Forget();
        }
        public static async UniTask InitializeAsync(IConfigProvider configProvider,IEgineBehaviour behaviour)
        {
            if (IsInitialized)
            {
                Debug.LogWarning("Engine is already initialized. Skipping initialization.");
                return;
            }
            Behaviour = behaviour ?? throw new ArgumentNullException(nameof(behaviour), "EngineBehaviour cannot be null. Please provide a valid instance.");
            Behaviour.OnBehaviourDestroy += Terminate;

            ConfigProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider), "ConfigProvider cannot be null. Please provide a valid instance.");

            RegisterAllServices();
            await ServiceLocator.InitializeAllServices();
            IsInitialized = true;
        }
    }
}