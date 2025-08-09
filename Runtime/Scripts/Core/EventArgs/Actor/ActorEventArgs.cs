using Sinkii09.Engine.Services;
using System;

namespace Sinkii09.Engine.Events
{
    /// <summary>
    /// Actor lifecycle event arguments
    /// </summary>
    public class ActorEventArgs : EventArgs
    {
        public IActor Actor { get; }
        public string Message { get; }
        public object Data { get; }

        public ActorEventArgs(IActor actor, string message = null, object data = null)
        {
            Actor = actor;
            Message = message;
            Data = data;
        }
    }
}