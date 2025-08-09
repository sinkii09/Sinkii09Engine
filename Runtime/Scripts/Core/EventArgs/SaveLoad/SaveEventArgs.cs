using System;

namespace Sinkii09.Engine.Events
{
    public class SaveEventArgs : EventArgs
    {
        public string SaveId { get; }
        public DateTime Timestamp { get; }
        public long DataSize { get; }
        
        public SaveEventArgs(string saveId, long dataSize = 0)
        {
            SaveId = saveId;
            Timestamp = DateTime.UtcNow;
            DataSize = dataSize;
        }
    }
}