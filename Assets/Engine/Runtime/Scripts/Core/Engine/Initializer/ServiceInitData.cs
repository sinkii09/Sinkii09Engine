using System;
using System.Collections.Generic;
using System.Linq;
using ZLinq;

namespace Sinkii09.Engine.Initializer
{

    public struct ServiceInitData : IEquatable<ServiceInitData>
    {
        public readonly Type Type;
        public readonly Type[] CtorArgs;
        public readonly int Priority;
        // TODO: Add priority and other fields as needed

        public ServiceInitData(Type type, InitializeAtRuntimeAttribute initAttribute)
        {
            Type = type;
            Priority = initAttribute.Priority;
            CtorArgs = Type.GetConstructors()
                .First()
                .GetParameters().AsValueEnumerable()
                .Select(param => param.ParameterType)
                .ToArray();
        }

        public bool Equals(ServiceInitData other)
        {
            return EqualityComparer<Type>.Default.Equals(Type, other.Type);
        }
        public override bool Equals(object obj)
        {
            return obj is ServiceInitData other && Equals(other);
        }
        public override int GetHashCode() => HashCode.Combine(Type);

        public static bool operator == (ServiceInitData left, ServiceInitData right) => left.Equals(right);
        public static bool operator != (ServiceInitData left, ServiceInitData right) => !(left == right);
    }
}
