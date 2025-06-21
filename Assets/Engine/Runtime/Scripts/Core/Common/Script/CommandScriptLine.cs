using Sinkii09.Engine.Commands;
using Sinkii09.Engine.Extensions;

namespace Sinkii09.Engine.Common.Script
{
    public class CommandScriptLine : ScriptLine
    {
        public const string CommandLinePrefix = "@";

        public Command Command { get; private set; }
        public CommandScriptLine(string scriptName, int lineIndex, string lineText) : base(scriptName, lineIndex, lineText)
        {
        }

        protected override void ParseLineText(string lineText, out string errors)
        {
            var cmdText = lineText.GetAfterFirst(CommandLinePrefix);
            Command = CommandParser.FromScriptText(ScriptName, LineIndex, 0, cmdText, out errors);
        }
    }
}