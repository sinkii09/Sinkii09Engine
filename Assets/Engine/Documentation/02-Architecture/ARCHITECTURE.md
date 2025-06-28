# Sinkii09 Engine Architecture Guide

This document outlines the modular design patterns, architectural decisions, and implementation guidelines for building scalable and extensible systems in the Sinkii09 Engine.

## 🏗️ Core Architectural Principles

### **1. Service-Oriented Architecture (SOA)**
The engine is built around autonomous services that handle specific responsibilities, communicate through well-defined interfaces, and can be developed, tested, and deployed independently.

### **2. Dependency Injection Pattern**
Services declare their dependencies explicitly, enabling loose coupling, easier testing, and flexible system composition.

### **3. Configuration-Driven Design**
System behavior is controlled through ScriptableObject configurations, allowing runtime flexibility without code changes.

### **4. Event-Driven Communication**
Services communicate through events to maintain loose coupling and enable reactive programming patterns.

---

## 🔧 Service Architecture Patterns

### **IEngineService Interface**
All engine systems implement this standardized interface:

```csharp
public interface IEngineService
{
    /// <summary>
    /// Service initialization priority (lower = earlier)
    /// </summary>
    int InitializationPriority { get; }
    
    /// <summary>
    /// Current service state
    /// </summary>
    ServiceState State { get; }
    
    /// <summary>
    /// Async service initialization with dependencies
    /// </summary>
    UniTask<bool> InitializeServiceAsync();
    
    /// <summary>
    /// Reset service state for new game sessions
    /// </summary>
    void ResetService();
    
    /// <summary>
    /// Cleanup and resource release
    /// </summary>
    void DestroyService();
}
```

### **Service Discovery Pattern**
Services are automatically discovered and registered using reflection and attributes:

```csharp
[InitializeAtRuntime(InitializationPriority = 100)]
public class AudioManager : IAudioManager
{
    // Service dependencies injected via constructor
    public AudioManager(IResourceManager resourceManager, IStateManager stateManager)
    {
        this.resourceManager = resourceManager;
        this.stateManager = stateManager;
    }
    
    public async UniTask<bool> InitializeServiceAsync()
    {
        // Initialize audio system
        return true;
    }
}
```

### **Service Lifecycle Management**

```csharp
public enum ServiceState
{
    Uninitialized,
    Initializing,
    Ready,
    Error,
    Destroyed
}

public class ServiceManager
{
    public async UniTask InitializeAllServicesAsync()
    {
        // 1. Discover all services via reflection
        var services = DiscoverServices();
        
        // 2. Resolve dependencies and create initialization order
        var initOrder = ResolveDependencyOrder(services);
        
        // 3. Initialize services in dependency order
        foreach (var service in initOrder)
        {
            await InitializeServiceSafely(service);
        }
    }
}
```

---

## 🎯 Manager Pattern for System Types

### **Generic Manager Pattern**
Provides a reusable pattern for managing collections of similar objects:

```csharp
public abstract class Manager<TItem, TState, TMetadata, TConfig> : IEngineService
    where TItem : IManagedItem
    where TState : class, new()
    where TMetadata : ScriptableObject
    where TConfig : Configuration
{
    protected Dictionary<string, TItem> items = new();
    protected TConfig configuration;
    
    // Generic CRUD operations
    public virtual async UniTask<TItem> CreateItemAsync(string id, TMetadata metadata)
    {
        if (items.ContainsKey(id))
            throw new InvalidOperationException($"Item with id '{id}' already exists");
            
        var item = await CreateItemImplementationAsync(id, metadata);
        items[id] = item;
        
        OnItemCreated?.Invoke(item);
        return item;
    }
    
    public virtual TItem GetItem(string id)
    {
        return items.TryGetValue(id, out var item) ? item : default(TItem);
    }
    
    public virtual async UniTask RemoveItemAsync(string id)
    {
        if (!items.TryGetValue(id, out var item))
            return;
            
        await DestroyItemImplementationAsync(item);
        items.Remove(id);
        
        OnItemRemoved?.Invoke(item);
    }
    
    // Abstract methods for concrete implementations
    protected abstract UniTask<TItem> CreateItemImplementationAsync(string id, TMetadata metadata);
    protected abstract UniTask DestroyItemImplementationAsync(TItem item);
    
    // Events for cross-system communication
    public event Action<TItem> OnItemCreated;
    public event Action<TItem> OnItemRemoved;
}
```

### **Actor Manager Implementation**
```csharp
public class CharacterManager : Manager<ICharacter, CharacterState, CharacterMetadata, CharacterConfiguration>
{
    private readonly IResourceManager resourceManager;
    private readonly IAnimationManager animationManager;
    
    public CharacterManager(IResourceManager resourceManager, IAnimationManager animationManager)
    {
        this.resourceManager = resourceManager;
        this.animationManager = animationManager;
    }
    
    protected override async UniTask<ICharacter> CreateItemImplementationAsync(string id, CharacterMetadata metadata)
    {
        // Load character resources
        var sprite = await resourceManager.LoadAsync<Sprite>(metadata.SpritePath);
        
        // Create character GameObject
        var characterGO = new GameObject($"Character_{id}");
        var spriteRenderer = characterGO.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        
        // Create character component
        var character = characterGO.AddComponent<SpriteCharacter>();
        character.Initialize(id, metadata, animationManager);
        
        return character;
    }
}
```

---

## 📦 Resource Management Architecture

### **Provider Pattern**
Supports multiple resource sources through a unified interface:

```csharp
public interface IResourceProvider
{
    bool SupportsType<T>() where T : UnityEngine.Object;
    bool SupportsPath(string path);
    UniTask<Resource<T>> LoadResourceAsync<T>(string path) where T : UnityEngine.Object;
    void UnloadResource(string path);
}

public class Resource<T> : IDisposable where T : UnityEngine.Object
{
    public T Object { get; private set; }
    public string Path { get; private set; }
    public bool IsValid => Object != null;
    
    private int referenceCount = 0;
    private readonly IResourceProvider provider;
    
    public Resource<T> Hold()
    {
        referenceCount++;
        return this;
    }
    
    public void Release()
    {
        referenceCount--;
        if (referenceCount <= 0)
        {
            provider.UnloadResource(Path);
        }
    }
}
```

### **Resource Manager Implementation**
```csharp
public class ResourceManager : IResourceManager
{
    private readonly List<IResourceProvider> providers = new();
    private readonly Dictionary<string, object> loadedResources = new();
    
    public void RegisterProvider(IResourceProvider provider)
    {
        providers.Add(provider);
    }
    
    public async UniTask<Resource<T>> LoadAsync<T>(string path) where T : UnityEngine.Object
    {
        // Check cache first
        if (loadedResources.TryGetValue(path, out var cached))
        {
            return ((Resource<T>)cached).Hold();
        }
        
        // Find appropriate provider
        var provider = providers.FirstOrDefault(p => p.SupportsType<T>() && p.SupportsPath(path));
        if (provider == null)
            throw new InvalidOperationException($"No provider found for resource: {path}");
        
        // Load resource
        var resource = await provider.LoadResourceAsync<T>(path);
        loadedResources[path] = resource;
        
        return resource.Hold();
    }
}
```

---

## 🎮 Command System Architecture

### **Command Pattern with Reflection**
Commands are discovered and executed dynamically:

```csharp
public abstract class Command
{
    public abstract UniTask ExecuteAsync(CancellationToken cancellationToken = default);
    
    // Marker interfaces for command capabilities
    public interface IForceWait { }      // Always wait for completion
    public interface ILocalizable { }    // Include in localization
    public interface IPreloadable { }    // Supports resource preloading
    public interface IStateful { }       // Affects game state
}

[CommandAlias("show")]
public class ShowCharacterCommand : Command, IPreloadable, IStateful
{
    [RequiredParameter]
    public StringParameter character = new();
    
    [ParameterAlias("at")]
    public StringParameter position = new();
    
    public DecimalParameter duration = new(0.35f);
    
    public override async UniTask ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var characterManager = Engine.GetService<ICharacterManager>();
        var character = characterManager.GetCharacter(this.character.Value);
        
        if (character == null)
        {
            character = await characterManager.CreateCharacterAsync(this.character.Value);
        }
        
        await character.ShowAsync(position.Value, duration.Value, cancellationToken);
    }
}
```

### **Command Parameter System**
```csharp
public interface ICommandParameter
{
    bool HasValue { get; }
    void SetValueFromString(string value, out string error);
}

public class StringParameter : ICommandParameter
{
    public string Value { get; private set; }
    public bool HasValue => !string.IsNullOrEmpty(Value);
    
    public StringParameter() { }
    public StringParameter(string defaultValue) => Value = defaultValue;
    
    public void SetValueFromString(string value, out string error)
    {
        error = null;
        Value = value;
    }
    
    public static implicit operator string(StringParameter parameter) => parameter.Value;
}

public class LocalizableTextParameter : StringParameter, ILocalizable
{
    public string LocalizedValue => Engine.GetService<ILocalizationManager>().GetText(Value);
    
    public static implicit operator string(LocalizableTextParameter parameter) => parameter.LocalizedValue;
}
```

---

## 💾 State Management Architecture

### **State Map Pattern**
Provides type-safe state management across services:

```csharp
public class GameStateMap
{
    private readonly Dictionary<Type, object> stateMap = new();
    
    public void SetState<T>(T state) where T : class
    {
        stateMap[typeof(T)] = state;
    }
    
    public T GetState<T>() where T : class, new()
    {
        if (stateMap.TryGetValue(typeof(T), out var state))
            return (T)state;
            
        // Create default state if not found
        var defaultState = new T();
        SetState(defaultState);
        return defaultState;
    }
    
    public bool HasState<T>() where T : class
    {
        return stateMap.ContainsKey(typeof(T));
    }
}
```

### **Stateful Service Pattern**
Services that need to persist state implement this interface:

```csharp
public interface IStatefulService<TState> where TState : class, new()
{
    TState GetState();
    void SetState(TState state);
}

public class CharacterManager : IStatefulService<CharacterManagerState>
{
    public CharacterManagerState GetState()
    {
        return new CharacterManagerState
        {
            Characters = items.Values.Select(c => c.GetState()).ToList()
        };
    }
    
    public void SetState(CharacterManagerState state)
    {
        // Clear existing characters
        foreach (var character in items.Values.ToList())
        {
            await RemoveItemAsync(character.Id);
        }
        
        // Restore characters from state
        foreach (var characterState in state.Characters)
        {
            var character = await CreateItemAsync(characterState.Id, characterState.Metadata);
            character.SetState(characterState);
        }
    }
}
```

---

## 🎨 UI Management Architecture

### **Managed UI Pattern**
Provides consistent UI behavior across all interface elements:

```csharp
public interface IManagedUI
{
    string Name { get; }
    bool Visible { get; set; }
    float Opacity { get; set; }
    RenderMode RenderMode { get; set; }
    
    UniTask ChangeVisibilityAsync(bool visible, float duration = 0, 
        Tweener<float> tweener = null, CancellationToken cancellationToken = default);
    UniTask ChangeOpacityAsync(float opacity, float duration = 0,
        Tweener<float> tweener = null, CancellationToken cancellationToken = default);
}

public abstract class ManagedUI : MonoBehaviour, IManagedUI
{
    [SerializeField] private string uiName;
    [SerializeField] private bool visibleOnStart = true;
    [SerializeField] private CanvasGroup canvasGroup;
    
    public string Name => uiName;
    public bool Visible { get; set; }
    public float Opacity 
    { 
        get => canvasGroup.alpha; 
        set => canvasGroup.alpha = value; 
    }
    
    public virtual async UniTask ChangeVisibilityAsync(bool visible, float duration = 0,
        Tweener<float> tweener = null, CancellationToken cancellationToken = default)
    {
        if (Visible == visible) return;
        
        Visible = visible;
        
        if (duration > 0)
        {
            var targetOpacity = visible ? 1f : 0f;
            await ChangeOpacityAsync(targetOpacity, duration, tweener, cancellationToken);
        }
        else
        {
            Opacity = visible ? 1f : 0f;
        }
        
        gameObject.SetActive(visible);
    }
}
```

### **UI Manager Implementation**
```csharp
public class UIManager : IUIManager
{
    private readonly Dictionary<string, IManagedUI> managedUIs = new();
    private readonly List<IManagedUI> modalStack = new();
    
    public void RegisterUI(IManagedUI ui)
    {
        managedUIs[ui.Name] = ui;
    }
    
    public async UniTask ShowUIAsync(string name, float duration = 0)
    {
        if (!managedUIs.TryGetValue(name, out var ui))
        {
            Debug.LogError($"UI '{name}' not found");
            return;
        }
        
        await ui.ChangeVisibilityAsync(true, duration);
    }
    
    public async UniTask ShowModalAsync(string name, float duration = 0)
    {
        var ui = managedUIs[name];
        
        // Disable input on all other UIs
        foreach (var otherUI in managedUIs.Values.Where(u => u != ui))
        {
            if (otherUI is MonoBehaviour mb && mb.TryGetComponent<GraphicRaycaster>(out var raycaster))
            {
                raycaster.enabled = false;
            }
        }
        
        modalStack.Add(ui);
        await ui.ChangeVisibilityAsync(true, duration);
    }
}
```

---

## 📡 Event System Architecture

### **Type-Safe Event System**
```csharp
public interface IEventBus
{
    void Subscribe<T>(Action<T> handler) where T : class;
    void Unsubscribe<T>(Action<T> handler) where T : class;
    void Publish<T>(T eventData) where T : class;
}

public class EventBus : IEventBus
{
    private readonly Dictionary<Type, List<Delegate>> handlers = new();
    
    public void Subscribe<T>(Action<T> handler) where T : class
    {
        var eventType = typeof(T);
        if (!handlers.ContainsKey(eventType))
            handlers[eventType] = new List<Delegate>();
            
        handlers[eventType].Add(handler);
    }
    
    public void Unsubscribe<T>(Action<T> handler) where T : class
    {
        var eventType = typeof(T);
        if (handlers.TryGetValue(eventType, out var eventHandlers))
        {
            eventHandlers.Remove(handler);
        }
    }
    
    public void Publish<T>(T eventData) where T : class
    {
        var eventType = typeof(T);
        if (handlers.TryGetValue(eventType, out var eventHandlers))
        {
            foreach (Action<T> handler in eventHandlers.Cast<Action<T>>())
            {
                try
                {
                    handler(eventData);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error in event handler: {e}");
                }
            }
        }
    }
}
```

### **Event-Driven Service Communication**
```csharp
public class GameStateChangedEvent
{
    public string StateName { get; set; }
    public object OldValue { get; set; }
    public object NewValue { get; set; }
}

public class CharacterManager : IEngineService
{
    private readonly IEventBus eventBus;
    
    public CharacterManager(IEventBus eventBus)
    {
        this.eventBus = eventBus;
        
        // Subscribe to relevant events
        eventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
    }
    
    private void OnGameStateChanged(GameStateChangedEvent eventData)
    {
        if (eventData.StateName == "SceneTransition")
        {
            // Handle scene transition logic
            HandleSceneTransition();
        }
    }
    
    private async UniTask CreateCharacterAsync(string id)
    {
        var character = await CreateCharacterImplementation(id);
        
        // Publish event for other systems
        eventBus.Publish(new CharacterCreatedEvent { Character = character });
    }
}
```

---

## 🔧 Configuration Architecture

### **ScriptableObject Configuration Pattern**
```csharp
public abstract class Configuration : ScriptableObject
{
    [Header("Configuration Info")]
    [SerializeField] private string configurationName;
    [SerializeField, TextArea] private string description;
    
    public string Name => configurationName;
    public string Description => description;
    
    protected virtual void OnValidate()
    {
        if (string.IsNullOrEmpty(configurationName))
            configurationName = GetType().Name.Replace("Configuration", "");
    }
}

[CreateAssetMenu(menuName = "Engine/Audio Configuration")]
public class AudioConfiguration : Configuration
{
    [Header("Audio Settings")]
    public float masterVolume = 1.0f;
    public float bgmVolume = 0.8f;
    public float sfxVolume = 1.0f;
    public float voiceVolume = 1.0f;
    
    [Header("Performance")]
    public int maxSimultaneousSources = 32;
    public bool enableAudioCompression = true;
    public AudioCompressionFormat compressionFormat = AudioCompressionFormat.Vorbis;
}
```

### **Configuration Provider Pattern**
```csharp
public interface IConfigurationProvider : IEngineService
{
    T GetConfiguration<T>() where T : Configuration;
    void SetConfiguration<T>(T configuration) where T : Configuration;
    event Action<Configuration> OnConfigurationChanged;
}

public class ConfigurationProvider : IConfigurationProvider
{
    private readonly Dictionary<Type, Configuration> configurations = new();
    
    public T GetConfiguration<T>() where T : Configuration
    {
        var type = typeof(T);
        if (configurations.TryGetValue(type, out var config))
            return (T)config;
            
        // Load default configuration from resources
        var defaultConfig = Resources.Load<T>($"Configurations/{type.Name}");
        if (defaultConfig != null)
        {
            configurations[type] = defaultConfig;
            return defaultConfig;
        }
        
        Debug.LogWarning($"No configuration found for type {type.Name}");
        return null;
    }
}
```

---

## 🧪 Testing Architecture

### **Service Testing Pattern**
```csharp
[TestFixture]
public class CharacterManagerTests
{
    private CharacterManager characterManager;
    private MockResourceManager mockResourceManager;
    private MockEventBus mockEventBus;
    
    [SetUp]
    public void Setup()
    {
        mockResourceManager = new MockResourceManager();
        mockEventBus = new MockEventBus();
        characterManager = new CharacterManager(mockResourceManager, mockEventBus);
    }
    
    [Test]
    public async Task CreateCharacter_ShouldCreateValidCharacter()
    {
        // Arrange
        var characterId = "TestCharacter";
        var metadata = CreateTestMetadata();
        
        // Act
        var character = await characterManager.CreateItemAsync(characterId, metadata);
        
        // Assert
        Assert.IsNotNull(character);
        Assert.AreEqual(characterId, character.Id);
        Assert.IsTrue(characterManager.HasItem(characterId));
    }
}
```

### **Mock Service Pattern**
```csharp
public class MockResourceManager : IResourceManager
{
    private readonly Dictionary<string, object> mockResources = new();
    
    public void SetMockResource<T>(string path, T resource) where T : UnityEngine.Object
    {
        mockResources[path] = new Resource<T>(resource, path, this);
    }
    
    public async UniTask<Resource<T>> LoadAsync<T>(string path) where T : UnityEngine.Object
    {
        if (mockResources.TryGetValue(path, out var resource))
        {
            return (Resource<T>)resource;
        }
        
        throw new InvalidOperationException($"Mock resource not found: {path}");
    }
}
```

---

## 📚 Best Practices

### **1. Service Design**
- Keep services focused on a single responsibility
- Use dependency injection for all service dependencies
- Implement proper error handling and logging
- Design for testability with clear interfaces

### **2. Resource Management**
- Always use reference counting for resources
- Implement proper cleanup in Dispose methods
- Use object pooling for frequently created objects
- Monitor memory usage and implement appropriate limits

### **3. Event Communication**
- Use events for loose coupling between services
- Keep event handlers lightweight and async when needed
- Implement proper error handling in event handlers
- Document event contracts clearly

### **4. Configuration Management**
- Use ScriptableObjects for all configuration data
- Implement validation in OnValidate methods
- Provide sensible defaults for all configuration values
- Support runtime configuration changes where appropriate

### **5. Performance Considerations**
- Profile regularly and identify bottlenecks
- Use async/await patterns for I/O operations
- Implement caching strategies for expensive operations
- Consider object pooling for frequently allocated objects

This architecture guide provides the foundation for building scalable, maintainable, and extensible systems in the Sinkii09 Engine while following proven design patterns and best practices.