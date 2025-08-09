using Sinkii09.Engine.Extensions;
using System;
using UnityEngine;

namespace Sinkii09.Engine.Commands
{
    public partial class CommandParameter<T>
    {
        private T ParseValueText(string valueText, out bool hasValue, out string errors)
        {
            if (string.IsNullOrEmpty(valueText))
            {
                hasValue = false;
                errors = null;
                return default;
            }
            else hasValue = true;
            return ParseValueText(valueText, out errors);
        }
        protected abstract T ParseValueText(string valueText, out string errors);

        protected static string ParseStringText(string stringText, out string errors)
        {
            errors = null;
            // Un-escape `{` and `}` literals.
            return stringText.Replace("\\{", "{").Replace("\\}", "}");
        }

        protected static int ParseIntegerText(string intText, out string errors)
        {
            errors = ParseUtils.TryInvariantInt(intText, out var result) ? null : $"Failed to parse `{intText}` string into `{nameof(Int32)}`";
            return result;
        }

        protected static float ParseFloatText(string floatText, out string errors)
        {
            errors = ParseUtils.TryInvariantFloat(floatText, out var result) ? null : $"Failed to parse `{floatText}` string into `{nameof(Single)}`";
            return result;
        }

        protected static bool ParseBooleanText(string boolText, out string errors)
        {
            errors = bool.TryParse(boolText, out var result) ? null : $"Failed to parse `{boolText}` string into `{nameof(Boolean)}`";
            return result;
        }

        protected static void ParseNamedValueText(string valueText, out string name, out string namedValueText, out string errors)
        {
            errors = null;
            var nameText = valueText.Contains(".") ? valueText.GetBefore(".") : valueText;
            name = string.IsNullOrEmpty(nameText) ? null : ParseStringText(nameText, out errors);
            namedValueText = valueText.GetAfterFirst(".");
        }
    }
}
