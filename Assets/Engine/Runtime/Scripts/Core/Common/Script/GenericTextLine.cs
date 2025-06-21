namespace Sinkii09.Engine.Common.Script
{
    public class GenericTextLine : ScriptLine
    {
        public GenericTextLine(string scriptName, int lineIndex, string lineText) : base(scriptName, lineIndex, lineText)
        {
        }
        protected override void ParseLineText(string lineText, out string errors)
        {
            errors = string.Empty;
        }
    }
}