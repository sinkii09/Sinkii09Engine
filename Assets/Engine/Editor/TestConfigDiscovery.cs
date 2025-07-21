using Sinkii09.Engine.Extensions;
using Sinkii09.Engine.Services;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Sinkii09.Engine.Editor
{
    /// <summary>
    /// Test script to verify configuration auto-discovery
    /// </summary>
    public static class TestConfigDiscovery
    {
        [MenuItem("Engine/Test/Test Config Discovery")]
        public static void TestDiscovery()
        {
            Debug.Log("=== Testing Configuration Discovery ===");

            var allTypes = ReflectionUtils.ExportedDomainTypes;
            int serviceCount = 0;
            int configCount = 0;

            foreach (var serviceType in allTypes)
            {
                if (!typeof(IEngineService).IsAssignableFrom(serviceType) || serviceType.IsAbstract)
                    continue;

                serviceCount++;
                var configAttr = serviceType.GetCustomAttribute<ServiceConfigurationAttribute>();
                if (configAttr?.ConfigurationType != null)
                {
                    configCount++;
                    Debug.Log($"Found: {serviceType.Name} -> {configAttr.ConfigurationType.Name}");
                }
            }

            Debug.Log($"Total Services: {serviceCount}, Services with Config: {configCount}");

            // Test the new menu functionality
            Debug.Log("\n=== Testing Menu Discovery ===");
            ConfigManagerMenu.RefreshConfigMenu();
        }
    }
}