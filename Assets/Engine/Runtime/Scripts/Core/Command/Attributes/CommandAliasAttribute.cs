using System;

namespace Sinkii09.Engine.Commands
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class CommandAliasAttribute : Attribute
    {
        public string Alias { get; }
        public CommandAliasAttribute(string alias)
        {
            Alias = alias;
        }
    }
}