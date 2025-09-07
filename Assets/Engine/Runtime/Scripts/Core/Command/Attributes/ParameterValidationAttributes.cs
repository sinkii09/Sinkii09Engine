using System;

namespace Sinkii09.Engine.Commands
{
    /// <summary>
    /// Validates that a numeric parameter falls within the specified range.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class RangeAttribute : Attribute
    {
        public double MinValue { get; }
        public double MaxValue { get; }

        public RangeAttribute(double minValue, double maxValue)
        {
            MinValue = minValue;
            MaxValue = maxValue;
        }

        public RangeAttribute(int minValue, int maxValue)
        {
            MinValue = minValue;
            MaxValue = maxValue;
        }
    }

    /// <summary>
    /// Validates that a string parameter is not null or empty.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class NotEmptyAttribute : Attribute
    {
        public string ErrorMessage { get; }

        public NotEmptyAttribute(string errorMessage = null)
        {
            ErrorMessage = errorMessage;
        }
    }

    /// <summary>
    /// Validates that an enum parameter has a valid value.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class ValidEnumAttribute : Attribute
    {
        public Type EnumType { get; }

        public ValidEnumAttribute(Type enumType)
        {
            if (!enumType.IsEnum)
                throw new ArgumentException($"Type {enumType} is not an enum type");
            
            EnumType = enumType;
        }
    }

    /// <summary>
    /// Validates that exactly one parameter from a group of mutually exclusive parameters is provided.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class MutuallyExclusiveAttribute : Attribute
    {
        public string GroupName { get; }
        public bool IsRequired { get; }

        public MutuallyExclusiveAttribute(string groupName, bool isRequired = true)
        {
            GroupName = groupName;
            IsRequired = isRequired;
        }
    }

    /// <summary>
    /// Validates that this parameter is provided only if another parameter meets a condition.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class RequiredIfAttribute : Attribute
    {
        public string DependentParameter { get; }
        public object ExpectedValue { get; }
        public ComparisonType ComparisonType { get; }

        public RequiredIfAttribute(string dependentParameter, object expectedValue, ComparisonType comparisonType = ComparisonType.Equals)
        {
            DependentParameter = dependentParameter;
            ExpectedValue = expectedValue;
            ComparisonType = comparisonType;
        }
    }

    /// <summary>
    /// Validates that this parameter is provided only if another parameter does NOT meet a condition.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class RequiredUnlessAttribute : Attribute
    {
        public string DependentParameter { get; }
        public object ExpectedValue { get; }
        public ComparisonType ComparisonType { get; }

        public RequiredUnlessAttribute(string dependentParameter, object expectedValue, ComparisonType comparisonType = ComparisonType.Equals)
        {
            DependentParameter = dependentParameter;
            ExpectedValue = expectedValue;
            ComparisonType = comparisonType;
        }
    }

    /// <summary>
    /// Provides a default value for a parameter if not specified.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class DefaultValueAttribute : Attribute
    {
        public object DefaultValue { get; }

        public DefaultValueAttribute(object defaultValue)
        {
            DefaultValue = defaultValue;
        }
    }

    /// <summary>
    /// Groups parameters together for better organization and validation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class ParameterGroupAttribute : Attribute
    {
        public string GroupName { get; }
        public bool AllRequired { get; }

        public ParameterGroupAttribute(string groupName, bool allRequired = false)
        {
            GroupName = groupName;
            AllRequired = allRequired;
        }
    }

    /// <summary>
    /// Comparison types for conditional validation attributes.
    /// </summary>
    public enum ComparisonType
    {
        Equals,
        NotEquals,
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual,
        IsNull,
        IsNotNull,
        IsEmpty,
        IsNotEmpty
    }
}