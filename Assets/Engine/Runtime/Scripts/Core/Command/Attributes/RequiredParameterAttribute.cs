using System;

namespace Sinkii09.Engine.Commands
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class RequiredParameterAttribute : Attribute { }
}