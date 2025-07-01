using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Configs;
using Sinkii09.Engine.Services;
using UnityEngine;

namespace Sinkii09.Engine.Initializer
{
    /// <summary>
    /// Simplified runtime initializer that only bootstraps core components
    /// All service discovery and management is handled by Engine class
    /// </summary>
    public class RuntimeInitializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnApplicationLoaded()
        {
            Debug.Log("Starting Engine bootstrap...");
            InitializeAsync().Forget();
        }

        private static async UniTask InitializeAsync()
        {
            try
            {
                // Create core components
                var configProvider = new ConfigProvider();
                var behaviour = EngineBehaviour.Create();

                // Initialize engine with automatic service discovery
                await Engine.InitializeAsync(configProvider, behaviour);
                
                if (!Application.isPlaying) 
                {
                    Debug.LogWarning("Application stopped playing during initialization");
                    return;
                }

                Debug.Log("Engine bootstrap completed successfully");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Engine bootstrap failed: {ex.Message}\n{ex.StackTrace}");
                
                // Attempt graceful shutdown on failure
                try
                {
                    await Engine.TerminateAsync();
                }
                catch (System.Exception terminateEx)
                {
                    Debug.LogError($"Failed to terminate engine after bootstrap failure: {terminateEx.Message}");
                }
            }
        }
    }
}
