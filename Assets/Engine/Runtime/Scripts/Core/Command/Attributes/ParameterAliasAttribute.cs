using System;

namespace Sinkii09.Engine.Commands
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class ParameterAliasAttribute : Attribute
    {
        /// <summary>
        /// Alias name of the parameter.
        /// </summary>
        public string Alias { get; }

        /// <param name="alias">Alias name of the parameter.</param>
        public ParameterAliasAttribute(string alias)
        {
            Alias = alias;
        }
    }
}