using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Sinkii09.Engine.Services;
using UnityEngine;

namespace Sinkii09.Engine.Tests
{
    /// <summary>
    /// Comprehensive unit tests for ResourcePathResolver
    /// Tests core functionality, edge cases, error handling, and configuration scenarios
    /// </summary>
    [TestFixture]
    public class ResourcePathResolverTests
    {
        private ResourcePathResolver _resolver;
        private ResourcePathResolverConfiguration _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<ResourcePathResolverConfiguration>();
            
            // Configure using reflection to set private fields
            var configType = typeof(ResourcePathResolverConfiguration);
            var bindingFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            
            configType.GetField("_maxCacheSize", bindingFlags).SetValue(_config, 1000);
            configType.GetField("_cacheEntryLifetime", bindingFlags).SetValue(_config, 300f);
            configType.GetField("_enableLRUEviction", bindingFlags).SetValue(_config, true);
            configType.GetField("_validateTemplatesAtStartup", bindingFlags).SetValue(_config, true);
            configType.GetField("_strictValidationMode", bindingFlags).SetValue(_config, false);
            configType.GetField("_enableExistenceChecking", bindingFlags).SetValue(_config, false);
            configType.GetField("_maxResolutionTimeMs", bindingFlags).SetValue(_config, 5f);
            configType.GetField("_enableMemoryPressureResponse", bindingFlags).SetValue(_config, true);
            configType.GetField("_defaultEnvironment", bindingFlags).SetValue(_config, ResourceEnvironment.Development);
            configType.GetField("_resourceRoot", bindingFlags).SetValue(_config, "");
            configType.GetField("_pathTemplates", bindingFlags).SetValue(_config, CreateTestPathTemplates());
            configType.GetField("_fallbackPaths", bindingFlags).SetValue(_config, CreateTestFallbackPaths());

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

        #region Core Path Resolution Tests

        [Test]
        public void ResolveResourcePath_ValidActorSprite_ReturnsCorrectPath()
        {
            // Arrange
            var parameters = new[]
            {
                new PathParameter(PathParameterNames.ACTOR_TYPE, "Character"),
                new PathParameter(PathParameterNames.APPEARANCE, "happy")
            };

            // Act
            var result = _resolver.ResolveResourcePath(ResourceType.Actor, "protagonist", ResourceCategory.Sprites, parameters);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("Actors/Character/Sprites/protagonist/happy", result);
        }

        [Test]
        public void ResolveResourcePathDetailed_ValidPath_ReturnsSuccessResult()
        {
            // Arrange
            var parameters = new[]
            {
                new PathParameter(PathParameterNames.ACTOR_TYPE, "Character"),
                new PathParameter(PathParameterNames.APPEARANCE, "neutral")
            };

            // Act
            var result = _resolver.ResolveResourcePathDetailed(ResourceType.Actor, "hero", ResourceCategory.Sprites, parameters);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("Actors/Character/Sprites/hero/neutral", result.ResolvedPath);
            Assert.AreEqual(PathPriority.High, result.UsedPriority);
            Assert.Greater(result.ResolutionTime.TotalMilliseconds, 0);
        }

        [Test]
        public void ResolveResourcePath_ScriptResource_ReturnsCorrectPath()
        {
            // Act
            var result = _resolver.ResolveResourcePath(ResourceType.Script, "gamestart", ResourceCategory.Source);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("Scripts/gamestart.script", result);
        }

        [Test]
        public void ResolveResourcePath_AudioResource_ReturnsCorrectPath()
        {
            // Arrange
            var parameters = new[]
            {
                new PathParameter("category", "ambient"),
                new PathParameter("trackName", "forest_sounds")
            };

            // Act
            var result = _resolver.ResolveResourcePath(ResourceType.Audio, "music_id", ResourceCategory.Music, parameters);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("Audio/Music/ambient/forest_sounds", result);
        }

        [Test]
        public void ResolveResourcePath_UIResource_ReturnsCorrectPath()
        {
            // Arrange
            var parameters = new[]
            {
                new PathParameter("category", "menu"),
                new PathParameter("elementName", "main_button")
            };

            // Act
            var result = _resolver.ResolveResourcePath(ResourceType.UI, "ui_id", ResourceCategory.Primary, parameters);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("UI/menu/main_button", result);
        }

        #endregion

        #region Cache Tests

        [Test]
        public void ResolveResourcePath_SecondCall_ReturnsCachedResult()
        {
            // Arrange
            var parameters = new[]
            {
                new PathParameter(PathParameterNames.ACTOR_TYPE, "Character"),
                new PathParameter(PathParameterNames.APPEARANCE, "happy")
            };

            // Act
            var firstResult = _resolver.ResolveResourcePathDetailed(ResourceType.Actor, "protagonist", ResourceCategory.Sprites, parameters);
            var secondResult = _resolver.ResolveResourcePathDetailed(ResourceType.Actor, "protagonist", ResourceCategory.Sprites, parameters);

            // Assert
            Assert.IsTrue(firstResult.IsSuccess);
            Assert.IsTrue(secondResult.IsSuccess);
            Assert.IsFalse(firstResult.FromCache);
            Assert.IsTrue(secondResult.FromCache);
            Assert.AreEqual(firstResult.ResolvedPath, secondResult.ResolvedPath);
        }

        [Test]
        public void ClearCache_AfterCaching_RemovesCachedEntries()
        {
            // Arrange
            var parameters = new[]
            {
                new PathParameter(PathParameterNames.ACTOR_TYPE, "Character"),
                new PathParameter(PathParameterNames.APPEARANCE, "happy")
            };

            // Cache a result
            _resolver.ResolveResourcePath(ResourceType.Actor, "protagonist", ResourceCategory.Sprites, parameters);
            var statsAfterCache = _resolver.GetStatistics();

            // Act
            _resolver.ClearCache();
            var statsAfterClear = _resolver.GetStatistics();

            // Assert
            Assert.Greater(statsAfterCache.CachedPaths, 0);
            Assert.AreEqual(0, statsAfterClear.CachedPaths);
        }

        #endregion

        #region Error Handling Tests

        [Test]
        public void ResolveResourcePath_NullResourceId_ReturnsNull()
        {
            // Act
            var result = _resolver.ResolveResourcePath(ResourceType.Actor, null, ResourceCategory.Sprites);

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void ResolveResourcePath_EmptyResourceId_ReturnsNull()
        {
            // Act
            var result = _resolver.ResolveResourcePath(ResourceType.Actor, "", ResourceCategory.Sprites);

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void ResolveResourcePathDetailed_InvalidTemplate_ReturnsFailure()
        {
            // Act - Try to resolve with a resource type that has no template
            var result = _resolver.ResolveResourcePathDetailed(ResourceType.Config, "test", ResourceCategory.Effects);

            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotNull(result.ErrorMessage);
            Assert.IsTrue(result.ErrorMessage.Contains("No template found"));
        }

        [Test]
        public void ResolveResourcePath_DisposedResolver_ReturnsNull()
        {
            // Arrange
            _resolver.Dispose();

            // Act
            var result = _resolver.ResolveResourcePath(ResourceType.Actor, "test", ResourceCategory.Sprites);

            // Assert
            Assert.IsNull(result);
        }

        #endregion

        #region Fallback Tests

        [Test]
        public void ResolveResourcePath_UseFallback_ReturnsDefaultPath()
        {
            // Arrange - Use a template that will trigger fallback
            var parameters = new[]
            {
                new PathParameter(PathParameterNames.ACTOR_TYPE, "Character"),
                new PathParameter(PathParameterNames.APPEARANCE, "missing")
            };

            // Act
            var result = _resolver.ResolveResourcePath(ResourceType.Actor, "missing_actor", ResourceCategory.Sprites, parameters);

            // Assert
            Assert.IsNotNull(result);
            // Should get fallback path from our test configuration
            Assert.IsTrue(result.Contains("Default") || result.Contains("Missing"));
        }

        [Test]
        public void GetFallbackPaths_ValidResource_ReturnsOrderedFallbacks()
        {
            // Act
            var fallbacks = _resolver.GetFallbackPaths(ResourceType.Actor, "test_actor", ResourceCategory.Sprites);

            // Assert
            Assert.IsNotNull(fallbacks);
            Assert.Greater(fallbacks.Length, 0);
            Assert.IsTrue(fallbacks[0].Contains("Default"));
        }

        [Test]
        public void GetFallbackPathsWithPriority_ValidResource_ReturnsCorrectPriorities()
        {
            // Act
            var fallbacksWithPriority = _resolver.GetFallbackPathsWithPriority(ResourceType.Actor, "test_actor", ResourceCategory.Sprites);

            // Assert
            Assert.IsNotNull(fallbacksWithPriority);
            Assert.Greater(fallbacksWithPriority.Count, 0);
            
            // Check that all entries have valid priorities
            foreach (var entry in fallbacksWithPriority)
            {
                Assert.IsNotNull(entry.Key);
                Assert.IsTrue(Enum.IsDefined(typeof(PathPriority), entry.Value));
            }
        }

        #endregion

        #region Parameter Substitution Tests

        [Test]
        public void ResolveResourcePath_MultipleParameters_SubstitutesCorrectly()
        {
            // Arrange
            var parameters = new[]
            {
                new PathParameter(PathParameterNames.ACTOR_TYPE, "Character"),
                new PathParameter(PathParameterNames.APPEARANCE, "happy"),
                new PathParameter("emotion", "joy")
            };

            // Act
            var result = _resolver.ResolveResourcePath(ResourceType.Actor, "protagonist", ResourceCategory.Sprites, parameters);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("Character"));
            Assert.IsTrue(result.Contains("happy"));
            Assert.IsTrue(result.Contains("protagonist"));
        }

        [Test]
        public void ResolveResourcePath_MissingParameter_KeepsPlaceholder()
        {
            // Arrange - Use a template that requires a parameter we don't provide
            var parameters = new[]
            {
                new PathParameter(PathParameterNames.ACTOR_TYPE, "Character")
                // Missing appearance parameter
            };

            // Act
            var result = _resolver.ResolveResourcePath(ResourceType.Actor, "protagonist", ResourceCategory.Sprites, parameters);

            // Assert
            Assert.IsNotNull(result);
            // Should still work but with placeholder for missing parameter
            Assert.IsTrue(result.Contains("Character"));
            Assert.IsTrue(result.Contains("protagonist"));
        }

        #endregion

        #region Template Management Tests

        [Test]
        public void RegisterPathTemplate_NewTemplate_AddsSuccessfully()
        {
            // Act
            _resolver.RegisterPathTemplate(ResourceType.Config, ResourceCategory.Primary, "Configs/{configType}/{configName}", PathPriority.Normal);
            var template = _resolver.GetPathTemplate(ResourceType.Config, ResourceCategory.Primary);

            // Assert
            Assert.IsNotNull(template);
            Assert.AreEqual("Configs/{configType}/{configName}", template);
        }

        [Test]
        public void UnregisterPathTemplate_ExistingTemplate_RemovesSuccessfully()
        {
            // Arrange
            _resolver.RegisterPathTemplate(ResourceType.Config, ResourceCategory.Primary, "Configs/{configType}/{configName}", PathPriority.Normal);

            // Act
            var removed = _resolver.UnregisterPathTemplate(ResourceType.Config, ResourceCategory.Primary);
            var template = _resolver.GetPathTemplate(ResourceType.Config, ResourceCategory.Primary);

            // Assert
            Assert.IsTrue(removed);
            Assert.IsNull(template);
        }

        [Test]
        public void GetAllPathTemplates_AfterSetup_ReturnsAllTemplates()
        {
            // Act
            var allTemplates = _resolver.GetAllPathTemplates();

            // Assert
            Assert.IsNotNull(allTemplates);
            Assert.Greater(allTemplates.Count, 0);
            
            // Check for expected templates from our test configuration
            Assert.IsTrue(allTemplates.ContainsKey((ResourceType.Actor, ResourceCategory.Sprites)));
            Assert.IsTrue(allTemplates.ContainsKey((ResourceType.Script, ResourceCategory.Source)));
        }

        #endregion

        #region Environment Tests

        [Test]
        public void SetResourceEnvironment_ChangesEnvironment_ClearsCacheAndUpdatesContext()
        {
            // Arrange
            var originalEnvironment = _resolver.GetResourceEnvironment();
            
            // Cache something first
            _resolver.ResolveResourcePath(ResourceType.Actor, "test", ResourceCategory.Sprites);
            var statsBeforeChange = _resolver.GetStatistics();

            // Act
            _resolver.SetResourceEnvironment(ResourceEnvironment.Production);
            var newEnvironment = _resolver.GetResourceEnvironment();
            var statsAfterChange = _resolver.GetStatistics();

            // Assert
            Assert.AreNotEqual(originalEnvironment, newEnvironment);
            Assert.AreEqual(ResourceEnvironment.Production, newEnvironment);
            Assert.Greater(statsBeforeChange.CachedPaths, 0);
            Assert.AreEqual(0, statsAfterChange.CachedPaths); // Cache should be cleared
        }

        [Test]
        public void WithEnvironmentOverride_TemporaryEnvironment_RestoresOriginal()
        {
            // Arrange
            var originalEnvironment = _resolver.GetResourceEnvironment();

            // Act
            var result = _resolver.WithEnvironmentOverride(ResourceEnvironment.Production, () =>
            {
                var currentEnvironment = _resolver.GetResourceEnvironment();
                return currentEnvironment;
            });

            var finalEnvironment = _resolver.GetResourceEnvironment();

            // Assert
            Assert.AreEqual(ResourceEnvironment.Production, result);
            Assert.AreEqual(originalEnvironment, finalEnvironment);
        }

        #endregion

        #region Path Validation Tests

        [Test]
        public void ValidateResourcePath_ValidPath_ReturnsTrue()
        {
            // Act
            var isValid = _resolver.ValidateResourcePath("Actors/Character/Sprites/hero/happy", out var corrected);

            // Assert
            Assert.IsTrue(isValid);
            Assert.AreEqual("Actors/Character/Sprites/hero/happy", corrected);
        }

        [Test]
        public void ValidateResourcePath_PathWithBackslashes_CorrectsThem()
        {
            // Act
            var isValid = _resolver.ValidateResourcePath("Actors\\Character\\Sprites\\hero\\happy", out var corrected);

            // Assert
            Assert.IsFalse(isValid); // Not valid due to backslashes
            Assert.AreEqual("Actors/Character/Sprites/hero/happy", corrected);
        }

        [Test]
        public void ValidateResourcePath_PathWithDoubleSlashes_CorrectsThem()
        {
            // Act
            var isValid = _resolver.ValidateResourcePath("Actors//Character//Sprites//hero//happy", out var corrected);

            // Assert
            Assert.IsFalse(isValid); // Not valid due to double slashes
            Assert.AreEqual("Actors/Character/Sprites/hero/happy", corrected);
        }

        [Test]
        public void ValidateResourcePaths_MultiplePaths_ReturnsCorrectResults()
        {
            // Arrange
            var paths = new[]
            {
                "Valid/Path/Here",
                "Invalid\\Path\\Here",
                "Another//Invalid//Path",
                "Another/Valid/Path"
            };

            // Act
            var results = _resolver.ValidateResourcePaths(paths);

            // Assert
            Assert.AreEqual(4, results.Count);
            Assert.IsTrue(results["Valid/Path/Here"]);
            Assert.IsFalse(results["Invalid\\Path\\Here"]);
            Assert.IsFalse(results["Another//Invalid//Path"]);
            Assert.IsTrue(results["Another/Valid/Path"]);
        }

        #endregion

        #region Async Tests

        [Test]
        public async UniTask ResolveResourcePathAsync_ValidPath_ReturnsSuccessfully()
        {
            // Arrange
            var parameters = new[]
            {
                new PathParameter(PathParameterNames.ACTOR_TYPE, "Character"),
                new PathParameter(PathParameterNames.APPEARANCE, "happy")
            };

            // Act
            var result = await _resolver.ResolveResourcePathAsync(ResourceType.Actor, "protagonist", ResourceCategory.Sprites, false, CancellationToken.None, parameters);

            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("Actors/Character/Sprites/protagonist/happy", result.ResolvedPath);
        }

        [Test]
        public async UniTask ValidateResourceExistsAsync_ValidPath_ReturnsTrue()
        {
            // Act
            var exists = await _resolver.ValidateResourceExistsAsync("Actors/Character/Sprites/hero/happy");

            // Assert
            Assert.IsTrue(exists); // Should return true since existence checking is disabled in test config
        }

        [Test]
        public async UniTask PreloadPathsAsync_ValidSpecs_PreloadsSuccessfully()
        {
            // Arrange
            var resourceSpecs = new[]
            {
                (ResourceType.Actor, "hero", ResourceCategory.Sprites),
                (ResourceType.Actor, "villain", ResourceCategory.Sprites),
                (ResourceType.Script, "intro", ResourceCategory.Source)
            };

            var statsBefore = _resolver.GetStatistics();

            // Act
            await _resolver.PreloadPathsAsync(resourceSpecs);
            var statsAfter = _resolver.GetStatistics();

            // Assert
            Assert.Greater(statsAfter.TotalResolutions, statsBefore.TotalResolutions);
            Assert.Greater(statsAfter.CachedPaths, statsBefore.CachedPaths);
        }

        #endregion

        #region Memory Pressure Tests

        [Test]
        public void OnMemoryPressure_HighPressure_ClearsCacheCompletely()
        {
            // Arrange
            _resolver.ResolveResourcePath(ResourceType.Actor, "test1", ResourceCategory.Sprites);
            _resolver.ResolveResourcePath(ResourceType.Actor, "test2", ResourceCategory.Sprites);
            var statsBefore = _resolver.GetStatistics();

            // Act
            _resolver.OnMemoryPressure(0.9f); // High pressure
            var statsAfter = _resolver.GetStatistics();

            // Assert
            Assert.Greater(statsBefore.CachedPaths, 0);
            Assert.AreEqual(0, statsAfter.CachedPaths);
        }

        [Test]
        public void OnMemoryPressure_MediumPressure_OptimizesCache()
        {
            // Arrange
            for (int i = 0; i < 50; i++)
            {
                _resolver.ResolveResourcePath(ResourceType.Actor, $"test{i}", ResourceCategory.Sprites);
            }
            var statsBefore = _resolver.GetStatistics();

            // Act
            _resolver.OnMemoryPressure(0.7f); // Medium pressure
            var statsAfter = _resolver.GetStatistics();

            // Assert
            Assert.Greater(statsBefore.CachedPaths, 0);
            // Should reduce cache size but not clear completely
            Assert.LessOrEqual(statsAfter.CachedPaths, statsBefore.CachedPaths);
        }

        #endregion

        #region Configuration Tests

        [Test]
        public void ValidateConfiguration_ValidConfig_ReturnsTrue()
        {
            // Act
            var isValid = _resolver.ValidateConfiguration(out var errors);

            // Assert
            Assert.IsTrue(isValid);
            Assert.IsEmpty(errors);
        }

        [Test]
        public async UniTask ReloadConfigurationAsync_ValidConfig_ReloadsSuccessfully()
        {
            // Arrange
            bool configReloaded = false;
            _resolver.OnConfigurationReloaded += (config) => configReloaded = true;

            // Act
            await _resolver.ReloadConfigurationAsync();

            // Assert
            Assert.IsTrue(configReloaded);
        }

        [Test]
        public void GetCurrentConfiguration_ReturnsCorrectConfig()
        {
            // Act
            var currentConfig = _resolver.GetCurrentConfiguration();

            // Assert
            Assert.IsNotNull(currentConfig);
            Assert.AreSame(_config, currentConfig);
        }

        #endregion

        #region Health Check Tests

        [Test]
        public async UniTask HealthCheckAsync_HealthyResolver_ReturnsHealthy()
        {
            // Act
            var healthStatus = await _resolver.HealthCheckAsync();

            // Assert
            Assert.IsNotNull(healthStatus);
            Assert.IsTrue(healthStatus.IsHealthy);
        }

        #endregion

        #region Statistics Tests

        [Test]
        public void GetStatistics_AfterResolutions_ReturnsAccurateStats()
        {
            // Arrange
            var parameters = new[]
            {
                new PathParameter(PathParameterNames.ACTOR_TYPE, "Character"),
                new PathParameter(PathParameterNames.APPEARANCE, "happy")
            };

            // Act
            for (int i = 0; i < 10; i++)
            {
                _resolver.ResolveResourcePath(ResourceType.Actor, $"actor{i}", ResourceCategory.Sprites, parameters);
            }

            var stats = _resolver.GetStatistics();

            // Assert
            Assert.IsNotNull(stats);
            Assert.AreEqual(10, stats.TotalResolutions);
            Assert.Greater(stats.AverageResolutionTime.TotalMilliseconds, 0);
            Assert.AreEqual(10, stats.CachedPaths); // All should be cached
        }

        [Test]
        public void GetStringInternStatistics_AfterResolutions_ReturnsStats()
        {
            // Arrange
            for (int i = 0; i < 20; i++)
            {
                // Use repeated strings to trigger interning
                _resolver.ResolveResourcePath(ResourceType.Actor, "repeated_actor", ResourceCategory.Sprites);
            }

            // Act
            var internStats = _resolver.GetStringInternStatistics();

            // Assert
            Assert.IsNotNull(internStats);
            Assert.Greater(internStats.PoolSize, 0);
        }

        #endregion

        #region Helper Methods

        private PathTemplateEntry[] CreateTestPathTemplates()
        {
            return new[]
            {
                // Actor templates
                new PathTemplateEntry(ResourceType.Actor, ResourceCategory.Sprites, "Actors/{actorType}/Sprites/{actorId}/{appearance}", PathPriority.High),
                new PathTemplateEntry(ResourceType.Actor, ResourceCategory.Audio, "Actors/{actorType}/Audio/{actorId}/{emotion}", PathPriority.High),
                new PathTemplateEntry(ResourceType.Actor, ResourceCategory.Animations, "Actors/{actorType}/Animations/{actorId}/{animationType}", PathPriority.High),
                
                // Script templates
                new PathTemplateEntry(ResourceType.Script, ResourceCategory.Source, "Scripts/{scriptName}.script", PathPriority.High),
                
                // Audio templates
                new PathTemplateEntry(ResourceType.Audio, ResourceCategory.Music, "Audio/Music/{category}/{trackName}", PathPriority.High),
                new PathTemplateEntry(ResourceType.Audio, ResourceCategory.Primary, "Audio/SFX/{category}/{soundName}", PathPriority.High),
                
                // UI templates
                new PathTemplateEntry(ResourceType.UI, ResourceCategory.Primary, "UI/{category}/{elementName}", PathPriority.High),
                
                // Prefab templates
                new PathTemplateEntry(ResourceType.Prefab, ResourceCategory.Primary, "Prefabs/{actorType}/{actorType}ActorPrefab", PathPriority.High)
            };
        }

        private FallbackPathEntry[] CreateTestFallbackPaths()
        {
            return new[]
            {
                new FallbackPathEntry(ResourceType.Actor, ResourceCategory.Sprites, "Actors/Default/Sprites/MissingSprite", PathPriority.Low),
                new FallbackPathEntry(ResourceType.Actor, ResourceCategory.Audio, "Actors/Default/Audio/MissingAudio", PathPriority.Low),
                new FallbackPathEntry(ResourceType.Script, ResourceCategory.Source, "Scripts/Default/EmptyScript.script", PathPriority.Low),
                new FallbackPathEntry(ResourceType.Audio, ResourceCategory.Primary, "Audio/Default/Silence", PathPriority.Low),
                new FallbackPathEntry(ResourceType.UI, ResourceCategory.Primary, "UI/Default/MissingUI", PathPriority.Low)
            };
        }

        #endregion
    }
}