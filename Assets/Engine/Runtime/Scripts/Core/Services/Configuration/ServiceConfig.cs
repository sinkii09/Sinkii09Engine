using Sirenix.Serialization;
using System.Collections.Generic;
using UnityEngine;

namespace Sinkii09.Engine.Services
{

    [CreateAssetMenu(fileName = "ServiceConfig", menuName = "Engine/Configs/ServiceConfig", order = 1)]
    public class ServiceConfig : ScriptableObject
    {
        [OdinSerialize]
        [SerializeReference]
        public List<IEngineService> Services;
    }
}