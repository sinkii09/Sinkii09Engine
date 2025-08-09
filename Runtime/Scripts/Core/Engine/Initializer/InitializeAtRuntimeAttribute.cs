using System;

namespace Sinkii09.Engine.Initializer
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    [Obsolete("Use EngineServiceAttribute instead. This attribute will be removed in a future version.")]
    public class InitializeAtRuntimeAttribute : Attribute
    {
        public readonly int Priority;
        public InitializeAtRuntimeAttribute(int priority = 0)
        {
            Priority = priority;
        }
    }
}
