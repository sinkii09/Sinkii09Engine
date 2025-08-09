using System;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Represents a cached path resolution result with metadata for LRU eviction
    /// </summary>
    internal class CachedPathEntry
    {
        public string ResolvedPath { get; set; }
        public PathPriority Priority { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastAccessedAt { get; set; }
        public int AccessCount { get; set; }
        public TimeSpan ResolutionTime { get; set; }
        public bool IsValid { get; set; }
        
        // LRU tracking
        public CachedPathEntry Previous { get; set; }
        public CachedPathEntry Next { get; set; }
        public string CacheKey { get; set; }
        
        public CachedPathEntry(string resolvedPath, PathPriority priority, TimeSpan resolutionTime, string cacheKey)
        {
            ResolvedPath = resolvedPath;
            Priority = priority;
            ResolutionTime = resolutionTime;
            CacheKey = cacheKey;
            CreatedAt = DateTime.UtcNow;
            LastAccessedAt = DateTime.UtcNow;
            AccessCount = 1;
            IsValid = true;
        }
        
        /// <summary>
        /// Updates access tracking for LRU management
        /// </summary>
        public void UpdateAccess()
        {
            LastAccessedAt = DateTime.UtcNow;
            AccessCount++;
        }
        
        /// <summary>
        /// Checks if the cache entry is expired based on configuration
        /// </summary>
        public bool IsExpired(float cacheEntryLifetimeSeconds)
        {
            return (DateTime.UtcNow - CreatedAt).TotalSeconds > cacheEntryLifetimeSeconds;
        }
        
        /// <summary>
        /// Gets the age of this cache entry in seconds
        /// </summary>
        public double AgeInSeconds => (DateTime.UtcNow - CreatedAt).TotalSeconds;
        
        /// <summary>
        /// Gets the time since last access in seconds
        /// </summary>
        public double SecondsSinceLastAccess => (DateTime.UtcNow - LastAccessedAt).TotalSeconds;
        
        public override string ToString()
        {
            return $"CachedPath[{CacheKey}] -> {ResolvedPath} (Access: {AccessCount}, Age: {AgeInSeconds:F1}s)";
        }
    }
}