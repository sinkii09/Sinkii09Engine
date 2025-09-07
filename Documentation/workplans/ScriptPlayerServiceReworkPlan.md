# ScriptPlayer Service Rework Plan

## Overview
The ScriptPlayer service is a critical component of the Sinkii09 Engine responsible for executing scripts, managing playback state, and coordinating command execution. This document outlines a comprehensive plan to rework the service with modern async patterns, robust error handling, and extensible architecture.

## Current State Analysis

### Issues with Current Implementation
- Service is completely commented out (non-functional)
- No clear separation between script execution and command processing
- Missing async/await support for long-running operations
- No pause/resume capabilities
- Lacks comprehensive error handling
- No performance monitoring or statistics

### Dependencies
- **ScriptService**: For loading and managing scripts
- **CommandParser**: For parsing command lines
- **ResourceService**: For resource loading
- **ServiceContainer**: For dependency injection

## Proposed Architecture

### Core Components

#### 1. IScriptPlayerService Interface
```csharp
public interface IScriptPlayerService : IEngineService
{
    // State Properties
    PlaybackState State { get; }
    Script CurrentScript { get; }
    int CurrentLineIndex { get; }
    float PlaybackSpeed { get; set; }
    
    // Execution Control
    UniTask<ScriptExecutionResult> PlayScriptAsync(Script script, CancellationToken cancellationToken = default);
    UniTask<ScriptExecutionResult> PlayScriptAsync(string scriptName, CancellationToken cancellationToken = default);
    UniTask PauseAsync();
    UniTask ResumeAsync();
    UniTask StopAsync();
    UniTask<bool> StepForwardAsync();
    UniTask<bool> StepBackwardAsync();
    UniTask SkipToLineAsync(int lineIndex);
    UniTask SkipToLabelAsync(string label);
    
    // Events
    event Action<Script> ScriptStarted;
    event Action<Script, ScriptExecutionResult> ScriptCompleted;
    event Action<ScriptLine, int> LineExecuting;
    event Action<ScriptLine, int> LineExecuted;
    event Action<PlaybackState> StateChanged;
    event Action<float> ProgressChanged;
}
```

#### 2. ScriptPlayerService Implementation
- Implements IScriptPlayerService
- Uses [EngineService] attribute
- Constructor dependency injection
- Integrates with ScriptService for script loading
- Manages execution context and state

#### 3. ScriptPlayerConfiguration
```csharp
[CreateAssetMenu(menuName = "Sinkii09/Services/ScriptPlayerConfiguration")]
public class ScriptPlayerConfiguration : ServiceConfiguration
{
    [Header("Execution Settings")]
    public float DefaultPlaybackSpeed = 1.0f;
    public float CommandExecutionTimeout = 30f;
    public bool EnableAutoSave = true;
    public float AutoSaveInterval = 60f;
    
    [Header("Performance")]
    public int MaxExecutionStackDepth = 10;
    public bool EnableCommandCaching = true;
    public int CommandCacheSize = 100;
    
    [Header("Debug")]
    public bool EnableBreakpoints = true;
    public bool LogExecutionFlow = false;
    public bool EnableStepMode = false;
}
```

#### 4. ScriptExecutionContext
```csharp
public class ScriptExecutionContext
{
    public Script Script { get; set; }
    public int CurrentLineIndex { get; set; }
    public Dictionary<string, object> Variables { get; set; }
    public Stack<ScriptExecutionFrame> CallStack { get; set; }
    public List<int> Breakpoints { get; set; }
    public CancellationTokenSource CancellationTokenSource { get; set; }
}
```

#### 5. ScriptPlaybackController
- Manages playback flow control
- Handles pause/resume logic
- Implements skip and step functionality
- Manages execution speed

## Implementation Phases

### Phase 1: Core Infrastructure (Week 1)
- [ ] Define IScriptPlayerService interface
- [ ] Create ScriptPlayerConfiguration ScriptableObject
- [ ] Implement basic ScriptPlayerService skeleton
- [ ] Set up dependency injection
- [ ] Create ScriptExecutionContext

### Phase 2: Basic Execution (Week 1-2)
- [ ] Implement script loading integration
- [ ] Add line-by-line execution logic
- [ ] Integrate CommandParser for command execution
- [ ] Implement basic play/stop functionality
- [ ] Add execution state management

### Phase 3: Advanced Control (Week 2)
- [ ] Implement pause/resume functionality
- [ ] Add step forward/backward
- [ ] Implement skip to line/label
- [ ] Add playback speed control
- [ ] Implement breakpoint support

### Phase 4: Command Integration (Week 2-3)
- [ ] Create command execution pipeline
- [ ] Integrate with existing command system
- [ ] Add command result handling
- [ ] Implement wait/delay commands
- [ ] Add flow control commands (if/else/goto)

### Phase 5: Error Handling & Recovery (Week 3)
- [ ] Implement comprehensive error handling
- [ ] Add timeout management
- [ ] Create fallback mechanisms
- [ ] Implement error recovery strategies
- [ ] Add validation system

### Phase 6: Events & Monitoring (Week 3-4)
- [ ] Implement event system
- [ ] Add progress tracking
- [ ] Create performance monitoring
- [ ] Implement statistics collection
- [ ] Add execution history

### Phase 7: Advanced Features (Week 4)
- [ ] Add save/load execution state
- [ ] Implement script nesting/includes
- [ ] Add variable context management
- [ ] Create conditional execution
- [ ] Implement loop constructs

### Phase 8: Testing & Documentation (Week 4-5)
- [ ] Write comprehensive unit tests
- [ ] Create integration tests
- [ ] Add performance benchmarks
- [ ] Write API documentation
- [ ] Create usage examples

## Key Features

### Execution Pipeline
1. **Script Loading**: Load script via ScriptService
2. **Preprocessing**: Validate and prepare script
3. **Line Execution**: Execute each line sequentially
4. **Command Processing**: Parse and execute commands
5. **State Management**: Update execution context
6. **Event Dispatch**: Notify listeners of progress
7. **Completion Handling**: Clean up and report results

### Command Execution Flow
```
ScriptLine -> Parse Command -> Validate Parameters -> Execute Command -> Handle Result -> Update Context
```

### State Machine
```
Idle -> Loading -> Playing -> [Paused] -> Playing -> Completed
                     |                        |
                     +-----> Stopped <--------+
```

### Error Handling Strategy
1. **Validation Errors**: Prevent execution, return detailed error
2. **Runtime Errors**: Log, attempt recovery, continue if possible
3. **Fatal Errors**: Stop execution, save state, notify user
4. **Timeout Errors**: Cancel operation, mark as failed, continue

## Integration Points

### Required Services
- **ScriptService**: Script loading and management
- **CommandService**: Command execution (if exists)
- **ResourceService**: Resource loading for commands
- **ActorService**: Character-related commands
- **AudioService**: Audio playback commands
- **UIService**: UI manipulation commands

### Event Integration
- Fire events for external systems to react
- Support for custom command handlers
- Plugin architecture for extending functionality

## Performance Considerations

### Optimization Strategies
1. **Command Caching**: Cache parsed commands
2. **Lazy Loading**: Load resources on demand
3. **Async Execution**: Non-blocking command execution
4. **Batch Processing**: Group similar operations
5. **Memory Pooling**: Reuse execution contexts

### Monitoring Metrics
- Script execution time
- Command execution performance
- Memory usage
- Cache hit rates
- Error frequencies

## Testing Strategy

### Unit Tests
- Command parsing tests
- State management tests
- Flow control tests
- Error handling tests
- Event system tests

### Integration Tests
- Script loading and execution
- Command integration
- Service dependencies
- Performance benchmarks

### Test Scripts
- Basic linear scripts
- Scripts with branches
- Scripts with loops
- Error scenarios
- Performance stress tests

## Migration Path

### From Current Implementation
1. Uncomment existing interface definitions
2. Migrate to new service architecture
3. Update command integration
4. Add async support
5. Implement new features incrementally

### Backward Compatibility
- Support existing script format
- Maintain command compatibility
- Preserve event signatures where possible

## Success Criteria

### Functional Requirements
- ✅ Scripts execute line by line
- ✅ Commands are parsed and executed
- ✅ Execution can be paused/resumed
- ✅ Errors are handled gracefully
- ✅ Events notify of progress

### Performance Requirements
- Script loading < 100ms
- Command execution < 50ms average
- Memory usage < 10MB for typical script
- Support 1000+ line scripts

### Quality Requirements
- 90%+ test coverage
- No memory leaks
- Thread-safe operations
- Comprehensive error messages

## Risks and Mitigations

### Technical Risks
1. **Async Complexity**: Use UniTask for simplified async
2. **Command Integration**: Define clear interfaces
3. **Performance Issues**: Profile and optimize early
4. **Memory Leaks**: Implement proper disposal

### Schedule Risks
1. **Scope Creep**: Define MVP features clearly
2. **Integration Delays**: Mock dependencies initially
3. **Testing Time**: Automate tests early

## Conclusion

This rework will transform the ScriptPlayer service into a robust, performant, and extensible system capable of handling complex script execution scenarios. The phased approach ensures incremental delivery of value while maintaining system stability.

## Appendix

### Example Usage
```csharp
// Basic script execution
var result = await scriptPlayer.PlayScriptAsync("MainStory");

// With cancellation
var cts = new CancellationTokenSource();
var result = await scriptPlayer.PlayScriptAsync("Tutorial", cts.Token);

// Event handling
scriptPlayer.LineExecuting += (line, index) => 
{
    Debug.Log($"Executing line {index}: {line}");
};

// Playback control
await scriptPlayer.PauseAsync();
await scriptPlayer.StepForwardAsync();
await scriptPlayer.ResumeAsync();
```

### Command Examples
```
@wait 2.0
@show Actor1
@say Actor1 "Hello, world!"
@hide Actor1
@goto NextScene
```

     │ Phase 7C: Memory Management and Cleanup Implementation Plan                                                     │
     │                                                                                                                 │
     │ Overview                                                                                                        │
     │                                                                                                                 │
     │ Implement comprehensive memory management and cleanup systems for the ScriptPlayerService to optimize memory    │
     │ usage, prevent memory leaks, and ensure efficient resource cleanup.                                             │
     │                                                                                                                 │
     │ Components to Create/Enhance:                                                                                   │
     │                                                                                                                 │
     │ 1. MemoryPressureMonitor Component                                                                              │
     │                                                                                                                 │
     │ - Real-time memory pressure detection                                                                           │
     │ - Automatic cleanup triggering based on thresholds                                                              │
     │ - Integration with Unity's memory management                                                                    │
     │ - Configurable memory pressure levels                                                                           │
     │                                                                                                                 │
     │ 2. ServicePerformanceMonitor                                                                                    │
     │                                                                                                                 │
     │ - Missing component referenced throughout codebase                                                              │
     │ - Memory usage tracking per command type                                                                        │
     │ - Performance metrics collection                                                                                │
     │ - GC event monitoring and optimization                                                                          │
     │                                                                                                                 │
     │ 3. Enhanced ResourcePreloader                                                                                   │
     │                                                                                                                 │
     │ - Add automatic resource cleanup based on usage patterns                                                        │
     │ - Implement resource usage tracking                                                                             │
     │ - Add memory pressure-based unloading                                                                           │
     │ - Smart cache eviction policies                                                                                 │
     │                                                                                                                 │
     │ 4. DisposableResourceManager                                                                                    │
     │                                                                                                                 │
     │ - Centralized management of IDisposable resources                                                               │
     │ - Automatic disposal on service shutdown                                                                        │
     │ - Weak reference tracking to prevent memory leaks                                                               │
     │ - Resource lifecycle management                                                                                 │
     │                                                                                                                 │
     │ 5. Enhanced CommandResultPool                                                                                   │
     │                                                                                                                 │
     │ - Add memory pressure monitoring                                                                                │
     │ - Implement adaptive pool sizing                                                                                │
     │ - Add memory usage optimization                                                                                 │
     │ - Pool statistics and monitoring                                                                                │
     │                                                                                                                 │
     │ 6. ScriptPlayerConfiguration Enhancement                                                                        │
     │                                                                                                                 │
     │ - Add memory management configuration options                                                                   │
     │ - Configure memory thresholds and cleanup policies                                                              │
     │ - Add GC optimization settings                                                                                  │
     │                                                                                                                 │
     │ Implementation Steps:                                                                                           │
     │                                                                                                                 │
     │ 1. Create MemoryPressureMonitor - Core memory monitoring system                                                 │
     │ 2. Implement ServicePerformanceMonitor - Fill missing dependency                                                │
     │ 3. Enhance ResourcePreloader - Add memory-aware cleanup                                                         │
     │ 4. Create DisposableResourceManager - Centralized resource disposal                                             │
     │ 5. Enhance CommandResultPool - Add memory pressure awareness                                                    │
     │ 6. Update Configuration - Add memory management settings                                                        │
     │ 7. Integration - Connect all components with ScriptPlayerService                                                │
     │                                                                                                                 │
     │ Memory Optimization Features:                                                                                   │
     │                                                                                                                 │
     │ - Automatic resource cleanup based on memory pressure                                                           │
     │ - Smart object pooling with adaptive sizing                                                                     │
     │ - Comprehensive memory usage tracking                                                                           │
     │ - GC optimization triggers                                                                                      │
     │ - Disposable resource lifecycle management                                                                      │
     │ - Memory leak prevention mechanisms                                                                             │
     │                                                                                                                 │
     │ Benefits:                                                                                                       │
     │                                                                                                                 │
     │ - Reduced memory footprint                                                                                      │
     │ - Prevention of memory leaks                                                                                    │
     │ - Automatic cleanup under memory pressure                                                                       │
     │ - Better performance through optimized GC                                                                       │
     │ - Comprehensive memory monitoring and reporting 

---
*Document Version: 1.0*  
*Last Updated: January 2025*  
*Author: Sinkii09 Engine Team*