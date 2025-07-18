using System;
using System.Collections.Generic;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Game-specific save data containing global game state, settings, and progression
    /// </summary>
    [Serializable]
    public class GameSaveData : SaveData
    {
        // Current Game State
        public string CurrentSceneName { get; set; }
        public int CurrentLevel { get; set; }
        public string GameMode { get; set; }
        public GameDifficulty Difficulty { get; set; }
        public float TotalPlayTime { get; set; }
        
        // Game Settings
        public float MasterVolume { get; set; } = 1.0f;
        public float MusicVolume { get; set; } = 0.8f;
        public float SfxVolume { get; set; } = 1.0f;
        public bool FullScreenMode { get; set; } = true;
        public int GraphicsQuality { get; set; } = 2; // 0=Low, 1=Medium, 2=High
        public string Language { get; set; } = "en";
        
        // Game Progression
        public List<string> UnlockedLevels { get; set; } = new List<string>();
        public List<string> CompletedLevels { get; set; } = new List<string>();
        public Dictionary<string, bool> GameFlags { get; set; } = new Dictionary<string, bool>();
        public Dictionary<string, int> GameCounters { get; set; } = new Dictionary<string, int>();
        
        // Global Game Data
        public int HighScore { get; set; }
        public Dictionary<string, object> CustomData { get; set; } = new Dictionary<string, object>();
        
        public GameSaveData() : base()
        {
            CurrentSceneName = "MainMenu";
            CurrentLevel = 1;
            GameMode = "Story";
            Difficulty = GameDifficulty.Normal;
        }
        
        protected override int GetCurrentVersion()
        {
            return 1; // Game save data version
        }
        
        protected override bool ValidateData()
        {
            // Validate required fields
            if (string.IsNullOrEmpty(CurrentSceneName))
                return false;
                
            if (CurrentLevel < 1)
                return false;
                
            if (string.IsNullOrEmpty(GameMode))
                return false;
                
            // Validate volume ranges
            if (MasterVolume < 0 || MasterVolume > 1)
                return false;
                
            if (MusicVolume < 0 || MusicVolume > 1)
                return false;
                
            if (SfxVolume < 0 || SfxVolume > 1)
                return false;
                
            // Validate collections are not null
            if (UnlockedLevels == null || CompletedLevels == null)
                return false;
                
            if (GameFlags == null || GameCounters == null || CustomData == null)
                return false;
                
            return true;
        }
        
        public override void OnBeforeSave()
        {
            base.OnBeforeSave();
            
            // Update play time if needed
            // This would be updated by a game time tracking system
        }
        
        /// <summary>
        /// Check if a level is unlocked
        /// </summary>
        public bool IsLevelUnlocked(string levelName)
        {
            return UnlockedLevels.Contains(levelName);
        }
        
        /// <summary>
        /// Unlock a level
        /// </summary>
        public void UnlockLevel(string levelName)
        {
            if (!UnlockedLevels.Contains(levelName))
            {
                UnlockedLevels.Add(levelName);
            }
        }
        
        /// <summary>
        /// Mark a level as completed
        /// </summary>
        public void CompleteLevel(string levelName)
        {
            if (!CompletedLevels.Contains(levelName))
            {
                CompletedLevels.Add(levelName);
            }
        }
        
        /// <summary>
        /// Set a game flag
        /// </summary>
        public void SetFlag(string flagName, bool value)
        {
            GameFlags[flagName] = value;
        }
        
        /// <summary>
        /// Get a game flag value
        /// </summary>
        public bool GetFlag(string flagName, bool defaultValue = false)
        {
            return GameFlags.GetValueOrDefault(flagName, defaultValue);
        }
        
        /// <summary>
        /// Set a game counter
        /// </summary>
        public void SetCounter(string counterName, int value)
        {
            GameCounters[counterName] = value;
        }
        
        /// <summary>
        /// Get a game counter value
        /// </summary>
        public int GetCounter(string counterName, int defaultValue = 0)
        {
            return GameCounters.GetValueOrDefault(counterName, defaultValue);
        }
        
        /// <summary>
        /// Increment a game counter
        /// </summary>
        public void IncrementCounter(string counterName, int amount = 1)
        {
            GameCounters[counterName] = GetCounter(counterName) + amount;
        }
    }
    
    /// <summary>
    /// Game difficulty levels
    /// </summary>
    [Serializable]
    public enum GameDifficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
        Expert = 3
    }
}