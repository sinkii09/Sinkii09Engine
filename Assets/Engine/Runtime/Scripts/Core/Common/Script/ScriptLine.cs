using Sirenix.Serialization;
using UnityEngine;

namespace Sinkii09.Engine.Common.Script
{
    [System.Serializable]
    public abstract class ScriptLine
    {
        [field: SerializeField]
        public string ScriptName { get; private set; } = default;

        [field: SerializeField]
        public int LineIndex { get; private set; } = -1;

        [field: SerializeField]
        public string LineHash { get; private set; } = default;

        [field: SerializeField]
        public string OriginalLineText { get; private set; } = default;

        [OdinSerialize]
        public int LineNumber => LineIndex + 1;

        public ScriptLine(string scriptName, int lineIndex, string lineText)
        {
            ScriptName = scriptName;
            LineIndex = lineIndex;
            OriginalLineText = lineText ?? string.Empty;
            LineHash = lineText.GetHashCode().ToString("X8"); // TODO: Use a more robust hash function if needed

            ParseLineText(lineText, out string errors);

            if (!string.IsNullOrEmpty(errors))
            {
                Debug.LogError($"Error parsing line {LineNumber} in script '{ScriptName}': {errors}");
            }
        }

        protected abstract void ParseLineText(string lineText, out string errors);
    }
}