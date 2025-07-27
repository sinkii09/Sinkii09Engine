using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Sinkii09.Engine.Services;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Sinkii09.Engine.Tests
{
    /// <summary>
    /// Performance benchmarking tests for ResourcePathResolver
    /// Validates cache efficiency, resolution speed, and memory usage targets
    /// </summary>
    [TestFixture]
    public class ResourcePathResolverPerformanceTests
    {
        private ResourcePathResolver _resolver;
        private ResourcePathResolverConfiguration _config;
        
        // Performance targets
        private const float MAX_AVERAGE_RESOLUTION_TIME_MS = 0.05f; // 0.05ms target
        private const float MIN_CACHE_HIT_RATE = 0.90f; // 90% hit rate target
        private const int HIGH_VOLUME_TEST_COUNT = 10000;
        private const int CONCURRENT_OPERATIONS = 100;

        [SetUp]
        public void SetUp()
        {
            // Configure for high-performance testing
            _config = ScriptableObject.CreateInstance<ResourcePathResolverConfiguration>();
            
            // Use reflection to set private fields
            var configType = typeof(ResourcePathResolverConfiguration);
            var bindingFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            
            configType.GetField("_maxCacheSize", bindingFlags).SetValue(_config, 2000);
            configType.GetField("_cacheEntryLifetime", bindingFlags).SetValue(_config, 3600f);
            configType.GetField("_enableLRUEviction", bindingFlags).SetValue(_config, true);
            configType.GetField("_validateTemplatesAtStartup", bindingFlags).SetValue(_config, false);
            configType.GetField("_strictValidationMode", bindingFlags).SetValue(_config, false);
            configType.GetField("_enableExistenceChecking", bindingFlags).SetValue(_config, false);
            configType.GetField("_maxResolutionTimeMs", bindingFlags).SetValue(_config, 1f);
            configType.GetField("_enableMemoryPressureResponse", bindingFlags).SetValue(_config, true);
            configType.GetField("_defaultEnvironment", bindingFlags).SetValue(_config, ResourceEnvironment.Development);
            configType.GetField("_resourceRoot", bindingFlags).SetValue(_config, "");
            
            // Add comprehensive templates for testing
            configType.GetField("_pathTemplates", bindingFlags).SetValue(_config, CreatePerformanceTestTemplates());
            configType.GetField("_fallbackPaths", bindingFlags).SetValue(_config, CreatePerformanceFallbackPaths());

            _resolver = new ResourcePathResolver(_config);
        }

        [TearDown]
        public void TearDown()
        {
            _resolver?.Dispose();
            if (_config != null)
            {
                UnityEngine.Object.DestroyImmediate(_config);
            }
        }

        private PathTemplateEntry[] CreatePerformanceTestTemplates()
        {
            return new[]
            {
                // High-frequency actor templates
                new PathTemplateEntry(ResourceType.Actor, ResourceCategory.Sprites, "Actors/{actorType}/Sprites/{actorId}/{appearance}", PathPriority.High),
                new PathTemplateEntry(ResourceType.Actor, ResourceCategory.Audio, "Actors/{actorType}/Audio/{actorId}/{emotion}", PathPriority.High),
                new PathTemplateEntry(ResourceType.Actor, ResourceCategory.Animations, "Actors/{actorType}/Animations/{actorId}/{animationName}", PathPriority.High),
                
                // Background templates
                new PathTemplateEntry(ResourceType.Actor, ResourceCategory.Sprites, "Backgrounds/{location}/{timeOfDay}/{weather}", PathPriority.High),
                
                // UI templates (frequent during gameplay)
                new PathTemplateEntry(ResourceType.UI, ResourceCategory.Sprites, "UI/{screenName}/Sprites/{elementName}", PathPriority.High),
                new PathTemplateEntry(ResourceType.UI, ResourceCategory.Prefabs, "UI/{screenName}/Prefabs/{componentType}", PathPriority.High),
                
                // Audio templates (frequent during gameplay)
                new PathTemplateEntry(ResourceType.Audio, ResourceCategory.Music, "Audio/Music/{genre}/{trackName}", PathPriority.Normal),
                new PathTemplateEntry(ResourceType.Audio, ResourceCategory.Effects, "Audio/SFX/{category}/{effectName}", PathPriority.Normal),
                new PathTemplateEntry(ResourceType.Audio, ResourceCategory.Voice, "Audio/Voice/{characterName}/{emotion}/{lineId}", PathPriority.High),
                
                // Script templates
                new PathTemplateEntry(ResourceType.Script, ResourceCategory.Source, "Scripts/{scriptName}.script", PathPriority.High),
                
                // Config templates
                new PathTemplateEntry(ResourceType.Config, ResourceCategory.Primary, "Configs/{configType}/{configName}", PathPriority.High)
            };
        }

        private FallbackPathEntry[] CreatePerformanceFallbackPaths()
        {
            return new[]
            {
                new FallbackPathEntry(ResourceType.Actor, ResourceCategory.Sprites, "Actors/Default/Sprites/{actorId}/default", PathPriority.Low),
                new FallbackPathEntry(ResourceType.Actor, ResourceCategory.Sprites, "Backgrounds/Default/default/clear", PathPriority.Low),
                new FallbackPathEntry(ResourceType.UI, ResourceCategory.Sprites, "UI/Common/Sprites/placeholder", PathPriority.Low),
                new FallbackPathEntry(ResourceType.Audio, ResourceCategory.Music, "Audio/Default/silence", PathPriority.Low),
                new FallbackPathEntry(ResourceType.Audio, ResourceCategory.Effects, "Audio/Default/click", PathPriority.Low)
            };
        }

        #region Cache Performance Tests

        [Test]
        public void CachePerformance_HighVolumeRepeatedAccess_AchievesTargetHitRate()
        {
            // Arrange
            var testActors = GenerateTestActors(50);
            var testAppearances = new[] { "happy", "sad", "angry", "neutral", "surprised" };
            var stopwatch = new Stopwatch();

            // Act - First pass: populate cache
            stopwatch.Start();
            foreach (var actor in testActors)
            {
                foreach (var appearance in testAppearances)
                {
                    var parameters = new[]
                    {
                        new PathParameter(PathParameterNames.ACTOR_TYPE, "Character"),
                        new PathParameter(PathParameterNames.APPEARANCE, appearance)
                    };
                    _resolver.ResolveResourcePath(ResourceType.Actor, actor, ResourceCategory.Sprites, parameters);
                }
            }
            var firstPassTime = stopwatch.ElapsedMilliseconds;

            // Reset timer for second pass
            stopwatch.Restart();

            // Second pass: should hit cache
            foreach (var actor in testActors)
            {
                foreach (var appearance in testAppearances)
                {
                    var parameters = new[]
                    {
                        new PathParameter(PathParameterNames.ACTOR_TYPE, "Character"),
                        new PathParameter(PathParameterNames.APPEARANCE, appearance)
                    };
                    _resolver.ResolveResourcePath(ResourceType.Actor, actor, ResourceCategory.Sprites, parameters);
                }
            }
            var secondPassTime = stopwatch.ElapsedMilliseconds;
            stopwatch.Stop();

            var stats = _resolver.GetStatistics();

            // Assert
            Assert.Greater(stats.CacheHitRate, MIN_CACHE_HIT_RATE, 
                $"Cache hit rate {stats.CacheHitRate:P2} should be above {MIN_CACHE_HIT_RATE:P2}");
            
            Assert.Less(secondPassTime, firstPassTime / 2, 
                "Second pass should be significantly faster due to caching");
            
            Debug.Log($"Cache Performance: Hit Rate = {stats.CacheHitRate:P2}, " +
                     $"First Pass = {firstPassTime}ms, Second Pass = {secondPassTime}ms");
        }

        [Test]
        public void CachePerformance_LRUEviction_MaintainsPerformance()
        {
            // Arrange - Use smaller cache to force eviction
            var smallCacheConfig = ScriptableObject.CreateInstance<ResourcePathResolverConfiguration>();
            
            // Use reflection to set private fields
            var configType = typeof(ResourcePathResolverConfiguration);
            var bindingFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            
            configType.GetField("_maxCacheSize", bindingFlags).SetValue(smallCacheConfig, 100);
            configType.GetField("_enableLRUEviction", bindingFlags).SetValue(smallCacheConfig, true);
            configType.GetField("_cacheEntryLifetime", bindingFlags).SetValue(smallCacheConfig, 3600f);
            configType.GetField("_pathTemplates", bindingFlags).SetValue(smallCacheConfig, _config.PathTemplates.ToArray());
            configType.GetField("_fallbackPaths", bindingFlags).SetValue(smallCacheConfig, _config.FallbackPaths.ToArray());
            configType.GetField("_resourceRoot", bindingFlags).SetValue(smallCacheConfig, "");

            var smallCacheResolver = new ResourcePathResolver(smallCacheConfig);
            var stopwatch = new Stopwatch();

            try
            {
                // Act - Load more items than cache size
                var testActors = GenerateTestActors(150); // More than cache size
                var parameters = new[]
                {
                    new PathParameter(PathParameterNames.ACTOR_TYPE, "Character"),
                    new PathParameter(PathParameterNames.APPEARANCE, "happy")
                };

                stopwatch.Start();
                foreach (var actor in testActors)
                {
                    smallCacheResolver.ResolveResourcePath(ResourceType.Actor, actor, ResourceCategory.Sprites, parameters);
                }
                var totalTime = stopwatch.ElapsedMilliseconds;
                stopwatch.Stop();

                var stats = smallCacheResolver.GetStatistics();
                var averageTime = (double)totalTime / testActors.Length;

                // Assert
                Assert.Less(averageTime, MAX_AVERAGE_RESOLUTION_TIME_MS * 10, // Allow 10x normal for eviction overhead
                    $"Average resolution time {averageTime:F3}ms should remain reasonable even with eviction");
                
                Assert.AreEqual(100, stats.CachedPaths, "Cache size should be maintained at maximum");
                
                Debug.Log($"LRU Eviction Performance: {stats.CachedPaths} cached, " +
                         $"Avg time = {averageTime:F3}ms, Total = {totalTime}ms");
            }
            finally
            {
                smallCacheResolver?.Dispose();
                UnityEngine.Object.DestroyImmediate(smallCacheConfig);
            }
        }

        #endregion

        #region Resolution Speed Tests

        [Test]
        public void ResolutionSpeed_SinglePath_MeetsPerformanceTarget()
        {
            // Arrange
            var parameters = new[]
            {
                new PathParameter(PathParameterNames.ACTOR_TYPE, "Character"),
                new PathParameter(PathParameterNames.APPEARANCE, "happy")
            };

            var resolutionTimes = new List<double>();
            var stopwatch = new Stopwatch();

            // Act - Measure multiple single resolutions
            for (int i = 0; i < 1000; i++)
            {
                stopwatch.Restart();
                _resolver.ResolveResourcePath(ResourceType.Actor, $"actor_{i}", ResourceCategory.Sprites, parameters);
                stopwatch.Stop();
                resolutionTimes.Add(stopwatch.Elapsed.TotalMilliseconds);
            }

            var averageTime = resolutionTimes.Average();
            var maxTime = resolutionTimes.Max();
            var minTime = resolutionTimes.Min();

            // Assert
            Assert.Less(averageTime, MAX_AVERAGE_RESOLUTION_TIME_MS, 
                $"Average resolution time {averageTime:F4}ms should be under {MAX_AVERAGE_RESOLUTION_TIME_MS}ms");
            
            Assert.Less(maxTime, MAX_AVERAGE_RESOLUTION_TIME_MS * 20, 
                $"Maximum resolution time {maxTime:F4}ms should be reasonable");
            
            Debug.Log($"Single Resolution Performance: Avg = {averageTime:F4}ms, " +
                     $"Min = {minTime:F4}ms, Max = {maxTime:F4}ms");
        }

        [Test]
        public void ResolutionSpeed_HighVolume_MaintainsPerformance()
        {
            // Arrange
            var testData = GenerateHighVolumeTestData(HIGH_VOLUME_TEST_COUNT);
            var stopwatch = Stopwatch.StartNew();

            // Act
            foreach (var testCase in testData)
            {
                _resolver.ResolveResourcePath(testCase.ResourceType, testCase.ResourceId, 
                    testCase.Category, testCase.Parameters);
            }

            stopwatch.Stop();
            var totalTime = stopwatch.ElapsedMilliseconds;
            var averageTime = (double)totalTime / HIGH_VOLUME_TEST_COUNT;
            var stats = _resolver.GetStatistics();

            // Assert
            Assert.Less(averageTime, MAX_AVERAGE_RESOLUTION_TIME_MS * 2, // Allow 2x for high volume
                $"High volume average time {averageTime:F4}ms should remain under threshold");
            
            Assert.Greater(stats.CacheHitRate, 0.5, 
                "Cache hit rate should be reasonable in high volume scenario");
            
            Debug.Log($"High Volume Performance: {HIGH_VOLUME_TEST_COUNT} resolutions in {totalTime}ms, " +
                     $"Avg = {averageTime:F4}ms, Hit Rate = {stats.CacheHitRate:P2}");
        }

        #endregion

        #region Concurrent Performance Tests

        [Test]
        public async UniTask ConcurrentPerformance_MultipleThreads_ThreadSafe()
        {
            // Arrange
            var tasks = new List<UniTask>();
            var results = new List<string>[CONCURRENT_OPERATIONS];
            var stopwatch = Stopwatch.StartNew();

            // Act - Launch concurrent resolution tasks
            for (int i = 0; i < CONCURRENT_OPERATIONS; i++)
            {
                var taskIndex = i;
                results[taskIndex] = new List<string>();
                
                var task = UniTask.RunOnThreadPool(() =>
                {
                    var parameters = new[]
                    {
                        new PathParameter(PathParameterNames.ACTOR_TYPE, "Character"),
                        new PathParameter(PathParameterNames.APPEARANCE, "happy")
                    };

                    for (int j = 0; j < 50; j++) // 50 resolutions per task
                    {
                        var result = _resolver.ResolveResourcePath(ResourceType.Actor, 
                            $"actor_{taskIndex}_{j}", ResourceCategory.Sprites, parameters);
                        results[taskIndex].Add(result);
                    }
                });
                
                tasks.Add(task);
            }

            await UniTask.WhenAll(tasks);
            stopwatch.Stop();

            var totalResolutions = CONCURRENT_OPERATIONS * 50;
            var averageTime = (double)stopwatch.ElapsedMilliseconds / totalResolutions;
            var stats = _resolver.GetStatistics();

            // Assert
            Assert.AreEqual(totalResolutions, stats.TotalResolutions, 
                "All resolutions should be counted correctly");
            
            // Verify all results are valid
            foreach (var resultList in results)
            {
                Assert.AreEqual(50, resultList.Count, "Each task should complete all resolutions");
                foreach (var result in resultList)
                {
                    Assert.IsNotNull(result, "All resolutions should succeed");
                    Assert.IsTrue(result.StartsWith("Actors/Character/Sprites/"), 
                        "All results should follow expected pattern");
                }
            }

            Assert.Less(averageTime, MAX_AVERAGE_RESOLUTION_TIME_MS * 5, // Allow 5x for concurrency overhead
                $"Concurrent average time {averageTime:F4}ms should remain reasonable");
            
            Debug.Log($"Concurrent Performance: {totalResolutions} resolutions across {CONCURRENT_OPERATIONS} tasks, " +
                     $"Total time = {stopwatch.ElapsedMilliseconds}ms, Avg = {averageTime:F4}ms");
        }

        #endregion

        #region Memory Performance Tests

        [Test]
        public void MemoryPerformance_StringInterning_ReducesMemoryUsage()
        {
            // Arrange
            var commonParameters = new[]
            {
                new PathParameter(PathParameterNames.ACTOR_TYPE, "Character"),
                new PathParameter(PathParameterNames.APPEARANCE, "happy")
            };

            // Force GC before test
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var memoryBefore = GC.GetTotalMemory(false);

            // Act - Resolve many paths with repeated strings
            for (int i = 0; i < 1000; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    _resolver.ResolveResourcePath(ResourceType.Actor, $"actor_{i % 100}", 
                        ResourceCategory.Sprites, commonParameters);
                }
            }

            var memoryAfter = GC.GetTotalMemory(false);
            var memoryUsed = memoryAfter - memoryBefore;
            var stringInternStats = _resolver.GetStringInternStatistics();

            // Assert
            Assert.Greater(stringInternStats.HitRate, 0.5, 
                "String interning should show significant hit rate");
            
            Assert.Greater(stringInternStats.EstimatedMemorySavings, 0, 
                "String interning should provide memory savings");
            
            Debug.Log($"Memory Performance: Used {memoryUsed / 1024}KB, " +
                     $"String intern hit rate = {stringInternStats.HitRate:P2}, " +
                     $"Estimated savings = {stringInternStats.EstimatedMemorySavings}B");
        }

        [Test]
        public void MemoryPerformance_MemoryPressureResponse_FreesCacheMemory()
        {
            // Arrange
            var parameters = new[]
            {
                new PathParameter(PathParameterNames.ACTOR_TYPE, "Character"),
                new PathParameter(PathParameterNames.APPEARANCE, "happy")
            };

            // Fill cache
            for (int i = 0; i < 500; i++)
            {
                _resolver.ResolveResourcePath(ResourceType.Actor, $"actor_{i}", ResourceCategory.Sprites, parameters);
            }

            var statsBefore = _resolver.GetStatistics();

            // Act - Simulate high memory pressure
            _resolver.OnMemoryPressure(0.9f);
            var statsAfter = _resolver.GetStatistics();

            // Assert
            Assert.Greater(statsBefore.CachedPaths, 100, "Cache should be populated before pressure");
            Assert.Less(statsAfter.CachedPaths, statsBefore.CachedPaths, 
                "Cache should be reduced after memory pressure");
            
            Debug.Log($"Memory Pressure Response: {statsBefore.CachedPaths} -> {statsAfter.CachedPaths} cached paths");
        }

        #endregion

        #region Performance Monitoring Tests

        [Test]
        public void PerformanceMonitoring_TrackingAccuracy_RecordsCorrectMetrics()
        {
            // Arrange
            var parameters = new[]
            {
                new PathParameter(PathParameterNames.ACTOR_TYPE, "Character"),
                new PathParameter(PathParameterNames.APPEARANCE, "happy")
            };

            // Act
            for (int i = 0; i < 100; i++)
            {
                _resolver.ResolveResourcePath(ResourceType.Actor, $"actor_{i}", ResourceCategory.Sprites, parameters);
            }

            var stats = _resolver.GetStatistics();
            var healthCheck = _resolver.GetPerformanceHealthCheck();

            // Assert
            Assert.AreEqual(100, stats.TotalResolutions, "Should track correct resolution count");
            Assert.Greater(stats.AverageResolutionTime.TotalMilliseconds, 0, 
                "Should track non-zero resolution times");
            Assert.IsNotNull(healthCheck, "Health check should be available");
            Assert.IsTrue(healthCheck.IsHealthy, "Performance should be healthy under normal load");
            
            Debug.Log($"Performance Monitoring: {stats.TotalResolutions} resolutions, " +
                     $"Avg = {stats.AverageResolutionTime.TotalMilliseconds:F4}ms, " +
                     $"Health = {healthCheck.IsHealthy}");
        }

        #endregion

        #region Benchmark Helper Methods

        private string[] GenerateTestActors(int count)
        {
            var actors = new string[count];
            for (int i = 0; i < count; i++)
            {
                actors[i] = $"actor_{i:D3}";
            }
            return actors;
        }

        private List<PerformanceTestCase> GenerateHighVolumeTestData(int count)
        {
            var testCases = new List<PerformanceTestCase>();
            var random = new System.Random(42); // Fixed seed for reproducibility
            
            var resourceTypes = new[] { ResourceType.Actor, ResourceType.UI, ResourceType.Audio };
            var categories = new[] { ResourceCategory.Sprites, ResourceCategory.Audio, ResourceCategory.Animations };
            var actorTypes = new[] { "Character", "NPC", "Enemy", "Prop" };
            var appearances = new[] { "happy", "sad", "angry", "neutral", "surprised", "excited" };

            for (int i = 0; i < count; i++)
            {
                var resourceType = resourceTypes[random.Next(resourceTypes.Length)];
                var category = categories[random.Next(categories.Length)];
                var actorType = actorTypes[random.Next(actorTypes.Length)];
                var appearance = appearances[random.Next(appearances.Length)];

                var parameters = new[]
                {
                    new PathParameter(PathParameterNames.ACTOR_TYPE, actorType),
                    new PathParameter(PathParameterNames.APPEARANCE, appearance)
                };

                testCases.Add(new PerformanceTestCase
                {
                    ResourceType = resourceType,
                    ResourceId = $"resource_{i}",
                    Category = category,
                    Parameters = parameters
                });
            }

            return testCases;
        }

        #endregion
    }

    #region Performance Test Data Structures

    public class PerformanceTestCase
    {
        public ResourceType ResourceType { get; set; }
        public string ResourceId { get; set; }
        public ResourceCategory Category { get; set; }
        public PathParameter[] Parameters { get; set; }
    }

    #endregion
}