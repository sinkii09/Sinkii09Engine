using System;

namespace Sinkii09.Engine.Services
{
    [Serializable]
    public class SaveMetadata
    {
        public string SaveId { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public long FileSize { get; set; }
        public long UncompressedSize { get; set; }
        public string SaveType { get; set; }
        public int SaveVersion { get; set; }
        public string GameVersion { get; set; }
        public string ThumbnailData { get; set; }
        public bool IsAutoSave { get; set; }
        public bool IsBackup { get; set; }
        public bool IsCompressed { get; set; }
        public string OriginalSaveId { get; set; }
        public int PlayTime { get; set; }
        public string Platform { get; set; }
        
        public SaveMetadata()
        {
            Platform = UnityEngine.Application.platform.ToString();
        }
        
        public SaveMetadata(string saveId) : this()
        {
            SaveId = saveId;
            CreatedAt = DateTime.UtcNow;
            ModifiedAt = DateTime.UtcNow;
        }
        
        public double CompressionRatio => UncompressedSize > 0 ? (double)FileSize / UncompressedSize : 1.0;
        
        public string GetFormattedPlayTime()
        {
            var hours = PlayTime / 3600;
            var minutes = PlayTime % 3600 / 60;
            var seconds = PlayTime % 60;
            
            if (hours > 0)
                return $"{hours}h {minutes}m";
            else if (minutes > 0)
                return $"{minutes}m {seconds}s";
            else
                return $"{seconds}s";
        }
    }
}