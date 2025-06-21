using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Sinkii09.Engine.Extensions
{
    public static class ReflectionUtils
    {
        public static HashSet<Type> ExportedDomainTypes => cachedDomainTypes ?? (cachedDomainTypes = GetExportedDomainTypes());

        private static HashSet<Type> cachedDomainTypes;

        public static HashSet<Assembly> GetDomainAssemblies(bool excludeDynamic = true)
        {
            var result = new HashSet<Assembly>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            result.UnionWith(excludeDynamic ? assemblies.Where(a => !IsDynamicAssembly(a)) : assemblies);
            return result;
        }

        public static HashSet<Type> GetExportedDomainTypes()
        {
            var result = new HashSet<Type>();
            result.UnionWith(GetDomainAssemblies().SelectMany(a => a.GetExportedTypes()));
            return result;
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