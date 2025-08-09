using Sinkii09.Engine.Commands;
using Sinkii09.Engine.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sinkii09.Engine
{
    /// <summary>
    /// Represents a serializable <see cref="Command"/> parameter with <see cref="Named{TValue}"/> value.
    /// </summary>
    public abstract class NamedParameter<TValue, TNamedValue> : CommandParameter<TValue>
        where TValue : INamed<TNamedValue>
        where TNamedValue : class, INullableValue
    {
        /// <summary>
        /// Name component of the value or null when value is not assigned.
        /// </summary>
        public string Name => HasValue ? Value.Name : null;
        /// <summary>
        /// Value component of the value or null when value is not assigned.
        /// </summary>
        public TNamedValue NamedValue => HasValue ? Value.Value : null;
    }

    /// <summary>
    /// Represents a <see cref="Command"/> parameter with a collection of nullable <typeparamref name="TItem"/> values.
    /// </summary>
    public abstract class ParameterList<TItem> : CommandParameter<List<TItem>>, IEnumerable<TItem>
        where TItem : class, new()
    {
        /// <summary>
        /// Number of items in the value collection; returns 0 when the value is not assigned.
        /// </summary>
        public int Length => HasValue ? Value.Count : 0;

        public TItem this[int index]
        {
            get => HasValue ? Value[index] : null;
            set { if (HasValue) Value[index] = value; }
        }

        IEnumerator IEnumerable.GetEnumerator() => Value?.GetEnumerator();

        IEnumerator<TItem> IEnumerable<TItem>.GetEnumerator() => Value?.GetEnumerator();

        public override string ToString()
        {
            if (!HasValue || IsDynamic) return base.ToString();
            return string.Join(",", Value);
        }

        /// <summary>
        /// Returns element at the provided index or null, in case the index is not valid 
        /// or element at that index is not assigned (skipped in naninovel script).
        /// </summary>
        public TItem ElementAtOrNull(int index)
        {
            if (!HasValue || !Value.IsIndexValid(index)) return null;
            return Value[index];
        }

        protected override List<TItem> ParseValueText(string valueText, out string errors)
        {
            errors = null;
            var items = valueText.Split(',');
            var list = new List<TItem>(items.Length);
            foreach (var item in items)
                list.Add(string.IsNullOrEmpty(item) ? null : ParseItemValueText(item, out errors));
            return list;
        }

        protected abstract TItem ParseItemValueText(string valueText, out string errors);
    }
    /// <summary>
    /// Represents a serializable <see cref="Command"/> parameter with <see cref="string"/> value.
    /// </summary>
    [Serializable]
    public class StringParameter : CommandParameter<string>
    {
        public static implicit operator StringParameter(string value) => new StringParameter { Value = value };
        public static implicit operator string(StringParameter param) => param?.Value;
        public static implicit operator StringParameter(NullableString value) => new StringParameter { Value = value };
        public static implicit operator NullableString(StringParameter param) => param?.Value;

        protected override string ParseValueText(string valueText, out string errors) => ParseStringText(valueText, out errors);
    }

    /// <summary>
    /// Represents a serializable <see cref="Command"/> parameter with a nullable <see cref="int"/> value.
    /// </summary>
    [Serializable]
    public class IntegerParameter : CommandParameter<int>
    {
        public static implicit operator IntegerParameter(int value) => new IntegerParameter { Value = value };
        public static implicit operator int?(IntegerParameter param) => param?.Value;
        public static implicit operator IntegerParameter(NullableInteger value) => new IntegerParameter { Value = value };
        public static implicit operator NullableInteger(IntegerParameter param) => param?.Value;

        protected override int ParseValueText(string valueText, out string errors) => ParseIntegerText(valueText, out errors);
    }

    /// <summary>
    /// Represents a serializable <see cref="Command"/> parameter with a nullable <see cref="float"/> value.
    /// </summary>
    [Serializable]
    public class DecimalParameter : CommandParameter<float>
    {
        public static implicit operator DecimalParameter(float value) => new DecimalParameter { Value = value };
        public static implicit operator float?(DecimalParameter param) => param?.Value;
        public static implicit operator DecimalParameter(NullableFloat value) => new DecimalParameter { Value = value };
        public static implicit operator NullableFloat(DecimalParameter param) => param?.Value;

        protected override float ParseValueText(string valueText, out string errors) => ParseFloatText(valueText, out errors);
    }

    /// <summary>
    /// Represents a serializable <see cref="Command"/> parameter with a nullable <see cref="bool"/> value.
    /// </summary>
    [Serializable]
    public class BooleanParameter : CommandParameter<bool>
    {
        public static implicit operator BooleanParameter(bool value) => new BooleanParameter { Value = value };
        public static implicit operator bool?(BooleanParameter param) => param?.Value;
        public static implicit operator BooleanParameter(NullableBoolean value) => new BooleanParameter { Value = value };
        public static implicit operator NullableBoolean(BooleanParameter param) => param?.Value;

        protected override bool ParseValueText(string valueText, out string errors) => ParseBooleanText(valueText, out errors);
    }

    /// <summary>
    /// Represents a serializable <see cref="Command"/> parameter with <see cref="NamedString"/> value.
    /// </summary>
    [Serializable]
    public class NamedStringParameter : NamedParameter<NamedString, NullableString>
    {
        public static implicit operator NamedStringParameter(NamedString value) => new NamedStringParameter { Value = value };
        public static implicit operator NamedString(NamedStringParameter param) => param?.Value;

        protected override NamedString ParseValueText(string valueText, out string errors)
        {
            ParseNamedValueText(valueText, out var name, out var namedValueText, out errors);
            var namedValue = string.IsNullOrEmpty(namedValueText) ? null : ParseStringText(namedValueText, out errors);
            return new NamedString(name, namedValue);
        }
    }

    /// <summary>
    /// Represents a serializable <see cref="Command"/> parameter with <see cref="NamedInteger"/> value.
    /// </summary>
    [Serializable]
    public class NamedIntegerParameter : NamedParameter<NamedInteger, NullableInteger>
    {
        public static implicit operator NamedIntegerParameter(NamedInteger value) => new NamedIntegerParameter { Value = value };
        public static implicit operator NamedInteger(NamedIntegerParameter param) => param?.Value;

        protected override NamedInteger ParseValueText(string valueText, out string errors)
        {
            ParseNamedValueText(valueText, out var name, out var namedValueText, out errors);
            var namedValue = string.IsNullOrEmpty(namedValueText) ? null : ParseIntegerText(namedValueText, out errors) as int?;
            return new NamedInteger(name, namedValue);
        }
    }

    /// <summary>
    /// Represents a serializable <see cref="Command"/> parameter with <see cref="NamedFloat"/> value.
    /// </summary>
    [Serializable]
    public class NamedDecimalParameter : NamedParameter<NamedFloat, NullableFloat>
    {
        public static implicit operator NamedDecimalParameter(NamedFloat value) => new NamedDecimalParameter { Value = value };
        public static implicit operator NamedFloat(NamedDecimalParameter param) => param?.Value;

        protected override NamedFloat ParseValueText(string valueText, out string errors)
        {
            ParseNamedValueText(valueText, out var name, out var namedValueText, out errors);
            var namedValue = string.IsNullOrEmpty(namedValueText) ? null : ParseFloatText(namedValueText, out errors) as float?;
            return new NamedFloat(name, namedValue);
        }
    }

    /// <summary>
    /// Represents a serializable <see cref="Command"/> parameter with <see cref="NamedBoolean"/> value.
    /// </summary>
    [Serializable]
    public class NamedBooleanParameter : NamedParameter<NamedBoolean, NullableBoolean>
    {
        public static implicit operator NamedBooleanParameter(NamedBoolean value) => new NamedBooleanParameter { Value = value };
        public static implicit operator NamedBoolean(NamedBooleanParameter param) => param?.Value;

        protected override NamedBoolean ParseValueText(string valueText, out string errors)
        {
            ParseNamedValueText(valueText, out var name, out var namedValueText, out errors);
            var namedValue = string.IsNullOrEmpty(namedValueText) ? null : ParseBooleanText(namedValueText, out errors) as bool?;
            return new NamedBoolean(name, namedValue);
        }
    }

    /// <summary>
    /// Represents a serializable <see cref="Command"/> parameter with a collection of <see cref="NullableString"/> values.
    /// </summary>
    [Serializable]
    public class StringListParameter : ParameterList<NullableString>
    {
        public static implicit operator StringListParameter(List<string> value) => new StringListParameter { Value = value?.Select(v => new NullableString { Value = v })?.ToList() };
        public static implicit operator List<string>(StringListParameter param) => param?.Value?.Select(v => v?.Value)?.ToList();
        public static implicit operator string[](StringListParameter param) => param?.Value?.Select(v => v?.Value)?.ToArray();

        protected override NullableString ParseItemValueText(string valueText, out string errors) => ParseStringText(valueText, out errors);
    }

    /// <summary>
    /// Represents a serializable <see cref="Command"/> parameter with a collection of <see cref="NullableInteger"/> values.
    /// </summary>
    [Serializable]
    public class IntegerListParameter : ParameterList<NullableInteger>
    {
        public static implicit operator IntegerListParameter(List<int?> value) => new IntegerListParameter { Value = value?.Select(v => v.HasValue ? new NullableInteger { Value = v.Value } : new NullableInteger())?.ToList() };
        public static implicit operator List<int?>(IntegerListParameter param) => param?.Value?.Select(v => v?.Value)?.ToList();
        public static implicit operator int?[](IntegerListParameter param) => param?.Value?.Select(v => v?.Value)?.ToArray();

        protected override NullableInteger ParseItemValueText(string valueText, out string errors) => ParseIntegerText(valueText, out errors);
    }

    /// <summary>
    /// Represents a serializable <see cref="Command"/> parameter with a collection of <see cref="NullableFloat"/> values.
    /// </summary>
    [Serializable]
    public class DecimalListParameter : ParameterList<NullableFloat>
    {
        public static implicit operator DecimalListParameter(List<float?> value) => new DecimalListParameter { Value = value?.Select(v => v.HasValue ? new NullableFloat { Value = v.Value } : new NullableFloat())?.ToList() };
        public static implicit operator List<float?>(DecimalListParameter param) => param?.Value?.Select(v => v?.Value)?.ToList();
        public static implicit operator float?[](DecimalListParameter param) => param?.Value?.Select(v => v?.Value)?.ToArray();

        protected override NullableFloat ParseItemValueText(string valueText, out string errors) => ParseFloatText(valueText, out errors);
    }

    /// <summary>
    /// Represents a serializable <see cref="Command"/> parameter with a collection of <see cref="NullableBoolean"/> values.
    /// </summary>
    [Serializable]
    public class BooleanListParameter : ParameterList<NullableBoolean>
    {
        public static implicit operator BooleanListParameter(List<bool?> value) => new BooleanListParameter { Value = value?.Select(v => v.HasValue ? new NullableBoolean { Value = v.Value } : new NullableBoolean())?.ToList() };
        public static implicit operator List<bool?>(BooleanListParameter param) => param?.Value?.Select(v => v?.Value)?.ToList();
        public static implicit operator bool?[](BooleanListParameter param) => param?.Value?.Select(v => v?.Value)?.ToArray();

        protected override NullableBoolean ParseItemValueText(string valueText, out string errors) => ParseBooleanText(valueText, out errors);
    }

    /// <summary>
    /// Represents a serializable <see cref="Command"/> parameter with a collection of <see cref="NullableNamedString"/> values.
    /// </summary>
    [Serializable]
    public class NamedStringListParameter : ParameterList<NullableNamedString>
    {
        public static implicit operator NamedStringListParameter(List<NullableNamedString> value) => new NamedStringListParameter { Value = value };
        public static implicit operator List<NullableNamedString>(NamedStringListParameter param) => param?.Value;

        protected override NullableNamedString ParseItemValueText(string valueText, out string errors)
        {
            ParseNamedValueText(valueText, out var name, out var namedValueText, out errors);
            var namedValue = string.IsNullOrEmpty(namedValueText) ? null : ParseStringText(namedValueText, out errors);
            return new NamedString(name, namedValue);
        }
    }

    /// <summary>
    /// Represents a serializable <see cref="Command"/> parameter with a collection of <see cref="NullableNamedInteger"/> values.
    /// </summary>
    [Serializable]
    public class NamedIntegerListParameter : ParameterList<NullableNamedInteger>
    {
        public static implicit operator NamedIntegerListParameter(List<NullableNamedInteger> value) => new NamedIntegerListParameter { Value = value };
        public static implicit operator List<NullableNamedInteger>(NamedIntegerListParameter param) => param?.Value;

        protected override NullableNamedInteger ParseItemValueText(string valueText, out string errors)
        {
            ParseNamedValueText(valueText, out var name, out var namedValueText, out errors);
            var namedValue = string.IsNullOrEmpty(namedValueText) ? null : ParseIntegerText(namedValueText, out errors) as int?;
            return new NamedInteger(name, namedValue);
        }
    }

    /// <summary>
    /// Represents a serializable <see cref="Command"/> parameter with a collection of <see cref="NullableNamedFloat"/> values.
    /// </summary>
    [Serializable]
    public class NamedDecimalListParameter : ParameterList<NullableNamedFloat>
    {
        public static implicit operator NamedDecimalListParameter(List<NullableNamedFloat> value) => new NamedDecimalListParameter { Value = value };
        public static implicit operator List<NullableNamedFloat>(NamedDecimalListParameter param) => param?.Value;

        protected override NullableNamedFloat ParseItemValueText(string valueText, out string errors)
        {
            ParseNamedValueText(valueText, out var name, out var namedValueText, out errors);
            var namedValue = string.IsNullOrEmpty(namedValueText) ? null : ParseFloatText(namedValueText, out errors) as float?;
            return new NamedFloat(name, namedValue);
        }
    }

    /// <summary>
    /// Represents a serializable <see cref="Command"/> parameter with a collection of <see cref="NullableNamedBoolean"/> values.
    /// </summary>
    [Serializable]
    public class NamedBooleanListParameter : ParameterList<NullableNamedBoolean>
    {
        public static implicit operator NamedBooleanListParameter(List<NullableNamedBoolean> value) => new NamedBooleanListParameter { Value = value };
        public static implicit operator List<NullableNamedBoolean>(NamedBooleanListParameter param) => param?.Value;

        protected override NullableNamedBoolean ParseItemValueText(string valueText, out string errors)
        {
            ParseNamedValueText(valueText, out var name, out var namedValueText, out errors);
            var namedValue = string.IsNullOrEmpty(namedValueText) ? null : ParseBooleanText(namedValueText, out errors) as bool?;
            return new NamedBoolean(name, namedValue);
        }
    }
}
