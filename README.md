# Sinkii09 Engine

A comprehensive, service-oriented Unity game engine framework featuring advanced resource management, actor systems, and modern async patterns.

## 🚀 Features

- **Service-Oriented Architecture** with full dependency injection
- **Enhanced ResourceService** with Addressables, circuit breaker patterns, and caching
- **Actor System** for game entity management
- **Save/Load System** with encryption and cloud storage support
- **Script Service** with hot-reload capabilities
- **Modern Async/Await** patterns with UniTask integration
- **Performance Optimized** with memory management and caching
- **Comprehensive Testing** with 100% service coverage

## 📦 Installation

### Method 1: Unity Package Manager (Recommended)

1. Open **Window > Package Manager**
2. Click **+ > Add package from git URL**
3. Enter: `https://github.com/sinkii09/engine.git`

### Method 2: Git Submodule

```bash
git submodule add https://github.com/sinkii09/engine.git Assets/Engine
```

### Method 3: Manual Download

1. Download the latest release from [Releases](https://github.com/sinkii09/engine/releases)
2. Extract to `Assets/Engine` in your project

## 🔧 Requirements

- **Unity 2022.3+** (Unity 6 recommended)
- **.NET Standard 2.1** or higher

### Dependencies (Auto-installed via UPM)
- **UniTask 2.3.3+** - Modern async/await patterns
- **ZLinq** - LINQ performance optimization  
- **R3** - Reactive Extensions for UI system
- **Newtonsoft.Json 3.2.1+** - JSON serialization
- **Addressables 2.2.2+** - Advanced resource management

### Required Asset Store Dependencies
- **DOTween** - Animation and tweening system (**REQUIRED**)
  - [DOTween Free](https://assetstore.unity.com/packages/tools/animation/dotween-hotween-v2-27676) or [DOTween Pro](https://assetstore.unity.com/packages/tools/animation/dotween-pro-32416)
  - Must be installed manually from Unity Asset Store

### Optional Dependencies
- **Odin Inspector** - Advanced inspector features (auto-detected)

## 🏁 Quick Start

### 1. Basic Engine Setup

```csharp
using Sinkii09.Engine;
using Cysharp.Threading.Tasks;

public class GameManager : MonoBehaviour
{
    private async void Start()
    {
        // Initialize the engine
        await Engine.InitializeAsync();
        
        // Access services
        var resourceService = Engine.GetService<IResourceService>();
        var actorService = Engine.GetService<IActorService>();
    }
}
```

### 2. Loading Resources

```csharp
// Load via Addressables (first priority)
var texture = await resourceService.LoadResourceAsync<Texture2D>("UI/MainMenu/Background");

// Load with fallback to Resources folder
var audioClip = await resourceService.LoadResourceAsync<AudioClip>("Audio/SFX/ButtonClick");

// Batch loading
var allTextures = await resourceService.LoadResourcesAsync<Texture2D>("UI/Icons");
```

### 3. Actor Management

```csharp
// Spawn an actor
var playerActor = await actorService.SpawnActorAsync<Player>("PlayerCharacter");

// Get actor by ID
var existingActor = actorService.GetActor("PlayerCharacter");

// Remove actor
await actorService.RemoveActorAsync("PlayerCharacter");
```

### 4. Save/Load System

```csharp
var saveLoadService = Engine.GetService<ISaveLoadService>();

// Save game data
var gameData = new GameData { Level = 5, Score = 1000 };
await saveLoadService.SaveDataAsync("GameSave", gameData);

// Load game data
var loadedData = await saveLoadService.LoadDataAsync<GameData>("GameSave");
```

## 📋 Service Configuration

Create service configurations via **Assets > Create > Engine > Services**:

- **ResourceServiceConfiguration** - Configure providers, caching, circuit breakers
- **ActorServiceConfiguration** - Configure actor spawning and management
- **SaveLoadServiceConfiguration** - Configure save data encryption and storage
- **ScriptServiceConfiguration** - Configure script loading and hot-reload

## 🎛️ Advanced Configuration

### Custom Service Registration

```csharp
// Register your own services
Engine.RegisterService<IMyService, MyService>();

// Use dependency injection
public class MyService : IMyService
{
    public MyService(IResourceService resourceService, IActorService actorService)
    {
        // Services auto-injected
    }
}
```

### Memory Management

```csharp
// Configure memory pressure response
resourceService.RegisterMemoryPressureCallback(pressure => {
    if (pressure > 0.8f) {
        // High memory pressure - cleanup non-essential resources
    }
});

// Force cleanup
await resourceService.ForceMemoryCleanupAsync();
```

### Performance Monitoring

```csharp
// Get service statistics
var stats = resourceService.GetStatistics();
Debug.Log($"Cache Hit Rate: {stats.CacheHitRate:P}");
Debug.Log($"Average Load Time: {stats.AverageLoadTime}ms");

// Get provider health
var healthStatus = await resourceService.GetProviderHealthStatusAsync();
```

## 🧪 Testing

The engine includes comprehensive tests:

```csharp
// Run in Unity Test Runner
// Tests located in Assets/Engine/Tests/
```

## 📖 Documentation

- **[Architecture Guide](Documentation/ARCHITECTURE.md)** - Technical deep-dive
- **[API Reference](Documentation/API.md)** - Complete API documentation  
- **[Best Practices](Documentation/BEST_PRACTICES.md)** - Recommended usage patterns
- **[Troubleshooting](Documentation/TROUBLESHOOTING.md)** - Common issues and solutions

## 🛠️ Development

### Building from Source

```bash
git clone https://github.com/sinkii09/engine.git
cd engine
# Open in Unity 2022.3+
```

### Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/amazing-feature`
3. Commit your changes: `git commit -m 'Add amazing feature'`
4. Push to the branch: `git push origin feature/amazing-feature`
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙋‍♂️ Support

- **Documentation**: [Wiki](https://github.com/sinkii09/engine/wiki)
- **Issues**: [GitHub Issues](https://github.com/sinkii09/engine/issues)
- **Discussions**: [GitHub Discussions](https://github.com/sinkii09/engine/discussions)

## 🔄 Changelog

See [CHANGELOG.md](CHANGELOG.md) for version history and breaking changes.

---

**Made with ❤️ for the Unity community**