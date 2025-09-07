using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Commands
{
    [Serializable]
    [CommandAlias("SampleMultiParam")]
    public class SampleMultiParamCommand : Command
    {
        // String parameter
        public StringParameter message = new StringParameter();

        // Integer parameter
        public IntegerParameter count = new IntegerParameter();

        // Float parameter
        public DecimalParameter speed = new DecimalParameter();

        // Boolean parameter
        public BooleanParameter isActive = new BooleanParameter();

        // List of strings parameter
        public StringListParameter tags = new StringListParameter();

        // Named string parameter
        public NamedStringParameter namedValue = new NamedStringParameter();

        // Example: You can add more parameters as needed
        public override UniTask ExecuteAsync(CancellationToken token = default)
        {
            Debug.Log($"Message: {message}, Count: {count}, Speed: {speed}, IsActive: {isActive}, Tags: {tags}, NamedValue: {namedValue}");
            return UniTask.CompletedTask;
        }
    }
}