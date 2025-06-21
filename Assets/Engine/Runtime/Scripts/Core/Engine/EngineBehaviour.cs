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
            OnBehaviourDestroy?.Invoke();
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