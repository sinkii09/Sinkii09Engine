using System;

namespace Sinkii09.Engine.Events
{
    public class LoadEventArgs : EventArgs
    {
        public string SaveId { get; }
        public DateTime Timestamp { get; }

        public LoadEventArgs(string saveId)
        {
            SaveId = saveId;
            Timestamp = DateTime.UtcNow;
        }
    }
}