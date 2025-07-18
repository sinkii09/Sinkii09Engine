using System;

namespace Sinkii09.Engine.Services
{
    [Serializable]
    public abstract class SaveData
    {
        public int Version { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public string SaveFormatVersion { get; set; }
        public string GameVersion { get; set; }
        
        protected SaveData()
        {
            Version = GetCurrentVersion();
            CreatedAt = DateTime.UtcNow;
            ModifiedAt = DateTime.UtcNow;
            SaveFormatVersion = "1.0.0";
            GameVersion = UnityEngine.Application.version;
        }
        
        protected abstract int GetCurrentVersion();
        
        public virtual bool Validate()
        {
            if (Version <= 0) return false;
            if (CreatedAt > DateTime.UtcNow) return false;
            if (ModifiedAt < CreatedAt) return false;
            if (string.IsNullOrEmpty(SaveFormatVersion)) return false;
            
            return ValidateData();
        }
        
        protected abstract bool ValidateData();
        
        public virtual void OnBeforeSave()
        {
            ModifiedAt = DateTime.UtcNow;
        }
        
        public virtual void OnAfterLoad()
        {
        }
    }
}