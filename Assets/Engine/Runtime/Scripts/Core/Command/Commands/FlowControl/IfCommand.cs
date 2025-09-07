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
    /// Command for conditional execution based on variable values
    /// 
    /// Usage examples:
    /// @if left:health op:> val:0 goto:alive
    /// @if left:playerName op:== val:"Hero" goto:heroPath  
    /// @if left:level op:>= val:5 line:100
    /// @if left:score op:< right:maxScore goto:continue
    /// </summary>
    [Serializable]
    [CommandAlias("if")]
    public class IfCommand : Command, IFlowControlCommand
    {
        #region Parameters
        [Header("Left Side")]
        [ParameterAlias(ParameterAliases.LeftVariable)]
        [RequiredParameter]
        public StringParameter leftVar = new StringParameter(); // Variable name or literal value
        
        [Header("Comparison")]
        [ParameterAlias(ParameterAliases.Operator)]
        [RequiredParameter]
        public StringParameter operatorType = new StringParameter(); // ==, !=, >, <, >=, <=, contains
        
        [Header("Right Side")]
        [ParameterAlias(ParameterAliases.RightVariable)]
        [MutuallyExclusive("RightSide", false)]
        public StringParameter rightVar = new StringParameter(); // Variable name (optional)
        [ParameterAlias(ParameterAliases.Value)]
        [MutuallyExclusive("RightSide", false)]
        public StringParameter rightValue = new StringParameter(); // Literal value (optional)
        
        [Header("True Branch Actions")]
        [ParameterAlias(ParameterAliases.GotoLabel)]
        [MutuallyExclusive("TrueBranch", false)]
        public StringParameter gotoLabel = new StringParameter(); // Jump to this label if true
        [ParameterAlias(ParameterAliases.GotoLine)]
        [MutuallyExclusive("TrueBranch", false)]
        [Range(1, int.MaxValue)]
        public IntegerParameter gotoLine = new IntegerParameter(); // Jump to this line if true
        
        [Header("False Branch Actions (Optional)")]
        [ParameterAlias(ParameterAliases.ElseLabel)]
        [MutuallyExclusive("FalseBranch", false)]
        public StringParameter elseLabel = new StringParameter(); // Jump to this label if false
        [ParameterAlias(ParameterAliases.ElseLine)]
        [MutuallyExclusive("FalseBranch", false)]
        [Range(1, int.MaxValue)]
        public IntegerParameter elseLine = new IntegerParameter(); // Jump to this line if false
        #endregion

        #region Static Configuration
        private static readonly Dictionary<string, ComparisonOperator> s_operatorMap = new Dictionary<string, ComparisonOperator>(StringComparer.OrdinalIgnoreCase)
        {
            ["=="] = ComparisonOperator.Equal,
            ["eq"] = ComparisonOperator.Equal,
            ["equal"] = ComparisonOperator.Equal,
            ["!="] = ComparisonOperator.NotEqual,
            ["ne"] = ComparisonOperator.NotEqual,
            ["neq"] = ComparisonOperator.NotEqual,
            ["notequal"] = ComparisonOperator.NotEqual,
            [">"] = ComparisonOperator.Greater,
            ["gt"] = ComparisonOperator.Greater,
            ["greater"] = ComparisonOperator.Greater,
            ["<"] = ComparisonOperator.Less,
            ["lt"] = ComparisonOperator.Less,
            ["less"] = ComparisonOperator.Less,
            [">="] = ComparisonOperator.GreaterEqual,
            ["ge"] = ComparisonOperator.GreaterEqual,
            ["gte"] = ComparisonOperator.GreaterEqual,
            ["greaterequal"] = ComparisonOperator.GreaterEqual,
            ["<="] = ComparisonOperator.LessEqual,
            ["le"] = ComparisonOperator.LessEqual,
            ["lte"] = ComparisonOperator.LessEqual,
            ["lessequal"] = ComparisonOperator.LessEqual,
            ["contains"] = ComparisonOperator.Contains,
            ["has"] = ComparisonOperator.Contains
        };

        private static readonly Dictionary<string, object> s_literalCache = new Dictionary<string, object>();
        private const double FLOAT_TOLERANCE = 1e-9;
        #endregion

        #region Enums
        private enum ComparisonOperator
        {
            Equal,
            NotEqual,
            Greater,
            Less,
            GreaterEqual,
            LessEqual,
            Contains
        }
        #endregion

        public override async UniTask ExecuteAsync(CancellationToken token = default)
        {
            var result = await ExecuteWithResultAsync(token);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(result.ErrorMessage, result.Exception);
            }
        }

        public async UniTask<CommandResult> ExecuteWithResultAsync(CancellationToken token = default)
        {
            try
            {
                var scriptPlayer = Engine.GetService<IScriptPlayerService>();
                if (scriptPlayer == null)
                {
                    return CommandResult.Failed("ScriptPlayerService is not available for if command");
                }

                // Validate required parameters
                if (!ValidateParameters(out string validationError))
                {
                    return CommandResult.Failed(validationError);
                }

                // Get comparison values
                var leftValue = GetValue(leftVar.Value, scriptPlayer);
                var rightValue = GetRightValue(scriptPlayer);

                // Get operator
                if (!s_operatorMap.TryGetValue(operatorType.Value, out var comparisonOp))
                {
                    return CommandResult.Failed($"Unknown comparison operator: {operatorType.Value}");
                }

                // Evaluate condition
                bool conditionResult = EvaluateComparison(leftValue, comparisonOp, rightValue);

                await UniTask.Yield(); // Ensure method is async

                // Execute appropriate branch
                return ExecuteBranch(conditionResult);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[IfCommand] Failed to execute: {ex.Message}");
                return CommandResult.Failed($"If command execution failed: {ex.Message}", ex);
            }
        }

        #region Helper Methods
        private bool ValidateParameters(out string error)
        {
            error = null;

            if (!leftVar.HasValue || string.IsNullOrEmpty(leftVar.Value))
            {
                error = "If command requires a left parameter";
                return false;
            }

            if (!operatorType.HasValue || string.IsNullOrEmpty(operatorType.Value))
            {
                error = "If command requires an op parameter";
                return false;
            }

            if ((!rightVar.HasValue || string.IsNullOrEmpty(rightVar.Value)) &&
                (!rightValue.HasValue || string.IsNullOrEmpty(rightValue.Value)))
            {
                error = "If command requires either right or val parameter";
                return false;
            }

            return true;
        }

        private object GetValue(string identifier, IScriptPlayerService scriptPlayer)
        {
            // Try to get as variable first
            if (scriptPlayer.HasVariable(identifier))
            {
                return scriptPlayer.GetVariable(identifier);
            }

            // Parse as literal value
            return ParseLiteralValue(identifier);
        }

        private object GetRightValue(IScriptPlayerService scriptPlayer)
        {
            // Prefer variable over literal value
            if (rightVar.HasValue && !string.IsNullOrEmpty(rightVar.Value))
            {
                return GetValue(rightVar.Value, scriptPlayer);
            }

            return ParseLiteralValue(rightValue.Value);
        }

        private object ParseLiteralValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            // Use cache for performance
            if (s_literalCache.TryGetValue(value, out var cached))
                return cached;

            var parsed = ParseLiteralValueInternal(value);
            
            // Cache commonly used values
            if (s_literalCache.Count < 100) // Prevent unbounded growth
            {
                s_literalCache[value] = parsed;
            }

            return parsed;
        }

        private object ParseLiteralValueInternal(string value)
        {
            value = value.Trim();

            // Handle quoted strings
            if (value.Length >= 2 && 
                ((value[0] == '"' && value[value.Length - 1] == '"') ||
                 (value[0] == '\'' && value[value.Length - 1] == '\'')))
            {
                return value.Substring(1, value.Length - 2);
            }

            // Handle special values
            if (value.Equals("null", StringComparison.OrdinalIgnoreCase))
                return null;

            if (value.Equals("true", StringComparison.OrdinalIgnoreCase))
                return true;
            
            if (value.Equals("false", StringComparison.OrdinalIgnoreCase))
                return false;

            // Try numeric parsing
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
                return intValue;

            if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double doubleValue))
                return doubleValue;

            // Default to string
            return value;
        }

        private bool EvaluateComparison(object left, ComparisonOperator op, object right)
        {
            // Handle null comparisons efficiently
            if (left == null || right == null)
            {
                return op switch
                {
                    ComparisonOperator.Equal => left == right,
                    ComparisonOperator.NotEqual => left != right,
                    _ => false // Numeric comparisons with null are false
                };
            }

            // Try numeric comparison for performance
            if (TryGetNumericValues(left, right, out double leftNum, out double rightNum))
            {
                return op switch
                {
                    ComparisonOperator.Equal => Math.Abs(leftNum - rightNum) < FLOAT_TOLERANCE,
                    ComparisonOperator.NotEqual => Math.Abs(leftNum - rightNum) >= FLOAT_TOLERANCE,
                    ComparisonOperator.Greater => leftNum > rightNum,
                    ComparisonOperator.Less => leftNum < rightNum,
                    ComparisonOperator.GreaterEqual => leftNum >= rightNum,
                    ComparisonOperator.LessEqual => leftNum <= rightNum,
                    ComparisonOperator.Contains => false, // Numeric values don't support contains
                    _ => false
                };
            }

            // String comparison
            var leftStr = left.ToString();
            var rightStr = right.ToString();

            return op switch
            {
                ComparisonOperator.Equal => string.Equals(leftStr, rightStr, StringComparison.OrdinalIgnoreCase),
                ComparisonOperator.NotEqual => !string.Equals(leftStr, rightStr, StringComparison.OrdinalIgnoreCase),
                ComparisonOperator.Greater => string.Compare(leftStr, rightStr, StringComparison.OrdinalIgnoreCase) > 0,
                ComparisonOperator.Less => string.Compare(leftStr, rightStr, StringComparison.OrdinalIgnoreCase) < 0,
                ComparisonOperator.GreaterEqual => string.Compare(leftStr, rightStr, StringComparison.OrdinalIgnoreCase) >= 0,
                ComparisonOperator.LessEqual => string.Compare(leftStr, rightStr, StringComparison.OrdinalIgnoreCase) <= 0,
                ComparisonOperator.Contains => leftStr.Contains(rightStr, StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }

        private bool TryGetNumericValues(object left, object right, out double leftNum, out double rightNum)
        {
            leftNum = rightNum = 0;

            if (!TryConvertToDouble(left, out leftNum) || !TryConvertToDouble(right, out rightNum))
                return false;

            return true;
        }

        private bool TryConvertToDouble(object value, out double result)
        {
            result = 0;

            return value switch
            {
                null => false,
                double d => (result = d) == d, // Always true, but assigns result
                float f => (result = f) == f,
                int i => (result = i) == i,
                long l => (result = l) == l,
                bool b => (result = b ? 1 : 0) >= 0,
                _ => double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out result)
            };
        }

        private CommandResult ExecuteBranch(bool conditionResult)
        {
            if (conditionResult)
            {
                // True branch
                if (gotoLabel.HasValue && !string.IsNullOrEmpty(gotoLabel.Value))
                {
                    return CommandResult.JumpToLabel(gotoLabel.Value);
                }
                if (gotoLine.HasValue)
                {
                    return CommandResult.JumpToLine(gotoLine.Value - 1);
                }
            }
            else
            {
                // False branch
                if (elseLabel.HasValue && !string.IsNullOrEmpty(elseLabel.Value))
                {
                    return CommandResult.JumpToLabel(elseLabel.Value);
                }
                if (elseLine.HasValue)
                {
                    return CommandResult.JumpToLine(elseLine.Value - 1);
                }
            }

            return CommandResult.Success();
        }
        #endregion
    }
}