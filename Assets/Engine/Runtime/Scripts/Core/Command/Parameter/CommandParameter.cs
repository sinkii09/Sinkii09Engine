using Sinkii09.Engine.Extensions;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Sinkii09.Engine.Commands
{
    public interface ICommandParameter
    {
        bool HasValue { get; }
        bool IsDynamic { get; }
        void SetValueFromScriptText(string paramValue, out string error);
    }
    public abstract partial class CommandParameter<T> : Common.Nullable<T>, ICommandParameter
    {
        [SerializeField] private DynamicValueData dynamicValueData = default;

        public bool IsDynamic => dynamicValueData?.Expressions?.Length > 0;

        public virtual void SetValueFromScriptText(string valueText, out string errors)
        {
            errors = null;

            var expressions = DynamicValueData.CaptureRegex.Matches(valueText).Cast<Match>().Select(m => m.Value).ToArray();
            if (expressions.Length > 0)
            {
                // Value contains injected script expressions (dynamic value); keep the text and parse it at runtime.
                dynamicValueData = new DynamicValueData { ValueText = valueText, Expressions = expressions };
                HasValue = true;
            }
            else
            {
                Value = ParseValueText(valueText, out var hasValue, out errors);
                HasValue = hasValue;
            }
        }
        protected virtual T EvaluateDynamicValue()
        {
            if (dynamicValueData == null || dynamicValueData.Expressions == null || dynamicValueData.Expressions.Length == 0)
            {
                Debug.LogError("Dynamic value data is not set or empty.");
                return default;
            }

            // TODO: Implement actual evaluation logic.
            var valueText = dynamicValueData.ValueText;
            var value = ParseValueText(valueText, out var _, out var errors);
            if (!string.IsNullOrEmpty(errors))
            {
                Debug.LogError($"Error evaluating dynamic value: {errors}");
                return default;
            }
            return value;
        }
        public override string ToString() => HasValue ? (IsDynamic ? dynamicValueData.ValueText : Value?.ToString() ?? string.Empty) : "Unassigned";

        protected override T GetValue() => IsDynamic ? EvaluateDynamicValue() : base.GetValue();

        protected override void SetValue(T value)
        {
            if (IsDynamic)
            {
                dynamicValueData = default;
            }

            base.SetValue(value);
        }
        
    }

}