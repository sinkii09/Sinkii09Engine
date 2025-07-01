using UnityEngine;

namespace Sinkii09.Engine.Services
{
    public enum ServiceState
    {
        Uninitialized,
        Initializing,
        Running,
        ShuttingDown,
        Shutdown,
        Error
    }
}
