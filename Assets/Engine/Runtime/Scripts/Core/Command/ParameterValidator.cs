using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using ZLinq;

namespace Sinkii09.Engine.Commands
{
    /// <summary>
    /// Handles validation of command parameters using validation attributes.
    /// </summary>
    public class ParameterValidator
    {
        // Cache for reflection results to avoid repeated GetFields/GetCustomAttribute calls
        private static readonly Dictionary<Type, CachedCommandInfo> s_commandInfoCache = new Dictionary<Type, CachedCommandInfo>();
        
        private struct CachedCommandInfo
        {
            public FieldInfo[] ParameterFields;
            public Dictionary<string, CachedFieldInfo> FieldInfoLookup;
        }
        
        private struct CachedFieldInfo
        {
            public FieldInfo Field;
            public RangeAttribute Range;
            public NotEmptyAttribute NotEmpty;
            public ValidEnumAttribute ValidEnum;
            public RequiredIfAttribute RequiredIf;
            public RequiredUnlessAttribute RequiredUnless;
            public MutuallyExclusiveAttribute MutuallyExclusive;
        }
        public ValidationResult ValidateAllParameters(Type commandType, Dictionary<string, ICommandParameter> parameters)
        {
            var result = new ValidationResult();
            
            // Get or cache command info for performance
            if (!s_commandInfoCache.TryGetValue(commandType, out var commandInfo))
            {
                commandInfo = CacheCommandInfo(commandType);
                s_commandInfoCache[commandType] = commandInfo;
            }

            // Validate individual parameters using cached info
            foreach (var kvp in commandInfo.FieldInfoLookup)
            {
                if (parameters.TryGetValue(kvp.Key, out var parameter))
                {
                    var fieldResult = ValidateParameterCached(kvp.Value, parameter, parameters);
                    result.Merge(fieldResult);
                }
            }

            // Validate parameter groups using cached info
            ValidateParameterGroupsCached(commandInfo, parameters, result);

            return result;
        }
        
        private CachedCommandInfo CacheCommandInfo(Type commandType)
        {
            var paramFields = commandType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .AsValueEnumerable()
                .Where(field => typeof(ICommandParameter).IsAssignableFrom(field.FieldType))
                .ToArray();
                
            var fieldInfoLookup = new Dictionary<string, CachedFieldInfo>();
            
            foreach (var field in paramFields)
            {
                var cachedInfo = new CachedFieldInfo
                {
                    Field = field,
                    Range = field.GetCustomAttribute<RangeAttribute>(),
                    NotEmpty = field.GetCustomAttribute<NotEmptyAttribute>(),
                    ValidEnum = field.GetCustomAttribute<ValidEnumAttribute>(),
                    RequiredIf = field.GetCustomAttribute<RequiredIfAttribute>(),
                    RequiredUnless = field.GetCustomAttribute<RequiredUnlessAttribute>(),
                    MutuallyExclusive = field.GetCustomAttribute<MutuallyExclusiveAttribute>()
                };
                
                fieldInfoLookup[field.Name] = cachedInfo;
            }
            
            return new CachedCommandInfo
            {
                ParameterFields = paramFields,
                FieldInfoLookup = fieldInfoLookup
            };
        }
        
        private ValidationResult ValidateParameterCached(CachedFieldInfo fieldInfo, ICommandParameter parameter, Dictionary<string, ICommandParameter> allParameters)
        {
            var result = new ValidationResult();
            
            // Use cached attributes instead of reflection
            if (fieldInfo.Range != null)
                ValidateRangeCached(fieldInfo.Field, fieldInfo.Range, parameter, result);
                
            if (fieldInfo.NotEmpty != null)
                ValidateNotEmptyCached(fieldInfo.Field, fieldInfo.NotEmpty, parameter, result);
                
            if (fieldInfo.ValidEnum != null)
                ValidateEnumCached(fieldInfo.Field, fieldInfo.ValidEnum, parameter, result);
                
            if (fieldInfo.RequiredIf != null || fieldInfo.RequiredUnless != null)
                ValidateConditionalRequirementsCached(fieldInfo, parameter, allParameters, result);
                
            return result;
        }

        public ValidationResult ValidateParameter(FieldInfo field, ICommandParameter parameter, Dictionary<string, ICommandParameter> allParameters)
        {
            var result = new ValidationResult();

            // Validate Range
            var rangeAttr = field.GetCustomAttribute<RangeAttribute>();
            if (rangeAttr != null)
                ValidateRangeCached(field, rangeAttr, parameter, result);

            // Validate NotEmpty
            var notEmptyAttr = field.GetCustomAttribute<NotEmptyAttribute>();
            if (notEmptyAttr != null)
                ValidateNotEmptyCached(field, notEmptyAttr, parameter, result);

            // Validate ValidEnum
            var validEnumAttr = field.GetCustomAttribute<ValidEnumAttribute>();
            if (validEnumAttr != null)
                ValidateEnumCached(field, validEnumAttr, parameter, result);

            // Validate RequiredIf/RequiredUnless
            var cachedFieldInfo = new CachedFieldInfo
            {
                Field = field,
                RequiredIf = field.GetCustomAttribute<RequiredIfAttribute>(),
                RequiredUnless = field.GetCustomAttribute<RequiredUnlessAttribute>()
            };
            ValidateConditionalRequirementsCached(cachedFieldInfo, parameter, allParameters, result);

            return result;
        }

        private void ValidateRangeCached(FieldInfo field, RangeAttribute rangeAttr, ICommandParameter parameter, ValidationResult result)
        {
            if (!parameter.HasValue) return;

            double value = 0;
            bool hasNumericValue = false;

            switch (parameter)
            {
                case CommandParameter<int> intParam:
                    value = intParam.Value;
                    hasNumericValue = true;
                    break;
                case CommandParameter<float> floatParam:
                    value = floatParam.Value;
                    hasNumericValue = true;
                    break;
                case CommandParameter<double> doubleParam:
                    value = doubleParam.Value;
                    hasNumericValue = true;
                    break;
            }

            if (hasNumericValue && (value < rangeAttr.MinValue || value > rangeAttr.MaxValue))
            {
                result.AddError($"Parameter '{field.Name}' value {value} is outside allowed range [{rangeAttr.MinValue}, {rangeAttr.MaxValue}]");
            }
        }

        private void ValidateNotEmptyCached(FieldInfo field, NotEmptyAttribute notEmptyAttr, ICommandParameter parameter, ValidationResult result)
        {

            if (parameter is CommandParameter<string> stringParam)
            {
                if (!stringParam.HasValue || string.IsNullOrEmpty(stringParam.Value))
                {
                    var message = notEmptyAttr.ErrorMessage ?? $"Parameter '{field.Name}' cannot be empty";
                    result.AddError(message);
                }
            }
        }

        private void ValidateEnumCached(FieldInfo field, ValidEnumAttribute enumAttr, ICommandParameter parameter, ValidationResult result)
        {
            if (!parameter.HasValue) return;

            var paramValue = GetParameterValue(parameter);
            if (paramValue != null && !Enum.IsDefined(enumAttr.EnumType, paramValue))
            {
                var validValues = string.Join(", ", Enum.GetNames(enumAttr.EnumType));
                result.AddError($"Parameter '{field.Name}' has invalid value '{paramValue}'. Valid values: {validValues}");
            }
        }

        private void ValidateConditionalRequirementsCached(CachedFieldInfo fieldInfo, ICommandParameter parameter, Dictionary<string, ICommandParameter> allParameters, ValidationResult result)
        {
            // Validate RequiredIf
            if (fieldInfo.RequiredIf != null)
            {
                if (ShouldBeRequired(fieldInfo.RequiredIf.DependentParameter, fieldInfo.RequiredIf.ExpectedValue, fieldInfo.RequiredIf.ComparisonType, allParameters))
                {
                    if (!parameter.HasValue)
                    {
                        result.AddError($"Parameter '{fieldInfo.Field.Name}' is required when '{fieldInfo.RequiredIf.DependentParameter}' is '{fieldInfo.RequiredIf.ExpectedValue}'");
                    }
                }
            }

            // Validate RequiredUnless
            if (fieldInfo.RequiredUnless != null)
            {
                if (!ShouldBeRequired(fieldInfo.RequiredUnless.DependentParameter, fieldInfo.RequiredUnless.ExpectedValue, fieldInfo.RequiredUnless.ComparisonType, allParameters))
                {
                    if (!parameter.HasValue)
                    {
                        result.AddError($"Parameter '{fieldInfo.Field.Name}' is required unless '{fieldInfo.RequiredUnless.DependentParameter}' is '{fieldInfo.RequiredUnless.ExpectedValue}'");
                    }
                }
            }
        }
        
        private void ValidateParameterGroupsCached(CachedCommandInfo commandInfo, Dictionary<string, ICommandParameter> parameters, ValidationResult result)
        {
            // Group mutually exclusive fields by group name for better performance
            var mutuallyExclusiveGroups = new Dictionary<string, List<CachedFieldInfo>>();
            
            foreach (var fieldInfo in commandInfo.FieldInfoLookup.Values)
            {
                if (fieldInfo.MutuallyExclusive != null)
                {
                    if (!mutuallyExclusiveGroups.TryGetValue(fieldInfo.MutuallyExclusive.GroupName, out var groupList))
                    {
                        groupList = new List<CachedFieldInfo>();
                        mutuallyExclusiveGroups[fieldInfo.MutuallyExclusive.GroupName] = groupList;
                    }
                    groupList.Add(fieldInfo);
                }
            }
            
            foreach (var groupKvp in mutuallyExclusiveGroups)
            {
                var group = groupKvp.Value;
                var attr = group[0].MutuallyExclusive;
                var providedParamsCount = 0;
                
                // Count provided parameters in this group without allocating lists
                foreach (var fieldInfo in group)
                {
                    if (parameters.TryGetValue(fieldInfo.Field.Name, out var param) && param.HasValue)
                    {
                        providedParamsCount++;
                    }
                }
                
                if (providedParamsCount > 1)
                {
                    // Build error message efficiently
                    var sb = new System.Text.StringBuilder();
                    bool first = true;
                    foreach (var fieldInfo in group)
                    {
                        if (parameters.TryGetValue(fieldInfo.Field.Name, out var param) && param.HasValue)
                        {
                            if (!first) sb.Append(", ");
                            sb.Append(fieldInfo.Field.Name);
                            first = false;
                        }
                    }
                    result.AddError($"Parameters in group '{attr.GroupName}' are mutually exclusive. Found: {sb.ToString()}");
                }
                else if (providedParamsCount == 0 && attr.IsRequired)
                {
                    // Build all parameter names for required group
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < group.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(group[i].Field.Name);
                    }
                    result.AddError($"At least one parameter from group '{attr.GroupName}' is required. Options: {sb.ToString()}");
                }
            }
        }

        private void ValidateParameterGroups(List<FieldInfo> paramFields, Dictionary<string, ICommandParameter> parameters, ValidationResult result)
        {
            var mutuallyExclusiveGroups = paramFields
                .AsValueEnumerable()
                .Where(f => f.GetCustomAttribute<MutuallyExclusiveAttribute>() != null)
                .GroupBy(f => f.GetCustomAttribute<MutuallyExclusiveAttribute>().GroupName);

            foreach (var group in mutuallyExclusiveGroups)
            {
                var attr = group.First().GetCustomAttribute<MutuallyExclusiveAttribute>();
                var providedParams = group.AsValueEnumerable().Where(f => parameters.TryGetValue(f.Name, out var param) && param.HasValue).ToList();

                if (providedParams.Count > 1)
                {
                    var paramNames = string.Join(", ", providedParams.AsValueEnumerable().Select(f => f.Name));
                    result.AddError($"Parameters in group '{attr.GroupName}' are mutually exclusive. Found: {paramNames}");
                }
                else if (providedParams.Count == 0 && attr.IsRequired)
                {
                    var paramNames = string.Join(", ", group.AsValueEnumerable().Select(f => f.Name));
                    result.AddError($"At least one parameter from group '{attr.GroupName}' is required. Options: {paramNames}");
                }
            }
        }

        private bool ShouldBeRequired(string dependentParam, object expectedValue, ComparisonType comparisonType, Dictionary<string, ICommandParameter> allParameters)
        {
            if (!allParameters.TryGetValue(dependentParam, out var param))
                return comparisonType == ComparisonType.IsNull;

            var paramValue = GetParameterValue(param);

            return comparisonType switch
            {
                ComparisonType.Equals => Equals(paramValue, expectedValue),
                ComparisonType.NotEquals => !Equals(paramValue, expectedValue),
                ComparisonType.IsNull => paramValue == null,
                ComparisonType.IsNotNull => paramValue != null,
                ComparisonType.IsEmpty => string.IsNullOrEmpty(paramValue?.ToString()),
                ComparisonType.IsNotEmpty => !string.IsNullOrEmpty(paramValue?.ToString()),
                _ => false
            };
        }

        private object GetParameterValue(ICommandParameter parameter)
        {
            if (!parameter.HasValue) return null;

            // Use reflection to get the Value property from the generic CommandParameter<T>
            var valueProperty = parameter.GetType().GetProperty("Value");
            return valueProperty?.GetValue(parameter);
        }
    }

    /// <summary>
    /// Represents the result of parameter validation.
    /// </summary>
    public class ValidationResult
    {
        private readonly List<string> errors = new List<string>();

        public bool IsValid => errors.Count == 0;
        public IReadOnlyList<string> Errors => errors;

        public void AddError(string error)
        {
            if (!string.IsNullOrEmpty(error))
                errors.Add(error);
        }

        public void Merge(ValidationResult other)
        {
            errors.AddRange(other.errors);
        }

        public override string ToString()
        {
            return string.Join("; ", errors);
        }
    }
}