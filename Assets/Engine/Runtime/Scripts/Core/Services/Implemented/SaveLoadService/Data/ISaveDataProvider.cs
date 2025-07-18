using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Threading;
using System.Linq;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Interface for custom save data providers - enables extensible data architecture
    /// </summary>
    public interface ISaveDataProvider
    {
        /// <summary>
        /// Unique identifier for this provider
        /// </summary>
        string ProviderId { get; }
        
        /// <summary>
        /// Display name for this provider
        /// </summary>
        string DisplayName { get; }
        
        /// <summary>
        /// Types of save data this provider can handle
        /// </summary>
        IReadOnlyList<Type> SupportedTypes { get; }
        
        /// <summary>
        /// Provider priority (higher values processed first)
        /// </summary>
        int Priority { get; }
        
        /// <summary>
        /// Check if this provider can handle the given save data type
        /// </summary>
        bool CanHandle(Type saveDataType);
        
        /// <summary>
        /// Check if this provider can handle the given save data instance
        /// </summary>
        bool CanHandle(SaveData saveData);
        
        /// <summary>
        /// Create default save data instance for supported types
        /// </summary>
        SaveData CreateDefaultSaveData(Type saveDataType);
        
        /// <summary>
        /// Validate save data before serialization
        /// </summary>
        UniTask<ValidationResult> ValidateAsync(SaveData saveData, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Pre-process save data before serialization (e.g., compression, encryption)
        /// </summary>
        UniTask<SaveData> PreProcessAsync(SaveData saveData, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Post-process save data after deserialization (e.g., decompression, decryption)
        /// </summary>
        UniTask<SaveData> PostProcessAsync(SaveData saveData, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Get custom metadata for the save data
        /// </summary>
        UniTask<Dictionary<string, object>> GetCustomMetadataAsync(SaveData saveData, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Handle save data migration between versions
        /// </summary>
        UniTask<SaveData> MigrateAsync(SaveData saveData, int fromVersion, int toVersion, CancellationToken cancellationToken = default);
    }
    
    /// <summary>
    /// Base implementation of ISaveDataProvider with common functionality
    /// </summary>
    public abstract class SaveDataProviderBase : ISaveDataProvider
    {
        public abstract string ProviderId { get; }
        public abstract string DisplayName { get; }
        public abstract IReadOnlyList<Type> SupportedTypes { get; }
        public virtual int Priority => 0;
        
        public virtual bool CanHandle(Type saveDataType)
        {
            return SupportedTypes.Contains(saveDataType) || 
                   SupportedTypes.Any(t => t.IsAssignableFrom(saveDataType));
        }
        
        public virtual bool CanHandle(SaveData saveData)
        {
            return saveData != null && CanHandle(saveData.GetType());
        }
        
        public virtual SaveData CreateDefaultSaveData(Type saveDataType)
        {
            if (!CanHandle(saveDataType))
                throw new ArgumentException($"Save data type {saveDataType.Name} is not supported by provider {ProviderId}");
                
            return Activator.CreateInstance(saveDataType) as SaveData;
        }
        
        public virtual async UniTask<ValidationResult> ValidateAsync(SaveData saveData, CancellationToken cancellationToken = default)
        {
            await UniTask.Yield();
            
            var result = new ValidationResult();
            
            if (saveData == null)
            {
                result.AddError("Save data is null");
                return result;
            }
            
            if (!CanHandle(saveData))
            {
                result.AddError($"Save data type {saveData.GetType().Name} is not supported by provider {ProviderId}");
                return result;
            }
            
            // Use SaveData's built-in validation
            if (!saveData.Validate())
            {
                result.AddError("Save data failed built-in validation");
            }
            
            return result;
        }
        
        public virtual async UniTask<SaveData> PreProcessAsync(SaveData saveData, CancellationToken cancellationToken = default)
        {
            await UniTask.Yield();
            
            // Default implementation: call OnBeforeSave hook
            saveData?.OnBeforeSave();
            return saveData;
        }
        
        public virtual async UniTask<SaveData> PostProcessAsync(SaveData saveData, CancellationToken cancellationToken = default)
        {
            await UniTask.Yield();
            
            // Default implementation: call OnAfterLoad hook
            saveData?.OnAfterLoad();
            return saveData;
        }
        
        public virtual async UniTask<Dictionary<string, object>> GetCustomMetadataAsync(SaveData saveData, CancellationToken cancellationToken = default)
        {
            await UniTask.Yield();
            return new Dictionary<string, object>();
        }
        
        public virtual async UniTask<SaveData> MigrateAsync(SaveData saveData, int fromVersion, int toVersion, CancellationToken cancellationToken = default)
        {
            await UniTask.Yield();
            
            // Default implementation: no migration
            if (fromVersion != toVersion)
            {
                throw new NotSupportedException($"Provider {ProviderId} does not support migration from version {fromVersion} to {toVersion}");
            }
            
            return saveData;
        }
    }
    
    /// <summary>
    /// Provider for GameSaveData
    /// </summary>
    public class GameSaveDataProvider : SaveDataProviderBase
    {
        public override string ProviderId => "game_save_data";
        public override string DisplayName => "Game Save Data Provider";
        public override IReadOnlyList<Type> SupportedTypes => new[] { typeof(GameSaveData) };
        public override int Priority => 100;
        
        public override async UniTask<ValidationResult> ValidateAsync(SaveData saveData, CancellationToken cancellationToken = default)
        {
            var result = await base.ValidateAsync(saveData, cancellationToken);
            
            if (saveData is GameSaveData gameData)
            {
                // Additional GameSaveData validation
                if (string.IsNullOrEmpty(gameData.CurrentSceneName))
                    result.AddError("CurrentSceneName is required");
                    
                if (gameData.CurrentLevel < 1)
                    result.AddError("CurrentLevel must be positive");
                    
                if (gameData.TotalPlayTime < 0)
                    result.AddError("TotalPlayTime cannot be negative");
            }
            
            return result;
        }
        
        public override async UniTask<Dictionary<string, object>> GetCustomMetadataAsync(SaveData saveData, CancellationToken cancellationToken = default)
        {
            var metadata = await base.GetCustomMetadataAsync(saveData, cancellationToken);
            
            if (saveData is GameSaveData gameData)
            {
                metadata["current_level"] = gameData.CurrentLevel;
                metadata["game_mode"] = gameData.GameMode;
                metadata["difficulty"] = gameData.Difficulty.ToString();
                metadata["play_time"] = gameData.TotalPlayTime;
                metadata["unlocked_levels_count"] = gameData.UnlockedLevels.Count;
                metadata["completed_levels_count"] = gameData.CompletedLevels.Count;
            }
            
            return metadata;
        }
    }
    
    /// <summary>
    /// Provider for PlayerSaveData
    /// </summary>
    public class PlayerSaveDataProvider : SaveDataProviderBase
    {
        public override string ProviderId => "player_save_data";
        public override string DisplayName => "Player Save Data Provider";
        public override IReadOnlyList<Type> SupportedTypes => new[] { typeof(PlayerSaveData) };
        public override int Priority => 100;
        
        public override async UniTask<ValidationResult> ValidateAsync(SaveData saveData, CancellationToken cancellationToken = default)
        {
            var result = await base.ValidateAsync(saveData, cancellationToken);

            if (saveData is PlayerSaveData playerData)
            {
                // Additional PlayerSaveData validation
                if (string.IsNullOrEmpty(playerData.PlayerName))
                    result.AddError("PlayerName is required");

                if (string.IsNullOrEmpty(playerData.PlayerId))
                    result.AddError("PlayerId is required");

                if (playerData.Level < 1)
                    result.AddError("Level must be positive");

                if (playerData.Experience < 0)
                    result.AddError("Experience cannot be negative");

                if (playerData.Inventory.Count > playerData.InventoryCapacity)
                    result.AddError("Inventory exceeds capacity");
            }

            return result;
        }
        
        public override async UniTask<Dictionary<string, object>> GetCustomMetadataAsync(SaveData saveData, CancellationToken cancellationToken = default)
        {
            var metadata = await base.GetCustomMetadataAsync(saveData, cancellationToken);

            if (saveData is PlayerSaveData playerData)
            {
                metadata["player_name"] = playerData.PlayerName;
                metadata["player_level"] = playerData.Level;
                metadata["experience"] = playerData.Experience;
                metadata["currency"] = playerData.Currency;
                metadata["inventory_count"] = playerData.Inventory.Count;
                metadata["achievements_count"] = playerData.Achievements.Count(a => a.Value);
                metadata["quests_completed"] = playerData.QuestsCompleted;
            }

            return metadata;
        }
    }
    
    /// <summary>
    /// Manager for save data providers - enables plugin system
    /// </summary>
    public class SaveDataProviderManager
    {
        private readonly List<ISaveDataProvider> _providers;
        private readonly Dictionary<Type, ISaveDataProvider> _providerCache;
        
        public SaveDataProviderManager()
        {
            _providers = new List<ISaveDataProvider>();
            _providerCache = new Dictionary<Type, ISaveDataProvider>();
            
            // Register default providers
            RegisterProvider(new GameSaveDataProvider());
            RegisterProvider(new PlayerSaveDataProvider());
        }
        
        /// <summary>
        /// Register a custom save data provider
        /// </summary>
        public void RegisterProvider(ISaveDataProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));
                
            // Check for duplicate provider IDs
            if (_providers.Any(p => p.ProviderId == provider.ProviderId))
                throw new ArgumentException($"Provider with ID '{provider.ProviderId}' is already registered");
                
            _providers.Add(provider);
            
            // Sort by priority (highest first)
            _providers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            
            // Clear cache to force re-evaluation
            _providerCache.Clear();
        }
        
        /// <summary>
        /// Get provider for save data type
        /// </summary>
        public ISaveDataProvider GetProvider(Type saveDataType)
        {
            if (_providerCache.TryGetValue(saveDataType, out var cachedProvider))
                return cachedProvider;
                
            var provider = _providers.FirstOrDefault(p => p.CanHandle(saveDataType));
            
            if (provider != null)
                _providerCache[saveDataType] = provider;
                
            return provider;
        }
        
        /// <summary>
        /// Get provider for save data instance
        /// </summary>
        public ISaveDataProvider GetProvider(SaveData saveData)
        {
            return saveData != null ? GetProvider(saveData.GetType()) : null;
        }
        
        /// <summary>
        /// Get all registered providers
        /// </summary>
        public IReadOnlyList<ISaveDataProvider> GetAllProviders()
        {
            return _providers.AsReadOnly();
        }
        
        /// <summary>
        /// Check if a save data type is supported
        /// </summary>
        public bool IsSupported(Type saveDataType)
        {
            return GetProvider(saveDataType) != null;
        }
    }
}