using System;
using System.Text.RegularExpressions;

namespace Sinkii09.Engine.Commands
{
    [Serializable]
    public class DynamicValueData
    {
        public static readonly Regex CaptureRegex = new Regex(@"(?<!\\)\{(.*?)(?<!\\)\}");
        public string ValueText;
        public string[] Expressions;
    }
    
}