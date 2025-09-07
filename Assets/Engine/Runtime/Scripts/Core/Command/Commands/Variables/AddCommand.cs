using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Commands;
using Sinkii09.Engine.Services;
using System;
using System.Globalization;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Commands
{
    /// <summary>
    /// High-performance command for mathematical operations on variables
    /// 
    /// Usage examples:
    /// @add var:score amount:100
    /// @add var:health amount:-10 min:0 max:100
    /// @add var:level amount:1
    /// @add var:coins amount:50.5
    /// </summary>
    [Serializable]
    [CommandAlias("add")]
    public class AddCommand : Command
    {
        #region Parameters
        [Header("Target")]
        [ParameterAlias(ParameterAliases.Variable)]
        [RequiredParameter]
        [NotEmpty("Variable name cannot be empty")]
        public StringParameter variable = new StringParameter(); // Target variable name
        [ParameterAlias(ParameterAliases.Amount)]
        [RequiredParameter]
        public DecimalParameter amount = new DecimalParameter(); // Amount to add (can be negative)
        
        [Header("Constraints (Optional)")]
        [ParameterAlias(ParameterAliases.MinValue)]
        [ParameterGroup("Constraints")]
        public DecimalParameter minValue = new DecimalParameter(); // Minimum allowed value
        [ParameterAlias(ParameterAliases.MaxValue)]
        [ParameterGroup("Constraints")]
        public DecimalParameter maxValue = new DecimalParameter(); // Maximum allowed value
        
        [Header("Behavior (Optional)")]
        [ParameterAlias(ParameterAliases.Create)]
        [DefaultValue(true)]
        public BooleanParameter createIfMissing = new BooleanParameter(); // Create variable if it doesn't exist (default: true)
        #endregion

        #region Static Configuration
        private const double EPSILON = 1e-9; // For floating point comparisons
        private const string DEFAULT_OPERATION_NAME = "add";
        #endregion

        public override async UniTask ExecuteAsync(CancellationToken token = default)
        {
            try
            {
                var scriptPlayer = Engine.GetService<IScriptPlayerService>();
                if (scriptPlayer == null)
                {
                    throw new InvalidOperationException("ScriptPlayerService is not available for add command");
                }

                // Validate parameters
                if (!ValidateParameters(out string error))
                {
                    throw new InvalidOperationException(error);
                }

                // Get current variable value
                double currentValue = GetCurrentValue(scriptPlayer);

                // Calculate new value
                double newValue = currentValue + (double)amount.Value;

                // Apply constraints
                newValue = ApplyConstraints(newValue);

                // Convert to optimal type and set variable
                object finalValue = ConvertToOptimalType(newValue);
                scriptPlayer.SetVariable(variable.Value, finalValue);

                // Log result in debug builds
                if (Debug.isDebugBuild)
                {
                    Debug.Log($"[AddCommand] {variable.Value}: {currentValue} + {amount.Value} = {finalValue}");
                }

                await UniTask.Yield();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AddCommand] Failed to execute: {ex.Message}");
                throw;
            }
        }

        #region Helper Methods
        private bool ValidateParameters(out string error)
        {
            error = null;

            if (!variable.HasValue || string.IsNullOrEmpty(variable.Value))
            {
                error = "Add command requires a var parameter";
                return false;
            }

            if (!amount.HasValue)
            {
                error = "Add command requires an amount parameter";
                return false;
            }

            return true;
        }

        private double GetCurrentValue(IScriptPlayerService scriptPlayer)
        {
            // Check if variable exists
            if (!scriptPlayer.HasVariable(variable.Value))
            {
                // Check if we should create missing variables (default: true)
                bool shouldCreate = !createIfMissing.HasValue || createIfMissing.Value;
                if (!shouldCreate)
                {
                    throw new InvalidOperationException($"Variable '{variable.Value}' does not exist and createIfMissing is false");
                }

                return 0.0; // Default value for new variables
            }

            // Get and convert existing value
            var varValue = scriptPlayer.GetVariable(variable.Value);
            if (!TryConvertToDouble(varValue, out double currentValue))
            {
                throw new InvalidOperationException($"Variable '{variable.Value}' cannot be converted to a number. Current value: {varValue} ({varValue?.GetType().Name})");
            }

            return currentValue;
        }

        private double ApplyConstraints(double value)
        {
            if (minValue.HasValue)
            {
                value = Math.Max(value, (double)minValue.Value);
            }

            if (maxValue.HasValue)
            {
                value = Math.Min(value, (double)maxValue.Value);
            }

            return value;
        }

        private object ConvertToOptimalType(double value)
        {
            // Check if it's a whole number within int range
            if (Math.Abs(value - Math.Round(value)) < EPSILON && 
                value >= int.MinValue && 
                value <= int.MaxValue)
            {
                return (int)Math.Round(value);
            }

            return value;
        }

        private bool TryConvertToDouble(object value, out double result)
        {
            result = 0;

            return value switch
            {
                null => true, // null becomes 0
                double d => (result = d) == d, // Always true, assigns result
                float f => (result = f) == f,
                int i => (result = i) == i,
                long l => (result = l) == l,
                bool b => (result = b ? 1 : 0) >= 0,
                string s => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out result),
                _ => double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out result)
            };
        }
        #endregion
    }
}