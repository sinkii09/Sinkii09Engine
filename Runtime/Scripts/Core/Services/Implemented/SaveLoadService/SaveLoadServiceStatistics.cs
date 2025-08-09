using System;

namespace Sinkii09.Engine.Services
{
    public class SaveLoadServiceStatistics
    {
        public int TotalSaves { get; set; }
        public int SuccessfulSaves { get; set; }
        public int FailedSaves { get; set; }
        public int TotalLoads { get; set; }
        public int SuccessfulLoads { get; set; }
        public int FailedLoads { get; set; }
        public int TotalDeletes { get; set; }
        public int TotalBackups { get; set; }
        public int TotalRestores { get; set; }
        
        public TimeSpan TotalSaveTime { get; set; }
        public TimeSpan TotalLoadTime { get; set; }
        public TimeSpan AverageSaveTime => TotalSaves > 0 ? TimeSpan.FromMilliseconds(TotalSaveTime.TotalMilliseconds / TotalSaves) : TimeSpan.Zero;
        public TimeSpan AverageLoadTime => TotalLoads > 0 ? TimeSpan.FromMilliseconds(TotalLoadTime.TotalMilliseconds / TotalLoads) : TimeSpan.Zero;
        
        public long TotalBytesWritten { get; set; }
        public long TotalBytesRead { get; set; }
        public long TotalBytesCompressed { get; set; }
        public double AverageCompressionRatio { get; set; }
        
        public DateTime LastSaveTime { get; set; }
        public DateTime LastLoadTime { get; set; }
        public DateTime ServiceStartTime { get; set; }
        public TimeSpan Uptime => DateTime.UtcNow - ServiceStartTime;
        
        public double SaveSuccessRate => TotalSaves > 0 ? (double)SuccessfulSaves / TotalSaves * 100 : 0;
        public double LoadSuccessRate => TotalLoads > 0 ? (double)SuccessfulLoads / TotalLoads * 100 : 0;
        
        public void Reset()
        {
            TotalSaves = 0;
            SuccessfulSaves = 0;
            FailedSaves = 0;
            TotalLoads = 0;
            SuccessfulLoads = 0;
            FailedLoads = 0;
            TotalDeletes = 0;
            TotalBackups = 0;
            TotalRestores = 0;
            TotalSaveTime = TimeSpan.Zero;
            TotalLoadTime = TimeSpan.Zero;
            TotalBytesWritten = 0;
            TotalBytesRead = 0;
            TotalBytesCompressed = 0;
            AverageCompressionRatio = 0;
            LastSaveTime = DateTime.MinValue;
            LastLoadTime = DateTime.MinValue;
            ServiceStartTime = DateTime.UtcNow;
        }
    }
}