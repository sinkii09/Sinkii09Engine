using System;
using System.Globalization;
using UnityEngine;

namespace Sinkii09.Engine.Commands
{
    /// <summary>
    /// Represents a serializable Command parameter with a Vector3 value for positions, rotations, and scales.
    /// Supports various input formats: "1,2,3", "left", "center", "right", etc.
    /// </summary>
    [Serializable]
    public class Vector3Parameter : CommandParameter<Vector3>
    {
        public static implicit operator Vector3Parameter(Vector3 value) => new Vector3Parameter { Value = value };
        public static implicit operator Vector3?(Vector3Parameter param) => param?.Value;

        protected override Vector3 ParseValueText(string valueText, out string errors)
        {
            errors = null;
            
            if (string.IsNullOrEmpty(valueText))
                return Vector3.zero;

            valueText = valueText.Trim().ToLowerInvariant();

            // Handle preset positions
            var presetPosition = valueText switch
            {
                "left" => new Vector3(-2f, 0f, 0f),
                "center" => Vector3.zero,
                "right" => new Vector3(2f, 0f, 0f),
                "up" => new Vector3(0f, 2f, 0f),
                "down" => new Vector3(0f, -2f, 0f),
                "front" => new Vector3(0f, 0f, -1f),
                "back" => new Vector3(0f, 0f, 1f),
                _ => (Vector3?)null
            };

            if (presetPosition.HasValue)
                return presetPosition.Value;

            // Parse comma-separated values
            var parts = valueText.Split(',');
            if (parts.Length != 3)
            {
                errors = $"Vector3 parameter must have 3 components or be a preset (left, center, right, etc.). Got: {valueText}";
                return Vector3.zero;
            }

            try
            {
                float x = float.Parse(parts[0].Trim(), CultureInfo.InvariantCulture);
                float y = float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture);
                float z = float.Parse(parts[2].Trim(), CultureInfo.InvariantCulture);
                return new Vector3(x, y, z);
            }
            catch (Exception ex)
            {
                errors = $"Failed to parse Vector3 parameter '{valueText}': {ex.Message}";
                return Vector3.zero;
            }
        }
    }

    /// <summary>
    /// Represents a serializable Command parameter with a Color value.
    /// Supports color names, hex codes, and RGB values.
    /// </summary>
    [Serializable]
    public class ColorParameter : CommandParameter<Color>
    {
        public static implicit operator ColorParameter(Color value) => new ColorParameter { Value = value };
        public static implicit operator Color?(ColorParameter param) => param?.Value;

        protected override Color ParseValueText(string valueText, out string errors)
        {
            errors = null;
            
            if (string.IsNullOrEmpty(valueText))
                return Color.white;

            valueText = valueText.Trim().ToLowerInvariant();

            // Handle preset colors
            var presetColor = valueText switch
            {
                "white" => Color.white,
                "black" => Color.black,
                "red" => Color.red,
                "green" => Color.green,
                "blue" => Color.blue,
                "yellow" => Color.yellow,
                "cyan" => Color.cyan,
                "magenta" => Color.magenta,
                "gray" or "grey" => Color.gray,
                "clear" => Color.clear,
                _ => (Color?)null
            };

            if (presetColor.HasValue)
                return presetColor.Value;

            // Handle hex colors
            if (valueText.StartsWith("#"))
            {
                if (ColorUtility.TryParseHtmlString(valueText, out Color hexColor))
                    return hexColor;
                
                errors = $"Invalid hex color format: {valueText}";
                return Color.white;
            }

            // Handle RGB format (r,g,b) or (r,g,b,a)
            if (valueText.Contains(","))
            {
                var parts = valueText.Split(',');
                if (parts.Length < 3 || parts.Length > 4)
                {
                    errors = $"RGB color must have 3 or 4 components. Got: {valueText}";
                    return Color.white;
                }

                try
                {
                    float r = float.Parse(parts[0].Trim(), CultureInfo.InvariantCulture);
                    float g = float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture);
                    float b = float.Parse(parts[2].Trim(), CultureInfo.InvariantCulture);
                    float a = parts.Length > 3 ? float.Parse(parts[3].Trim(), CultureInfo.InvariantCulture) : 1f;
                    
                    return new Color(r, g, b, a);
                }
                catch (Exception ex)
                {
                    errors = $"Failed to parse RGB color '{valueText}': {ex.Message}";
                    return Color.white;
                }
            }

            errors = $"Unrecognized color format: {valueText}. Use color names, hex (#FF0000), or RGB (1,0,0)";
            return Color.white;
        }
    }

    /// <summary>
    /// Represents a serializable Command parameter with a strongly-typed enum value.
    /// </summary>
    [Serializable]
    public class EnumParameter<T> : CommandParameter<T> where T : struct, Enum
    {
        public static implicit operator EnumParameter<T>(T value) => new EnumParameter<T> { Value = value };
        public static implicit operator T?(EnumParameter<T> param) => param?.Value;

        protected override T ParseValueText(string valueText, out string errors)
        {
            errors = null;
            
            if (string.IsNullOrEmpty(valueText))
                return default(T);

            // Try exact match first
            if (Enum.TryParse<T>(valueText, true, out T result))
                return result;

            // Try to find by partial match or alias
            var enumNames = Enum.GetNames(typeof(T));
            var lowerValue = valueText.ToLowerInvariant();
            
            foreach (var name in enumNames)
            {
                if (name.ToLowerInvariant().StartsWith(lowerValue))
                    return (T)Enum.Parse(typeof(T), name, true);
            }

            errors = $"Invalid {typeof(T).Name} value: '{valueText}'. Valid options: {string.Join(", ", enumNames)}";
            return default(T);
        }
    }

    /// <summary>
    /// Represents a serializable Command parameter with actor ID validation.
    /// </summary>
    [Serializable]
    public class ActorIdParameter : CommandParameter<string>
    {
        public static implicit operator ActorIdParameter(string value) => new ActorIdParameter { Value = value };
        public static implicit operator string(ActorIdParameter param) => param?.Value;

        protected override string ParseValueText(string valueText, out string errors)
        {
            errors = null;
            
            if (string.IsNullOrEmpty(valueText))
                return null;

            var trimmed = valueText.Trim();
            
            // Validate actor ID format (basic validation)
            if (trimmed.Length < 1)
            {
                errors = "Actor ID cannot be empty";
                return null;
            }

            // Check for invalid characters
            if (trimmed.Contains(" ") && !trimmed.Contains(",") && trimmed != "all" && trimmed != "*")
            {
                errors = $"Actor ID '{trimmed}' contains spaces. Use quotes if this is intentional, or separate multiple actors with commas";
                return null;
            }

            return trimmed;
        }
    }
}