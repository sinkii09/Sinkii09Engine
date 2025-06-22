using System;

namespace Sinkii09.Engine.Initializer
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class InitializeAtRuntimeAttribute : Attribute
    {
        public readonly int Priority;
        public InitializeAtRuntimeAttribute(int priority = 0)
        {
            Priority = priority;
        }
    }
}
