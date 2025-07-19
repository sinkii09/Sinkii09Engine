using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Services;
using Sinkii09.Engine.Events;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sinkii09.Engine.Tests
{
    /// <summary>
    /// Comprehensive test script for SaveLoadService with compression and encryption
    /// Tests the complete save/load pipeline including security features
    /// </summary>
    public class SaveLoadTestScript : MonoBehaviour
    {
        [Header("Test Configuration")]
        [SerializeField] private bool enableCompression = true;
        [SerializeField] private bool enableEncryption = true;
        [SerializeField] private bool enableDetailedLogging = true;
        [SerializeField] private string testSaveId = "test_save_001";
        
        [Header("Test Data")]
        [SerializeField] private string testSceneName = "TestLevel";
        [SerializeField] private int testLevel = 5;
        [SerializeField] private float testPlayTime = 3600.5f; // 1 hour
        [SerializeField] private int testHighScore = 999999;
        
        [Header("Performance Monitoring")]
        [SerializeField] private bool measurePerformance = true;
        [SerializeField] private int performanceTestIterations = 10;
        
        private ISaveLoadService _saveLoadService;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                StartTest();
            }
        }
        void StartTest()
        {
            // Get the SaveLoadService from Engine
            _saveLoadService = Engine.GetService<ISaveLoadService>();
            
            if (_saveLoadService == null)
            {
                Debug.LogError("[SaveLoadTest] SaveLoadService not available! Make sure it's initialized.");
                return;
            }
            
            Debug.Log("[SaveLoadTest] SaveLoadService found, starting tests...");
            
            // Subscribe to events for monitoring
            SubscribeToEvents();
            
            // Start the test sequence
            RunTestSequence().Forget();
        }
        
        void OnDestroy()
        {
            UnsubscribeFromEvents();
        }
        
        /// <summary>
        /// Main test sequence covering all SaveLoadService features
        /// </summary>
        private async UniTaskVoid RunTestSequence()
        {
            try
            {
                LogInfo("=== SaveLoadService Test Sequence Started ===");
                
                // Test 1: Basic Save/Load Test
                await TestBasicSaveLoad();
                
                // Test 2: Compression Test
                if (enableCompression)
                {
                    await TestCompression();
                }
                
                // Test 3: Encryption Test
                if (enableEncryption)
                {
                    await TestEncryption();
                }
                
                // Test 4: Metadata and Backup Test
                await TestMetadataAndBackup();
                
                // Test 5: Performance Test
                if (measurePerformance)
                {
                    await TestPerformance();
                }
                
                // Test 6: Error Handling Test
                await TestErrorHandling();
                
                // Test 7: Statistics and Monitoring
                await TestStatistics();
                
                LogInfo("=== All Tests Completed Successfully ===");
                
                // Display final report
                await DisplayPerformanceReport();
            }
            catch (Exception ex)
            {
                LogError($"Test sequence failed: {ex.Message}");
                Debug.LogException(ex);
            }
        }
        
        /// <summary>
        /// Test basic save and load functionality
        /// </summary>
        private async UniTask TestBasicSaveLoad()
        {
            LogInfo("--- Test 1: Basic Save/Load ---");
            
            // Create test data
            var testData = CreateTestGameData();
            LogInfo($"Created test data: Level {testData.CurrentLevel}, Scene '{testData.CurrentSceneName}'");
            
            // Save the data
            var saveResult = await _saveLoadService.SaveAsync(testSaveId, testData);
            if (!saveResult.Success)
            {
                throw new Exception($"Save failed: {saveResult.Message}");
            }
            LogInfo($"Save completed in {saveResult.Duration.TotalMilliseconds:F2}ms");
            
            // Verify save exists
            var exists = await _saveLoadService.ExistsAsync(testSaveId);
            if (!exists)
            {
                throw new Exception("Save file does not exist after saving");
            }
            LogInfo("Save file existence verified");
            
            // Load the data
            var loadResult = await _saveLoadService.LoadAsync<GameSaveData>(testSaveId);
            if (!loadResult.Success)
            {
                throw new Exception($"Load failed: {loadResult.Message}");
            }
            LogInfo($"Load completed in {loadResult.Duration.TotalMilliseconds:F2}ms");
            
            // Verify data integrity
            VerifyLoadedData(testData, loadResult.Data);
            LogInfo("Data integrity verified ✓");
        }
        
        /// <summary>
        /// Test compression functionality and efficiency
        /// </summary>
        private async UniTask TestCompression()
        {
            LogInfo("--- Test 2: Compression Test ---");
            
            // Create large test data for better compression testing
            var largeTestData = CreateLargeTestData();
            LogInfo($"Created large test data with {largeTestData.GameFlags.Count} flags");
            
            var compressedSaveId = testSaveId + "_compressed";
            
            // Save with compression
            var saveResult = await _saveLoadService.SaveAsync(compressedSaveId, largeTestData);
            if (!saveResult.Success)
            {
                throw new Exception($"Compressed save failed: {saveResult.Message}");
            }
            
            LogInfo($"Compressed save completed in {saveResult.Duration.TotalMilliseconds:F2}ms");
            LogInfo($"Original size: {saveResult.UncompressedSize} bytes");
            LogInfo($"Compressed size: {saveResult.CompressedSize} bytes");
            LogInfo($"Compression ratio: {((1 - saveResult.CompressionRatio) * 100):F1}%");
            
            // Load and verify
            var loadResult = await _saveLoadService.LoadAsync<GameSaveData>(compressedSaveId);
            if (!loadResult.Success)
            {
                throw new Exception($"Compressed load failed: {loadResult.Message}");
            }
            
            VerifyLoadedData(largeTestData, loadResult.Data);
            LogInfo("Compression test passed ✓");
        }
        
        /// <summary>
        /// Test encryption functionality
        /// </summary>
        private async UniTask TestEncryption()
        {
            LogInfo("--- Test 3: Encryption Test ---");
            
            var testData = CreateTestGameData();
            var encryptedSaveId = testSaveId + "_encrypted";
            
            // Save with encryption
            var saveResult = await _saveLoadService.SaveAsync(encryptedSaveId, testData);
            if (!saveResult.Success)
            {
                throw new Exception($"Encrypted save failed: {saveResult.Message}");
            }
            
            LogInfo($"Encrypted save completed in {saveResult.Duration.TotalMilliseconds:F2}ms");
            
            // Load and verify
            var loadResult = await _saveLoadService.LoadAsync<GameSaveData>(encryptedSaveId);
            if (!loadResult.Success)
            {
                throw new Exception($"Encrypted load failed: {loadResult.Message}");
            }
            
            LogInfo($"Decryption completed in {loadResult.Duration.TotalMilliseconds:F2}ms");
            VerifyLoadedData(testData, loadResult.Data);
            LogInfo("Encryption test passed ✓");
        }
        
        /// <summary>
        /// Test metadata retrieval and backup functionality
        /// </summary>
        private async UniTask TestMetadataAndBackup()
        {
            LogInfo("--- Test 4: Metadata and Backup Test ---");
            
            // Get metadata for our test save
            var metadata = await _saveLoadService.GetSaveMetadataAsync(testSaveId);
            if (metadata == null)
            {
                throw new Exception("Failed to retrieve save metadata");
            }
            
            LogInfo($"Metadata retrieved - Size: {metadata.FileSize} bytes, Created: {metadata.CreatedAt}");
            
            // Create backup
            var backupResult = await _saveLoadService.CreateBackupAsync(testSaveId);
            if (!backupResult)
            {
                throw new Exception("Failed to create backup");
            }
            LogInfo("Backup created successfully");

            //List backups
            var backups = await _saveLoadService.GetBackupsAsync(testSaveId);
            LogInfo($"Found {backups.Count} backup(s) for save '{testSaveId}'");

            // List all saves
            var allSaves = await _saveLoadService.GetAllSavesAsync();
            LogInfo($"Total saves in system: {allSaves.Count}");

            LogInfo("Metadata and backup test passed ✓");
        }
        
        /// <summary>
        /// Test performance with multiple operations
        /// </summary>
        private async UniTask TestPerformance()
        {
            LogInfo("--- Test 5: Performance Test ---");
            
            var testData = CreateTestGameData();
            var performanceSaveId = testSaveId + "_perf";
            
            var totalSaveTime = 0.0;
            var totalLoadTime = 0.0;
            
            for (int i = 0; i < performanceTestIterations; i++)
            {
                var currentSaveId = $"{performanceSaveId}_{i}";
                
                // Measure save time
                var saveStart = DateTime.UtcNow;
                var saveResult = await _saveLoadService.SaveAsync(currentSaveId, testData);
                var saveTime = (DateTime.UtcNow - saveStart).TotalMilliseconds;
                totalSaveTime += saveTime;
                
                if (!saveResult.Success)
                {
                    LogError($"Performance test save {i} failed: {saveResult.Message}");
                    continue;
                }
                
                // Measure load time
                var loadStart = DateTime.UtcNow;
                var loadResult = await _saveLoadService.LoadAsync<GameSaveData>(currentSaveId);
                var loadTime = (DateTime.UtcNow - loadStart).TotalMilliseconds;
                totalLoadTime += loadTime;
                
                if (!loadResult.Success)
                {
                    LogError($"Performance test load {i} failed: {loadResult.Message}");
                }
            }
            
            var avgSaveTime = totalSaveTime / performanceTestIterations;
            var avgLoadTime = totalLoadTime / performanceTestIterations;
            
            LogInfo($"Performance Test Results ({performanceTestIterations} iterations):");
            LogInfo($"  Average Save Time: {avgSaveTime:F2}ms");
            LogInfo($"  Average Load Time: {avgLoadTime:F2}ms");
            LogInfo($"  Total Save Time: {totalSaveTime:F2}ms");
            LogInfo($"  Total Load Time: {totalLoadTime:F2}ms");
            
            LogInfo("Performance test completed ✓");
        }
        
        /// <summary>
        /// Test error handling scenarios
        /// </summary>
        private async UniTask TestErrorHandling()
        {
            LogInfo("--- Test 6: Error Handling Test ---");
            
            // Test loading non-existent save
            var nonExistentLoadResult = await _saveLoadService.LoadAsync<GameSaveData>("non_existent_save");
            if (nonExistentLoadResult.Success)
            {
                LogError("Expected load of non-existent save to fail, but it succeeded");
            }
            else
            {
                LogInfo($"Non-existent save load correctly failed: {nonExistentLoadResult.Message}");
            }
            
            // Test validation
            var validationResult = await _saveLoadService.ValidateSaveAsync(testSaveId);
            LogInfo($"Save validation result: {validationResult}");
            
            LogInfo("Error handling test completed ✓");
        }
        
        /// <summary>
        /// Test statistics and monitoring
        /// </summary>
        private async UniTask TestStatistics()
        {
            LogInfo("--- Test 7: Statistics and Monitoring ---");
            
            var statistics = _saveLoadService.GetStatistics();
            LogInfo($"Service Statistics:");
            LogInfo($"  Total Saves: {statistics.TotalSaves}");
            LogInfo($"  Total Loads: {statistics.TotalLoads}");
            LogInfo($"  Failed Saves: {statistics.FailedSaves}");
            LogInfo($"  Failed Loads: {statistics.FailedLoads}");
            LogInfo($"  Average Save Time: {statistics.AverageSaveTime.TotalMilliseconds:F2}ms");
            LogInfo($"  Average Load Time: {statistics.AverageLoadTime.TotalMilliseconds:F2}ms");
            
            var performanceSummary = _saveLoadService.GetPerformanceSummary();
            LogInfo($"Performance Summary:");
            LogInfo($"  Total Operations: {performanceSummary.TotalOperations}");
            LogInfo($"  Average Total Time: {performanceSummary.AverageTotalTime:F2}ms");
            
            LogInfo("Statistics test completed ✓");

            await UniTask.CompletedTask;
        }
        
        /// <summary>
        /// Display final performance report
        /// </summary>
        private async UniTask DisplayPerformanceReport()
        {
            LogInfo("=== Final Performance Report ===");
            
            var report = _saveLoadService.GeneratePerformanceReport();
            LogInfo(report);
            await UniTask.CompletedTask;
        }
        
        /// <summary>
        /// Create test game data
        /// </summary>
        private GameSaveData CreateTestGameData()
        {
            return new GameSaveData
            {
                CurrentSceneName = testSceneName,
                CurrentLevel = testLevel,
                GameMode = "Test",
                Difficulty = GameDifficulty.Normal,
                TotalPlayTime = testPlayTime,
                HighScore = testHighScore,
                MasterVolume = 0.8f,
                MusicVolume = 0.6f,
                SfxVolume = 0.9f,
                UnlockedLevels = new List<string> { "Level1", "Level2", "Level3" },
                CompletedLevels = new List<string> { "Level1", "Level2" },
                GameFlags = new Dictionary<string, bool>
                {
                    ["tutorial_completed"] = true,
                    ["boss_defeated"] = false,
                    ["secret_found"] = true
                },
                GameCounters = new Dictionary<string, int>
                {
                    ["deaths"] = 5,
                    ["secrets_found"] = 2,
                    ["enemies_defeated"] = 150
                }
            };
        }
        
        /// <summary>
        /// Create large test data for compression testing
        /// </summary>
        private GameSaveData CreateLargeTestData()
        {
            var data = CreateTestGameData();
            
            // Add many flags and counters for better compression testing
            for (int i = 0; i < 1000; i++)
            {
                data.GameFlags[$"flag_{i}"] = i % 2 == 0;
                data.GameCounters[$"counter_{i}"] = i * 10;
            }
            
            // Add many unlocked levels
            for (int i = 1; i <= 100; i++)
            {
                data.UnlockedLevels.Add($"Level{i}");
                if (i <= 80)
                {
                    data.CompletedLevels.Add($"Level{i}");
                }
            }
            
            return data;
        }
        
        /// <summary>
        /// Verify that loaded data matches original data
        /// </summary>
        private void VerifyLoadedData(GameSaveData original, GameSaveData loaded)
        {
            if (loaded == null)
            {
                throw new Exception("Loaded data is null");
            }
            
            if (original.CurrentSceneName != loaded.CurrentSceneName)
                throw new Exception($"Scene name mismatch: {original.CurrentSceneName} != {loaded.CurrentSceneName}");
                
            if (original.CurrentLevel != loaded.CurrentLevel)
                throw new Exception($"Level mismatch: {original.CurrentLevel} != {loaded.CurrentLevel}");
                
            if (Math.Abs(original.TotalPlayTime - loaded.TotalPlayTime) > 0.001f)
                throw new Exception($"Play time mismatch: {original.TotalPlayTime} != {loaded.TotalPlayTime}");
                
            if (original.HighScore != loaded.HighScore)
                throw new Exception($"High score mismatch: {original.HighScore} != {loaded.HighScore}");
                
            if (original.GameFlags.Count != loaded.GameFlags.Count)
                throw new Exception($"Game flags count mismatch: {original.GameFlags.Count} != {loaded.GameFlags.Count}");
        }
        
        /// <summary>
        /// Subscribe to SaveLoadService events for monitoring
        /// </summary>
        private void SubscribeToEvents()
        {
            if (_saveLoadService == null) return;
            
            _saveLoadService.OnSaveStarted += OnSaveStarted;
            _saveLoadService.OnSaveCompleted += OnSaveCompleted;
            _saveLoadService.OnSaveFailed += OnSaveFailed;
            _saveLoadService.OnLoadStarted += OnLoadStarted;
            _saveLoadService.OnLoadCompleted += OnLoadCompleted;
            _saveLoadService.OnLoadFailed += OnLoadFailed;
        }
        
        /// <summary>
        /// Unsubscribe from SaveLoadService events
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (_saveLoadService == null) return;
            
            _saveLoadService.OnSaveStarted -= OnSaveStarted;
            _saveLoadService.OnSaveCompleted -= OnSaveCompleted;
            _saveLoadService.OnSaveFailed -= OnSaveFailed;
            _saveLoadService.OnLoadStarted -= OnLoadStarted;
            _saveLoadService.OnLoadCompleted -= OnLoadCompleted;
            _saveLoadService.OnLoadFailed -= OnLoadFailed;
        }
        
        #region Event Handlers
        
        private void OnSaveStarted(SaveEventArgs args)
        {
            if (enableDetailedLogging)
                LogInfo($"[Event] Save started: {args.SaveId}");
        }
        
        private void OnSaveCompleted(SaveEventArgs args)
        {
            if (enableDetailedLogging)
                LogInfo($"[Event] Save completed: {args.SaveId}");
        }
        
        private void OnSaveFailed(SaveErrorEventArgs args)
        {
            LogError($"[Event] Save failed: {args.SaveId} - {args.Exception.Message}");
        }
        
        private void OnLoadStarted(LoadEventArgs args)
        {
            if (enableDetailedLogging)
                LogInfo($"[Event] Load started: {args.SaveId}");
        }
        
        private void OnLoadCompleted(LoadEventArgs args)
        {
            if (enableDetailedLogging)
                LogInfo($"[Event] Load completed: {args.SaveId}");
        }
        
        private void OnLoadFailed(LoadErrorEventArgs args)
        {
            LogError($"[Event] Load failed: {args.SaveId} - {args.Exception.Message}");
        }
        
        #endregion
        
        #region Logging Helpers
        
        private void LogInfo(string message)
        {
            Debug.Log($"[SaveLoadTest] {message}");
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[SaveLoadTest] {message}");
        }
        
        #endregion
        
        #region Public Test Methods (for manual testing)
        
        [ContextMenu("Run Quick Test")]
        public void RunQuickTest()
        {
            TestBasicSaveLoad().Forget();
        }
        
        [ContextMenu("Test Compression")]
        public void TestCompressionOnly()
        {
            TestCompression().Forget();
        }
        
        [ContextMenu("Test Encryption")]
        public void TestEncryptionOnly()
        {
            TestEncryption().Forget();
        }
        
        [ContextMenu("Clear All Test Saves")]
        public async void ClearAllTestSaves()
        {
            try
            {
                await _saveLoadService.DeleteAsync(testSaveId);
                await _saveLoadService.DeleteAsync(testSaveId + "_compressed");
                await _saveLoadService.DeleteAsync(testSaveId + "_encrypted");
                
                for (int i = 0; i < performanceTestIterations; i++)
                {
                    await _saveLoadService.DeleteAsync($"{testSaveId}_perf_{i}");
                }
                
                LogInfo("All test saves cleared");
            }
            catch (Exception ex)
            {
                LogError($"Failed to clear test saves: {ex.Message}");
            }
        }
        
        #endregion
    }
}