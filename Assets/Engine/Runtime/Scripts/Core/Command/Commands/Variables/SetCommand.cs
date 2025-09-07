using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Commands;
using Sinkii09.Engine.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Commands
{
    /// <summary>
    /// Command for setting variable values with high-performance parsing
    /// 
    /// Usage examples:
    /// @set var:health val:100
    /// @set var:playerName val:"Hero"
    /// @set var:level val:5 type:int
    /// @set var:isAlive val:true
    /// @set var:score val:1000.5 type:float
    /// @set var:config from:sourceConfig  // Copy from another variable
    /// </summary>
    [Serializable]
    [CommandAlias("set")]
    public class SetCommand : Command
    {
        #region Parameters
        [Header("Target Variable")]
        [ParameterAlias(ParameterAliases.Variable)]
        [RequiredParameter]
        public StringParameter variable = new StringParameter(); // Target variable name
        
        [Header("Source")]
        [ParameterAlias(ParameterAliases.Value)]
        public StringParameter value = new StringParameter(); // Literal value to set
        [ParameterAlias(ParameterAliases.From)]
        public StringParameter rightVar = new StringParameter(); // Source variable to copy from
        
        [Header("Type Control (Optional)")]
        [ParameterAlias(ParameterAliases.Type)]
        public StringParameter type = new StringParameter(); // Explicit type: auto, string, int, float, bool, null
        [ParameterAlias(ParameterAliases.Force)]
        public BooleanParameter forceType = new BooleanParameter(); // Force type conversion even if it might fail
        #endregion

        #region Static Configuration
        private static readonly Dictionary<string, ValueType> s_typeMap = new Dictionary<string, ValueType>(StringComparer.OrdinalIgnoreCase)
        {
            ["auto"] = ValueType.Auto,
            ["string"] = ValueType.String,
            ["str"] = ValueType.String,
            ["text"] = ValueType.String,
            ["int"] = ValueType.Integer,
            ["integer"] = ValueType.Integer,
            ["number"] = ValueType.Integer,
            ["float"] = ValueType.Float,
            ["double"] = ValueType.Float,
            ["decimal"] = ValueType.Float,
            ["num"] = ValueType.Float,
            ["bool"] = ValueType.Boolean,
            ["boolean"] = ValueType.Boolean,
            ["flag"] = ValueType.Boolean,
            ["null"] = ValueType.Null,
            ["void"] = ValueType.Null
        };

        private static readonly Dictionary<string, object> s_parseCache = new Dictionary<string, object>();
        private static readonly HashSet<string> s_booleanTrue = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
        { "true", "yes", "on", "1", "enabled", "active" };
        private static readonly HashSet<string> s_booleanFalse = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
        { "false", "no", "off", "0", "disabled", "inactive" };
        #endregion

        #region Enums
        private enum ValueType
        {
            Auto,
            String,
            Integer,
            Float,
            Boolean,
            Null
        }
        #endregion

        public override async UniTask ExecuteAsync(CancellationToken token = default)
        {
            try
            {
                var scriptPlayer = Engine.GetService<IScriptPlayerService>();
                if (scriptPlayer == null)
                {
                    throw new InvalidOperationException("ScriptPlayerService is not available for set command");
                }

                // Validate parameters
                if (!ValidateParameters(out string error))
                {
                    throw new InvalidOperationException(error);
                }

                // Get the value to set
                object sourceValue = GetSourceValue(scriptPlayer);

                // Apply type conversion if needed
                object finalValue = ApplyTypeConversion(sourceValue);

                // Set the variable
                scriptPlayer.SetVariable(variable.Value, finalValue);

                if (Debug.isDebugBuild)
                {
                    Debug.Log($"[SetCommand] {variable.Value} = {finalValue} ({finalValue?.GetType().Name ?? "null"})");
                }

                await UniTask.Yield();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SetCommand] Failed to execute: {ex.Message}");
                throw;
            }
        }

        #region Helper Methods
        private bool ValidateParameters(out string error)
        {
            error = null;

            if (!variable.HasValue || string.IsNullOrEmpty(variable.Value))
            {
                error = "Set command requires a var parameter";
                return false;
            }

            bool hasValue = value.HasValue && !string.IsNullOrEmpty(value.Value);
            bool hasRightVar = rightVar.HasValue && !string.IsNullOrEmpty(rightVar.Value);

            if (!hasValue && !hasRightVar)
            {
                error = "Set command requires either val or from parameter";
                return false;
            }

            return true;
        }

        private object GetSourceValue(IScriptPlayerService scriptPlayer)
        {
            // Prefer variable copy over literal value
            if (rightVar.HasValue && !string.IsNullOrEmpty(rightVar.Value))
            {
                if (scriptPlayer.HasVariable(rightVar.Value))
                {
                    return scriptPlayer.GetVariable(rightVar.Value);
                }
                
                // If the variable doesn't exist, treat as literal
                return ParseLiteralValue(rightVar.Value);
            }

            // Parse literal value
            return ParseLiteralValue(value.Value);
        }

        private object ParseLiteralValue(string valueStr)
        {
            if (string.IsNullOrEmpty(valueStr))
                return null;

            // Use cache for commonly used values
            if (s_parseCache.TryGetValue(valueStr, out var cached))
                return cached;

            var parsed = ParseLiteralValueInternal(valueStr);

            // Cache frequently used values (limit cache size)
            if (s_parseCache.Count < 200)
            {
                s_parseCache[valueStr] = parsed;
            }

            return parsed;
        }

        private object ParseLiteralValueInternal(string valueStr)
        {
            valueStr = valueStr.Trim();

            // Handle null explicitly
            if (valueStr.Equals("null", StringComparison.OrdinalIgnoreCase))
                return null;

            // Handle quoted strings
            if (valueStr.Length >= 2)
            {
                char first = valueStr[0];
                char last = valueStr[valueStr.Length - 1];
                if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
                {
                    return valueStr.Substring(1, valueStr.Length - 2);
                }
            }

            // Try boolean using predefined sets
            if (s_booleanTrue.Contains(valueStr))
                return true;
            if (s_booleanFalse.Contains(valueStr))
                return false;

            // Try integer parsing
            if (int.TryParse(valueStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
                return intValue;

            // Try float parsing
            if (double.TryParse(valueStr, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double doubleValue))
                return doubleValue;

            // Default to string
            return valueStr;
        }

        private object ApplyTypeConversion(object sourceValue)
        {
            if (!type.HasValue || string.IsNullOrEmpty(type.Value))
                return sourceValue; // No conversion needed

            if (!s_typeMap.TryGetValue(type.Value, out var targetType))
            {
                Debug.LogWarning($"[SetCommand] Unknown type hint: {type.Value}");
                return sourceValue;
            }

            try
            {
                return ConvertToType(sourceValue, targetType);
            }
            catch (Exception ex)
            {
                if (forceType.HasValue && forceType.Value)
                {
                    throw new ArgumentException($"Failed to convert value to {targetType}: {ex.Message}");
                }

                Debug.LogWarning($"[SetCommand] Type conversion failed, using original value: {ex.Message}");
                return sourceValue;
            }
        }

        private object ConvertToType(object value, ValueType targetType)
        {
            return targetType switch
            {
                ValueType.Auto => value, // No conversion
                ValueType.Null => null,
                ValueType.String => value?.ToString() ?? string.Empty,
                ValueType.Integer => ConvertToInteger(value),
                ValueType.Float => ConvertToFloat(value),
                ValueType.Boolean => ConvertToBoolean(value),
                _ => value
            };
        }

        private int ConvertToInteger(object value)
        {
            return value switch
            {
                null => 0,
                int i => i,
                long l => checked((int)l),
                float f => checked((int)f),
                double d => checked((int)d),
                bool b => b ? 1 : 0,
                string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) => result,
                _ => throw new ArgumentException($"Cannot convert {value} ({value?.GetType().Name}) to integer")
            };
        }

        private double ConvertToFloat(object value)
        {
            return value switch
            {
                null => 0.0,
                int i => i,
                long l => l,
                float f => f,
                double d => d,
                bool b => b ? 1.0 : 0.0,
                string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) => result,
                _ => throw new ArgumentException($"Cannot convert {value} ({value?.GetType().Name}) to float")
            };
        }

        private bool ConvertToBoolean(object value)
        {
            return value switch
            {
                null => false,
                bool b => b,
                int i => i != 0,
                long l => l != 0,
                float f => Math.Abs(f) > float.Epsilon,
                double d => Math.Abs(d) > double.Epsilon,
                string s when s_booleanTrue.Contains(s) => true,
                string s when s_booleanFalse.Contains(s) => false,
                string s when bool.TryParse(s, out bool result) => result,
                _ => throw new ArgumentException($"Cannot convert {value} ({value?.GetType().Name}) to boolean")
            };
        }
        #endregion
    }
}