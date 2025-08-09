using System;
using System.Collections.Generic;
using UnityEngine;
using R3;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Global static access to current game runtime data.
    /// Central hub for all save data used by UI and game systems.
    /// Maintains SaveLoadService compatibility by storing instances statically.
    /// </summary>
    public static class RuntimeData
    {
        private static readonly ReactiveProperty<GameSaveData> _data = new(null);
        private static readonly ReactiveProperty<PlayerSaveData> _player = new(null);
        private static readonly Dictionary<Type, SaveData> _customData = new Dictionary<Type, SaveData>();
        private static readonly Subject<(Type Type, SaveData Data)> _customDataChanged = new();
        
        // R3 Reactive properties for data changes
        public static ReadOnlyReactiveProperty<GameSaveData> GameData => _data;
        public static ReadOnlyReactiveProperty<PlayerSaveData> PlayerData => _player;
        public static Observable<(Type Type, SaveData Data)> CustomDataObservable => _customDataChanged.AsObservable();
               
        /// <summary>
        /// Check if any save data is loaded
        /// </summary>
        public static bool HasData => _data.Value != null || _player.Value != null;
        
        /// <summary>
        /// Check if game data is loaded
        /// </summary>
        public static bool HasGameData => _data.Value != null;
        
        /// <summary>
        /// Check if player data is loaded
        /// </summary>
        public static bool HasPlayerData => _player.Value != null;
        
        /// <summary>
        /// Load game save data (called by SaveLoadService)
        /// </summary>
        public static void LoadData(GameSaveData gameData)
        {
            _data.Value = gameData;
        }
        
        /// <summary>
        /// Load player save data (called by SaveLoadService)
        /// </summary>
        public static void LoadPlayer(PlayerSaveData playerData)
        {
            _player.Value = playerData;
        }
        
        /// <summary>
        /// Load custom save data of specific type
        /// </summary>
        public static void LoadCustomData<T>(T customData) where T : SaveData
        {
            var type = typeof(T);
            _customData[type] = customData;
            _customDataChanged.OnNext((type, customData));
        }
        
        /// <summary>
        /// Get custom save data of specific type
        /// </summary>
        public static T GetCustomData<T>() where T : SaveData
        {
            var type = typeof(T);
            return _customData.TryGetValue(type, out var data) ? data as T : null;
        }
        
        /// <summary>
        /// Check if custom data of specific type is loaded
        /// </summary>
        public static bool HasCustomData<T>() where T : SaveData
        {
            return _customData.ContainsKey(typeof(T));
        }
        
        /// <summary>
        /// Clear all save data (for new game or logout)
        /// </summary>
        public static void ClearAll()
        {
            _data.Value = null;
            _player.Value = null;
            _customData.Clear();
        }
        
        /// <summary>
        /// Dispose all reactive properties and clean up resources.
        /// Call this when shutting down the application or when RuntimeData is no longer needed.
        /// </summary>
        public static void Dispose()
        {
            _data?.Dispose();
            _player?.Dispose();
            _customDataChanged?.Dispose();
            _customData.Clear();
        }
        
        /// <summary>
        /// Clear only game data
        /// </summary>
        public static void ClearData()
        {
            _data.Value = null;
        }
        
        /// <summary>
        /// Clear only player data
        /// </summary>
        public static void ClearPlayer()
        {
            _player.Value = null;
        }
        
        /// <summary>
        /// Clear custom data of specific type
        /// </summary>
        public static void ClearCustomData<T>() where T : SaveData
        {
            var type = typeof(T);
            if (_customData.Remove(type))
            {
                _customDataChanged.OnNext((type, null));
            }
        }
        
        /// <summary>
        /// Create new game with default data
        /// </summary>
        public static void CreateNewGame()
        {
            LoadData(new GameSaveData());
            LoadPlayer(new PlayerSaveData());
        }
        
        /// <summary>
        /// Create new game with custom player name
        /// </summary>
        public static void CreateNewGame(string playerName)
        {
            LoadData(new GameSaveData());
            LoadPlayer(new PlayerSaveData(playerName));
        }
        
        /// <summary>
        /// Get all loaded save data types
        /// </summary>
        public static IReadOnlyList<Type> GetLoadedDataTypes()
        {
            var types = new List<Type>();
            
            if (_data.Value != null)
                types.Add(typeof(GameSaveData));
                
            if (_player.Value != null)
                types.Add(typeof(PlayerSaveData));
                
            types.AddRange(_customData.Keys);
            
            return types;
        }
        
        /// <summary>
        /// Get all loaded save data instances for SaveLoadService
        /// </summary>
        public static IReadOnlyList<SaveData> GetAllLoadedData()
        {
            var data = new List<SaveData>();
            
            if (_data.Value != null)
                data.Add(_data.Value);
                
            if (_player.Value != null)
                data.Add(_player.Value);
                
            data.AddRange(_customData.Values);
            
            return data;
        }
    }
}