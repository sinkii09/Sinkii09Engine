using System;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Runtime utilities for test service detection and filtering
    /// </summary>
    public static class ServiceTestUtils
    {
        /// <summary>
        /// Check if test services should be included at runtime
        /// This is the runtime version of the editor-only TestToggle functionality
        /// </summary>
        public static bool ShouldIncludeTestServices()
        {
            // Layer 1: Assembly-level protection
            #if !UNITY_INCLUDE_TESTS
            return false; // Test assembly not available in builds
            #endif
            
            // Layer 2: Build configuration protection
            #if !DEVELOPMENT_BUILD && !UNITY_EDITOR
            return false; // Production build - never include test services
            #endif
            
            // Layer 3: Explicit enablement check
            #if ENABLE_ENGINE_TESTS
            return true; // Explicitly enabled via scripting define symbol
            #endif
            
            // Layer 4: Runtime toggle check (Editor only)
            #if UNITY_EDITOR
            if (Application.isPlaying)
            {
                // Check runtime toggle state during play mode
                return GetRuntimeTestServiceState();
            }
            return false; // Not in play mode
#elif DEVELOPMENT_BUILD
            return Debug.isDebugBuild; // In dev builds, only if debug is enabled
#endif

#pragma warning disable CS0162 // Unreachable code detected
            return false; // Default: disabled
#pragma warning restore CS0162 // Unreachable code detected
        }
        
        /// <summary>
        /// Get the runtime test service state (Editor only)
        /// Uses reflection to avoid editor assembly dependencies
        /// </summary>
        private static bool GetRuntimeTestServiceState()
        {
            #if UNITY_EDITOR
            try
            {
                // Use reflection to access editor-only SessionState
                var sessionStateType = System.Type.GetType("UnityEditor.SessionState, UnityEditor");
                if (sessionStateType != null)
                {
                    var getBoolMethod = sessionStateType.GetMethod("GetBool", 
                        new[] { typeof(string), typeof(bool) });
                    if (getBoolMethod != null)
                    {
                        return (bool)getBoolMethod.Invoke(null, 
                            new object[] { "EngineTestServices_RuntimeEnabled", false });
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to get runtime test service state: {ex.Message}");
            }
            #endif
            
            return false; // Fallback: disabled
        }
        
        /// <summary>
        /// Determine if a service type is a test service based on various criteria
        /// </summary>
        public static bool IsTestService(Type serviceType)
        {
            if (serviceType == null)
                return false;
            
            // Check 1: Namespace-based detection
            if (IsTestNamespace(serviceType.Namespace))
                return true;
            
            // Check 2: Attribute-based detection
            var serviceAttribute = serviceType.GetEngineServiceAttribute();
            if (serviceAttribute.Category == ServiceCategory.Test)
                return true;
            
            // Check 3: Name-based detection (fallback)
            if (IsTestServiceName(serviceType.Name))
                return true;
            
            return false;
        }
        
        /// <summary>
        /// Check if a namespace indicates test code
        /// </summary>
        private static bool IsTestNamespace(string namespaceName)
        {
            if (string.IsNullOrEmpty(namespaceName))
                return false;
            
            return namespaceName.Contains(".Test") ||
                   namespaceName.Contains(".Tests") ||
                   namespaceName.Contains(".TestHelpers") ||
                   namespaceName.Contains(".Mock") ||
                   namespaceName.Contains(".Mocks");
        }
        
        /// <summary>
        /// Check if a type name indicates test code
        /// </summary>
        private static bool IsTestServiceName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return false;
            
            return typeName.StartsWith("Test") ||
                   typeName.StartsWith("Mock") ||
                   typeName.EndsWith("Test") ||
                   typeName.EndsWith("Tests") ||
                   typeName.EndsWith("Mock") ||
                   typeName.Contains("TestService") ||
                   typeName.Contains("MockService");
        }
        
        /// <summary>
        /// Get a description of why test services are included/excluded
        /// Useful for debugging and logging
        /// </summary>
        public static string GetTestServiceStatus()
        {
            if (!ShouldIncludeTestServices())
            {
                #if !UNITY_INCLUDE_TESTS
                return "Test services excluded: Test assembly not available";
                #elif !DEVELOPMENT_BUILD && !UNITY_EDITOR
                return "Test services excluded: Production build";
                #elif UNITY_EDITOR
                if (Application.isPlaying)
                {
                    var runtimeEnabled = GetRuntimeTestServiceState();
                    return runtimeEnabled ? 
                        "Test services excluded: Runtime toggle disabled" :
                        "Test services excluded: Runtime toggle disabled";
                }
                return "Test services excluded: Not in play mode";
                #else
                return "Test services excluded: Not explicitly enabled";
                #endif
            }
            
            #if ENABLE_ENGINE_TESTS
            return "Test services included: Explicitly enabled via ENABLE_ENGINE_TESTS";
            #elif UNITY_EDITOR
            if (Application.isPlaying && GetRuntimeTestServiceState())
            {
                return "Test services included: Runtime toggle enabled in play mode";
            }
            return "Test services included: Unity Editor play mode";
            #elif DEVELOPMENT_BUILD
            return "Test services included: Development build with debug enabled";
            #else
            return "Test services included: Default development mode";
            #endif
        }
        
        /// <summary>
        /// Log the current test service configuration
        /// </summary>
        public static void LogTestServiceConfiguration()
        {
            var status = GetTestServiceStatus();
            var includeTests = ShouldIncludeTestServices();
            
            if (includeTests)
            {
                Debug.Log($"🧪 {status}");
            }
            else
            {
                Debug.Log($"🚫 {status}");
            }
        }
    }
}