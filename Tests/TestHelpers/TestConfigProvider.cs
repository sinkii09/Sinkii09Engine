using Sinkii09.Engine.Configs;
using UnityEngine;

namespace Sinkii09.Engine.Tests.TestHelpers
{
    public class TestConfigProvider : IConfigProvider
    {
        public void ClearCache()
        {

        }

        public T GetConfiguration<T>() where T : Configuration
        {
            return ScriptableObject.CreateInstance<T>();
        }

        public Configuration GetConfiguration(System.Type type)
        {
            return ScriptableObject.CreateInstance(type) as Configuration;
        }

        public T ReloadConfiguration<T>() where T : Configuration
        {
            return null;
        }
    }
}