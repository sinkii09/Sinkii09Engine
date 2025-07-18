using System;
using System.Collections.Generic;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Player-specific save data containing character stats, inventory, and personal progression
    /// </summary>
    [Serializable]
    public class PlayerSaveData : SaveData
    {
        // Player Identity
        public string PlayerName { get; set; }
        public string PlayerId { get; set; }
        
        // Player Stats
        public int Level { get; set; } = 1;
        public long Experience { get; set; }
        public long Currency { get; set; }
        public int Health { get; set; } = 100;
        public int MaxHealth { get; set; } = 100;
        public int Mana { get; set; } = 50;
        public int MaxMana { get; set; } = 50;
        
        // Player Attributes
        public int Strength { get; set; } = 10;
        public int Intelligence { get; set; } = 10;
        public int Dexterity { get; set; } = 10;
        public int Constitution { get; set; } = 10;
        public int Luck { get; set; } = 10;
        
        // Inventory System
        public List<InventoryItem> Inventory { get; set; } = new List<InventoryItem>();
        public List<string> EquippedItems { get; set; } = new List<string>();
        public int InventoryCapacity { get; set; } = 50;
        
        // Skills and Abilities
        public Dictionary<string, int> Skills { get; set; } = new Dictionary<string, int>();
        public List<string> UnlockedAbilities { get; set; } = new List<string>();
        public List<string> ActiveAbilities { get; set; } = new List<string>();
        
        // Progress Tracking
        public Dictionary<string, bool> Achievements { get; set; } = new Dictionary<string, bool>();
        public Dictionary<string, int> QuestProgress { get; set; } = new Dictionary<string, int>();
        public List<string> CompletedQuests { get; set; } = new List<string>();
        public List<string> ActiveQuests { get; set; } = new List<string>();
        
        // Player Preferences
        public Dictionary<string, string> Preferences { get; set; } = new Dictionary<string, string>();
        public List<string> Favorites { get; set; } = new List<string>();
        
        // Statistics
        public int EnemiesDefeated { get; set; }
        public int ItemsCollected { get; set; }
        public int QuestsCompleted { get; set; }
        public float DistanceTraveled { get; set; }
        public int DeathCount { get; set; }
        
        // Custom Player Data
        public Dictionary<string, object> CustomPlayerData { get; set; } = new Dictionary<string, object>();
        
        public PlayerSaveData() : base()
        {
            PlayerName = "Player";
            PlayerId = Guid.NewGuid().ToString();
        }
        
        public PlayerSaveData(string playerName) : this()
        {
            PlayerName = playerName;
        }
        
        protected override int GetCurrentVersion()
        {
            return 1; // Player save data version
        }
        
        protected override bool ValidateData()
        {
            // Validate required fields
            if (string.IsNullOrEmpty(PlayerName))
                return false;
                
            if (string.IsNullOrEmpty(PlayerId))
                return false;
                
            // Validate level is positive
            if (Level < 1)
                return false;
                
            // Validate health/mana values
            if (Health < 0 || MaxHealth <= 0 || Health > MaxHealth)
                return false;
                
            if (Mana < 0 || MaxMana <= 0 || Mana > MaxMana)
                return false;
                
            // Validate attributes are positive
            if (Strength < 0 || Intelligence < 0 || Dexterity < 0 || Constitution < 0 || Luck < 0)
                return false;
                
            // Validate collections are not null
            if (Inventory == null || EquippedItems == null)
                return false;
                
            if (Skills == null || UnlockedAbilities == null || ActiveAbilities == null)
                return false;
                
            if (Achievements == null || QuestProgress == null || CompletedQuests == null || ActiveQuests == null)
                return false;
                
            if (Preferences == null || Favorites == null || CustomPlayerData == null)
                return false;
                
            // Validate inventory capacity
            if (InventoryCapacity <= 0)
                return false;
                
            return true;
        }
        
        /// <summary>
        /// Calculate experience needed for next level
        /// </summary>
        public long GetExperienceToNextLevel()
        {
            // Simple formula: Level * 1000 experience per level
            long nextLevelExp = Level * 1000L;
            return Math.Max(0, nextLevelExp - Experience);
        }
        
        /// <summary>
        /// Add experience and handle level ups
        /// </summary>
        public bool AddExperience(long amount)
        {
            Experience += amount;
            bool leveledUp = false;
            
            // Check for level up
            while (Experience >= Level * 1000L)
            {
                Level++;
                leveledUp = true;
                
                // Increase max health and mana on level up
                MaxHealth += 10;
                MaxMana += 5;
                Health = MaxHealth; // Full heal on level up
                Mana = MaxMana;
            }
            
            return leveledUp;
        }
        
        /// <summary>
        /// Add item to inventory
        /// </summary>
        public bool AddItem(InventoryItem item)
        {
            if (Inventory.Count >= InventoryCapacity)
                return false;
                
            Inventory.Add(item);
            ItemsCollected++;
            return true;
        }
        
        /// <summary>
        /// Remove item from inventory
        /// </summary>
        public bool RemoveItem(string itemId)
        {
            var item = Inventory.Find(i => i.ItemId == itemId);
            if (item != null)
            {
                Inventory.Remove(item);
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Get item from inventory
        /// </summary>
        public InventoryItem GetItem(string itemId)
        {
            return Inventory.Find(i => i.ItemId == itemId);
        }
        
        /// <summary>
        /// Check if achievement is unlocked
        /// </summary>
        public bool HasAchievement(string achievementId)
        {
            return Achievements.GetValueOrDefault(achievementId, false);
        }
        
        /// <summary>
        /// Unlock achievement
        /// </summary>
        public void UnlockAchievement(string achievementId)
        {
            Achievements[achievementId] = true;
        }
        
        /// <summary>
        /// Get skill level
        /// </summary>
        public int GetSkillLevel(string skillName)
        {
            return Skills.GetValueOrDefault(skillName, 0);
        }
        
        /// <summary>
        /// Set skill level
        /// </summary>
        public void SetSkillLevel(string skillName, int level)
        {
            Skills[skillName] = Math.Max(0, level);
        }
        
        /// <summary>
        /// Increase skill level
        /// </summary>
        public void IncreaseSkill(string skillName, int amount = 1)
        {
            Skills[skillName] = GetSkillLevel(skillName) + amount;
        }
        
        /// <summary>
        /// Get quest progress
        /// </summary>
        public int GetQuestProgress(string questId)
        {
            return QuestProgress.GetValueOrDefault(questId, 0);
        }
        
        /// <summary>
        /// Set quest progress
        /// </summary>
        public void SetQuestProgress(string questId, int progress)
        {
            QuestProgress[questId] = Math.Max(0, progress);
        }
        
        /// <summary>
        /// Complete a quest
        /// </summary>
        public void CompleteQuest(string questId)
        {
            if (!CompletedQuests.Contains(questId))
            {
                CompletedQuests.Add(questId);
                QuestsCompleted++;
            }
            
            // Remove from active quests
            ActiveQuests.Remove(questId);
        }
        
        /// <summary>
        /// Start a quest
        /// </summary>
        public void StartQuest(string questId)
        {
            if (!ActiveQuests.Contains(questId) && !CompletedQuests.Contains(questId))
            {
                ActiveQuests.Add(questId);
            }
        }
    }
    
    /// <summary>
    /// Represents an item in player inventory
    /// </summary>
    [Serializable]
    public class InventoryItem
    {
        public string ItemId { get; set; }
        public string ItemName { get; set; }
        public string ItemType { get; set; }
        public int Quantity { get; set; } = 1;
        public int Durability { get; set; } = 100;
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
        
        public InventoryItem() { }
        
        public InventoryItem(string itemId, string itemName, string itemType, int quantity = 1)
        {
            ItemId = itemId;
            ItemName = itemName;
            ItemType = itemType;
            Quantity = quantity;
        }
    }
}