# AutoSaveService

A comprehensive auto-save system for the Sinkii09 Engine that provides automatic save triggers, configurable conditions, and extensible provider architecture.

## Features

- **Automatic Save Triggers**: Timer-based, scene change, and application lifecycle triggers
- **Flexible Provider System**: Games implement their own save data providers
- **Configurable Conditions**: Control when auto-saves can occur
- **Slot Management**: Rotating, timestamped, or numbered save slot strategies
- **Thread-Safe Operations**: Async save operations with proper cancellation support
- **Unity Integration**: MonoBehaviour components for easy setup
- **Comprehensive Testing**: Full test suite with mock implementations

## Quick Start

### 1. Create Configuration

Create an AutoSaveServiceConfiguration asset:

```csharp
// Right-click in Project window
// Create > Engine > Configurations > AutoSaveService
```

### 2. Implement Auto-Save Provider

```csharp
public class MyGameAutoSaveProvider : IAutoSaveProvider
{
    public string ProviderName => "MyGame";
    
    public async UniTask<SaveData> CreateSaveDataAsync(CancellationToken cancellationToken)
    {
        var saveData = new SaveData();
        saveData["player"] = GetPlayerData();
        saveData["world"] = GetWorldData();
        return saveData;
    }
    
    public bool CanAutoSave() => !IsPlayerDead() && !IsInCutscene();
    public AutoSavePriority GetAutoSavePriority() => AutoSavePriority.Normal;
    public bool MeetsConditions(AutoSaveConditions conditions) => true;
    
    public void OnAutoSaveCompleted(AutoSaveEvent evt) { }
    public void OnAutoSaveFailed(AutoSaveEvent evt) { }
}
```

### 3. Register Services

```csharp
// In your game initialization
container.RegisterService<IAutoSaveProvider, MyGameAutoSaveProvider>();
container.RegisterService<IAutoSaveService, AutoSaveService>();
```

### 4. Add Unity Component

Add `AutoSaveServiceBehaviour` to a GameObject in your scene to enable Unity integration.

## Configuration Options

### General Settings
- **EnableAutoSave**: Enable/disable auto-save functionality
- **UseAsyncSave**: Use async save operations (recommended)
- **SaveCooldown**: Minimum time between saves (prevents spam)

### Slot Management
- **MaxAutoSaveSlots**: Maximum number of auto-save slots to maintain
- **SlotStrategy**: How to handle slot rotation (Rotating, Timestamped, Numbered)
- **AutoSavePrefix**: Prefix for auto-save slot names

### Triggers
- **Timer**: Automatic saves at regular intervals
- **Scene Changes**: Save when changing scenes
- **Application Lifecycle**: Save on pause, focus loss, or quit

### Conditions
- **RequirePlayerAlive**: Only save when player is alive
- **RequireNotInCombat**: Only save when not in combat
- **RequireNotInCutscene**: Only save when not in cutscene
- **RequireNotInMainMenu**: Only save when not in main menu
- **RequireNotLoading**: Only save when not loading
- **RequireUnsavedChanges**: Only save when there are changes

## Architecture

```
AutoSaveService
├── Triggers (When to save)
│   ├── TimerAutoSaveTrigger
│   ├── SceneChangeAutoSaveTrigger
│   └── ApplicationLifecycleAutoSaveTrigger
├── Providers (What to save)
│   └── IAutoSaveProvider (implemented by games)
├── Slot Management (Where to save)
│   ├── Rotating slots
│   ├── Timestamped slots
│   └── Numbered slots
└── Conditions (Whether to save)
    └── Configurable conditions
```

## Usage Examples

### Manual Auto-Save
```csharp
var autoSaveService = Engine.GetService<IAutoSaveService>();
var result = await autoSaveService.TriggerAutoSaveAsync(AutoSaveReason.Manual);
```

### Custom Trigger
```csharp
public class CheckpointAutoSaveTrigger : AutoSaveTriggerBase
{
    public override string TriggerName => "Checkpoint";
    
    public void OnPlayerReachedCheckpoint()
    {
        TriggerAutoSave(AutoSaveReason.Checkpoint);
    }
}

// Register the custom trigger
autoSaveService.RegisterTrigger(new CheckpointAutoSaveTrigger());
```

### High-Priority Provider
```csharp
public class CriticalProgressProvider : IAutoSaveProvider
{
    public AutoSavePriority GetAutoSavePriority() => AutoSavePriority.Critical;
    
    public bool CanAutoSave() => HasCriticalProgress();
    
    // Implementation...
}
```

### Event Handling
```csharp
autoSaveService.AutoSaveCompleted += evt => 
{
    Debug.Log($"Auto-save completed: {evt.SlotName} in {evt.SaveDurationMs}ms");
    ShowSaveNotification();
};

autoSaveService.AutoSaveFailed += evt => 
{
    Debug.LogError($"Auto-save failed: {evt.Error}");
    ShowSaveErrorDialog(evt.Error);
};
```

## Testing

The AutoSaveService includes comprehensive tests:

```csharp
[Test]
public async UniTask TriggerAutoSaveAsync_WithValidProvider_ShouldSucceed()
{
    await _autoSaveService.InitializeAsync(_serviceContainer, CancellationToken.None);
    _autoSaveService.RegisterProvider(_mockProvider);
    
    var result = await _autoSaveService.TriggerAutoSaveAsync(AutoSaveReason.Manual);
    
    Assert.IsTrue(result.Success);
}
```

## Best Practices

### Provider Implementation
1. **Fast Data Gathering**: Keep `CreateSaveDataAsync` fast to avoid blocking
2. **Proper Conditions**: Implement `CanAutoSave()` and `MeetsConditions()` carefully
3. **Error Handling**: Handle exceptions gracefully in save data creation
4. **Memory Efficiency**: Don't create unnecessary objects in save data

### Configuration
1. **Reasonable Intervals**: Don't set timer intervals too low (minimum 30 seconds)
2. **Scene Filtering**: Add loading screens and menus to ignored scenes
3. **Condition Balance**: Don't make conditions too restrictive
4. **Slot Limits**: Keep max slots reasonable (3-10 typically)

### Performance
1. **Async Operations**: Always use async save operations
2. **Cancellation Support**: Respect cancellation tokens
3. **Memory Pressure**: Monitor memory usage with many auto-saves
4. **Background Saving**: Enable background saving for better performance

## Integration with Other Services

The AutoSaveService integrates seamlessly with other engine services:

- **SaveLoadService**: Handles the actual save operations
- **ServiceContainer**: Provides dependency injection
- **ConfigurationService**: Manages auto-save settings
- **Engine**: Central access point for all services

## Troubleshooting

### Auto-Save Not Triggering
1. Check if auto-save is enabled in configuration
2. Verify providers are registered and `CanAutoSave()` returns true
3. Check if cooldown period is active
4. Ensure conditions are met (`MeetsConditions()`)

### Performance Issues
1. Reduce auto-save frequency
2. Optimize save data creation in providers
3. Enable background saving
4. Monitor memory usage and clean old saves

### Save Failures
1. Check SaveLoadService health
2. Verify storage permissions
3. Monitor disk space
4. Check provider error handling

## Advanced Features

### Custom Conditions
```csharp
// Add custom conditions to configuration
conditions.CustomConditions = AutoSaveCondition.HasUnsavedChanges;

// Handle in provider
public bool MeetsConditions(AutoSaveConditions conditions)
{
    if (conditions.CustomConditions.HasFlag(AutoSaveCondition.HasUnsavedChanges))
    {
        return HasMeaningfulChanges();
    }
    return true;
}
```

### Multiple Providers
```csharp
// Register multiple providers with different priorities
container.RegisterService<IAutoSaveProvider, PlayerDataProvider>();
container.RegisterService<IAutoSaveProvider, WorldDataProvider>();
container.RegisterService<IAutoSaveProvider, SettingsProvider>();

// AutoSaveService will use the highest priority provider that can save
```

### Custom Slot Strategies
The service supports extensible slot strategies. Future versions may include:
- LRU (Least Recently Used) strategy
- Size-based cleanup
- Date-based cleanup
- Player-defined strategies

This AutoSaveService provides a robust foundation for automatic saving in any Unity game while remaining flexible and extensible for specific game requirements.