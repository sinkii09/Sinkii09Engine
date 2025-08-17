# ActorService Refactoring Plan

## 🔍 **Current Problem Analysis**

### **ActorService Issues**
- **Size**: 723 lines - WAY too big for a single service
- **Complexity**: 35+ public methods violating Single Responsibility Principle
- **Mixed Concerns**: 8 different responsibility areas in one class

### **Current Responsibilities**
1. **🏭 Actor Creation** (3 methods) - `CreateCharacterActorAsync()`, `CreateBackgroundActorAsync()`, `CreateCustomActorAsync()`
2. **📋 Actor Registry** (11 methods) - `RegisterActor()`, `UnregisterActor()`, `GetActor()`, `TryGetActor()`, etc.
3. **⚙️ Service Lifecycle** (3 methods) - `InitializeAsync()`, `ShutdownAsync()`, `HealthCheckAsync()`
4. **🎬 Animation Control** (4 methods) - `StopAllAnimations()`, `PauseAllAnimations()`, etc.
5. **🎭 Scene Management** (4 methods) - `SetMainBackgroundAsync()`, `ClearSceneAsync()`, etc.
6. **💾 State Management** (2 methods) - `GetAllActorStates()`, `ApplyAllActorStatesAsync()`
7. **📈 Performance Monitoring** (4 methods) - `GetStatistics()`, `ValidateAllActors()`, `GetDebugInfo()`, etc.
8. **🔧 Runtime Configuration** (1 method) - `Configure()`

---

## 🏗️ **Proposed Modular Architecture**

### **Split into 5 Focused Services**

```
┌─────────────────────────────────────────────────────────────┐
│                    ActorService (Facade)                    │
│  • Service lifecycle (Initialize/Shutdown)                  │
│  • Dependency coordination                                   │
│  • Main service interface                                    │
│  • ~150 lines                                              │
└─────────────────────────────────────────────────────────────┘
                               │
                               │ coordinates
                               ▼
┌─────────────────┬─────────────────┬─────────────────┬─────────────────┐
│  ActorFactory   │ ActorRegistry   │ SceneManager    │ ActorMonitor    │
│                 │                 │                 │                 │
│ • CreateXXXAsync│ • Register      │ • MainBG        │ • Statistics    │
│ • Validation    │ • Unregister    │ • Clear         │ • Validation    │
│ • Prefab mgmt   │ • Get/Try/Has   │ • Preload       │ • Debug info    │
│ • Pool mgmt     │ • Collections   │ • Show/Hide     │ • Performance   │
│                 │                 │ • State save    │ • Animation     │
│ ~120 lines      │ ~100 lines      │ ~150 lines      │ ~100 lines      │
└─────────────────┴─────────────────┴─────────────────┴─────────────────┘
```

---

## 🎯 **Detailed Service Interfaces**

### **1. ActorService (Main Facade) - ~150 lines**
```csharp
[EngineService(ServiceCategory.Core, ServicePriority.Critical)]
public class ActorService : IActorService
{
    private readonly IActorFactory _factory;
    private readonly IActorRegistry _registry;
    private readonly ISceneManager _sceneManager;
    private readonly IActorMonitor _monitor;
    
    public ActorService(
        IActorFactory factory,
        IActorRegistry registry, 
        ISceneManager sceneManager,
        IActorMonitor monitor)
    {
        _factory = factory;
        _registry = registry;
        _sceneManager = sceneManager;
        _monitor = monitor;
    }
    
    // Delegates to specialized services
    public async UniTask<ICharacterActor> CreateCharacterActorAsync(...)
        => await _factory.CreateCharacterActorAsync(...);
        
    public IActor GetActor(string id)
        => _registry.GetActor(id);
        
    public async UniTask SetMainBackgroundAsync(...)
        => await _sceneManager.SetMainBackgroundAsync(...);
        
    public ActorServiceStatistics GetStatistics()
        => _monitor.GetStatistics();
}
```

### **2. IActorFactory (Creation Responsibility) - ~120 lines**
```csharp
public interface IActorFactory
{
    UniTask<ICharacterActor> CreateCharacterActorAsync(string id, CharacterAppearance appearance, Vector3 position = default, CancellationToken cancellationToken = default);
    UniTask<IBackgroundActor> CreateBackgroundActorAsync(string id, BackgroundAppearance appearance, Vector3 position = default, CancellationToken cancellationToken = default);
    UniTask<T> CreateCustomActorAsync<T>(string id, GameObject prefab, Vector3 position = default, CancellationToken cancellationToken = default) where T : class, IActor;
    
    // Pool management
    void ConfigurePooling(bool enabled, int size);
    int GetPoolSize(ActorType type);
    void WarmupPool(ActorType type, int count);
    
    // Validation
    bool ValidateActorCreation(string id, ActorType type);
    bool CanCreateActor(ActorType type);
}

[EngineService(ServiceCategory.Core, ServicePriority.High)]
public class ActorFactory : IActorFactory
{
    private readonly ActorServiceConfiguration _config;
    private readonly IResourceService _resourceService;
    private readonly IActorRegistry _registry;
    
    // Object pooling logic
    // Prefab management
    // Validation logic
    // Creation workflow
}
```

### **3. IActorRegistry (Storage Responsibility) - ~100 lines**
```csharp
public interface IActorRegistry
{
    // Registration
    bool RegisterActor(IActor actor);
    bool UnregisterActor(string id);
    
    // Retrieval
    IActor GetActor(string id);
    ICharacterActor GetCharacterActor(string id);
    IBackgroundActor GetBackgroundActor(string id);
    T GetActor<T>(string id) where T : class, IActor;
    
    // Safe retrieval
    bool TryGetActor(string id, out IActor actor);
    bool TryGetCharacterActor(string id, out ICharacterActor actor);
    bool TryGetBackgroundActor(string id, out IBackgroundActor actor);
    
    // Collections
    IReadOnlyCollection<IActor> AllActors { get; }
    IReadOnlyCollection<ICharacterActor> CharacterActors { get; }
    IReadOnlyCollection<IBackgroundActor> BackgroundActors { get; }
    IReadOnlyCollection<T> GetActorsOfType<T>() where T : class, IActor;
    
    // Utilities
    bool HasActor(string id);
    string[] GetActorIds();
    int ActorCount { get; }
    
    // Events
    event Action<IActor> OnActorRegistered;
    event Action<string> OnActorUnregistered;
}

[EngineService(ServiceCategory.Core, ServicePriority.High)]
public class ActorRegistry : IActorRegistry
{
    private readonly ConcurrentDictionary<string, IActor> _actors = new();
    private readonly ConcurrentDictionary<string, ICharacterActor> _characterActors = new();
    private readonly ConcurrentDictionary<string, IBackgroundActor> _backgroundActors = new();
    
    // Thread-safe registration/retrieval logic
    // Event management
    // Collection management
}
```

### **4. ISceneManager (Scene Operations Responsibility) - ~150 lines**
```csharp
public interface ISceneManager
{
    // Background management
    UniTask SetMainBackgroundAsync(string backgroundId, CancellationToken cancellationToken = default);
    IBackgroundActor GetMainBackground();
    
    // Scene operations
    UniTask ClearSceneAsync(CancellationToken cancellationToken = default);
    UniTask PreloadSceneActorsAsync(string[] actorIds, CancellationToken cancellationToken = default);
    
    // Visibility management
    UniTask ShowActorAsync(string actorId, float duration = 1.0f, CancellationToken cancellationToken = default);
    UniTask HideActorAsync(string actorId, float duration = 1.0f, CancellationToken cancellationToken = default);
    UniTask ShowAllActorsAsync(float duration = 1.0f, CancellationToken cancellationToken = default);
    UniTask HideAllActorsAsync(float duration = 1.0f, CancellationToken cancellationToken = default);
    
    // State management
    Dictionary<string, ActorState> GetAllActorStates();
    UniTask ApplyAllActorStatesAsync(Dictionary<string, ActorState> states, float duration = 0f, CancellationToken cancellationToken = default);
    
    // Resource management
    UniTask LoadActorResourcesAsync(string actorId, CancellationToken cancellationToken = default);
    UniTask LoadAllActorResourcesAsync(CancellationToken cancellationToken = default);
    UniTask UnloadActorResourcesAsync(string actorId, CancellationToken cancellationToken = default);
    UniTask UnloadAllActorResourcesAsync(CancellationToken cancellationToken = default);
    
    // Lifecycle
    UniTask DestroyActorAsync(string actorId, CancellationToken cancellationToken = default);
    UniTask DestroyAllActorsAsync(CancellationToken cancellationToken = default);
    
    // Events
    event Action<IBackgroundActor> OnMainBackgroundChanged;
}

[EngineService(ServiceCategory.Core, ServicePriority.High)]
public class SceneManager : ISceneManager
{
    private readonly IActorRegistry _registry;
    private readonly IResourceService _resourceService;
    private IBackgroundActor _mainBackground;
    
    // Scene transition logic
    // Background management
    // Batch operations
    // State persistence
}
```

### **5. IActorMonitor (Monitoring Responsibility) - ~100 lines**
```csharp
public interface IActorMonitor
{
    // Statistics
    ActorServiceStatistics GetStatistics();
    void UpdateStatistics();
    
    // Validation
    Dictionary<string, string[]> ValidateAllActors();
    bool ValidateActor(string actorId, out string[] errors);
    
    // Debug information
    string GetDebugInfo();
    string GetActorDebugInfo(string actorId);
    
    // Animation control (global)
    void StopAllAnimations();
    void PauseAllAnimations();
    void ResumeAllAnimations();
    void SetGlobalAnimationSpeed(float speedMultiplier);
    
    // Performance monitoring
    void StartPerformanceMonitoring();
    void StopPerformanceMonitoring();
    TimeSpan GetAverageOperationTime(string operationType);
    
    // Configuration
    void Configure(int maxConcurrentLoads, bool enableActorPooling, bool enablePerformanceMonitoring);
}

[EngineService(ServiceCategory.Core, ServicePriority.Normal)]
public class ActorMonitor : IActorMonitor
{
    private readonly IActorRegistry _registry;
    private readonly ActorServiceConfiguration _config;
    private ActorServiceStatistics _statistics = new();
    
    // Performance tracking
    // Validation logic
    // Debug information generation
    // Global animation control
}
```

---

## 📋 **Implementation Plan**

### **Phase 1: Extract Registry (Low Risk) - Week 1**
**Goal**: Move actor storage/retrieval logic to dedicated service

**Steps**:
1. Create `IActorRegistry` interface
2. Create `ActorRegistry` implementation
3. Move all Get/Register/Unregister methods from ActorService
4. Move the three ConcurrentDictionary collections
5. Update ActorService to inject and delegate to registry
6. Update tests

**Result**: -100 lines from ActorService, cleaner separation

**Risk**: Low - Pure data storage operations

### **Phase 2: Extract Factory (Medium Risk) - Week 2**
**Goal**: Move actor creation logic to dedicated service

**Steps**:
1. Create `IActorFactory` interface
2. Create `ActorFactory` implementation 
3. Move all CreateXXXAsync methods from ActorService
4. Move pooling and validation logic
5. Move semaphore and loading operations management
6. Update ActorService to inject and delegate to factory
7. Update tests

**Result**: -120 lines from ActorService, better creation management

**Risk**: Medium - Complex async operations and pooling

### **Phase 3: Extract Scene Manager (Medium Risk) - Week 3**
**Goal**: Move scene/state management to dedicated service

**Steps**:
1. Create `ISceneManager` interface
2. Create `SceneManager` implementation
3. Move background management, show/hide, state management
4. Move resource loading/unloading methods
5. Move scene transition logic
6. Update ActorService to inject and delegate to scene manager
7. Update tests

**Result**: -150 lines from ActorService, better scene management

**Risk**: Medium - Complex state management and resource operations

### **Phase 4: Extract Monitor (Low Risk) - Week 4**
**Goal**: Move monitoring/statistics to dedicated service

**Steps**:
1. Create `IActorMonitor` interface
2. Create `ActorMonitor` implementation
3. Move statistics, validation, debug methods
4. Move animation control methods
5. Move performance timers and monitoring
6. Update ActorService to inject and delegate to monitor
7. Update tests

**Result**: -100 lines from ActorService, better monitoring

**Risk**: Low - Mostly reporting and utility methods

### **Phase 5: Finalize Facade (Low Risk) - Week 5**
**Goal**: Complete ActorService as lightweight coordinator

**Steps**:
1. Ensure ActorService is clean facade (~150 lines)
2. Register all new services in ServiceContainer
3. Update dependency injection configuration
4. Comprehensive integration testing
5. Performance testing
6. Documentation updates

**Result**: Clean, maintainable architecture

**Risk**: Low - Integration and cleanup

---

## ✅ **Benefits of This Refactoring**

### **🎯 Single Responsibility Principle**
- Each service has **one clear purpose**
- Easier to understand, test, and maintain
- Clear boundaries between concerns

### **🔧 Better Testing**
- Unit test each service independently
- Mock dependencies easily
- Isolated test scenarios

### **🚀 Performance**
- Services can be optimized individually
- Parallel initialization possible
- Better resource management

### **📈 Scalability**
- Easy to add new features to specific services
- Clear extension points
- Better composition opportunities

### **🔄 Maintainability**
- Smaller, focused classes
- Clear responsibility boundaries
- Easier debugging and troubleshooting

### **🧩 Reusability**
- Services can be used independently
- Better composition opportunities
- Flexible architecture

---

## ⚠️ **Risks and Mitigation**

### **Integration Complexity**
- **Risk**: Services need to coordinate properly
- **Mitigation**: Use dependency injection, clear interfaces, comprehensive testing

### **Performance Overhead**
- **Risk**: More service calls and indirection
- **Mitigation**: Profile before/after, optimize hot paths

### **Breaking Changes**
- **Risk**: Existing code might break
- **Mitigation**: Maintain IActorService interface compatibility, gradual migration

### **Testing Complexity**
- **Risk**: More complex integration testing
- **Mitigation**: Good unit test coverage, mock services, automated testing

---

## 📊 **Success Metrics**

### **Code Quality**
- [ ] ActorService reduced to ~150 lines
- [ ] Each service < 150 lines
- [ ] Single responsibility for each service
- [ ] 95%+ test coverage maintained

### **Performance**
- [ ] No performance regression (< 5% overhead)
- [ ] Memory usage unchanged or improved
- [ ] Startup time unchanged or improved

### **Maintainability**
- [ ] Cyclomatic complexity reduced
- [ ] Clear service boundaries
- [ ] Easy to add new features
- [ ] Better error isolation

---

## 📁 **File Structure After Refactoring**

```
ActorService/
├── ActorService.cs                    # Main facade (~150 lines)
├── IActorService.cs                   # Main interface
├── Configuration/
│   └── ActorServiceConfiguration.cs   # Configuration
├── Core/
│   ├── IActor.cs                      # Actor interfaces
│   ├── BaseActor.cs                   # Base implementation
│   ├── ActorTypes.cs                  # Type system
│   ├── ActorAppearance.cs             # Appearance system
│   ├── ActorState.cs                  # State management
│   ├── ActorStateFactory.cs           # State utilities
│   └── PlaceholderActors.cs           # Current implementations
├── Factory/
│   ├── IActorFactory.cs               # Factory interface
│   └── ActorFactory.cs                # Factory implementation (~120 lines)
├── Registry/
│   ├── IActorRegistry.cs              # Registry interface
│   └── ActorRegistry.cs               # Registry implementation (~100 lines)
├── Scene/
│   ├── ISceneManager.cs               # Scene interface
│   └── SceneManager.cs                # Scene implementation (~150 lines)
└── Monitoring/
    ├── IActorMonitor.cs               # Monitor interface
    └── ActorMonitor.cs                # Monitor implementation (~100 lines)
```

---

## 🎯 **Next Steps**

1. **Review and Approve** this refactoring plan
2. **Start with Phase 1** (Registry extraction) - lowest risk
3. **Set up integration tests** before starting refactoring
4. **Create performance baseline** for comparison
5. **Begin implementation** with clear milestones

**Estimated Timeline**: 5 weeks for complete refactoring
**Risk Level**: Medium (manageable with proper testing)
**Benefits**: High (much cleaner, maintainable architecture)