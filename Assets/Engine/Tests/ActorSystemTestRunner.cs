using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using Sinkii09.Engine.Services;

namespace Sinkii09.Engine.Tests
{
    /// <summary>
    /// Comprehensive test runner for the Actor System
    /// Tests creation, animation, state management, and performance
    /// </summary>
    public class ActorSystemTestRunner : MonoBehaviour
    {
        #region Configuration
        
        [Header("Test Configuration")]
        [SerializeField] private bool _runTestsOnStart = true;
        [SerializeField] private bool _enableDetailedLogging = true;
        [SerializeField] private float _testStepDelay = 2f;
        
        [Header("Actor Creation Tests")]
        [SerializeField] private int _characterActorsToCreate = 3;
        [SerializeField] private int _backgroundActorsToCreate = 2;
        [SerializeField] private Vector3[] _characterPositions = {
            new Vector3(-3f, 0f, 0f),
            new Vector3(0f, 0f, 0f), 
            new Vector3(3f, 0f, 0f)
        };
        
        [Header("Animation Tests")]
        [SerializeField] private bool _testAnimations = true;
        [SerializeField] private bool _testTransitions = true;
        [SerializeField] private float _animationDuration = 1.5f;
        
        [Header("Performance Tests")]
        [SerializeField] private bool _testPerformance = true;
        [SerializeField] private int _performanceTestActorCount = 10;
        
        #endregion
        
        #region Private Fields
        
        private IActorService _actorService;
        private CancellationTokenSource _testCts;
        private List<ICharacterActor> _testCharacters = new();
        private List<IBackgroundActor> _testBackgrounds = new();
        private bool _testsRunning = false;
        
        #endregion
        
        #region Unity Lifecycle
        
        private async void Start()
        {
            if (_runTestsOnStart)
            {
                await UniTask.DelayFrame(10); // Wait for engine initialization
                await RunAllTestsAsync();
            }
        }
        
        private void OnDestroy()
        {
            _testCts?.Cancel();
            _testCts?.Dispose();
        }
        
        #endregion
        
        #region Public Test Interface
        
        [ContextMenu("Run All Tests")]
        public async void RunAllTestsFromMenu()
        {
            await RunAllTestsAsync();
        }
        
        [ContextMenu("Test Actor Creation")]
        public async void TestActorCreationFromMenu()
        {
            await TestActorCreationAsync();
        }
        
        [ContextMenu("Test Animations")]
        public async void TestAnimationsFromMenu()
        {
            await TestAnimationsAsync();
        }
        
        [ContextMenu("Test Performance")]
        public async void TestPerformanceFromMenu()
        {
            await TestPerformanceAsync();
        }
        
        [ContextMenu("Cleanup Test Actors")]
        public async void CleanupTestActorsFromMenu()
        {
            await CleanupTestActorsAsync();
        }
        
        #endregion
        
        #region Main Test Runner
        
        public async UniTask RunAllTestsAsync()
        {
            if (_testsRunning)
            {
                Log("Tests already running, skipping...");
                return;
            }
            
            _testsRunning = true;
            _testCts = new CancellationTokenSource();
            
            try
            {
                Log("🎭 Starting Actor System Comprehensive Tests");
                
                // Initialize Actor Service
                if (!await InitializeActorServiceAsync())
                {
                    LogError("Failed to initialize Actor Service - aborting tests");
                    return;
                }
                
                // Test 1: Service Health Check
                await TestServiceHealthAsync();
                await DelayBetweenTests();
                
                // Test 2: Actor Creation
                await TestActorCreationAsync();
                await DelayBetweenTests();
                
                // Test 3: Actor Lookup and Registry
                await TestActorLookupAsync();
                await DelayBetweenTests();
                
                // Test 4: Animation System
                if (_testAnimations)
                {
                    await TestAnimationsAsync();
                    await DelayBetweenTests();
                }
                
                // Test 5: Background Transitions
                if (_testTransitions)
                {
                    await TestBackgroundTransitionsAsync();
                    await DelayBetweenTests();
                }
                
                // Test 6: State Management
                await TestStateManagementAsync();
                await DelayBetweenTests();
                
                // Test 7: Performance Tests
                if (_testPerformance)
                {
                    await TestPerformanceAsync();
                    await DelayBetweenTests();
                }
                
                // Test 8: Error Handling
                await TestErrorHandlingAsync();
                await DelayBetweenTests();
                
                // Final cleanup
                await CleanupTestActorsAsync();
                
                Log("✅ All Actor System tests completed successfully!");
                
                // Display final statistics
                DisplayFinalStatistics();
            }
            catch (OperationCanceledException)
            {
                Log("🚫 Tests cancelled");
            }
            catch (Exception ex)
            {
                LogError($"❌ Test suite failed: {ex.Message}");
                LogError($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                _testsRunning = false;
                _testCts?.Dispose();
                _testCts = null;
            }
        }
        
        #endregion
        
        #region Individual Test Methods
        
        private async UniTask<bool> InitializeActorServiceAsync()
        {
            try
            {
                Log("🔧 Initializing Actor Service...");
                
                // Check if Engine is initialized
                if (!Engine.Initialized)
                {
                    LogError("Engine not initialized - waiting for initialization...");
                    
                    // Wait for engine initialization (with timeout)
                    var timeout = 10f;
                    var elapsed = 0f;
                    while (!Engine.Initialized && elapsed < timeout)
                    {
                        await UniTask.DelayFrame(1);
                        elapsed += Time.deltaTime;
                    }
                    
                    if (!Engine.Initialized)
                    {
                        LogError($"Engine failed to initialize within {timeout} seconds");
                        return false;
                    }
                }
                
                // Get Actor Service
                _actorService = Engine.GetService<IActorService>();
                if (_actorService == null)
                {
                    LogError("Actor Service not available in Engine");
                    return false;
                }
                
                Log($"✅ Actor Service initialized: {_actorService.GetType().Name}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to initialize Actor Service: {ex.Message}");
                return false;
            }
        }
        
        private async UniTask TestServiceHealthAsync()
        {
            Log("🏥 Testing Actor Service Health...");
            
            try
            {
                var healthStatus = await _actorService.HealthCheckAsync(_testCts.Token);
                Log($"Service Health: {(healthStatus.IsHealthy ? "Healthy" : "Unhealthy")} - {healthStatus.StatusMessage}");
                
                // Test basic properties
                Log($"Current Actor Count: {_actorService.ActorCount}");
                Log($"Loading Actors Count: {_actorService.LoadingActorsCount}");
                
                // Get statistics
                var stats = _actorService.GetStatistics();
                Log($"Service Statistics: {stats.GetSummary()}");
                
                Log("✅ Service health check completed");
            }
            catch (Exception ex)
            {
                LogError($"Service health check failed: {ex.Message}");
            }
        }
        
        private async UniTask TestActorCreationAsync()
        {
            Log("🎭 Testing Actor Creation...");
            
            try
            {
                // Test Character Actor Creation
                for (int i = 0; i < _characterActorsToCreate; i++)
                {
                    var characterId = $"TestCharacter_{i}";
                    var appearance = new CharacterAppearance
                    {
                        Expression = CharacterExpression.Neutral, 
                        Pose = CharacterPose.Standing, 
                        OutfitId = 0
                    };
                    
                    var position = i < _characterPositions.Length ? _characterPositions[i] : Vector3.zero;
                    
                    Log($"Creating character: {characterId} at position {position}");
                    
                    var character = await _actorService.CreateCharacterActorAsync(
                        characterId, 
                        appearance, 
                        position, 
                        _testCts.Token
                    );
                    
                    if (character != null)
                    {
                        _testCharacters.Add(character);
                        Log($"✅ Created character: {characterId} - {character.DisplayName}");
                    }
                    else
                    {
                        LogError($"Failed to create character: {characterId}");
                    }
                }
                
                // Test Background Actor Creation
                for (int i = 0; i < _backgroundActorsToCreate; i++)
                {
                    var backgroundId = $"TestBackground_{i}";
                    var appearance = new BackgroundAppearance(
                        location: (i == 0) ? SceneLocation.Classroom : SceneLocation.Library,
                        timeOfDay: TimeOfDay.Day,
                        weather: Weather.Clear,
                        variantId: BackgroundVariant.Standard
                    );
                    
                    Log($"Creating background: {backgroundId}");
                    
                    var background = await _actorService.CreateBackgroundActorAsync(
                        backgroundId,
                        appearance,
                        Vector3.zero,
                        _testCts.Token
                    );
                    
                    if (background != null)
                    {
                        _testBackgrounds.Add(background);
                        Log($"✅ Created background: {backgroundId} - {background.DisplayName}");
                        
                        // Set first background as main
                        if (i == 0)
                        {
                            await _actorService.SetMainBackgroundAsync(backgroundId, _testCts.Token);
                            Log($"Set {backgroundId} as main background");
                        }
                    }
                    else
                    {
                        LogError($"Failed to create background: {backgroundId}");
                    }
                }
                
                Log($"✅ Actor creation test completed - Created {_testCharacters.Count} characters, {_testBackgrounds.Count} backgrounds");
            }
            catch (Exception ex)
            {
                LogError($"Actor creation test failed: {ex.Message}");
            }
        }
        
        private async UniTask TestActorLookupAsync()
        {
            Log("🔍 Testing Actor Lookup and Registry...");
            
            try
            {
                // Test individual lookups
                foreach (var character in _testCharacters)
                {
                    var foundCharacter = _actorService.GetCharacterActor(character.Id);
                    if (foundCharacter != null)
                    {
                        Log($"✅ Found character: {character.Id}");
                    }
                    else
                    {
                        LogError($"❌ Failed to find character: {character.Id}");
                    }
                }
                
                // Test collections
                var allActors = _actorService.AllActors;
                var characterActors = _actorService.CharacterActors;
                var backgroundActors = _actorService.BackgroundActors;
                
                Log($"Registry totals - All: {allActors.Count}, Characters: {characterActors.Count}, Backgrounds: {backgroundActors.Count}");
                
                // Test TryGet methods
                if (_testCharacters.Count > 0)
                {
                    var testId = _testCharacters[0].Id;
                    if (_actorService.TryGetCharacterActor(testId, out var testCharacter))
                    {
                        Log($"✅ TryGetCharacterActor succeeded for: {testId}");
                    }
                    else
                    {
                        LogError($"❌ TryGetCharacterActor failed for: {testId}");
                    }
                }
                
                // Test HasActor
                foreach (var character in _testCharacters)
                {
                    if (_actorService.HasActor(character.Id))
                    {
                        Log($"✅ HasActor confirmed for: {character.Id}");
                    }
                    else
                    {
                        LogError($"❌ HasActor failed for: {character.Id}");
                    }
                }
                
                Log("✅ Actor lookup test completed");
            }
            catch (Exception ex)
            {
                LogError($"Actor lookup test failed: {ex.Message}");
            }
            await UniTask.CompletedTask;
        }
        
        private async UniTask TestAnimationsAsync()
        {
            Log("🎬 Testing Animation System...");
            
            try
            {
                if (_testCharacters.Count == 0)
                {
                    Log("No characters available for animation testing");
                    return;
                }
                
                // Test basic position animation
                Log("Testing position animations...");
                var tasks = new List<UniTask>();
                
                for (int i = 0; i < _testCharacters.Count; i++)
                {
                    var character = _testCharacters[i];
                    var newPosition = new Vector3(
                        UnityEngine.Random.Range(-5f, 5f),
                        0f,
                        UnityEngine.Random.Range(-2f, 2f)
                    );
                    
                    tasks.Add(character.ChangePositionAsync(newPosition, _animationDuration, DG.Tweening.Ease.OutQuad, _testCts.Token));
                }
                
                await UniTask.WhenAll(tasks);
                Log("✅ Position animations completed");
                
                await UniTask.Delay(500, cancellationToken: _testCts.Token);
                
                // Test visibility animations
                Log("Testing visibility animations...");
                tasks.Clear();
                
                foreach (var character in _testCharacters)
                {
                    tasks.Add(character.ChangeVisibilityAsync(false, _animationDuration, cancellationToken: _testCts.Token));
                }
                
                await UniTask.WhenAll(tasks);
                Log("✅ Hide animations completed");
                
                await UniTask.Delay(1000, cancellationToken: _testCts.Token);
                
                // Show them again
                tasks.Clear();
                foreach (var character in _testCharacters)
                {
                    tasks.Add(character.ChangeVisibilityAsync(true, _animationDuration, cancellationToken: _testCts.Token));
                }
                
                await UniTask.WhenAll(tasks);
                Log("✅ Show animations completed");
                
                // Test appearance changes
                if (_testCharacters.Count > 0)
                {
                    Log("Testing appearance changes...");
                    var character = _testCharacters[0];
                    var newAppearance = new CharacterAppearance(
                        expression: CharacterExpression.Happy,
                        pose: CharacterPose.Waving,
                        outfitId: 1
                    );
                    
                    await character.ChangeAppearanceAsync(newAppearance, _animationDuration, _testCts.Token);
                    Log("✅ Appearance change completed");
                }
                
                Log("✅ Animation system test completed");
            }
            catch (Exception ex)
            {
                LogError($"Animation test failed: {ex.Message}");
            }
        }
        
        private async UniTask TestBackgroundTransitionsAsync()
        {
            Log("🌄 Testing Background Transitions...");
            
            try
            {
                if (_testBackgrounds.Count < 2)
                {
                    Log("Need at least 2 backgrounds for transition testing");
                    return;
                }
                
                var background = _testBackgrounds[0];
                
                // Test different transition types
                var transitionTypes = new[] { 
                    TransitionType.Fade, 
                    TransitionType.Slide, 
                    TransitionType.Dissolve 
                };
                
                var appearances = new[]
                {
                    new BackgroundAppearance(SceneLocation.Library, TimeOfDay.Day, Weather.Clear, BackgroundVariant.Standard),
                    new BackgroundAppearance(SceneLocation.Cafeteria, TimeOfDay.Evening, Weather.Cloudy, BackgroundVariant.Alternate),
                    new BackgroundAppearance(SceneLocation.Classroom, TimeOfDay.Night, Weather.Clear, BackgroundVariant.Standard)
                };
                
                for (int i = 0; i < transitionTypes.Length && i < appearances.Length; i++)
                {
                    Log($"Testing {transitionTypes[i]} transition to {appearances[i].Location}...");
                    
                    var config = new BackgroundTransitionConfig
                    {
                        Type = transitionTypes[i],
                        Duration = _animationDuration,
                        Ease = DG.Tweening.Ease.InOutQuad
                    };
                    
                    await background.TransitionToAsync(appearances[i], config, _testCts.Token);
                    Log($"✅ {transitionTypes[i]} transition completed");
                    
                    await UniTask.Delay(500, cancellationToken: _testCts.Token);
                }
                
                Log("✅ Background transition test completed");
            }
            catch (Exception ex)
            {
                LogError($"Background transition test failed: {ex.Message}");
            }
        }
        
        private async UniTask TestStateManagementAsync()
        {
            Log("💾 Testing State Management...");
            
            try
            {
                if (_testCharacters.Count == 0)
                {
                    Log("No characters available for state testing");
                    return;
                }
                
                var character = _testCharacters[0];
                
                // Capture current state
                var originalState = character.GetState();
                Log($"Captured state for: {character.Id}");
                
                // Modify character
                await character.ChangePositionAsync(new Vector3(10f, 5f, 0f), 0.5f, cancellationToken: _testCts.Token);
                await character.ChangeAlphaAsync(0.5f, 0.5f, cancellationToken: _testCts.Token);
                
                Log("Modified character properties");
                
                // Wait a moment
                await UniTask.Delay(1000, cancellationToken: _testCts.Token);
                
                // Restore original state
                await character.ApplyStateAsync(originalState, _animationDuration, _testCts.Token);
                Log("✅ Restored original state");
                
                // Test all actor states
                var allStates = _actorService.GetAllActorStates();
                Log($"Captured {allStates.Count} actor states");
                
                Log("✅ State management test completed");
            }
            catch (Exception ex)
            {
                LogError($"State management test failed: {ex.Message}");
            }
        }
        
        private async UniTask TestPerformanceAsync()
        {
            Log($"⚡ Testing Performance with {_performanceTestActorCount} actors...");
            
            var performanceActors = new List<ICharacterActor>();
            
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                // Create many actors quickly
                var creationTasks = new List<UniTask<ICharacterActor>>();
                
                for (int i = 0; i < _performanceTestActorCount; i++)
                {
                    var appearance = new CharacterAppearance(
                        expression: (CharacterExpression)(i % 4),
                        pose: (CharacterPose)(i % 3),
                        outfitId: 0
                    );
                    
                    var position = new Vector3(
                        UnityEngine.Random.Range(-10f, 10f),
                        0f,
                        UnityEngine.Random.Range(-5f, 5f)
                    );
                    
                    creationTasks.Add(_actorService.CreateCharacterActorAsync(
                        $"PerfTest_{i}",
                        appearance,
                        position,
                        _testCts.Token
                    ));
                }
                
                var results = await UniTask.WhenAll(creationTasks);
                stopwatch.Stop();
                
                foreach (var actor in results)
                {
                    if (actor != null)
                        performanceActors.Add(actor);
                }
                
                Log($"✅ Created {performanceActors.Count} actors in {stopwatch.ElapsedMilliseconds}ms");
                Log($"Average: {stopwatch.ElapsedMilliseconds / (float)performanceActors.Count:F2}ms per actor");
                
                // Test concurrent animations
                stopwatch.Restart();
                var animationTasks = new List<UniTask>();
                
                foreach (var actor in performanceActors)
                {
                    var newPos = new Vector3(
                        UnityEngine.Random.Range(-15f, 15f),
                        0f,
                        UnityEngine.Random.Range(-8f, 8f)
                    );
                    
                    animationTasks.Add(actor.ChangePositionAsync(newPos, 2f, cancellationToken: _testCts.Token));
                }
                
                await UniTask.WhenAll(animationTasks);
                stopwatch.Stop();
                
                Log($"✅ Animated {performanceActors.Count} actors concurrently in {stopwatch.ElapsedMilliseconds}ms");
                
                // Cleanup performance test actors
                Log("Cleaning up performance test actors...");
                
                foreach (var actor in performanceActors)
                {
                    await _actorService.DestroyActorAsync(actor.Id, _testCts.Token);
                }
                
                Log($"✅ Performance test completed and cleaned up");
            }
            catch (Exception ex)
            {
                LogError($"Performance test failed: {ex.Message}");
                
                // Cleanup on failure
                foreach (var actor in performanceActors)
                {
                    try
                    {
                        await _actorService.DestroyActorAsync(actor.Id, _testCts.Token);
                    }
                    catch { /* Ignore cleanup errors */ }
                }
            }
        }
        
        private async UniTask TestErrorHandlingAsync()
        {
            Log("🚨 Testing Error Handling...");
            
            try
            {
                // Test invalid actor creation
                try
                {
                    await _actorService.CreateCharacterActorAsync("", new CharacterAppearance(CharacterExpression.Neutral), Vector3.zero, _testCts.Token);
                    LogError("Should have failed with empty ID");
                }
                catch (ArgumentException)
                {
                    Log("✅ Correctly rejected empty actor ID");
                }
                
                // Test duplicate actor creation
                if (_testCharacters.Count > 0)
                {
                    try
                    {
                        var existingId = _testCharacters[0].Id;
                        await _actorService.CreateCharacterActorAsync(existingId, new CharacterAppearance(CharacterExpression.Neutral), Vector3.zero, _testCts.Token);
                        LogError("Should have failed with duplicate ID");
                    }
                    catch (ArgumentException)
                    {
                        Log("✅ Correctly rejected duplicate actor ID");
                    }
                }
                
                // Test invalid actor lookup
                var invalidActor = _actorService.GetCharacterActor("NonExistentActor");
                if (invalidActor == null)
                {
                    Log("✅ Correctly returned null for non-existent actor");
                }
                else
                {
                    LogError("Should have returned null for non-existent actor");
                }
                
                Log("✅ Error handling test completed");
            }
            catch (Exception ex)
            {
                LogError($"Error handling test failed: {ex.Message}");
            }
        }
        
        private async UniTask CleanupTestActorsAsync()
        {
            Log("🧹 Cleaning up test actors...");
            
            try
            {
                // Destroy test characters
                foreach (var character in _testCharacters)
                {
                    try
                    {
                        await _actorService.DestroyActorAsync(character.Id, _testCts.Token);
                        Log($"Destroyed character: {character.Id}");
                    }
                    catch (Exception ex)
                    {
                        LogError($"Failed to destroy character {character.Id}: {ex.Message}");
                    }
                }
                
                // Destroy test backgrounds
                foreach (var background in _testBackgrounds)
                {
                    try
                    {
                        await _actorService.DestroyActorAsync(background.Id, _testCts.Token);
                        Log($"Destroyed background: {background.Id}");
                    }
                    catch (Exception ex)
                    {
                        LogError($"Failed to destroy background {background.Id}: {ex.Message}");
                    }
                }
                
                _testCharacters.Clear();
                _testBackgrounds.Clear();
                
                Log("✅ Test actor cleanup completed");
            }
            catch (Exception ex)
            {
                LogError($"Cleanup failed: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Helper Methods
        
        private async UniTask DelayBetweenTests()
        {
            if (_testStepDelay > 0)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_testStepDelay), cancellationToken: _testCts.Token);
            }
        }
        
        private void DisplayFinalStatistics()
        {
            if (_actorService == null) return;
            
            try
            {
                var stats = _actorService.GetStatistics();
                var debugInfo = _actorService.GetDebugInfo();
                
                Log("📊 Final Actor Service Statistics:");
                Log(stats.GetDetailedReport());
                Log("\n🔍 Debug Information:");
                Log(debugInfo);
            }
            catch (Exception ex)
            {
                LogError($"Failed to get final statistics: {ex.Message}");
            }
        }
        
        private void Log(string message)
        {
            if (_enableDetailedLogging)
            {
                Debug.Log($"[ActorSystemTest] {message}");
            }
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[ActorSystemTest] {message}");
        }
        
        #endregion
        
        #region GUI for Manual Testing
        
        private void OnGUI()
        {
            if (_testsRunning)
            {
                GUI.Label(new Rect(10, 10, 300, 20), "Actor System Tests Running...");
                return;
            }
            
            GUILayout.BeginArea(new Rect(10, 10, 300, 400));
            GUILayout.Label("Actor System Test Runner", GUI.skin.box);
            
            if (GUILayout.Button("Run All Tests"))
            {
                _ = RunAllTestsAsync();
            }
            
            if (GUILayout.Button("Test Actor Creation"))
            {
                _ = TestActorCreationAsync();
            }
            
            if (GUILayout.Button("Test Animations"))
            {
                _ = TestAnimationsAsync();
            }
            
            if (GUILayout.Button("Test Performance"))
            {
                _ = TestPerformanceAsync();
            }
            
            if (GUILayout.Button("Cleanup Test Actors"))
            {
                _ = CleanupTestActorsAsync();
            }
            
            GUILayout.Space(10);
            
            if (_actorService != null)
            {
                GUILayout.Label($"Current Actors: {_actorService.ActorCount}");
                GUILayout.Label($"Test Characters: {_testCharacters.Count}");
                GUILayout.Label($"Test Backgrounds: {_testBackgrounds.Count}");
            }
            else
            {
                GUILayout.Label("Actor Service: Not Available");
            }
            
            GUILayout.EndArea();
        }
        
        #endregion
    }
}