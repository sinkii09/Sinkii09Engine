using Sinkii09.Engine.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Sinkii09.Engine.Common.Script
{
    [Serializable]
    public class Script : ScriptableObject
    {
        public string Name => name;
        public ReadOnlyCollection<ScriptLine> Lines => lines.AsReadOnly();

        [SerializeReference]
        private List<ScriptLine> lines = default;

        public static Script FromScripText(string scriptName, string scriptText)
        {
            Script script = CreateInstance<Script>();
            script.name = scriptName;
            script.lines = new List<ScriptLine>();

            string[] scriptLines = SplitScriptText(scriptText);
            
            for (int i = 0; i < scriptLines.Length; i++)
            {
                string lineText = scriptLines[i].Trim();
                var lineType = ResolveLineType(lineText);
                var line = Activator.CreateInstance(lineType, script.name, i, lineText) as ScriptLine;

                script.lines.Add(line);
            }
            return script;
        }

        private static Type ResolveLineType(string lineText)
        {
            if (string.IsNullOrWhiteSpace(lineText))
                return typeof(GenericTextLine); //TODO: Comment line instead
            else if (lineText.StartsWithFast(CommandScriptLine.CommandLinePrefix)) 
                return typeof(CommandScriptLine);
            else 
                return typeof(GenericTextLine);
        }

        public static string[] SplitScriptText(string scriptText)
        {
            // uFEFF - BOM, u200B - zero-width space.
            return scriptText?.Trim(new char[] { '\uFEFF', '\u200B' })?.SplitByNewLine() ?? new[] { string.Empty };
        }
    }
}