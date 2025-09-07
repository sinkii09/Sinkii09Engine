using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Extensions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using UnityEngine;
using ZLinq;

namespace Sinkii09.Engine.Commands
{
    public static class CommandParser
    {
        private const string ParamSeparator = ":";
        public static Dictionary<string, Type> CommandTypes => GetCommandTypes();

        public static Command FromScriptText(string scriptName, int index, int inlineIndex, string bodyText, out string errors)
        {
            errors = null;

            string commandId = ExtractCommandId(bodyText, out errors);
            if (!string.IsNullOrEmpty(errors)) return null;

            Type commandType = ResolveCommandType(commandId, out errors);
            if (!string.IsNullOrEmpty(errors)) return null;


            Command command = Activator.CreateInstance(commandType) as Command;
            //TODO: Handle Command playback spot.

            Dictionary<string, string> parameters = ExtractCommandParams(bodyText, out errors);
            if (!string.IsNullOrEmpty(errors)) return null;

            var paramfields = commandType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                        .AsValueEnumerable()
                        .Where(field => typeof(ICommandParameter).IsAssignableFrom(field.FieldType));

            List<string> supportedParams = paramfields.Select(field => field.Name).ToList();
            var createdParameters = new Dictionary<string, ICommandParameter>();
            var errorList = new List<string>();

            // Phase 1: Create parameters and apply values/defaults
            foreach (var field in paramfields)
            {
                var alias = field.GetCustomAttribute<ParameterAliasAttribute>()?.Alias;
                if (!string.IsNullOrEmpty(alias))
                {
                    // Use faster lookup - avoid Contains() on List
                    bool alreadyExists = false;
                    for (int i = 0; i < supportedParams.Count; i++)
                    {
                        if (StringComparer.OrdinalIgnoreCase.Equals(supportedParams[i], alias))
                        {
                            alreadyExists = true;
                            break;
                        }
                    }
                    if (!alreadyExists)
                    {
                        supportedParams.Add(alias);
                    }
                }
                
                bool isRequired = field.GetCustomAttribute<RequiredParameterAttribute>() != null;
                var paramId = alias != null && parameters.ContainsKey(alias) ? alias : field.Name;
                var parameter = Activator.CreateInstance(field.FieldType) as ICommandParameter;
                
                // Try to set value from script
                if (parameters.ContainsKey(paramId))
                {
                    var paramValue = parameters[paramId];
                    parameter.SetValueFromScriptText(paramValue, out string error);
                    if (!string.IsNullOrEmpty(error))
                    {
                        errorList.Add($"Failed to set value for parameter '{paramId}' in command '{commandId}': {error}");
                    }
                }
                // Apply default value if no value provided
                else
                {
                    var defaultValueAttr = field.GetCustomAttribute<DefaultValueAttribute>();
                    if (defaultValueAttr != null)
                    {
                        parameter.SetValueFromScriptText(defaultValueAttr.DefaultValue?.ToString(), out string defaultError);
                        if (!string.IsNullOrEmpty(defaultError))
                        {
                            errorList.Add($"Failed to set default value for parameter '{paramId}' in command '{commandId}': {defaultError}");
                        }
                    }
                    else if (isRequired)
                    {
                        errorList.Add($"Required parameter '{paramId}' is missing for command '{commandId}'.");
                    }
                }
                
                createdParameters[field.Name] = parameter;
                field.SetValue(command, parameter);
            }
            
            // Phase 2: Run validation on all parameters
            var validator = new ParameterValidator();
            var validationResult = validator.ValidateAllParameters(commandType, createdParameters);
            if (!validationResult.IsValid)
            {
                errorList.AddRange(validationResult.Errors);
            }
            
            // Report all errors
            if (errorList.Count > 0)
            {
                errors = string.Join("; ", errorList);
                return null;
            }
            // Check for unsupported parameters using HashSet for O(1) lookups
            var supportedParamsSet = new HashSet<string>(supportedParams, StringComparer.OrdinalIgnoreCase);
            var unsupportedParams = new List<string>();
            
            foreach (var paramKey in parameters.Keys)
            {
                if (!supportedParamsSet.Contains(paramKey))
                {
                    unsupportedParams.Add(paramKey);
                }
            }
            
            if (unsupportedParams.Count > 0)
            {
                string warning = $"Command '{commandId}' does not support parameters: {string.Join(", ", unsupportedParams)}.";
                Debug.LogWarning(warning);
            }
            return command;
        }

        private static Dictionary<string, Type> GetCommandTypes()
        {
            var result = new Dictionary<string, Type>();
            var commandTypes = ReflectionUtils.ExportedDomainTypes.AsValueEnumerable()
                .Where(type => type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(Command)));
            foreach (var commandType in commandTypes)
            {
                // Try to get the CommandAliasAttribute
                var aliasAttr = commandType
                    .GetCustomAttributes(typeof(CommandAliasAttribute), false)
                    .AsValueEnumerable()
                    .OfType<CommandAliasAttribute>()
                    .FirstOrDefault();

                string commandKey;
                if (aliasAttr != null && !string.IsNullOrEmpty(aliasAttr.Alias))
                {
                    commandKey = aliasAttr.Alias;
                }
                else if (commandType.Name.EndsWith("Cmd", StringComparison.OrdinalIgnoreCase))
                {
                    commandKey = commandType.Name.Substring(0, commandType.Name.Length - 3);
                }
                else
                {
                    commandKey = commandType.Name;
                }

                if (result.ContainsKey(commandKey))
                {
                    Debug.LogWarning($"Failed to add `{commandType.Name}` (`{commandKey}` alias). Command with the same type name or alias already exists.");
                    continue;
                }
                else result.Add(commandKey, commandType);
            }
            return result;
        }
        public static Type ResolveCommandType(string commandId, out string errors)
        {
            errors = null;
            if (string.IsNullOrEmpty(commandId))
            {
                errors = "Command ID cannot be null or empty.";
                return null;
            }

            // First, try to resolve by key.
            if (!CommandTypes.TryGetValue(commandId, out Type result))
            {
                result = CommandTypes.Values.AsValueEnumerable().FirstOrDefault(commandType => commandType.Name.EqualsFastIgnoreCase(commandId));
                if (result == null)
                {
                    errors = $"Command type for ID '{commandId}' not found.";
                    return null;
                }
            }
            return result;
        }

        private static string ExtractCommandId(string bodyText, out string errors)
        {
            errors = null;

            var id = bodyText.GetBefore(" ") ?? bodyText.GetBefore("\t") ?? bodyText.Trim();

            if (string.IsNullOrEmpty(id))
            {
                errors = "Failed to parse command ID.";
                return null;
            }
            return id;
        }
        private static Dictionary<string, string> ExtractCommandParams(string bodyText, out string errors)
        {
            errors = null;

            var pairs = ExtractCommandPairs(bodyText);

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (pairs == null || pairs.Count == 0)
            {
                return result;
            }

            foreach (var pair in pairs)
            {
                if (IsNamelessParam(pair))
                {
                    // Handle nameless parameter
                    if (result.ContainsKey(string.Empty))
                    {
                        errors = "Multiple nameless parameters found.";
                        return null;
                    }
                    result[string.Empty] = pair.Trim();
                }
                else
                {
                    var parts = pair.Split(new[] { ParamSeparator }, 2, StringSplitOptions.None);
                    if (parts.Length != 2 || string.IsNullOrEmpty(parts[0]) || string.IsNullOrEmpty(parts[1]))
                    {
                        errors = $"Invalid parameter format: '{pair}'.";
                        return null;
                    }
                    var key = parts[0].Trim();
                    var value = parts[1].Trim();

                    if (value.WrappedIn("\""))
                    {
                        value = value.Substring(1, value.Length - 2);
                    }
                    value = value.Replace("\\\"", "\""); // Unescape quotes

                    if (result.ContainsKey(key))
                    {
                        errors = $"Duplicate parameter key found: '{key}'.";
                        continue;
                    }
                    result[key] = value;
                }
            }

            return result;
        }
        private static List<string> ExtractCommandPairs(string lineText)
        {
            int paramStartIndex = lineText.IndexOf(' ') + 1;
            if (paramStartIndex == 0)
                paramStartIndex = lineText.IndexOf('\t') + 1;
            if (paramStartIndex == 0) return null;

            string paramText = lineText.Substring(paramStartIndex);
            var pairs = new List<string>();
            
            int startIndex = -1;
            bool isInsideQuotes = false;

            for (int i = 0; i < paramText.Length; i++)
            {
                char c = paramText[i];

                if (!IsCapturing(startIndex) && IsDelimeterChar(c)) continue;
                if (!IsCapturing(startIndex)) StartCaptureAt(i, ref startIndex);

                if (IsQuoteAt(paramText, i))
                {
                    isInsideQuotes = !isInsideQuotes;
                }

                if (isInsideQuotes) continue; // Skip inside quotes

                if (IsDelimeterChar(c))
                {
                    EndCaptureAt(i - 1, ref startIndex, paramText, pairs);
                    continue;
                }

                if (i == paramText.Length - 1)
                {
                    EndCaptureAt(i, ref startIndex, paramText, pairs);
                }
            }
            return pairs;
        }

        private static bool IsNamelessParam(string pair)
        {
            if (!pair.Contains(ParamSeparator)) return true;
            bool isInsideQuotes = false;
            for (int i = 0; i < pair.Length; i++)
            {
                if (IsQuoteAt(pair, i))
                {
                    isInsideQuotes = !isInsideQuotes;
                }
                
                if (isInsideQuotes) continue; // Skip inside quotes

                if (pair[i] == ParamSeparator[0])
                {
                    return false; // Found a separator outside quotes, so it's not nameless
                }
            }
            return true;
        }
        private static bool IsQuoteAt(string text, int index)
        {
            var c = text[index];
            if (c != '"') return false;
            if (index > 0 && text[index - 1] == '\\') return false; // Check for escaped quote
            return true;
        }
        private static bool IsCapturing(int index) => index >= 0;
        private static bool IsDelimeterChar(char c) => c == ' ' || c == '\t';
        private static void StartCaptureAt(int index, ref int startIndex) => startIndex = index;
        private static void EndCaptureAt(int index, ref int startIndex, string paramText, List<string> pairs)
        {
            if (startIndex < 0) return; // No capture started
            if (index <= startIndex) return; // Invalid capture range
            var captured = index > startIndex ? index - startIndex + 1 : 0;
            if (captured > 0)
            {
                pairs.Add(paramText.Substring(startIndex, captured));
            }
            startIndex = -1; // Reset capture
        }
    }
}