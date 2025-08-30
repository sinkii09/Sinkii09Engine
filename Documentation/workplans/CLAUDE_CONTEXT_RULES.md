# Claude Context Rules - Sinkii09 Engine Development

## Core Development Principles

### 🚫 ABSOLUTE NO-NO's

#### 1. **ZERO HARDCODED STRINGS**
- ❌ **NEVER** use string literals in code suggestions
- ❌ **NEVER** use magic strings for paths, IDs, or names
- ❌ **NEVER** suggest `"ui/icons/player"` or similar hardcoded paths
- ❌ **NEVER** use string parameters for asset loading
- ❌ **NEVER** hardcode configuration values

#### 2. **FORBIDDEN PATTERNS**
```csharp
// ❌ NEVER SHOW THESE PATTERNS
await LoadAsync("hardcoded_string");
var path = "Assets/Resources/UI/";
[SerializeField] private string screenName = "MainMenu";
if (screenType == "dialog") { }
```

### ✅ REQUIRED PATTERNS

#### 1. **ScriptableObject References Only**
```csharp
// ✅ ALWAYS USE THESE PATTERNS
[SerializeField] private UIScreenAsset screenAsset;
[SerializeField] private UIAssetReference iconReference;
await LoadAsync(assetReference);
```

#### 2. **Enum-Based Type Safety**
```csharp
// ✅ ALWAYS USE ENUMS
public enum ScreenType { MainMenu, Settings, Gameplay }
public enum UICategory { Core, Gameplay, Dialog }
```

#### 3. **Configuration-Driven Everything**
```csharp
// ✅ ALL SETTINGS VIA SCRIPTABLEOBJECTS
[SerializeField] private UIServiceConfiguration config;
var timeout = config.DefaultTimeout;
var maxRetries = config.MaxRetryAttempts;
```

## Engine Architecture Rules

### 1. **Service Pattern Compliance**
- ✅ **MUST** implement `IEngineService`
- ✅ **MUST** use `[EngineService]` attribute
- ✅ **MUST** have `ServiceConfiguration` class
- ✅ **MUST** use constructor dependency injection
- ✅ **MUST** implement async lifecycle methods

### 2. **Service Dependencies**
- ✅ **MUST** declare dependencies with `[RequiredService]` / `[OptionalService]`
- ✅ **MUST** use existing services (ResourceService, ResourcePathResolver)
- ✅ **MUST** follow dependency injection patterns
- ✅ **NEVER** use static singletons or service locators

### 3. **Resource Loading**
- ✅ **MUST** use `ResourceService.LoadResourceByIdAsync<T>()`
- ✅ **MUST** use `ResourceType.UI` for UI assets
- ✅ **MUST** use `ResourcePathResolver` for path consistency
- ✅ **MUST** use `PathParameter` for dynamic parameters

### 4. **API Design**
- ✅ **MUST** keep method names short and clean (`ShowAsync`, not `ShowScreenAsync`)
- ✅ **MUST** use UniTask for async operations
- ✅ **MUST** support CancellationToken
- ✅ **MUST** return meaningful result objects

## Code Quality Standards

### 1. **KISS Principle**
- ✅ Keep core functionality simple
- ✅ Add advanced features incrementally
- ✅ Prefer simple solutions over complex ones
- ✅ Minimal API surface for core features

### 2. **Type Safety**
- ✅ **MUST** use compile-time safety
- ✅ **MUST** use ScriptableObject references
- ✅ **MUST** avoid reflection where possible
- ✅ **MUST** provide IDE autocomplete support

### 3. **Performance**
- ✅ **MUST** implement object pooling for frequently used objects
- ✅ **MUST** use async/await patterns with UniTask
- ✅ **MUST** implement proper cancellation
- ✅ **MUST** cache expensive operations

### 4. **Error Handling**
- ✅ **MUST** return result objects (not exceptions for expected failures)
- ✅ **MUST** use proper CancellationToken handling
- ✅ **MUST** validate ScriptableObject references
- ✅ **MUST** provide meaningful error messages

## File Structure Rules

### 1. **Organization**
```
Assets/Engine/Runtime/Scripts/Core/Services/Implemented/[ServiceName]/
├── Core/
│   ├── I[ServiceName].cs
│   ├── [ServiceName].cs
│   └── [ServiceName]Types.cs
├── Configuration/
│   └── [ServiceName]Configuration.cs
├── Components/
│   └── [ServiceName]Behaviour.cs
└── Editor/
    └── [ServiceName]Editor.cs
```

### 2. **Asset Organization**
```
Assets/Engine/Resources/
├── Configurations/
│   └── [ServiceName]Configuration.asset
├── [AssetType]s/
│   └── [AssetName].asset
└── References/
    └── [AssetName]Reference.asset
```

## Testing Requirements

### 1. **Test Coverage**
- ✅ **MUST** provide comprehensive test suite
- ✅ **MUST** test all public API methods
- ✅ **MUST** test error conditions
- ✅ **MUST** test service lifecycle

### 2. **Test Patterns**
- ✅ **MUST** use mock services for dependencies
- ✅ **MUST** test with real ScriptableObject configurations
- ✅ **MUST** test async operations with proper cancellation
- ✅ **MUST** test memory management and cleanup

## Documentation Standards

### 1. **Code Documentation**
- ✅ **MUST** document all public APIs
- ✅ **MUST** provide usage examples (without hardcoded strings)
- ✅ **MUST** document configuration options
- ✅ **MUST** explain integration patterns

### 2. **Architecture Documentation**
- ✅ **MUST** explain design decisions
- ✅ **MUST** document service dependencies
- ✅ **MUST** provide implementation phases
- ✅ **MUST** include migration guides

## Integration Rules

### 1. **Existing Services**
- ✅ **MUST** integrate with ResourceService for all asset loading
- ✅ **MUST** use ResourcePathResolver for path management
- ✅ **MUST** follow existing service patterns exactly
- ✅ **MUST** maintain backward compatibility

### 2. **Unity Integration**
- ✅ **MUST** use Unity's asset pipeline properly
- ✅ **MUST** provide custom editors for ScriptableObjects
- ✅ **MUST** support Unity's serialization system
- ✅ **MUST** integrate with Unity's lifecycle

## Communication Rules

### 1. **Response Format**
- ✅ Keep responses concise (< 4 lines unless asked for detail)
- ✅ Focus on the specific question asked
- ✅ No unnecessary preamble or explanations
- ✅ Use TodoWrite for complex multi-step tasks

### 2. **Code Suggestions**
- ✅ **ONLY** show code that follows ALL rules above
- ✅ **NEVER** show code with hardcoded strings
- ✅ Provide complete, working examples
- ✅ Include proper error handling

### 3. **Problem Solving**
- ✅ Always consider type-safe alternatives first
- ✅ Suggest ScriptableObject-based solutions
- ✅ Keep solutions simple and maintainable
- ✅ Follow existing engine patterns

## Validation Checklist

Before showing ANY code suggestion, verify:

- [ ] **NO hardcoded strings anywhere**
- [ ] Uses ScriptableObject references
- [ ] Follows service pattern
- [ ] Integrates with existing services
- [ ] Uses proper async/await patterns
- [ ] Includes error handling
- [ ] Maintains type safety
- [ ] Follows KISS principle

## Examples of GOOD vs BAD

### ❌ BAD - Never Show This
```csharp
await uiService.ShowAsync("MainMenu");
var sprite = Resources.Load<Sprite>("UI/Icons/player");
if (screenName == "settings") { }
```

### ✅ GOOD - Always Show This
```csharp
await uiService.ShowAsync(mainMenuAsset);
var sprite = await LoadAssetAsync<Sprite>(playerIconReference);
if (screenAsset.Category == UICategory.Settings) { }
```

## Scalability & Extensibility Rules

### 1. **Plugin Architecture Pattern**
- ✅ **MUST** design with extension points
- ✅ **MUST** use interface segregation (multiple small interfaces)
- ✅ **MUST** support runtime feature addition
- ✅ **MUST** avoid sealed/final classes for extensible components

```csharp
// ✅ GOOD - Extensible design
public interface IUIRenderer { }
public interface IUIAnimator { }
public interface IUIValidator { }

public class UIService : IUIService
{
    private readonly List<IUIExtension> _extensions = new();
    
    public void RegisterExtension<T>(T extension) where T : IUIExtension
    {
        _extensions.Add(extension);
    }
}
```

### 2. **Event-Driven Architecture**
- ✅ **MUST** use events for loose coupling
- ✅ **MUST** provide extension hooks via events
- ✅ **MUST** support event subscription/unsubscription
- ✅ **MUST** use typed event args (no object parameters)

```csharp
// ✅ GOOD - Event-based extension points
public class UIService : IUIService
{
    public event Action<UIScreenShownEventArgs> ScreenShown;
    public event Action<UIScreenHiddenEventArgs> ScreenHidden;
    
    // Extensions can subscribe to these events
}
```

### 3. **Strategy Pattern for Behaviors**
- ✅ **MUST** use strategy pattern for variable behaviors
- ✅ **MUST** make strategies configurable via ScriptableObjects
- ✅ **MUST** support strategy swapping at runtime
- ✅ **MUST** provide default implementations

```csharp
// ✅ GOOD - Configurable strategies
[CreateAssetMenu(menuName = "UI/Transition Strategy")]
public class UITransitionStrategy : ScriptableObject
{
    public virtual UniTask ExecuteAsync(UIScreen screen) { }
}

public class UIService : IUIService
{
    [SerializeField] private UITransitionStrategy defaultTransition;
    private readonly Dictionary<UIScreenAsset, UITransitionStrategy> _customTransitions = new();
}
```

### 4. **Modular Component System**
- ✅ **MUST** break features into independent modules
- ✅ **MUST** use composition over inheritance
- ✅ **MUST** support module hot-swapping
- ✅ **MUST** provide module dependency resolution

```csharp
// ✅ GOOD - Modular design
public abstract class UIModule : ScriptableObject
{
    public abstract UniTask InitializeAsync(IUIService uiService);
    public abstract UniTask ShutdownAsync();
}

public class UIAnimationModule : UIModule { }
public class UIDataBindingModule : UIModule { }
public class UILocalizationModule : UIModule { }
```

## Feature Addition Patterns

### 1. **Progressive Enhancement**
- ✅ **MUST** design core features to work independently
- ✅ **MUST** add advanced features as optional modules
- ✅ **MUST** maintain backward compatibility
- ✅ **MUST** use feature flags for gradual rollout

```csharp
// ✅ GOOD - Progressive feature addition
[CreateAssetMenu(menuName = "UI/Feature Configuration")]
public class UIFeatureConfiguration : ScriptableObject
{
    [Header("Core Features")]
    public bool enableBasicNavigation = true;
    
    [Header("Advanced Features")]
    public bool enableAnimations = false;
    public bool enableDataBinding = false;
    public bool enableLocalization = false;
    
    [Header("Experimental Features")]
    public bool enableVoiceControl = false;
    public bool enableGestureRecognition = false;
}
```

### 2. **Configuration-Driven Features**
- ✅ **MUST** make new features configurable via ScriptableObjects
- ✅ **MUST** provide sensible defaults
- ✅ **MUST** support feature-specific configurations
- ✅ **MUST** validate feature combinations

```csharp
// ✅ GOOD - Feature-specific configurations
[CreateAssetMenu(menuName = "UI/Animation Configuration")]
public class UIAnimationConfiguration : ScriptableObject
{
    [Header("Animation Settings")]
    public AnimationCurve defaultCurve;
    public float defaultDuration = 0.3f;
    public bool enableParallelAnimations = true;
    
    public override bool Validate(out List<string> errors)
    {
        // Validate animation-specific settings
    }
}
```

### 3. **Extension Point Documentation**
- ✅ **MUST** document all extension points
- ✅ **MUST** provide example implementations
- ✅ **MUST** explain when to use each pattern
- ✅ **MUST** maintain extension compatibility matrix

## Advanced Feature Integration Rules

### 1. **Lazy Loading Pattern**
- ✅ **MUST** load advanced features on-demand
- ✅ **MUST** provide async initialization for heavy features
- ✅ **MUST** support feature preloading
- ✅ **MUST** handle feature loading failures gracefully

```csharp
// ✅ GOOD - Lazy feature loading
public class UIService : IUIService
{
    private readonly Dictionary<Type, Lazy<IUIFeature>> _features = new();
    
    public async UniTask<T> GetFeatureAsync<T>() where T : class, IUIFeature
    {
        if (!_features.ContainsKey(typeof(T)))
        {
            var feature = await LoadFeatureAsync<T>();
            _features[typeof(T)] = new Lazy<IUIFeature>(() => feature);
        }
        
        return _features[typeof(T)].Value as T;
    }
}
```

### 2. **Version Compatibility**
- ✅ **MUST** use semantic versioning for features
- ✅ **MUST** maintain compatibility matrices
- ✅ **MUST** provide migration paths for breaking changes
- ✅ **MUST** support multiple feature versions simultaneously

```csharp
// ✅ GOOD - Version-aware features
[CreateAssetMenu(menuName = "UI/Versioned Feature")]
public abstract class VersionedUIFeature : ScriptableObject
{
    [SerializeField] private SemanticVersion version;
    [SerializeField] private SemanticVersion[] compatibleVersions;
    
    public SemanticVersion Version => version;
    public bool IsCompatibleWith(SemanticVersion otherVersion) { }
}
```

### 3. **Hot-Reload Support**
- ✅ **MUST** support runtime feature updates
- ✅ **MUST** preserve state during feature reloads
- ✅ **MUST** validate feature integrity after reload
- ✅ **MUST** provide rollback mechanisms

```csharp
// ✅ GOOD - Hot-reload capable features
public abstract class ReloadableUIFeature : UIModule
{
    public abstract void SaveState(UIFeatureState state);
    public abstract void RestoreState(UIFeatureState state);
    public abstract bool ValidateIntegrity();
}
```

## Code Evolution Patterns

### 1. **Interface Evolution**
- ✅ **MUST** extend interfaces without breaking existing implementations
- ✅ **MUST** use interface segregation for new capabilities
- ✅ **MUST** provide adapter patterns for legacy interfaces
- ✅ **MUST** deprecate interfaces gradually

```csharp
// ✅ GOOD - Non-breaking interface evolution
public interface IUIService { } // Core interface - never change

public interface IUIServiceV2 : IUIService // Extended capabilities
{
    UniTask<bool> SupportsFeatureAsync(UIFeature feature);
}

public interface IAdvancedUIService : IUIService // Advanced features
{
    UniTask RegisterCustomRendererAsync<T>(T renderer) where T : IUIRenderer;
}
```

### 2. **Configuration Evolution**
- ✅ **MUST** support configuration migration
- ✅ **MUST** maintain backward compatibility
- ✅ **MUST** provide configuration upgrade paths
- ✅ **MUST** validate migrated configurations

```csharp
// ✅ GOOD - Evolvable configuration
[CreateAssetMenu(menuName = "UI/Service Configuration")]
public class UIServiceConfiguration : ServiceConfiguration
{
    [Header("Version Info")]
    [SerializeField] private int configVersion = 1;
    
    public override void OnValidate()
    {
        base.OnValidate();
        MigrateIfNeeded();
    }
    
    private void MigrateIfNeeded()
    {
        if (configVersion < CURRENT_VERSION)
        {
            MigrateFromVersion(configVersion);
            configVersion = CURRENT_VERSION;
        }
    }
}
```

### 3. **Performance Scaling**
- ✅ **MUST** design for performance scaling
- ✅ **MUST** provide performance monitoring hooks
- ✅ **MUST** support performance profiling
- ✅ **MUST** implement adaptive performance strategies

```csharp
// ✅ GOOD - Performance-scalable design
public class UIService : IUIService
{
    private readonly IPerformanceMonitor _performanceMonitor;
    private readonly Dictionary<PerformanceLevel, IUIRenderingStrategy> _renderingStrategies;
    
    public async UniTask AdaptToPerformanceAsync(PerformanceMetrics metrics)
    {
        var level = DeterminePerformanceLevel(metrics);
        var strategy = _renderingStrategies[level];
        await ApplyRenderingStrategyAsync(strategy);
    }
}
```

## Future-Proofing Checklist

Before implementing any feature, verify:

- [ ] **Extension Points**: Can new behaviors be added without modifying core?
- [ ] **Configuration**: Are all behaviors configurable via ScriptableObjects?
- [ ] **Modularity**: Can features be enabled/disabled independently?
- [ ] **Events**: Are extension hooks provided via typed events?
- [ ] **Versioning**: Is version compatibility maintained?
- [ ] **Performance**: Will it scale with increased usage?
- [ ] **Testing**: Can new features be tested in isolation?
- [ ] **Documentation**: Are extension patterns documented?

## Progressive Development Methodology

### 1. **Build Foundation First**
- ✅ **MUST** implement basic functionality before any advanced features
- ✅ **MUST** ensure core system works completely before extensions
- ✅ **MUST** validate each phase before proceeding to next
- ✅ **MUST** provide working examples at each step

#### Development Phase Order:
```
Phase 1: Core Service (Show/Hide screens only)
├── Test: Can show and hide a single screen
├── Validate: Service lifecycle works
└── Document: Basic usage patterns

Phase 2: Stack Management (Add navigation)
├── Test: Can navigate between screens
├── Validate: Stack operations work correctly
└── Document: Navigation patterns

Phase 3: Resource Loading (Add PathResolver integration)
├── Test: Assets load via ResourceService
├── Validate: All paths resolve correctly
└── Document: Asset loading patterns

Phase N: Advanced Features (Only after core is solid)
├── Test: New feature works in isolation
├── Validate: Doesn't break existing functionality
└── Document: Integration patterns
```

### 2. **Code Validation Requirements**
- ✅ **MUST** verify code compiles before suggesting
- ✅ **MUST** check compatibility with existing services
- ✅ **MUST** validate against engine patterns
- ✅ **MUST** ensure no breaking changes to working code

#### Pre-Suggestion Validation Checklist:
```
□ Does this code compile with existing engine?
□ Does it follow established service patterns?
□ Is it compatible with current ResourceService?
□ Does it maintain type safety throughout?
□ Are all dependencies properly declared?
□ Does it work without hardcoded strings?
□ Can it be tested in isolation?
□ Does it maintain existing functionality?
```

### 3. **Justification Requirements**
- ✅ **MUST** explain WHY suggesting new code
- ✅ **MUST** identify specific problem being solved
- ✅ **MUST** compare with current approach
- ✅ **MUST** show clear benefits of change

#### Required Explanation Format:
```
## Why This Change?

**Problem**: [Specific issue with current approach]
**Solution**: [How new code solves it]
**Benefits**: [Clear advantages over old way]

## Old vs New Comparison

### Before (Current):
```csharp
// Show existing approach with problems highlighted
```

### After (Proposed):
```csharp
// Show new approach with improvements highlighted
```

## Impact Analysis

- **Breaking Changes**: [None/List specific changes]
- **Migration Required**: [Yes/No - if yes, provide steps]
- **Testing Impact**: [What needs to be tested]
- **Performance Impact**: [Better/Same/Worse - explain]
```

### 4. **Incremental Enhancement Pattern**
- ✅ **MUST** add one feature at a time
- ✅ **MUST** maintain backward compatibility
- ✅ **MUST** provide migration path if needed
- ✅ **MUST** test thoroughly before next feature

#### Feature Addition Protocol:
```
Step 1: Analyze Current State
├── What works now?
├── What are the limitations?
└── What specific problem needs solving?

Step 2: Design Minimal Solution
├── Smallest possible change
├── Maintains existing functionality
└── Adds only essential new capability

Step 3: Validate Integration
├── Compiles with existing code
├── Doesn't break current features
└── Follows engine patterns

Step 4: Provide Justification
├── Why this change is necessary
├── How it improves current system
└── What alternatives were considered

Step 5: Show Migration Path
├── How to update existing code (if needed)
├── Backward compatibility strategy
└── Testing approach
```

## Code Comparison Standards

### 1. **Side-by-Side Analysis**
- ✅ **MUST** show before/after code comparison
- ✅ **MUST** highlight specific improvements
- ✅ **MUST** explain each change made
- ✅ **MUST** identify potential issues

### 2. **Impact Assessment**
- ✅ **MUST** analyze breaking changes
- ✅ **MUST** evaluate performance implications
- ✅ **MUST** consider maintenance overhead
- ✅ **MUST** assess learning curve for users

### 3. **Risk Analysis**
- ✅ **MUST** identify potential problems
- ✅ **MUST** provide mitigation strategies
- ✅ **MUST** suggest testing approaches
- ✅ **MUST** plan rollback procedures

## Example Proper Suggestion Format

### Problem Identification
```
Current UIService has hardcoded asset loading:
```csharp
var prefab = Resources.Load<GameObject>("UI/Screens/MainMenu"); // ❌ Hardcoded
```

### Proposed Solution
```csharp
var prefab = await _resourceService.LoadResourceByIdAsync<GameObject>(
    ResourceType.UI, screenAsset.ScreenId, ResourceCategory.Primary); // ✅ Type-safe
```

### Why This Change?
1. **Eliminates hardcoded strings** - follows engine's type-safety rules
2. **Uses existing ResourceService** - consistent with engine architecture  
3. **Supports ResourcePathResolver** - dynamic path resolution
4. **Enables Addressables** - better asset management

### Migration Path
1. Replace direct Resources.Load calls
2. Update screen assets to use UIScreenAsset references
3. Test existing screens still load correctly
4. No breaking changes to public API

### Testing Strategy
1. Verify all existing screens still load
2. Test with both Resources and Addressables
3. Validate path resolution works correctly
4. Performance test asset loading times

## Mandatory Pre-Response Validation

Before ANY code suggestion, verify:

1. **Foundation Check**: Is the basic system working?
2. **Compatibility Check**: Does this integrate properly?
3. **Progression Check**: Is this the right next step?
4. **Quality Check**: Does this follow all rules?

## Anti-Refactor Hell Rules

### 🚫 **NEVER DO THESE THINGS**

#### 1. **Mass Code Generation**
- ❌ **NEVER** suggest creating 10+ files at once
- ❌ **NEVER** generate large class hierarchies
- ❌ **NEVER** create complex inheritance trees
- ❌ **NEVER** suggest "architecture overhauls"

#### 2. **Premature Abstraction**
- ❌ **NEVER** create interfaces until you have 2+ implementations
- ❌ **NEVER** add abstraction layers "for future needs"
- ❌ **NEVER** create generic systems without concrete use cases
- ❌ **NEVER** suggest design patterns without proven necessity

#### 3. **Breaking Working Code**
- ❌ **NEVER** refactor code that's currently working
- ❌ **NEVER** suggest "improvements" to stable systems
- ❌ **NEVER** change APIs that have users
- ❌ **NEVER** modify core functionality for "cleanliness"

#### 4. **Over-Engineering**
- ❌ **NEVER** suggest Enterprise patterns for simple problems
- ❌ **NEVER** create frameworks within frameworks
- ❌ **NEVER** add complexity for "flexibility"
- ❌ **NEVER** build for requirements that don't exist

### ✅ **ANTI-VIBE CODING PRINCIPLES**

#### 1. **Prove It Works First**
```csharp
// ✅ GOOD - Start with working example
public class SimpleUIService : IUIService
{
    public async UniTask ShowAsync(UIScreenAsset asset)
    {
        // Minimal working implementation
        var prefab = await LoadPrefabAsync(asset);
        var instance = Instantiate(prefab);
        instance.SetActive(true);
    }
}

// ❌ BAD - Complex architecture without proof
public abstract class AbstractUIServiceFactory<T> where T : IUIServiceProvider
{
    // 50 lines of "flexible" code that might not work
}
```

#### 2. **One File at a Time**
- ✅ **MUST** suggest changes to ONE file maximum
- ✅ **MUST** make that file work completely
- ✅ **MUST** test that file in isolation
- ✅ **MUST** validate integration before next file

#### 3. **Minimal Change Rule**
- ✅ **MUST** suggest smallest possible change
- ✅ **MUST** preserve existing working behavior
- ✅ **MUST** add only ONE new capability at a time
- ✅ **MUST** avoid changing method signatures

#### 4. **Concrete Before Abstract**
- ✅ **MUST** show working concrete implementation first
- ✅ **MUST** use it in real scenarios
- ✅ **MUST** identify actual duplication before abstracting
- ✅ **MUST** have 3+ use cases before creating interfaces

### 🛡️ **Refactor Safety Protocol**

#### Before ANY refactoring suggestion:

1. **Working Code Validation**
   ```
   □ Is the current code actually broken?
   □ Does it have real performance issues?
   □ Are there actual bugs being fixed?
   □ Is there proven duplication?
   ```

2. **Change Impact Assessment**
   ```
   □ How many files will be affected?
   □ Will existing tests still pass?
   □ Are there any breaking changes?
   □ Can we roll back easily?
   ```

3. **Value Proposition Check**
   ```
   □ What concrete problem does this solve?
   □ Is the benefit worth the risk?
   □ Are we solving a real user problem?
   □ Will this make development faster?
   ```

4. **Complexity Analysis**
   ```
   □ Is the new code simpler than the old?
   □ Will new developers understand it easily?
   □ Does it reduce cognitive load?
   □ Are we adding or removing concepts?
   ```

### 📋 **Mandatory Refactor Checklist**

Before suggesting ANY code changes:

#### Stage 1: Necessity Check
- [ ] **Real Problem**: Is there an actual problem with current code?
- [ ] **User Impact**: Does this solve a real user pain point?
- [ ] **Risk/Benefit**: Is improvement worth the risk of breaking things?
- [ ] **Alternative**: Can we solve this with configuration instead?

#### Stage 2: Scope Limitation
- [ ] **Single File**: Does this change affect only one file?
- [ ] **Minimal Change**: Is this the smallest possible modification?
- [ ] **Backward Compatible**: Will existing code still work?
- [ ] **Rollback Plan**: Can we easily revert this change?

#### Stage 3: Implementation Validation
- [ ] **Compiles**: Does the suggested code actually compile?
- [ ] **Dependencies**: Are all required dependencies available?
- [ ] **Integration**: Will this work with existing services?
- [ ] **Testing**: Can this be tested in isolation?

#### Stage 4: Quality Gates
- [ ] **Simpler**: Is new code actually simpler than old?
- [ ] **Readable**: Will team members understand this easily?
- [ ] **Maintainable**: Is this easier to maintain long-term?
- [ ] **Debuggable**: Can issues be diagnosed quickly?

### 🚨 **RED FLAGS - Never Suggest If:**

1. **"Let's redesign the architecture"** - Architecture is working
2. **"We should use more design patterns"** - Patterns solve specific problems
3. **"This will be more flexible"** - Current system meets requirements
4. **"Let's make it more maintainable"** - Maintainable means stable, not complex
5. **"We can generalize this"** - Generalization creates complexity
6. **"This follows best practices"** - Best practices serve business goals
7. **"Let's future-proof this"** - Future requirements are unknown
8. **"We need better separation of concerns"** - Current separation is adequate

### ✅ **GREEN FLAGS - Good Reasons to Change:**

1. **"Current code has a bug"** - Fix specific issue
2. **"This is duplicated 5 times"** - Real duplication exists
3. **"Users can't do X"** - Missing required functionality
4. **"This crashes in production"** - Real reliability issue
5. **"Performance is 10x slower"** - Measurable performance problem
6. **"Code violates engine rules"** - Hardcoded strings, etc.
7. **"Tests are failing"** - Broken functionality
8. **"Cannot add required feature"** - Blocking business requirement

## Stability-First Development Rules

### 1. **Working Code is Sacred**
- ✅ If it works, don't touch it
- ✅ Add new features alongside old ones
- ✅ Deprecate gradually with migration periods
- ✅ Keep old APIs until new ones are proven

### 2. **Evolution Over Revolution**
- ✅ Small incremental improvements
- ✅ One responsibility at a time
- ✅ Measure impact of each change
- ✅ User feedback drives changes

### 3. **Simplicity Over Cleverness**
- ✅ Boring code is good code
- ✅ Obvious solutions are usually correct
- ✅ Readability trumps performance
- ✅ Maintainable beats optimal

### 4. **Proof-Driven Development**
- ✅ Show it works with concrete example
- ✅ Measure before and after
- ✅ User testing validates changes
- ✅ Rollback plan always exists

## Example: Good vs Bad Refactor Suggestions

### ❌ BAD - Vibe Coding Refactor
```
"Let's create an abstract UIServiceProvider factory with dependency injection, 
observer patterns, and plugin architecture. We'll need:
- IUIServiceFactory<T>
- AbstractUIServiceProvider
- UIServiceRegistry
- UIServiceLocator
- UIServiceConfiguration
- UIServiceBuilder
- UIServiceManager

This will make it more maintainable and follow SOLID principles."
```

### ✅ GOOD - Problem-Driven Change
```
## Problem
Current UIService.ShowAsync() method loads prefab every time, causing 200ms delay.

## Solution
Add simple prefab caching to UIService:

```csharp
private readonly Dictionary<UIScreenAsset, GameObject> _prefabCache = new();

public async UniTask ShowAsync(UIScreenAsset asset)
{
    if (!_prefabCache.TryGetValue(asset, out var prefab))
    {
        prefab = await LoadPrefabAsync(asset);
        _prefabCache[asset] = prefab;
    }
    
    var instance = Instantiate(prefab);
    instance.SetActive(true);
}
```

## Benefits
- 200ms → 5ms screen show time
- Zero breaking changes
- 5 lines of code added
- Easy to test and rollback
```

## Engine System Analysis Rules

### 🔍 **Existing Feature Detection Protocol**

#### Before suggesting ANY new functionality:

1. **Comprehensive Engine Scan**
   ```
   □ Search entire codebase for similar functionality
   □ Check all service interfaces for related methods
   □ Examine existing ScriptableObject configurations
   □ Review ResourceService and PathResolver capabilities
   □ Analyze ActorService, ScriptService, SaveLoadService patterns
   □ Look for existing utilities and extension methods
   ```

2. **Feature Overlap Analysis**
   ```
   □ Does ResourceService already handle this?
   □ Can ResourcePathResolver solve this need?
   □ Is there existing configuration for this?
   □ Do current services provide similar capability?
   □ Are there utilities that could be extended?
   ```

3. **Pattern Matching Check**
   ```
   □ How do other services solve similar problems?
   □ What patterns are already established?
   □ Can we follow existing conventions?
   □ Is there a service that should own this feature?
   ```

### 📋 **Mandatory Discovery Process**

#### Stage 1: Engine Knowledge Verification
Before any suggestion, I MUST:

1. **Examine Current Architecture**
   - Read relevant service interfaces
   - Check existing configuration patterns
   - Understand current resource loading approach
   - Identify established conventions

2. **Search for Existing Solutions**
   - Grep for similar method names
   - Look for related ScriptableObject types
   - Check utility classes and extensions
   - Review service capabilities

3. **Analyze Integration Points**
   - How do services currently interact?
   - What dependencies exist?
   - Where would new feature fit best?
   - Which service should own this functionality?

#### Stage 2: Best Approach Selection

1. **Option Evaluation Matrix**
   ```
   For each potential approach, evaluate:
   □ Uses existing services vs creating new ones
   □ Follows established patterns vs introducing new ones
   □ Minimal code changes vs extensive modifications
   □ Leverages current architecture vs fighting it
   □ Maintains consistency vs breaking conventions
   ```

2. **Implementation Path Analysis**
   ```
   Option A: Extend existing service
   - Pros: [List benefits]
   - Cons: [List drawbacks]
   - Integration effort: [Low/Medium/High]
   - Risk level: [Low/Medium/High]

   Option B: Create new component
   - Pros: [List benefits]
   - Cons: [List drawbacks]
   - Integration effort: [Low/Medium/High]
   - Risk level: [Low/Medium/High]

   Recommended: [Option with reasoning]
   ```

### 🎯 **Best Approach Selection Criteria**

#### Priority Order (Highest to Lowest):

1. **Use Existing Feature** - If functionality already exists
2. **Extend Existing Service** - If closely related capability exists
3. **Configure Existing System** - If ScriptableObject config can solve it
4. **Compose Existing Services** - If multiple services can provide solution
5. **Add Utility Method** - If simple helper can bridge gaps
6. **Create New Component** - Only if no existing solution possible

#### Decision Framework:
```
┌─ Does exact feature exist? ──── YES ─→ Use existing feature
├─ Does similar feature exist? ── YES ─→ Extend existing service
├─ Can configuration solve it? ── YES ─→ Add ScriptableObject config
├─ Can services be composed? ──── YES ─→ Create service composition
├─ Is simple utility enough? ──── YES ─→ Add utility method
└─ All options exhausted? ─────── YES ─→ Create new component (with full justification)
```

### 🔎 **Engine Feature Database**

#### Current Engine Capabilities:
```
ResourceService:
- LoadResourceAsync<T>(string path)
- LoadResourceByIdAsync<T>(ResourceType, string id, ResourceCategory, PathParameter[])
- Support for Resources and Addressables
- Circuit breaker patterns and retry policies
- Memory management and caching

ResourcePathResolver:
- Dynamic path resolution with parameters
- ResourceType.UI support
- Category-based organization
- Path parameter substitution

ScriptService:
- Script loading and caching
- Hot-reload capabilities
- Event system (ScriptLoadStarted, ScriptLoadCompleted, etc.)
- Memory pressure response

ActorService:
- Actor lifecycle management
- Registry for characters and backgrounds
- Resource loading integration
- Performance monitoring

SaveLoadService:
- Save/load operations with validation
- Encryption and compression
- Backup/restore functionality
- Multiple storage providers

ServiceContainer:
- Dependency injection
- Service lifecycle management
- Performance optimization
- Configuration validation
```

### 📝 **Mandatory Response Format**

Every suggestion MUST include:

#### 1. **Feature Existence Check**
```
## Engine Analysis Results

**Searched For**: [Description of needed functionality]
**Found Existing**: [List any existing similar features]
**Service Review**: [Relevant services examined]
**Pattern Analysis**: [How other services solve similar problems]
```

#### 2. **Approach Comparison**
```
## Solution Options Evaluated

### Option 1: [Extend ResourceService]
- **Implementation**: [Specific changes needed]
- **Pros**: [Benefits of this approach]
- **Cons**: [Limitations or drawbacks]
- **Risk**: [Low/Medium/High]

### Option 2: [Create New Component]
- **Implementation**: [Specific changes needed]
- **Pros**: [Benefits of this approach]
- **Cons**: [Limitations or drawbacks]
- **Risk**: [Low/Medium/High]

### Recommended Approach: [Selected option with reasoning]
```

#### 3. **Integration Justification**
```
## Why This Approach?

**Aligns With**: [Which existing patterns it follows]
**Leverages**: [Which existing services/features it uses]
**Maintains**: [Which conventions it preserves]
**Minimizes**: [What complexity/risk it avoids]
```

### 🚫 **Forbidden Patterns**

#### Never suggest without checking:
- ❌ Creating new services when existing ones could be extended
- ❌ Duplicating functionality that already exists
- ❌ Ignoring established patterns in favor of "better" approaches
- ❌ Adding features without understanding current capabilities
- ❌ Suggesting solutions that fight the current architecture

### ✅ **Required Investigation Process**

Before any code suggestion:

1. **Use Grep tool** to search for related functionality
2. **Read relevant service interfaces** to understand capabilities
3. **Examine configuration patterns** to see what's configurable
4. **Check utility classes** for existing helpers
5. **Review integration patterns** to understand service relationships

Example Investigation:
```csharp
// Before suggesting UI caching, I would search:
// 1. Grep for "cache" patterns in engine
// 2. Check ResourceService for existing caching
// 3. Look at ScriptService caching implementation
// 4. Review ActorService resource management
// 5. Understand ServiceContainer memory management
// 6. Then suggest approach that leverages existing patterns
```

## Real-World Usage Validation

### 🎮 **Mandatory Practical Application**

Every suggestion MUST include real-world usage validation:

#### 1. **Concrete Game Scenarios**
- ✅ **MUST** show usage in RPG context (inventory, dialog, character screens)
- ✅ **MUST** demonstrate puzzle game integration (level select, pause menu)
- ✅ **MUST** include action game examples (HUD, score display, game over)
- ✅ **MUST** cover mobile/desktop/console considerations

#### 2. **Designer Workflow Integration**
- ✅ **MUST** show how non-programmers use the feature
- ✅ **MUST** demonstrate Unity Inspector workflows
- ✅ **MUST** include asset creation and configuration steps
- ✅ **MUST** provide clear setup instructions

#### 3. **Development Team Usage**
- ✅ **MUST** show programmer implementation patterns
- ✅ **MUST** demonstrate artist asset integration
- ✅ **MUST** include QA testing approaches
- ✅ **MUST** provide debugging and troubleshooting guides

#### Required Usage Documentation:
```
## Real-World Usage Examples

**RPG Game**: [How feature works in character menu, inventory, dialog system]
**Puzzle Game**: [Level selection, pause menu, settings integration]
**Action Game**: [HUD elements, score display, real-time UI updates]

**Designer Workflow**: [Step-by-step asset creation and configuration]
**Programmer Integration**: [Code examples for common use cases]
**Team Collaboration**: [How different roles interact with the feature]
```

## Error Handling & Edge Cases

### 🛡️ **Mandatory Robustness Requirements**

Every suggestion MUST include comprehensive error handling:

#### 1. **Failure Mode Analysis**
- ✅ **MUST** identify all possible failure points
- ✅ **MUST** define recovery strategies for each failure
- ✅ **MUST** provide graceful degradation paths
- ✅ **MUST** include user-facing error communication

#### 2. **Edge Case Coverage**
- ✅ **MUST** handle null/missing asset references
- ✅ **MUST** manage network connectivity issues
- ✅ **MUST** address memory pressure scenarios
- ✅ **MUST** cover concurrent access conflicts

#### 3. **Developer Experience**
- ✅ **MUST** provide meaningful error messages
- ✅ **MUST** include debugging information
- ✅ **MUST** offer troubleshooting guidance
- ✅ **MUST** validate configuration integrity

#### Required Error Handling Documentation:
```
## Error Handling Strategy

**Failure Modes**: [List all ways the feature can fail]
**Recovery Strategies**: [How system recovers from each failure]
**Graceful Degradation**: [What happens when dependencies are unavailable]
**Error Messages**: [User-friendly messages for common issues]
**Debug Information**: [What logs/info help diagnose problems]
```

#### Example Error Handling:
```csharp
public async UniTask ShowAsync(UIScreenAsset screenAsset)
{
    // Input validation
    if (screenAsset == null)
    {
        Debug.LogError("UIService.ShowAsync: screenAsset cannot be null");
        return;
    }
    
    try
    {
        // Attempt normal operation
        var screen = await LoadScreenAsync(screenAsset);
        await DisplayScreenAsync(screen);
    }
    catch (ResourceLoadException ex)
    {
        // Graceful fallback
        Debug.LogWarning($"Failed to load screen {screenAsset.ScreenId}: {ex.Message}");
        await ShowErrorScreenAsync("Screen temporarily unavailable");
    }
    catch (OutOfMemoryException)
    {
        // Memory pressure handling
        await ClearScreenCacheAsync();
        Debug.LogWarning("Memory pressure detected, cleared screen cache");
        // Retry with simpler approach
    }
}
```

## Auto-Applied Rules

These rules are AUTOMATICALLY applied to every response:

1. **Scan all code for hardcoded strings** - reject if found
2. **Verify ScriptableObject usage** - require for all references
3. **Check service integration** - must use existing services
4. **Validate type safety** - no string parameters
5. **Ensure KISS compliance** - simple solutions preferred
6. **Verify extensibility** - must support future enhancements
7. **Check modularity** - features must be independent
8. **Validate configuration** - all behaviors must be configurable
9. **Verify progression** - builds on working foundation
10. **Validate compatibility** - works with existing code
11. **Require justification** - explain why suggesting change
12. **Demand comparison** - show old vs new approach
13. **Refactor safety check** - validate necessity and scope
14. **Anti-vibe coding check** - prevent over-engineering
15. **Stability validation** - preserve working functionality
16. **Feature existence check** - search entire engine before adding new
17. **Best approach analysis** - evaluate all options, pick optimal
18. **Engine pattern alignment** - follow established conventions
19. **Real-world usage validation** - provide concrete game scenarios and workflows
20. **Error handling coverage** - define failure modes and recovery strategies

---

**These rules are MANDATORY and NEVER optional. Any violation means the suggestion should not be presented.**