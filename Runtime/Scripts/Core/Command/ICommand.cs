using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Commands
{
    public interface ICommand
    {
        UniTask ExecuteAsync(CancellationToken token = default);
    }

    [Serializable]
    public abstract class Command : ICommand, ISerializationCallbackReceiver
    {
        public const string CommandLinePrefix = "@";
        public const string CommandNamelessAlias = "";
        public abstract UniTask ExecuteAsync(CancellationToken token = default);

        public virtual void OnAfterDeserialize()
        {
        }

        public void OnBeforeSerialize()
        {
        }

        
    }
}