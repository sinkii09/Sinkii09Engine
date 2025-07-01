using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Sinkii09.Engine.Extensions
{
    public static class ReflectionUtils
    {
        public static HashSet<Type> ExportedDomainTypes => cachedDomainTypes ?? (cachedDomainTypes = GetExportedDomainTypes());

        private static HashSet<Type> cachedDomainTypes;
        private static readonly object cacheLock = new object();
        
        // Assemblies to exclude for performance and stability
        private static readonly HashSet<string> ExcludedAssemblyPrefixes = new HashSet<string>
        {
            "System",
            "mscorlib",
            "Unity",
            "UnityEngine",
            "UnityEditor",
            "Mono",
            "netstandard",
            "Microsoft",
            "Sirenix",
            "nunit",
            "JetBrains"
        };

        public static HashSet<Assembly> GetDomainAssemblies(bool excludeDynamic = true)
        {
            var result = new HashSet<Assembly>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            
            foreach (var assembly in assemblies)
            {
                try
                {
                    // Skip dynamic assemblies if requested
                    if (excludeDynamic && IsDynamicAssembly(assembly))
                        continue;
                    
                    // Skip system and Unity assemblies for better performance
                    var assemblyName = assembly.GetName().Name;
                    if (ExcludedAssemblyPrefixes.Any(prefix => assemblyName.StartsWith(prefix)))
                        continue;
                    
                    // Only include assemblies from our project
                    if (assemblyName.Contains("Sinkii09") || assemblyName.Contains("Engine"))
                    {
                        result.Add(assembly);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to process assembly {assembly.FullName}: {ex.Message}");
                }
            }
            
            return result;
        }

        public static HashSet<Type> GetExportedDomainTypes()
        {
            lock (cacheLock)
            {
                if (cachedDomainTypes != null)
                    return cachedDomainTypes;
                
                var result = new HashSet<Type>();
                var assemblies = GetDomainAssemblies();
                
                foreach (var assembly in assemblies)
                {
                    try
                    {
                        var types = assembly.GetExportedTypes();
                        result.UnionWith(types);
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        // Handle partial type loading failures
                        Debug.LogWarning($"Failed to load some types from {assembly.FullName}: {ex.Message}");
                        if (ex.Types != null)
                        {
                            result.UnionWith(ex.Types.Where(t => t != null));
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Failed to get exported types from {assembly.FullName}: {ex.Message}");
                    }
                }
                
                cachedDomainTypes = result;
                Debug.Log($"Cached {result.Count} exported domain types from {assemblies.Count} assemblies");
                return result;
            }
        }

        public static bool IsDynamicAssembly(Assembly assembly)
        {
#if NET_4_6 || NET_STANDARD_2_0
            return assembly.IsDynamic;
#else
            return assembly is System.Reflection.Emit.AssemblyBuilder;
#endif
        }
    }
}