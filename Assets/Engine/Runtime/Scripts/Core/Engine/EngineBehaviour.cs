using Sinkii09.Engine.Services;
using System;
using UnityEngine;

namespace Sinkii09.Engine
{
    public interface IEgineBehaviour
    {
        public event Action OnBehaviourUpdate;
        public event Action OnBehaviourLateUpdate;
        public event Action OnBehaviourDestroy;

        void Destroy();
    }
    public class EngineBehaviour : MonoBehaviour, IEgineBehaviour
    {
        public event Action OnBehaviourUpdate;
        public event Action OnBehaviourLateUpdate;

        public event Action OnBehaviourDestroy;
        private void Start()
        {
            // Check if engine is already initialized
            if (Engine.Initialized)
            {
                OnEngineInitialized(Engine.GetInitializationReport());
            }
            else
            {
                // Subscribe to initialization event
                Engine.EngineInitialized += OnEngineInitialized;
            }
        }

        protected virtual void OnEngineInitialized(ServiceInitializationReport report)
        {
        }

        protected virtual void OnEngineShuttingDown()
        {
        }

        private void Update()
        {
            OnBehaviourUpdate?.Invoke();
        }
        private void LateUpdate()
        {
            OnBehaviourLateUpdate?.Invoke();
        }
        private void OnDestroy()
        {
            // Only invoke destroy callbacks in play mode
            // This prevents services from shutting down when editing in the Unity Editor
            if (Application.isPlaying)
            {
                OnBehaviourDestroy?.Invoke();
            }
            
            // Unsubscribe from events
            Engine.EngineInitialized -= OnEngineInitialized;
            Engine.EngineShuttingDown -= OnEngineShuttingDown;
        }
        public static EngineBehaviour Create(string name = "EngineBehaviour_Runtime")
        {
            GameObject engineObject = new GameObject(name);
            EngineBehaviour engineBehaviour = engineObject.AddComponent<EngineBehaviour>();
            DontDestroyOnLoad(engineObject);
            return engineBehaviour;
        }

        public void Destroy()
        {

        }
    }
}